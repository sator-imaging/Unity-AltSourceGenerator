#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;

namespace SatorImaging.UnitySourceGenerator.Editor
{
    public static class EditorExtension
    {
        const string ROOT_MENU = @"Assets/Unity Source Generator/";
        const string TEMPLATE_PATH = @"Packages/com.sator-imaging.alt-source-generator/Template/Template_";
        const string CS_EXT = @".cs";
        const string TXT_EXT = @".txt";


        [MenuItem(ROOT_MENU + "Force Generate while Overwriting Disabled")]
        static void ForceGenerateSelectedScripts()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
                return;

            // NOTE: when multiple files selected, first import event initialize C# environment.
            //       --> https://docs.unity3d.com/2021.3/Documentation/Manual/DomainReloading.html
            //       so that need to process files at once.
            var filePathList = new List<string>(Selection.assetGUIDs.Length);

            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) is not MonoScript)
                    continue;

                filePathList.Add(path);
            }
            USGEngine.Process(filePathList.ToArray(), true);
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
