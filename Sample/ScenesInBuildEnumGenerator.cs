using static SatorImaging.UnitySourceGenerator.USGFullNameOf;
using SatorImaging.UnitySourceGenerator;
using System.Text;
using Debug = UnityEngine.Debug;
using System.Text.RegularExpressions;
using System.IO;

namespace SatorImaging.UnitySourceGenerator
{
    // HOW TO USE: Add the following attribute to *target* class.
    //             enum, ScenesInBuild will be generated in the target class namespace.
    //[UnitySourceGenerator(typeof(ScenesInBuildEnumGenerator))]
    public partial class ScenesInBuildEnumGenerator
    {
#if UNITY_EDITOR   // Code Generator methods should not be included in build
#pragma warning disable IDE0051

        static string OutputFileName() => "Enum.cs";  // -> Test.<ClassName>.g.cs


        const string ENUM_NAME = nameof(ScenesInBuild);
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
            //----------------------------------------------------------------------

            sb.IndentLevel(2);
            for (int i = 0; i < UnityEditor.EditorBuildSettings.scenes.Length; i++)
            {
                var scene = UnityEditor.EditorBuildSettings.scenes[i];  //SceneManager.GetSceneAt(i);
                var name = RE_REMOVE_INVALID.Replace(Path.GetFileNameWithoutExtension(scene.path), "_");
                sb.IndentLine($"{name} = {i},");
            }

            //----------------------------------------------------------------------
            sb.Append($@"
    }}
}}
");
            return true;
        }


        static void ForceUpdate() =>
            USGUtility.ForceGenerateByName(nameof(ScenesInBuildEnumGenerator), false);

        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEvent()
        {
            UnityEditor.EditorBuildSettings.sceneListChanged -= ForceUpdate;
            UnityEditor.EditorBuildSettings.sceneListChanged += ForceUpdate;
        }

#pragma warning restore IDE0051
#endif
    }
}
