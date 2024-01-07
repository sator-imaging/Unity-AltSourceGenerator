#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;


namespace SatorImaging.UnitySourceGenerator.Editor
{
    public class ProjectSettingsData : ProjectSettingsSingleton<ProjectSettingsData>
    {
        public void Save() => base.Save(true);

        [SerializeField] public bool AutoEmitOnScriptUpdate = true;
        [SerializeField] public List<string> AutoEmitDisabledPaths = new();
        [Range(0, 1920)]
        [SerializeField] public int DenseViewWidthThreshold = 512;

        //properties
        [HideInInspector][SerializeField] bool _disableAutoReloadInBackground = false;
        public bool SuspendAutoReloadWhileEditorInBackground
        {
            get => _disableAutoReloadInBackground;
            set
            {
                _disableAutoReloadInBackground = value;
                EditorEvent.RegisterFocusChangedEvent(value);
                if (value)
                    Debug.Log($"[{nameof(UnitySourceGenerator)}] Auto Reload completely disabled while Unity Editor in Background.");
                //else
                //    Debug.Log($"[{nameof(UnitySourceGenerator)}] Unity Editor event was unregistered.");
            }
        }


        // temporary storage between domain reloading.
        [HideInInspector][SerializeField] internal List<string> ImportedScriptPaths = new();
        [HideInInspector][SerializeField] internal List<string> PathsToSkipImportEvent = new();
        [HideInInspector][SerializeField] internal List<string> PathsToIgnoreOverwriteSettingOnAttribute = new();

    }
}

#endif
