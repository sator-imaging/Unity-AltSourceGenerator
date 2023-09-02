#if UNITY_EDITOR

using System.IO;
using UnityEditorInternal;
using UnityEngine;


namespace SatorImaging.UnitySourceGenerator
{
    ///<summary>Polyfill for use in Unity 2019.</summary>
    ///<remarks>Output path is hard-coded.</remarks>
    public abstract class ProjectSettingsSingleton<T> : ScriptableObject
        where T : ScriptableObject
    {
        private static string _path = null;
        protected static string GetFilePath()
            => _path ?? (_path = Application.dataPath + "/../ProjectSettings/" + typeof(T).FullName + ".asset");


        #region https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ScriptableSingleton.cs

        static T s_Instance;

        public static T instance
        {
            get
            {
                if (s_Instance == null)
                    CreateAndLoad();

                return s_Instance;
            }
        }

        // On domain reload ScriptableObject objects gets reconstructed from a backup. We therefore set the s_Instance here
        protected ProjectSettingsSingleton()
        {
            if (s_Instance != null)
            {
                Debug.LogError("ScriptableSingleton already exists. Did you query the singleton in a constructor?");
            }
            else
            {
                object casted = this;
                s_Instance = casted as T;
                System.Diagnostics.Debug.Assert(s_Instance != null);
            }
        }

        private static void CreateAndLoad()
        {
            System.Diagnostics.Debug.Assert(s_Instance == null);

            // Load
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                // If a file exists then load it and deserialize it.
                // This creates an instance of T which will set s_Instance in the constructor. Then it will deserialize it and call relevant serialization callbacks.
                InternalEditorUtility.LoadSerializedFileAndForget(filePath);
            }

            if (s_Instance == null)
            {
                // Create
                T t = CreateInstance<T>();
                t.hideFlags = HideFlags.HideAndDontSave;
            }

            System.Diagnostics.Debug.Assert(s_Instance != null);
        }

        protected virtual void Save(bool saveAsText)
        {
            if (s_Instance == null)
            {
                Debug.LogError("Cannot save ScriptableSingleton: no instance!");
                return;
            }

            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string folderPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                InternalEditorUtility.SaveToSerializedFileAndForget(new[] { s_Instance }, filePath, saveAsText);
            }
            else
            {
                Debug.LogWarning($"Saving has no effect. Your class '{GetType()}' is missing the FilePathAttribute. Use this attribute to specify where to save your ScriptableSingleton.\nOnly call Save() and use this attribute if you want your state to survive between sessions of Unity.");
            }
        }

        #endregion


    }


}

#endif
