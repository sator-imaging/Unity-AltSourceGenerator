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


        ///<summary>Force perform source code generation by class name.</summary>
        ///<param name="showInProjectPanel">works only when Unity is not building app.</param>
        public static void ForceGenerateByName(string clsName, bool showInProjectPanel = true)
        {
            var path = GetAssetPathByName(clsName);
            if (path == null) return;


            var restoreOverwriteSetting = USGEngine.IgnoreOverwriteSettingByAttribute;
            USGEngine.IgnoreOverwriteSettingByAttribute = true;  // always disabled after import event.

            // NOTE: Invoking unity editor event while building app causes fatal error.
            //       just generate code and not need to import it.
            if (BuildPipeline.isBuildingPlayer)
            {
                USGEngine.ProcessFile(path);
                // because of Editor event doesn't happens.
                USGEngine.IgnoreOverwriteSettingByAttribute = restoreOverwriteSetting;
                return;
            }

            // NOTE: When working in Unity Editor, Do NOT perform ProcessFile(...).
            //       It will generate code correctly but generated source code is not imported.
            //       To see changes, you need to import script and as you imagine, code generation happens again.
            if (showInProjectPanel)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
            AssetDatabase.ImportAsset(path);
        }


        ///<summary>Returns "Assets/" or "Packages/" starting path to the script. (relative path from Unity project directory)</summary>
        ///<returns>null if not found</returns>
        public static string GetAssetPathByName(string clsName)
        {
            var GUIDs = AssetDatabase.FindAssets(clsName);
            foreach (var GUID in GUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(GUID);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName != clsName) continue;

                return path;
            }
            return null;
        }

    }
}
#endif
