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

namespace SatorImaging.UnitySourceGenerator.Editor
{
    /// <summary>
    /// > [!WARNING]
    /// > Works only on Unity Editor
    /// </summary>
    public sealed class USGEngine : AssetPostprocessor
    {
        const int BUFFER_LENGTH = 1024 * 64;
        const int BUFFER_MAX_CHAR_LENGTH = BUFFER_LENGTH / 3;  // worst case of UTF-8
        const string GENERATOR_PREFIX = ".";
        const string GENERATOR_EXT = ".g";
        const string GENERATOR_DIR = @"/USG.g";   // don't append last slash. used to determine file is generated one or not.
        const string ASSETS_DIR_NAME = "Assets";
        const string ASSETS_DIR_SLASH = ASSETS_DIR_NAME + "/";
        const string TARGET_FILE_EXT = @".cs";
        const string PATH_PREFIX_TO_IGNORE = @"Packages/";
        private const string METHOD_NAME_OUTPUT_FILE_NAME = "OutputFileName";
        private const string METHOD_NAME_EMIT = "Emit";
        readonly static char[] DIR_SEPARATORS = new char[] { '\\', '/' };


        static bool IsAppropriateTarget(string filePath)
        {
            if (!filePath.EndsWith(TARGET_FILE_EXT, StringComparison.OrdinalIgnoreCase) ||
                !filePath.StartsWith(ASSETS_DIR_SLASH, StringComparison.OrdinalIgnoreCase))
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

            // TODO: Unity sometimes reloads scripts in background automatically.
            //       (it happens when Save All command was done in Visual Studio, for example.)
            //       In this situation, code generation will be done with script data right before saving.
            //       so that generated code is not what expected, and this behaviour cannot be solved on C#.
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
                if (_settings.PathsToSkipImportEvent.TryRemove(importedAssets[i]))
                    continue;
                if (_settings.AutoEmitDisabledPaths.Contains(importedAssets[i], StringComparer.Ordinal))
                    continue;
                if (!IsAppropriateTarget(importedAssets[i]))
                    continue;

                _settings.ImportedScriptPaths.TryAddUnique(importedAssets[i]);
            }
            _settings.PathsToSkipImportEvent.Clear();
            _settings.Save();

            // NOTE: processing files are done in CompilationPipeline callback.
        }


        // NOTE: event registration is done in InitializeOnLoad
        static void OnCompilationFinished(object context)
        {
            if (!_settings.AutoEmitOnScriptUpdate)
                return;

            try
            {
                RunGenerators(_settings.ImportedScriptPaths.ToArray());//, false);
            }
            catch
            {
                _settings.ImportedScriptPaths.Clear();
                _settings.Save();
                throw;
            }
        }


