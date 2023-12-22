#if UNITY_EDITOR

using System;
using System.Reflection;
using UnityEditor;

namespace SatorImaging.UnitySourceGenerator
{
    public static class EditorEvent
    {
        // NOTE: Unity doesn't invoke asset import event correctly when load script in background.
        //       This will prevent Unity to reload updated scripts in background.
        [InitializeOnLoadMethod]
        static void InitializeOnLoad()
        {
            ProjectSettingsData.instance.SuspendAutoReloadWhileEditorInBackground
                = ProjectSettingsData.instance.SuspendAutoReloadWhileEditorInBackground;
        }


        static bool _eventWasRegistered = false;  // no way to determine other classes use focusChanged event?
        internal static void RegisterFocusChangedEvent(bool registerOrRemove)
        {
            //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorApplication.cs#L275
            var focusChanged = typeof(EditorApplication).GetField("focusChanged",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (focusChanged == null)
                return;

            // TODO: better cleanup.
            //       currently, event can be unregistered but it seems empty action runs on focus changed event...?
            if (!_eventWasRegistered)
            {
                if (!registerOrRemove)
                    return;
                EditorApplication.quitting += () => OnEditorApplicationFocus(true);
            }

            var currentAction = focusChanged.GetValue(null) as Action<bool>;
            if (registerOrRemove)
            {
                currentAction -= OnEditorApplicationFocus;
                currentAction += OnEditorApplicationFocus;
                _eventWasRegistered = true;
            }
            else
            {
                currentAction -= OnEditorApplicationFocus;
            }
            focusChanged.SetValue(null, currentAction);
            //Debug.Log($"[USG] Null? {currentAction == null}  Method:{currentAction.Method} Target:{currentAction.Target}");

            _restoreAutoRefresh = EditorPrefs.GetInt(PREF_AUTO_REFRESH, EditorPrefs.GetInt(PREF_AUTO_REFRESH_OLD, DEFAULT_AUTO_REFRESH));
            //_restoreDirMonitoring = EditorPrefs.GetBool(PREF_DIR_MONITORING, DEFAULT_DIR_MONITORING);
        }

        const bool DEFAULT_DIR_MONITORING = true;
        const int DEFAULT_AUTO_REFRESH = 1;
        const string PREF_AUTO_REFRESH = "kAutoRefreshMode";
        const string PREF_AUTO_REFRESH_OLD = "kAutoRefresh";
        //const string PREF_DIR_MONITORING = "DirectoryMonitoring";
        //static bool _restoreDirMonitoring = DEFAULT_DIR_MONITORING;
        static int _restoreAutoRefresh = DEFAULT_AUTO_REFRESH;
        static void OnEditorApplicationFocus(bool focus)
        {
            //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/PreferencesWindow/AssetPipelinePreferences.cs#L94
            if (focus == false)
            {
                _restoreAutoRefresh = EditorPrefs.GetInt(PREF_AUTO_REFRESH, EditorPrefs.GetInt(PREF_AUTO_REFRESH_OLD, DEFAULT_AUTO_REFRESH));
                //_restoreDirMonitoring = EditorPrefs.GetBool(PREF_DIR_MONITORING, DEFAULT_DIR_MONITORING);

                //AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                //EditorPrefs.SetBool(PREF_DIR_MONITORING, false);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH, 0);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH_OLD, 0);
            }
            else
            {
                //EditorPrefs.SetBool(PREF_DIR_MONITORING, _restoreDirMonitoring);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH, _restoreAutoRefresh);
                EditorPrefs.SetInt(PREF_AUTO_REFRESH_OLD, _restoreAutoRefresh);

                //AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);  // option is required...?
            }
            //Debug.Log($"[USG] Focus:{focus}  Restore:{_restoreAutoRefresh}/{_restoreDirMonitoring}");
        }


    }
}

#endif
