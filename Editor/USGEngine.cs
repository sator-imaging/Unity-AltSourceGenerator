#if UNITY_EDITOR

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SatorImaging.UnitySourceGenerator
{
    public class USGEngine : AssetPostprocessor
    {
        /////<summary>This will be disabled automatically after every `ProcessFile()` call or Unity import event.</summary>
        //static bool IgnoreOverwriteSettingOnAttribute = false;


        const int BUFFER_LENGTH = 1024 * 64;
        const int BUFFER_MAX_CHAR_LENGTH = BUFFER_LENGTH / 3;  // worst case of UTF-8
        const string GENERATOR_PREFIX = ".";
        const string GENERATOR_EXT = ".g";
        const string GENERATOR_DIR = @"/USG.g";   // don't append last slash. used to determine file is generated one or not.
        const string ASSETS_DIR_NAME = "Assets";
        const string ASSETS_DIR_SLASH = ASSETS_DIR_NAME + "/";
        const string TARGET_FILE_EXT = @".cs";
        const string PATH_PREFIX_TO_IGNORE = @"Packages/";
        readonly static char[] DIR_SEPARATORS = new char[] { '\\', '/' };


        static bool IsAppropriateTarget(string filePath)
        {
            if (!filePath.EndsWith(TARGET_FILE_EXT) ||
                !filePath.StartsWith(ASSETS_DIR_SLASH))
            {
                return false;
            }
            return true;
        }


        static readonly ProjectSettingsData _settings = ProjectSettingsData.instance;

        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // NOTE: Do NOT handle deleted assets because Unity tracking changes perfectly.
            //       Even if delete file while Unity shutted down, asset deletion event happens on next Unity launch.
            //       As a result, delete/import event loops infinitely and file cannot be deleted.
            // NOTE: [DidReloadScripts] is executed before AssetPostprocessor, cannot be used.

            // TODO: Unity sometimes reloads updated scripts by Visual Studio in background automatically.
            //       In this situation, code generation will be done with script data right before saving.
            //       It cannot be solved on C#, simply restart Unity.
            //       Using [DidReloadScripts] or EditorApplication.delayCall, It works fine with Reimport
            //       menu command but OnPostprocessAllAssets event doesn't work as expected.
            //       (script runs with static field cleared even though .Clear() is only in ProcessingFiles().
            //        it's weird that event happens and asset paths retrieved but hashset items gone.)
            //        --> https://docs.unity3d.com/2021.3/Documentation/Manual/DomainReloading.html

            if (!_settings.AutoEmitOnScriptUpdate)
                return;

            // NOTE: Use project-wide ScriptableObject as a temporary storage.
            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (_settings.PathsToSkipImportEvent.TryRemove(importedAssets[i])) continue;
                if (_settings.AutoEmitDisabledPaths.Contains(importedAssets[i])) continue;
                if (!IsAppropriateTarget(importedAssets[i])) continue;

                _settings.ImportedScriptPaths.TryAddUnique(importedAssets[i]);
            }
            _settings.PathsToSkipImportEvent.Clear();
            _settings.Save();

            // NOTE: processing files are done in CompilationPipeline callback.
        }


        // NOTE: event registration is done in InitializeOnLoadMethod
        static void OnCompilationFinished(object context)
        {
            if (!_settings.AutoEmitOnScriptUpdate)
                return;

            try
            {
                RunGenerators(_settings.ImportedScriptPaths.ToArray(), false);
            }
            catch
            {
                _settings.ImportedScriptPaths.Clear();
                _settings.Save();
            }
        }


        // NOTE: script update event sequence.
        //       -> collect updated generators & referencing emitters
        //       -> run only collected generators
        //       -> if something updated, queue referencing emitter on next event
        //          else run referencing emitters.

        static bool TryGetEmitterInfo(string emitterName, out Dictionary<string, CachedTypeInfo> outEmitterPathToCachedInfo)
        {
            // TODO: more efficient way to find generator
            outEmitterPathToCachedInfo = new();
            foreach (var info in TypeNameToInfo.Values)
            {
                if (info.TargetClass == null || info.Attribute.GeneratorClass?.Name != emitterName)
                    continue;

                var path = USGUtility.GetAssetPathByName(info.TargetClass.Name);
                if (path != null && IsAppropriateTarget(path))
                {
                    outEmitterPathToCachedInfo.TryAdd(path, info);
                }
            }

            return outEmitterPathToCachedInfo.Count > 0;
        }

        static bool RunGenerators(string[] targetPaths, bool runReferencingEmittersNow)
        {
            var generatorsToRun = new Dictionary<string, CachedTypeInfo>();
            var emittersToRun = new Dictionary<string, CachedTypeInfo>();
            for (int i = 0; i < targetPaths.Length; i++)
            {
                if (!TryGetGeneratorInfoOrEmitterName(targetPaths[i], out var generatorInfo, out var emitterName))
                    continue;

                if (generatorInfo != null)
                    generatorsToRun.TryAdd(targetPaths[i], generatorInfo);

                if (emitterName != null)
                {
                    if (TryGetEmitterInfo(emitterName, out var emitterPathToInfo))
                        foreach (var path_info in emitterPathToInfo)
                        {
                            emittersToRun.TryAdd(path_info.Key, path_info.Value);
                        }
                }
            }

        RUN_EMITTERS_NOW:
            if (runReferencingEmittersNow)
            {
                foreach (var path_info in emittersToRun)
                {
                    generatorsToRun.TryAdd(path_info.Key, path_info.Value);
                }
                emittersToRun.Clear();
            }

            bool somethingUpdated = false;
            try
            {
                var pathsToImportSet = new HashSet<string>();
                foreach (var path_info in generatorsToRun)
                {
                    if (TryEmit(path_info.Key, path_info.Value))
                    {
                        somethingUpdated = true;
                        pathsToImportSet.Add(GetGeneratorOutputPath(path_info.Key, path_info.Value.OutputFileName));
                    }
                }

                // referencing emitters run immediately if nothing updated.
                if (!somethingUpdated && emittersToRun.Count > 0)
                {
                    runReferencingEmittersNow = true;
                    goto RUN_EMITTERS_NOW;
                }
                // or queue on next update
                foreach (var path_info in emittersToRun)
                {
                    // don't re-run
                    if (generatorsToRun.ContainsKey(path_info.Key))
                        continue;
                    pathsToImportSet.Add(path_info.Key);
                }

                //import
                if (!BuildPipeline.isBuildingPlayer)
                {
                    foreach (var path in pathsToImportSet)
                    {
                        if (generatorsToRun.ContainsKey(path))
                            _settings.PathsToSkipImportEvent.TryAddUnique(path);
                        AssetDatabase.ImportAsset(path);
                        somethingUpdated = true;
                    }

                    if (somethingUpdated)
                        //// need a delay?
                        //EditorApplication.delayCall += () =>
                        AssetDatabase.Refresh();
                }
            }
            finally
            {
                foreach (var path_info in generatorsToRun)
                    _settings.PathsToIgnoreOverwriteSettingOnAttribute.TryRemove(path_info.Key);
            }

            return somethingUpdated;
        }


        ///<summary>Run specified generator upon request. Designed for use in Unity build event.</summary>
        ///<param name="assetsRelPath">Path need to be started with "Assets/"</param>
        ///<param name="autoRunReferencingEmittersNow">Set true to run referencing emitters immediately. For use in build event.</param>
        ///<returns>true if file is written</returns>
        public static bool ProcessFile(string assetsRelPath,
            bool ignoreOverwriteSettingOnAttribute, bool autoRunReferencingEmittersNow = false)
        {
            if (ignoreOverwriteSettingOnAttribute)
                _settings.PathsToIgnoreOverwriteSettingOnAttribute.TryAddUnique(assetsRelPath);
            return RunGenerators(new string[] { assetsRelPath }, autoRunReferencingEmittersNow);
        }


        static bool TryGetGeneratorInfoOrEmitterName(
            string assetsRelPath, out CachedTypeInfo outGeneratorInfo, out string outEmitterName)
        {
            if (!File.Exists(assetsRelPath))
                throw new FileNotFoundException(assetsRelPath);


            outGeneratorInfo = null;
            outEmitterName = null;

            var clsName = Path.GetFileNameWithoutExtension(assetsRelPath);

            // find generator from emitted file.
            if (!TypeNameToInfo.ContainsKey(clsName))
            {
                // NOTE: When generated code has error, fix it and save will invoke Unity
                //       import event and then same error code will be generated again.
                //       Emit from generated file should only work when forced.
                //       (delaying code generation won't solve this behaviour...? return anyway)
                if (!_settings.PathsToIgnoreOverwriteSettingOnAttribute.Contains(assetsRelPath))
                    return false;

                // try find generator
                if (!clsName.EndsWith(GENERATOR_EXT))
                    return false;

                clsName = Path.GetFileNameWithoutExtension(clsName);
                clsName = Path.GetExtension(clsName);

                if (clsName.Length == 0) return false;
                clsName = clsName.Substring(1);

                if (!TypeNameToInfo.ContainsKey(clsName))
                    return false;
            }


            var info = TypeNameToInfo[clsName];
            if (info == null) return false;

            if (info.TargetClass == null)
            {
                outEmitterName = clsName;
                return true;
            }


            if (!TryBuildOutputFileName(info))
            {
                Debug.LogError($"[{nameof(UnitySourceGenerator)}] Output file name is invalid: {info.OutputFileName}");
                return false;
            }

            outGeneratorInfo = info;
            return true;
        }


        static bool TryEmit(string assetsRelPath, CachedTypeInfo info)
        {
            var generatorCls = info.Attribute.GeneratorClass ?? info.TargetClass;
            string outputPath = GetGeneratorOutputPath(assetsRelPath, info.OutputFileName);

            var context = new USGContext
            {
                TargetClass = info.TargetClass,
                AssetPath = assetsRelPath.Replace('\\', '/'),
                OutputPath = outputPath.Replace('\\', '/'),
            };

            var sb = new StringBuilder();
            sb.AppendLine($"// <auto-generated>{generatorCls.Name}</auto-generated>");

            var isSaveFile = false;
            try
            {
                isSaveFile = (bool)info.EmitMethod.Invoke(null, new object[] { context, sb });
            }
            catch
            {
                //Debug.LogError($"[{nameof(UnitySourceGenerator)}] Unhandled Error on Emit(): {generatorCls}");
                throw;
            }

            //save??
            if (!isSaveFile || sb == null || string.IsNullOrWhiteSpace(context.OutputPath))
                return false;

            // NOTE: overwrite check must be done after Emit() due to allowing output path modification.
            // TODO: code generation happens but file is not written when overwrite is disabled.
            //       any way to skip code generation?
            if (File.Exists(context.OutputPath))
            {
                if (!info.Attribute.OverwriteIfFileExists &&
                    !_settings.PathsToIgnoreOverwriteSettingOnAttribute.Contains(assetsRelPath))
                    return false;
            }


            var outputDir = Path.GetDirectoryName(context.OutputPath);
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

#if UNITY_2021_3_OR_NEWER

            // OPTIMIZE: use sb.GetChunks() in future release of Unity. 2021 LTS doesn't support it.
            using (var fs = new FileStream(context.OutputPath, FileMode.Create, FileAccess.Write))
            {
                //Span<byte> buffer = stackalloc byte[BUFFER_LENGTH];
                Span<byte> buffer = ArrayPool<byte>.Shared.Rent(BUFFER_LENGTH).AsSpan(0, BUFFER_LENGTH);
                var span = sb.ToString().AsSpan();
                int len, written;
                for (int start = 0; start < span.Length; start += BUFFER_MAX_CHAR_LENGTH)
                {
                    len = BUFFER_MAX_CHAR_LENGTH;
                    if (len + start > span.Length) len = span.Length - start;

                    written = info.Attribute.OutputFileEncoding.GetBytes(span.Slice(start, len), buffer);
                    fs.Write(buffer.Slice(0, written));
                }
                fs.Flush();
            }

#else
            File.WriteAllText(context.OutputPath, sb.ToString(), info.Attribute.OutputFileEncoding);
#endif

            Debug.Log($"[{nameof(UnitySourceGenerator)}] Generated: {context.OutputPath}",
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(context.OutputPath));
            return true;
        }



        //internals----------------------------------------------------------------------

        static string GetGeneratorOutputFileNameFromGeneratorPath(string generatorPath)
        {
            var clsName = Path.GetFileNameWithoutExtension(generatorPath);
            return TypeNameToInfo.FirstOrDefault(x => x.Key == clsName).Value?.OutputFileName;
        }

        ///<param name="generatorPath">Relative path from Unity project directory.</param>
        ///<param name="fileName">null to auto retrieve from database.</param>
        internal static string GetGeneratorOutputPath(string generatorPath, string fileName)
        {
            fileName ??= GetGeneratorOutputFileNameFromGeneratorPath(generatorPath);

            // NOTE: use relative path, not full path.
            //       revert it back to full path if something went wrong.
            string outputPath = Path.GetDirectoryName(generatorPath).Replace('\\', '/');
            if (!outputPath.EndsWith(GENERATOR_DIR))
                outputPath += GENERATOR_DIR;
            return outputPath + '/' + fileName;
        }


        static readonly BindingFlags METHOD_FLAGS =
                                        BindingFlags.NonPublic |
                                        BindingFlags.Public |
                                        BindingFlags.Static
                                        ;

        // OPTIMIZE: Be ref struct?
        internal class CachedTypeInfo
        {
            // TODO: more streamlined.
            ///<summary>null if generator is only referenced from emitter classes.</summary>
            public Type TargetClass;
            public UnitySourceGeneratorAttribute Attribute;

            public string OutputFileName;
            public MethodInfo EmitMethod;
            public MethodInfo OutputFileNameMethod;
        }


        internal readonly static Dictionary<string, CachedTypeInfo> TypeNameToInfo = new();

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            /* NOTE: truncate unnecessary DLLs.
            //       build LINQ here and copy paste resulting .Where() below
            var assems = AppDomain.CurrentDomain.GetAssemblies()
                .Where(static x =>
                {
                    var n = x.GetName().Name;
                    if ( //total 234files
                        n.StartsWith("Unity") ||    // truncate to 95
                        n.StartsWith("System.") ||  // truncate to 225
                        n.StartsWith("Mono.") ||    // truncate to 229
                        n == "mscorlib" ||          // tons of types inside
                        n == "System"               // tons of types inside
                    )
                        return false;
                    return true;
                })
                .Select(static x => x.GetName().Name + ": " + x.GetTypes().Count())
                ;
            Debug.Log($"#{assems.Count()} " + string.Join("\n", assems));
            */


            // OPTIMIZE: ReflectionOnlyGetType() can be used??
            var infos = AppDomain.CurrentDomain.GetAssemblies()
                .Where(static x =>
                {
                    var n = x.GetName().Name;
                    if (
                        n.StartsWith("Unity") ||    // truncate 234 files to 95
                        n.StartsWith("System.") ||  // these have tons of types inside
                        n.StartsWith("Mono.") ||
                        n == "System" ||
                        n == "mscorlib"
                    )
                        return false;
                    return true;
                })
                .SelectMany(static a => a.GetTypes())
                .Where(static t =>
                    t.GetCustomAttribute<UnitySourceGeneratorAttribute>(false) != null &&
                    // waiting for C# 11.0 //typeof(IUnitySourceGenerator).IsAssignableFrom(t) &&
                    !TypeNameToInfo.ContainsKey(t.Name)
                    )
                .Select(static t =>
                {
                    var attr = t.GetCustomAttribute<UnitySourceGeneratorAttribute>(false);
                    return new CachedTypeInfo
                    {
                        TargetClass = t,
                        Attribute = attr,
                    };
                })
                ;


            // TODO: Export constants definition
            foreach (var info in infos)
            {
                //Debug.Log($"[{nameof(UnitySourceGenerator)}] Processing...: {info.ClassName}");

                var generatorCls = info.Attribute.GeneratorClass ?? info.TargetClass;
                var outputMethod = generatorCls.GetMethod("OutputFileName", METHOD_FLAGS, null, Type.EmptyTypes, null);
                var emitMethod = generatorCls.GetMethod("Emit", METHOD_FLAGS, null, new Type[] { typeof(USGContext), typeof(StringBuilder) }, null);

                if (outputMethod == null || emitMethod == null)
                {
                    Debug.LogError($"[{nameof(UnitySourceGenerator)}] Required static method(s) not found: {generatorCls}");
                    continue;
                }

                info.EmitMethod = emitMethod;
                info.OutputFileNameMethod = outputMethod;

                //filename??
                if (!TryBuildOutputFileName(info))
                {
                    Debug.LogError($"[{nameof(UnitySourceGenerator)}] Output file name is invalid: {info.OutputFileName}");
                    continue;
                }


                TypeNameToInfo.TryAdd(info.TargetClass.Name, info);
                if (generatorCls != info.TargetClass)
                {
                    // TODO: more streamlined.
                    var genInfo = new CachedTypeInfo
                    {
                        TargetClass = null,
                        OutputFileName = null,
                        EmitMethod = null,
                        OutputFileNameMethod = null,
                        Attribute = info.Attribute,
                    };

                    TypeNameToInfo.TryAdd(generatorCls.Name, genInfo);
                }


            }//foreach
        }


        static bool TryBuildOutputFileName(CachedTypeInfo info)
        {
            info.OutputFileName = (string)info.OutputFileNameMethod?.Invoke(null, null);
            if (string.IsNullOrWhiteSpace(info.OutputFileName))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(info.OutputFileName);
            string fileExt = Path.GetExtension(info.OutputFileName);
            info.OutputFileName = fileName + GENERATOR_PREFIX + info.TargetClass.Name;
            if (info.Attribute.GeneratorClass != null)
                info.OutputFileName += GENERATOR_PREFIX + info.Attribute.GeneratorClass.Name;
            info.OutputFileName += GENERATOR_EXT + fileExt;

            return true;
        }


    }
}
#endif
