#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;

namespace SatorImaging.UnitySourceGenerator
{
    /// <summary>
    /// > [!WARNING]
    /// > Works only on Unity Editor
    /// </summary>
    public static class USGUtility
    {
        const string SEARCH_FILTER = "t:" + nameof(MonoScript) + " ";


        // NOTE: AssetDatabase.FindAssets() returns many of partial name matches and deep type checking on them
        //       is so slow. as a result, ProjectSettingsPanel initialization takes a while to launch even if
        //       only a dozen generators and tagets in the project.
        //       caching will significantly gain the query performance.
        readonly static Dictionary<Type, string> _typeToAssetPath = new();

        ///<summary>Returns "Assets/" or "Packages/" starting path to the script. (relative path from Unity project directory)</summary>
        ///<returns>null if not found</returns>
        public static string GetAssetPathByType(Type t)
        {
            if (_typeToAssetPath.ContainsKey(t))
                return _typeToAssetPath[t];

            var GUIDs = AssetDatabase.FindAssets(SEARCH_FILTER + t.Name);
            foreach (var GUID in GUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(GUID);
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) is not MonoScript mono
                || mono.GetClass() != t)
                    continue;

                _typeToAssetPath.Add(t, path);
                return path;
            }
            return null;
        }

        ///<summary>Force perform source code generation by class name.</summary>
        ///<param name="showInProjectPanel">works only when Unity is not building app.</param>
        public static void ForceGenerateByType(Type t, bool showInProjectPanel = false)
        {
            var path = GetAssetPathByType(t);
            if (path == null)
                return;

            ForceGenerate(path, t, showInProjectPanel);
        }


        static void ForceGenerate(string path, Type t, bool showInProjectPanel)
        {
            USGEngine.Process(new string[] { path }, true);

            if (BuildPipeline.isBuildingPlayer)
                return;

            AssetDatabase.Refresh();

            if (showInProjectPanel)
            {
                if (t == null && AssetDatabase.LoadAssetAtPath<MonoScript>(path) is MonoScript mono)
                    t = mono.GetClass();

                if (t != null)
                {
                    var infos = USGEngine.GeneratorInfoList.Where(x => x.TargetClass == t);

                    if (infos.Count() > 1)
                    {
                        UnityEngine.Debug.Log($"[{nameof(UnitySourceGenerator)}] there are multiple output files: "
                            + string.Join(", ", infos.Select(static x => x.OutputFileName)));
                    }

                    if (infos.Count() > 0)
                        path = USGEngine.GetGeneratorOutputPath(infos.ElementAt(0));
                }

                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<MonoScript>(path));
            }
        }


        /*  internal  ---------------------------------------------------------------------- */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryRemove<T>(this List<T> list, T val)
            where T : class
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryAddUnique<T>(this List<T> list, T val)
            where T : class
        {
            if (val is null || list.Contains(val))
                return false;

            list.Add(val);
            return true;
        }


        /*  obsolete  ================================================================ */

        ///<summary>Force perform source code generation by class name.</summary>
        ///<param name="showInProjectPanel">works only when Unity is not building app.</param>
        [Obsolete("use ForceGenerateByType() instead.")]
        public static void ForceGenerateByName(string clsName, bool showInProjectPanel = false)
        {
            var path = GetAssetPathByName(clsName);
            if (path == null)
                return;

            ForceGenerate(path, null, showInProjectPanel);
        }

        ///<summary>Returns "Assets/" or "Packages/" starting path to the script. (relative path from Unity project directory)</summary>
        ///<returns>null if not found</returns>
        [Obsolete("use GetAssetPathByType() instead.")]
        public static string GetAssetPathByName(string clsName)
        {
            var GUIDs = AssetDatabase.FindAssets(SEARCH_FILTER + clsName);
            foreach (var GUID in GUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(GUID);
                if (Path.GetFileNameWithoutExtension(path) != clsName)
                    continue;

                return path;
            }
            return null;
        }

    }
}
#endif
