using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace SatorImaging.UnitySourceGenerator
{
    // HOW TO USE: Add the following attribute to *target* class.
    //             enum, ScenesInBuild will be generated in the target class namespace.
    //[UnitySourceGenerator(typeof(SceneBuildIndexEnumGenerator))]
    public class SceneBuildIndexGenerator

#if UNITY_EDITOR
        // NOTE: class definition is required to avoid build error due to referencing from USG attributes.
        //       (or exclude [UnitySourceGenerator(typeof(...))] attributes from build)
        : IPreprocessBuildWithReport
#endif

    {

#if UNITY_EDITOR   // USG: class definition is required to avoid build error but methods are not.
#pragma warning disable IDE0051

        static string OutputFileName() => "Enum.cs";  // -> Test.<ClassName>.g.cs


        const string ENUM_NAME = nameof(SceneBuildIndex);
        readonly static Regex RE_REMOVE_INVALID = new Regex(@"[^A-Za-z_0-9]+", RegexOptions.Compiled);

        static bool Emit(USGContext context, StringBuilder sb)
        {
            // code generation
            sb.Append($@"
namespace {context.TargetClass.Namespace}
{{
    public enum {ENUM_NAME}
    {{
");
            /*  enum  ================================================================ */
            sb.IndentLevel(2);

            var scenePathList = new List<string>();
            var sceneNameList = new List<string>();
            for (int i = 0; i < UnityEditor.EditorBuildSettings.scenes.Length; i++)
            {
                var scene = UnityEditor.EditorBuildSettings.scenes[i];  //SceneManager.GetSceneAt(i);
                scenePathList.Add(scene.path);
                sceneNameList.Add(Path.GetFileNameWithoutExtension(scene.path));

                var name = RE_REMOVE_INVALID.Replace(Path.GetFileNameWithoutExtension(scene.path), "_");
                sb.IndentLine($"{name} = {i},");
            }

            //----------------------------------------------------------------------
            sb.Append($@"
    }}

    public static class {ENUM_NAME}Resolver
    {{
        readonly static string[] Paths = new string[]
        {{
");
            /*  paths  ================================================================ */
            sb.IndentLevel(3);
            foreach (var path in scenePathList)
            {
                sb.IndentLine($"\"{path}\",");
            }
            //----------------------------------------------------------------------
            sb.Append($@"
        }};

        readonly static string[] Names = new string[]
        {{
");
            /*  names  ================================================================ */
            sb.IndentLevel(3);
            foreach (var name in sceneNameList)
            {
                sb.IndentLine($"\"{name}\",");
            }
            //----------------------------------------------------------------------
            sb.Append($@"
        }};

        public static {ENUM_NAME} GetByName(string name)
        {{
            int found = -1;
            for (int i = 0; i < Names.Length; i++)
            {{
                if (Names[i] == name)
                {{
                    if (found < 0)
                        found = i;
                    else
                        throw new System.Exception($""multiple scenes are found: '{{name}}'"");
                }}
            }}
            if (found < 0)
                throw new System.Exception($""scene file '{{name}}' is not registered in build settings."");
            return ({ENUM_NAME})found;
        }}

        public static System.Collections.Generic.List<{ENUM_NAME}> GetListByPrefix(string fileNamePrefix)
        {{
            var ret = new System.Collections.Generic.List<{ENUM_NAME}>(capacity: Names.Length);
            for (int i = 0; i < Names.Length; i++)
            {{
                if (Names[i].StartsWith(fileNamePrefix, System.StringComparison.Ordinal))
                {{
                    ret.Add(({ENUM_NAME})i);
                }}
            }}
            return ret;
        }}

        ///<summary>Path must be started with 'Assets/'.</summary>
        public static System.Collections.Generic.List<{ENUM_NAME}> GetListByPath(string assetsPath)
        {{
            var ret = new System.Collections.Generic.List<{ENUM_NAME}>(capacity: Paths.Length);
            for (int i = 0; i < Paths.Length; i++)
            {{
                if (Paths[i].StartsWith(assetsPath, System.StringComparison.Ordinal))
                {{
                    ret.Add(({ENUM_NAME})i);
                }}
            }}
            return ret;
        }}
    }}
}}
");
            return true;
        }


        /*  events  ================================================================ */

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => ForceUpdate();

        static void ForceUpdate() => USGUtility.ForceGenerateByType(typeof(SceneBuildIndexGenerator), false);

        [InitializeOnLoadMethod]
        static void RegisterEvent()
        {
            EditorBuildSettings.sceneListChanged -= ForceUpdate;
            EditorBuildSettings.sceneListChanged += ForceUpdate;
        }


#pragma warning restore IDE0051
#endif
    }
}