        static bool RunGenerators(string[] targetPaths)
        {
            bool somethingUpdated = false;
            try
            {
                var pathsToImportSet = new HashSet<string>();

                for (int i = 0; i < targetPaths.Length; i++)
                {
                    if (!TryGetTargetOrGeneratorClassByPath(targetPaths[i], out var targetOrGeneratorCls))
                        continue;

                    // NOTE: need to search both target and generator
                    foreach (var info in _generatorInfoList
                        .Where(x => x.TargetClass == targetOrGeneratorCls || x.Attribute.GeneratorClass == targetOrGeneratorCls))
                    {
                        if (TryEmit(info))
                        {
                            somethingUpdated = true;
                            pathsToImportSet.Add(GetGeneratorOutputPath(info));
                        }
                    }
                }

                //import
                if (!BuildPipeline.isBuildingPlayer)
                {
                    foreach (var path in pathsToImportSet)
                    {
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
                for (int i = 0; i < targetPaths.Length; i++)
                {
                    _settings.PathsToIgnoreOverwriteSettingOnAttribute.TryRemove(targetPaths[i]);
                }
            }

            return somethingUpdated;
        }


        static bool TryGetTargetOrGeneratorClassByPath(string assetsRelPath, out Type targetOrGeneratorCls)
        {
            if (!File.Exists(assetsRelPath))
                throw new FileNotFoundException(assetsRelPath);

            targetOrGeneratorCls = null;

            var generatorClsName = Path.GetFileNameWithoutExtension(assetsRelPath);

            // NOTE: File naming convention
            //       Emitter: <Prefix>.<EmitterClassName>.<GeneratorClassName>.g.cs
            //       SelfGen: <Prefix>.<GeneratorClassName>.g.cs
            if (!generatorClsName.EndsWith(GENERATOR_EXT, StringComparison.OrdinalIgnoreCase))
            {
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(assetsRelPath) is not MonoScript mono)
                    throw new NotSupportedException("path is not script file: " + assetsRelPath);

                targetOrGeneratorCls = mono.GetClass();
            }
            else  // try find generator for .g.cs file
            {
                // NOTE: When generated code has error, fix it and save will invoke Unity
                //       import event and then same error code will be generated again.
                //       Emit from generated file should only work when forced.
                //       (delaying code generation won't solve this behaviour...? return anyway)
                if (!_settings.PathsToIgnoreOverwriteSettingOnAttribute.Contains(assetsRelPath, StringComparer.Ordinal))
                    return false;

                generatorClsName = Path.GetFileNameWithoutExtension(generatorClsName);
                generatorClsName = Path.GetExtension(generatorClsName);
                if (generatorClsName.Length == 0)
                    return false;
                generatorClsName = generatorClsName.Substring(1);

                var found = _generatorInfoList.FirstOrDefault(x => x.Attribute.GeneratorClass.Name == generatorClsName);
                if (found == default)
                    return false;

                targetOrGeneratorCls = found.Attribute.GeneratorClass;
            }

            return true;
        }


        static bool TryEmit(CachedGeneratorInfo info)
        {
            var assetsRelPath = USGUtility.GetAssetPathByType(info.TargetClass);
            if (assetsRelPath == null)
                throw new FileNotFoundException("target class not found.");

            var generatorCls = info.Attribute.GeneratorClass;
            string outputPath = GetGeneratorOutputPath(info);

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
                    !_settings.PathsToIgnoreOverwriteSettingOnAttribute.Contains(assetsRelPath, StringComparer.Ordinal))
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
                var rentalBuffer = ArrayPool<byte>.Shared.Rent(BUFFER_LENGTH);
                try
                {
                    Span<byte> buffer = rentalBuffer;
                    var span = sb.ToString().AsSpan();
                    int len, written;
                    for (int start = 0; start < span.Length; start += BUFFER_MAX_CHAR_LENGTH)
                    {
                        len = BUFFER_MAX_CHAR_LENGTH;
                        if (len + start > span.Length)
                            len = span.Length - start;

                        written = info.Attribute.OutputFileEncoding.GetBytes(span.Slice(start, len), buffer);
                        fs.Write(buffer.Slice(0, written));
                    }
                    fs.Flush();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentalBuffer);
                }
            }

#else
            File.WriteAllText(context.OutputPath, sb.ToString(), info.Attribute.OutputFileEncoding);
