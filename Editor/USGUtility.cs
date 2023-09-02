#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SatorImaging.UnitySourceGenerator
{
    public static class USGUtility
    {
        ///<summary>Force perform source code generation by class name.</summary>
        ///<param name="showInProjectPanel">works only when Unity is not building app.</param>
        public static void ForceGenerateByName(string clsName, bool showInProjectPanel = false)
        {
            var path = GetAssetPathByName(clsName);
            if (path == null) return;

            USGEngine.ProcessFile(path, true, true);

            if (BuildPipeline.isBuildingPlayer)
                return;

            AssetDatabase.Refresh();

            if (showInProjectPanel)
            {
                var genPath = USGEngine.GetGeneratorOutputPath(path, null) ?? path;
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(genPath));
            }
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


        /* internal ---------------------------------------------------------------------- */

        internal static bool TryRemove<T>(this List<T> list, T val)
        {
            if (val is null || !list.Contains(val))
                return false;

            do
            {
                list.Remove(val);
            }
            while (list.Contains(val));

            return true;
        }

        internal static bool TryAddUnique<T>(this List<T> list, T val)
        {
            if (val is null || list.Contains(val))
                return false;

            list.Add(val);
            return true;
        }

    }
}
#endif
