#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;


namespace SatorImaging.UnitySourceGenerator
{
    public class USGEngine : AssetPostprocessor
    {
        ///<summary>This will be disabled automatically after Unity Editor import event.</summary>
        public static bool IgnoreOverwriteSettingByAttribute = false;


        const int BUFFER_LENGTH = 61_440;
        const int BUFFER_MAX_CHAR_LENGTH = BUFFER_LENGTH / 3;  // worst case of UTF-8
        const string GENERATOR_PREFIX = ".";
        const string GENERATOR_EXT = ".g";
        const string GENERATOR_DIR = @"/USG.g";   // don't append last slash. used to determine file is generated one or not.
        const string ASSETS_DIR_NAME = "Assets";
        const string ASSETS_DIR_SLASH = ASSETS_DIR_NAME + "/";
        const string TARGET_FILE_EXT = @".cs";
        const string PATH_PREFIX_TO_IGNORE = @"Packages/";
        readonly static char[] DIR_SEPARATORS = new char[] { '\\', '/' };

        // OPTIMIZE: Avoiding explicit static ctor is best practice for performance???
        readonly static string s_projectDirPath;
        static USGEngine()
        {
            s_projectDirPath = Application.dataPath.TrimEnd(DIR_SEPARATORS);
            if (s_projectDirPath.EndsWith(ASSETS_DIR_NAME))
                s_projectDirPath = s_projectDirPath.Substring(0, s_projectDirPath.Length - ASSETS_DIR_NAME.Length);
        }

        static bool IsAppropriateTarget(string filePath)
        {
            if (!filePath.EndsWith(TARGET_FILE_EXT) ||
                !filePath.StartsWith(ASSETS_DIR_SLASH))
            {
                return false;
            }
            return true;
        }


        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // NOTE: Do NOT handle deleted assets because Unity tracking changes perfectly.
            //       Even if delete file while Unity shutted down, asset deletion event happens on next Unity launch.
            //       As a result, delete/import event loops infinitely and file cannot be deleted.

            // TODO: Unity sometimes reloads updated scripts by Visual Studio in background automatically.
            //       In this situation, code generation will be done with script data right before saving.
            //       It cannot be solved on C#, simply restart Unity.
            //       Using [DidReloadScripts] or EditorApplication.delayCall, It works fine with Reimport
            //       menu command but OnPostprocessAllAssets event doesn't work as expected.
            //       (script runs with static field cleared even though .Clear() is only in ProcessingFiles().
            //        it's weird that event happens and asset paths retrieved but hashset items gone.)
            ////EditorApplication.delayCall += () =>
            {
                ProcessingFiles(importedAssets);
            };
        }


        readonly static HashSet<string> s_updatedGeneratorNames = new();
        static void ProcessingFiles(string[] targetPaths)
        {
            bool somethingUpdated = false;
            for (int i = 0; i < targetPaths.Length; i++)
            {
                // NOTE: Do NOT early return in this method.
                //       check path here to allow generator class can be lie outside of Assets/ folder.
                if (!IsAppropriateTarget(targetPaths[i])) continue;

                if (ProcessFile(targetPaths[i]))
                    somethingUpdated = true;
            }

            // TODO: more efficient way to process related targets
            var overwriteEnabledByCaller = IgnoreOverwriteSettingByAttribute;
            foreach (var generatorName in s_updatedGeneratorNames)
            {
                foreach (var info in s_typeNameToInfo.Values)
                {
                    if (info.TargetClass == null ||
                        info.Attribute.GeneratorClass?.Name != generatorName)
                        continue;

                    var path = USGUtility.GetAssetPathByName(info.TargetClass.Name);
                    if (path != null && IsAppropriateTarget(path))
                    {
                        IgnoreOverwriteSettingByAttribute = overwriteEnabledByCaller
                            || info.Attribute.OverwriteIfFileExists;
                        if (ProcessFile(path))
                            somethingUpdated = true;
                    }
                }
            }

            if (somethingUpdated)
                AssetDatabase.Refresh();

            s_updatedGeneratorNames.Clear();

            IgnoreOverwriteSettingByAttribute = false;  // always turn it off.
        }