#endif

            Debug.Log($"[{nameof(UnitySourceGenerator)}] Generated: {context.OutputPath}",
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(context.OutputPath));
            return true;
        }


        /*  entry  ================================================================ */

        ///<summary>Run specified generator upon request. Designed for use in Unity build event.</summary>
        ///<param name="assetsRelPath">Path need to be started with "Assets/"</param>
        ///<param name="autoRunReferencingEmittersNow">Set true to run referencing emitters immediately. For use in build event.</param>
        ///<returns>true if file is written</returns>
        [Obsolete("use USGUtility.ForceGenerateByType() instead.")]
        public static bool ProcessFile(string assetsRelPath, bool ignoreOverwriteSettingOnAttribute
            /* TODO: remove for future release */
            , bool autoRunReferencingEmittersNow = false)
            => Process(new string[] { assetsRelPath }, ignoreOverwriteSettingOnAttribute);


        internal static bool Process(string[] assetsRelPaths, bool ignoreOverwriteSettingOnAttribute)
        {
            if (ignoreOverwriteSettingOnAttribute)
            {
                for (int i = 0; i < assetsRelPaths.Length; i++)
                    _settings.PathsToIgnoreOverwriteSettingOnAttribute.TryAddUnique(assetsRelPaths[i]);
            }
            return RunGenerators(assetsRelPaths);
        }


        /*  utility  ================================================================ */

        ///<returns>throw if failed.</returns>
        internal static string GetGeneratorOutputPath(CachedGeneratorInfo info)
        {
            var fileName = info.OutputFileName;
            if (fileName == null || fileName.Length == 0)
                throw new Exception("cannot retrieve output path.");

            string dirPath = USGUtility.GetAssetPathByType(info.TargetClass);
            if (dirPath == null)
                throw new FileNotFoundException("generator script file is not found.");

            dirPath = Path.GetDirectoryName(dirPath).Replace('\\', '/');
            if (!dirPath.EndsWith(GENERATOR_DIR, StringComparison.OrdinalIgnoreCase))
                dirPath += GENERATOR_DIR;

            return dirPath + '/' + fileName;
        }


        /*  typedef  ================================================================ */

        internal sealed class CachedGeneratorInfo
        {
            public Type TargetClass { get; internal init; }
            public UnitySourceGeneratorAttribute Attribute { get; internal init; }

            public string OutputFileName { get; internal set; }
            public MethodInfo EmitMethod { get; internal set; }
            public MethodInfo OutputFileNameMethod { get; internal set; }
        }


        /*  initialize  ================================================================ */

        static readonly BindingFlags METHOD_FLAGS
                                        = BindingFlags.NonPublic
                                        | BindingFlags.Public
                                        | BindingFlags.Static
                                        ;


        readonly static List<CachedGeneratorInfo> _generatorInfoList = new();
        internal static IReadOnlyList<CachedGeneratorInfo> GeneratorInfoList => _generatorInfoList;


        [InitializeOnLoadMethod]
        static void InitializeOnLoad()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;


            // fantastic UnityEditor.TypeCache system!!
            var generatorInfos = TypeCache.GetTypesWithAttribute<UnitySourceGeneratorAttribute>()
                .SelectMany(static t =>
                {
                    var attrs = t.GetCustomAttributes<UnitySourceGeneratorAttribute>(false);
                    var ret = new CachedGeneratorInfo[attrs.Count()];
                    for (int i = 0; i < ret.Length; i++)
                    {
                        ret[i] = new CachedGeneratorInfo
                        {
                            TargetClass = t,
                            Attribute = attrs.ElementAt(i),
                        };
                    }
                    return ret;
                })
                ;


            foreach (var generatorInfo in generatorInfos)
            {
                // NOTE: self-emit generators which initialized without generator type parameter,
                //       need to fill it correctly.
                generatorInfo.Attribute._generatorClass ??= generatorInfo.TargetClass;

                var generatorCls = generatorInfo.Attribute.GeneratorClass;
                var outputMethod = generatorCls.GetMethod(METHOD_NAME_OUTPUT_FILE_NAME, METHOD_FLAGS, null, Type.EmptyTypes, null);
                var emitMethod = generatorCls.GetMethod(METHOD_NAME_EMIT, METHOD_FLAGS, null, new Type[] { typeof(USGContext), typeof(StringBuilder) }, null);

                if (outputMethod == null || emitMethod == null)
                {
                    Debug.LogError($"[{nameof(UnitySourceGenerator)}] Required static method(s) not found: {generatorCls}");
                    continue;
                }

                generatorInfo.EmitMethod = emitMethod;
                generatorInfo.OutputFileNameMethod = outputMethod;

                //filename??
                if (!TryBuildOutputFileName(generatorInfo))
                {
                    Debug.LogError($"[{nameof(UnitySourceGenerator)}] Output file name is invalid: {generatorInfo.OutputFileName}");
                    continue;
                }

                //register!!
                _generatorInfoList.Add(generatorInfo);
            }
        }


        static bool TryBuildOutputFileName(CachedGeneratorInfo info)
        {
            info.OutputFileName = (string)info.OutputFileNameMethod?.Invoke(null, null);
            if (string.IsNullOrWhiteSpace(info.OutputFileName))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(info.OutputFileName);
            string fileExt = Path.GetExtension(info.OutputFileName);
            info.OutputFileName = fileName + GENERATOR_PREFIX + info.TargetClass.Name;
            if (info.Attribute.GeneratorClass != info.TargetClass)
                info.OutputFileName += GENERATOR_PREFIX + info.Attribute.GeneratorClass.Name;
            info.OutputFileName += GENERATOR_EXT + fileExt;

            return true;
        }


    }
}
#endif
