#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Callbacks;
using System;
using System.Reflection;

namespace SatorImaging.UnitySourceGenerator
{
    class EditorExtension
    {
        const string ROOT_MENU = @"Assets/Unity Source Generator/";
        const string TEMPLATE_PATH = @"Packages/com.sator-imaging.alt-source-generator/Template/Template_";
        const string CS_EXT = @".cs";
        const string TXT_EXT = @".txt";


        [MenuItem(ROOT_MENU + "Force Generate while Overwriting Disabled")]
        static void ForceGenerateSelectedScripts()
        {
            USGEngine.IgnoreOverwriteSettingByAttribute = true;  // always disabled after import event.

            foreach (var GUID in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(GUID);
                AssetDatabase.ImportAsset(path);
            }
        }


        [MenuItem(ROOT_MENU + "Method Generator Template", priority = 100)]
        static void MethodGenerator()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                TEMPLATE_PATH + nameof(MethodGenerator) + TXT_EXT,
                nameof(MethodGenerator) + CS_EXT);
        }


        [MenuItem(ROOT_MENU + "Generic Generator Template", priority = 100)]
        static void GenericGenerator()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                TEMPLATE_PATH + nameof(GenericGenerator) + TXT_EXT,
                nameof(GenericGenerator) + CS_EXT);
        }


        /* kept for reference. USGEngine update seems to work as expected.
        // NOTE: This will prevent Unity to reload updated scripts in background.
        //       Unity doesn't invoke asset import event correctly when load script in background.
        [InitializeOnLoadMethod]
        static void InitializeFocusChangedEvent()
        {
            //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorApplication.cs#L275
            var focusChanged = typeof(EditorApplication).GetField("focusChanged",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (focusChanged == null) return;

            var focusAction = focusChanged.GetValue(null) as Action<bool>;
            focusAction -= OnEditorApplicationFocus;
            focusAction += OnEditorApplicationFocus;
            focusChanged.SetValue(null, focusAction);

            s_restoreAutoRefresh = EditorPrefs.GetInt(PREF_AUTO_REFRESH, EditorPrefs.GetInt(PREF_AUTO_REFRESH_OLD, 1));
            s_restoreDirMonitoring = EditorPrefs.GetBool(PREF_DIR_MONITORING, true);
        }

        const string PREF_AUTO_REFRESH = "kAutoRefreshMode";
        const string PREF_AUTO_REFRESH_OLD = "kAutoRefresh";
        const string PREF_DIR_MONITORING = "DirectoryMonitoring";
        static bool s_restoreDirMonitoring;
        static int s_restoreAutoRefresh;
        static void OnEditorApplicationFocus(bool focus)
        {
            //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/PreferencesWindow/AssetPipelinePreferences.cs#L94
            if (focus == false)
            {
                s_restoreAutoRefresh = EditorPrefs.GetInt(PREF_AUTO_REFRESH, EditorPrefs.GetInt(PREF_AUTO_REFRESH_OLD, 1));
                s_restoreDirMonitoring = EditorPrefs.GetBool(PREF_DIR_MONITORING, true);
                //AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                EditorPrefs.SetBool(PREF_DIR_MONITORING, false);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH, 0);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH_OLD, 0);
            }
            else
            {
                //AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
                EditorPrefs.SetBool(PREF_DIR_MONITORING, s_restoreDirMonitoring);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH, s_restoreAutoRefresh);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH_OLD, s_restoreAutoRefresh);

                AssetDatabase.Refresh();
            }
            Debug.Log($"[USG] Focus:{focus} / AutoRefresh:{s_restoreAutoRefresh} / DirMonitoring:{s_restoreDirMonitoring}");
        }
        */

    }
}

#endif
