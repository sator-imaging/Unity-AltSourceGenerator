#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using Object = UnityEngine.Object;


namespace SatorImaging.UnitySourceGenerator
{
    public class USGUtility
    {
        [MenuItem("Assets/Unity Source Generator/Force Generate while Overwriting Disabled")]
        static void ForceGenerateSelectedScripts()
        {
            USGEngine.IgnoreOverwriteSettingByAttribute = true;  // always disabled after import event.

            foreach (var GUID in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(GUID);
                AssetDatabase.ImportAsset(path);
            }
        }


        public static void ForceGenerate(string clsName, bool showInProjectPanel = true)
        {
            var GUIDs = AssetDatabase.FindAssets(clsName);
            foreach (var GUID in GUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(GUID);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName != clsName) continue;

                if (showInProjectPanel)
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));


                USGEngine.IgnoreOverwriteSettingByAttribute = true;  // always disabled after import event.
                AssetDatabase.ImportAsset(path);
                return;
            }
        }


    }
}
#endif
