#if UNITY_EDITOR

using System.IO;
using UnityEditor;

namespace SatorImaging.UnitySourceGenerator
{
    static class EditorExtension
    {
        const string ROOT_MENU = @"Assets/Unity Source Generator/";
        const string TEMPLATE_PATH = @"Packages/com.sator-imaging.alt-source-generator/Template/Template_";
        const string CS_EXT = @".cs";
        const string TXT_EXT = @".txt";


        [MenuItem(ROOT_MENU + "Force Generate while Overwriting Disabled")]
        static void ForceGenerateSelectedScripts()
        {
            // NOTE: when multiple files selected, first import event initialize C# environment.
            //       --> https://docs.unity3d.com/2021.3/Documentation/Manual/DomainReloading.html
            //       need to use USGEngine.ProcessFile() instead.
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                //USGEngine.IgnoreOverwriteSettingOnAttribute = true;
                //USGEngine.ProcessFile(path);
                USGUtility.ForceGenerateByName(Path.GetFileNameWithoutExtension(path), false);
            }
        }


        [MenuItem(ROOT_MENU + "Method Generator Template", priority = 100)]
        static void MethodGenerator()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                TEMPLATE_PATH + nameof(MethodGenerator) + TXT_EXT,
                nameof(MethodGenerator) + CS_EXT);
        }


        [MenuItem(ROOT_MENU + "Self-Emit Generator Template", priority = 100)]
        static void SelfEmitGenerator()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                TEMPLATE_PATH + nameof(SelfEmitGenerator) + TXT_EXT,
                nameof(SelfEmitGenerator) + CS_EXT);
        }


    }
}

#endif