        ///<summary>This method respects "OverwriteIfFileExists" attribute setting.</summary>
        ///<param name="assetsRelPath">Path need to be started with "Assets/"</param>
        ///<returns>true if file is written</returns>
        public static bool ProcessFile(string assetsRelPath)
        {
            if (!File.Exists(assetsRelPath)) throw new FileNotFoundException(assetsRelPath);


            var clsName = Path.GetFileNameWithoutExtension(assetsRelPath);

            if (!s_typeNameToInfo.ContainsKey(clsName))
            {
                if (!clsName.EndsWith(GENERATOR_EXT))
                    return false;

                // try find generator
                clsName = Path.GetFileNameWithoutExtension(clsName);
                clsName = Path.GetExtension(clsName);

                if (clsName.Length == 0) return false;
                clsName = clsName.Substring(1);

                if (!s_typeNameToInfo.ContainsKey(clsName))
                    return false;
            }


            var info = s_typeNameToInfo[clsName];
            if (info == null) return false;

            // TODO: more streamlined.
            if (info.TargetClass == null)
            {
                s_updatedGeneratorNames.Add(clsName);
                return false;
            }


            if (!TryBuildOutputFileName(info))
            {
                Debug.LogError($"[{nameof(UnitySourceGenerator)}] Output file name is invalid: {info.OutputFileName}");
                return false;
            }

            var generatorCls = info.Attribute.GeneratorClass ?? info.TargetClass;


            // build path
            string outputPath = Path.Combine(s_projectDirPath, Path.GetDirectoryName(assetsRelPath)).Replace('\\', '/');
            if (!outputPath.EndsWith(GENERATOR_DIR)) outputPath += GENERATOR_DIR;

            outputPath = Path.Combine(outputPath, info.OutputFileName);


            // do it.
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
                Debug.LogError($"[{nameof(UnitySourceGenerator)}] Unhandled Error on Emit(): {generatorCls}");
                throw;
            }

            //save??
            if (!isSaveFile || sb == null || string.IsNullOrWhiteSpace(context.OutputPath))
                return false;

            if (File.Exists(context.OutputPath) &&
                (!info.Attribute.OverwriteIfFileExists && !IgnoreOverwriteSettingByAttribute)
                )
            {
                return false;
            }


            var outputDir = Path.GetDirectoryName(context.OutputPath);
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

#if UNITY_2021_3_OR_NEWER

            // OPTIMIZE: use sb.GetChunks() in future release of Unity. 2021 LTS doesn't support it.
            using (var fs = new FileStream(context.OutputPath, FileMode.Create, FileAccess.Write))
            {
                Span<byte> buffer = stackalloc byte[BUFFER_LENGTH];
                var span = sb.ToString().AsSpan();
                for (int start = 0; start < span.Length; start += BUFFER_MAX_CHAR_LENGTH)
                {
                    var len = BUFFER_MAX_CHAR_LENGTH;
                    if (len + start > span.Length) len = span.Length - start;

                    int written = info.Attribute.OutputFileEncoding.GetBytes(span.Slice(start, len), buffer);
                    fs.Write(buffer.Slice(0, written));
                }
                fs.Flush();
            }

#else
            File.WriteAllText(context.OutputPath, sb.ToString(), info.Attribute.OutputFileEncoding);
#endif

            Debug.Log($"[{nameof(UnitySourceGenerator)}] Generated: {context.OutputPath}");
            return true;
        }



        //internals----------------------------------------------------------------------

        static readonly BindingFlags METHOD_FLAGS =
                                        BindingFlags.NonPublic |
                                        BindingFlags.Public |
                                        BindingFlags.Static
                                        ;

        class CachedTypeInfo
        {
            // TODO: more streamlined.
            ///<summary>null if generator that only referenced from other classes.</summary>
            public Type TargetClass;
            public UnitySourceGeneratorAttribute Attribute;

            public string OutputFileName;
            public MethodInfo EmitMethod;
            public MethodInfo OutputFileNameMethod;
        }


        readonly static Dictionary<string, CachedTypeInfo> s_typeNameToInfo = new();

        [InitializeOnLoadMethod]
        static void CollectTargets()
        {
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
                    !s_typeNameToInfo.ContainsKey(t.Name)
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


                s_typeNameToInfo.TryAdd(info.TargetClass.Name, info);
                if (generatorCls != info.TargetClass)
                {
                    // TODO: more streamlined.
                    //Debug.Log($"[USG] Generator found: {generatorCls.Name}");

                    var genInfo = new CachedTypeInfo
                    {
                        TargetClass = null,
                        OutputFileName = null,
                        EmitMethod = null,
                        OutputFileNameMethod = null,
                        Attribute = info.Attribute,
                    };

                    s_typeNameToInfo.TryAdd(generatorCls.Name, genInfo);
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
