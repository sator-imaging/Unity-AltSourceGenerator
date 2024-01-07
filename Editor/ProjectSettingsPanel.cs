#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace SatorImaging.UnitySourceGenerator.Editor
{
    public class ProjectSettingsPanel : SettingsProvider
    {
        const float PADDING_WIDTH = 4;
        const string DISPLAY_NAME = "Alternative Source Generator for Unity";


        #region ////////  SETTINGS PROVIDER  ////////

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new ProjectSettingsPanel("Project/" + DISPLAY_NAME, SettingsScope.Project, null);
        }

        public ProjectSettingsPanel(string path, SettingsScope scopes, IEnumerable<string> keywords)
            : base(path, scopes, keywords)
        {
        }


        Vector2 _scroll;
        UnityEditor.Editor _cachedEditor;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            Wakeup(ref _cachedEditor);
        }

        public override void OnGUI(string searchContext)
        {
            DrawEditor(_cachedEditor, ref _scroll);
        }

        public override void OnDeactivate()
        {
            _settings.Save();
        }

        #endregion


        #region ////////  EDITOR WINDOW  ////////

        [MenuItem("Tools/" + DISPLAY_NAME)]
        static void ShowWindow()
        {
            var wnd = EditorWindow.GetWindow<USGWindow>("USG");
            wnd.Show();
        }

        public class USGWindow : EditorWindow
        {
            Vector2 _scroll;
            UnityEditor.Editor _cachedEditor;

            void OnEnable()
            {
                Wakeup(ref _cachedEditor);
            }

            void OnGUI()
            {
                DrawEditor(_cachedEditor, ref _scroll);
            }

            void OnLostFocus() => _settings.Save();
            void OnDisable() => _settings.Save();
        }


        #endregion


        static ProjectSettingsData _settings = ProjectSettingsData.instance;
        static Type _generatorTypeToShowEmittersInGUI = null;
        static Type[] _referencingEmittersToShowInGUI = Array.Empty<Type>();
        static Type[] _generatorTypes = Array.Empty<Type>();
        static bool[] _isGeneratorHasEmitters = Array.Empty<bool>();
        static Dictionary<Type, string> _targetClassToScriptFilePath = new();
        static Dictionary<Type, string[]> _targetClassToOutputFilePaths = new();
        static Dictionary<Type, string[]> _targetClassToOutputFileNames = new();
        // some GUI classes cannot be accessed on field definition.
        static GUIContent gui_emittersBtn;
        static GUIContent gui_deleteBtn;
        static GUIContent gui_runBtn;
        static GUIContent gui_unveilBtn;
        static GUIStyle gui_noBGButtonStyle;
        static GUIStyle gui_deleteMiniLabel;
        static GUIContent gui_suspendAutoReloadLabel = new GUIContent(" Suspend Auto Reload while Unity Editor in Background  *experimental");
        static GUIContent gui_autoRunLabel = new GUIContent(" Auto Run Generators on Script Update / Reimport");
        static GUIContent gui_buttonColumnLabel = new GUIContent("On    Run");
        static GUIContent gui_refEmittersLabel = new GUIContent("Referencing Emitters");
        static GUIContent gui_multiGeneratorsLabel = new GUIContent("MULTIPLE GENERATORS");
        static GUIContent gui_noSourceGenLabel = new GUIContent("NO SOURCE GENERATORS IN PROJECT");
        static GUIContent gui_debugLabel = new GUIContent("DEBUG");
        static GUILayoutOption gui_toggleWidth = GUILayout.Width(16);
        static GUILayoutOption gui_buttonWidth = GUILayout.Width(32);

        // NOTE: class is reference type and reference type variable is "passed by value".
        //       to take reference to newly created object, need `ref` chain.
        static void Wakeup(ref UnityEditor.Editor cachedEditor)
        {
            gui_emittersBtn ??= new(EditorGUIUtility.IconContent("d_icon dropdown"));
            gui_deleteBtn ??= new(EditorGUIUtility.IconContent("d_TreeEditor.Trash"));
            gui_runBtn ??= new(EditorGUIUtility.IconContent("PlayButton On"));
            gui_unveilBtn ??= new(EditorGUIUtility.IconContent("d_Linked"));
            if (gui_noBGButtonStyle == null)
            {
                gui_noBGButtonStyle = new(EditorStyles.iconButton);
                gui_noBGButtonStyle.alignment = TextAnchor.LowerCenter;
                gui_noBGButtonStyle.imagePosition = ImagePosition.ImageOnly;
                gui_noBGButtonStyle.margin.top = EditorStyles.inspectorDefaultMargins.padding.top;
            }
            if (gui_deleteMiniLabel == null)
            {
                gui_deleteMiniLabel = new(EditorStyles.centeredGreyMiniLabel);
                gui_deleteMiniLabel.alignment = TextAnchor.MiddleRight;
                gui_deleteMiniLabel.fixedHeight = EditorGUIUtility.singleLineHeight;
                gui_deleteMiniLabel.padding.top = 1;
            }

            _settings = ProjectSettingsData.instance;
            _settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;

            // caching heavy ops
            _targetClassToOutputFilePaths = USGEngine.GeneratorInfoList
                .ToLookup(static x => x.TargetClass)
                .ToDictionary(
                    static x => x.Key,
                    static x => x.Select(static x => USGEngine.GetGeneratorOutputPath(x)).ToArray());
            _targetClassToOutputFileNames = _targetClassToOutputFilePaths
                .ToDictionary(
                    static x => x.Key,
                    static x => x.Value.Select(static x => Path.GetFileName(x)).ToArray());

            _generatorTypes = USGEngine.GeneratorInfoList
                .Select(static x => x.Attribute.GeneratorClass)
                .Union(_targetClassToOutputFileNames
                    .Where(static x => x.Value.Length > 1)
                    .Select(static x => x.Key))
                .Distinct()
                .OrderBy(static x => x.FullName)
                .ToArray();
            _isGeneratorHasEmitters = _generatorTypes
                .Select(static x => GetReferencingEmitters(x).Length > 0)
                .ToArray();
            _referencingEmittersToShowInGUI = GetReferencingEmitters(null);  // not only set variable, but clear ref emitters panel settings.

            _targetClassToScriptFilePath = USGEngine.GeneratorInfoList
                .Select(static x => x.TargetClass)
                .Union(_generatorTypes)
                .ToDictionary(
                    static x => x,
                    static x => USGUtility.GetAssetPathByType(x) ?? throw new Exception());

            UnityEditor.Editor.CreateCachedEditor(_settings, null, ref cachedEditor);
        }


        static bool _debugFoldout;
        static void DrawEditor(UnityEditor.Editor cachedEditor, ref Vector2 currentScroll)
        {
            var restoreLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.2f;

            currentScroll = EditorGUILayout.BeginScrollView(currentScroll);
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(GUIContent.none, GUILayout.MaxWidth(PADDING_WIDTH));
                EditorGUILayout.BeginVertical();
                {
                    EditorGUI.BeginChangeCheck();
                    var suspendAutoReload = EditorGUILayout.ToggleLeft(gui_suspendAutoReloadLabel, _settings.SuspendAutoReloadWhileEditorInBackground);
                    var autoEmit = EditorGUILayout.ToggleLeft(gui_autoRunLabel, _settings.AutoEmitOnScriptUpdate);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _settings.SuspendAutoReloadWhileEditorInBackground = suspendAutoReload;
                        _settings.AutoEmitOnScriptUpdate = autoEmit;
                        _settings.Save();
                    }
                    //EditorGUILayout.Space();

                    EditorGUILayout.LabelField(gui_buttonColumnLabel, EditorStyles.miniLabel);
                    if (_generatorTypes.Length == 0)
                        EditorGUILayout.LabelField(gui_noSourceGenLabel);
                    else
                        for (int i = 0; i < _generatorTypes.Length; i++)
                        {
                            DrawGenerator(_generatorTypes[i], _isGeneratorHasEmitters[i]);
                        }
                    EditorGUILayout.Space();

                    if (_generatorTypeToShowEmittersInGUI != null)
                    {
                        EditorGUILayout.LabelField(gui_refEmittersLabel, EditorStyles.largeLabel);
                        for (int i = 0; i < _referencingEmittersToShowInGUI.Length; i++)
                        {
                            DrawGenerator(_referencingEmittersToShowInGUI[i], false);
                        }
                    }
                    EditorGUILayout.Space();

                    GUILayout.FlexibleSpace();
                    _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, gui_debugLabel, true);
                    if (_debugFoldout)
                    {
                        cachedEditor.OnInspectorGUI();
                    }


                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUIUtility.labelWidth = restoreLabelWidth;
        }


        static void DrawGenerator(Type t, bool showEmitterBtn)//string filePath, bool showEmitterBtn)
        {
            EditorGUILayout.BeginHorizontal();
            {
                // NOTE: USGUtility functions are heavy for use in GUI loops, cache it!!
                var filePath = _targetClassToScriptFilePath[t];

                EditorGUI.BeginChangeCheck();
                var isOn = EditorGUILayout.Toggle(!_settings.AutoEmitDisabledPaths.Contains(filePath), gui_toggleWidth);
                if (EditorGUI.EndChangeCheck())
                {
                    if (isOn)
                        _settings.AutoEmitDisabledPaths.TryRemove(filePath);
                    else
                        _settings.AutoEmitDisabledPaths.TryAddUnique(filePath);

                    _settings.Save();
                }

                //run
                if (GUILayout.Button(gui_runBtn, gui_buttonWidth))
                {
                    Debug.Log($"[{nameof(UnitySourceGenerator)}] Generator running: {t.FullName}");
                    USGUtility.ForceGenerateByType(t, false);
                }

                //label
                if (EditorGUILayout.LinkButton(t.Name))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<MonoScript>(filePath));
                }

                //emitters??
                if (showEmitterBtn)
                {
                    if (GUILayout.Button(gui_emittersBtn, gui_noBGButtonStyle))
                    {
                        _referencingEmittersToShowInGUI = GetReferencingEmitters(t);
                    }
                }
                //unveil??
                else
                {
                    if (_targetClassToOutputFilePaths.ContainsKey(t))
                        for (int i = 0; i < _targetClassToOutputFilePaths[t].Length; i++)
                        {
                            if (GUILayout.Button(gui_unveilBtn, gui_noBGButtonStyle))
                            {
                                EditorGUIUtility.PingObject(
                                    AssetDatabase.LoadAssetAtPath<MonoScript>(_targetClassToOutputFilePaths[t][i]));
                            }
                        }
                }

                //deleteBtn
                if (_targetClassToOutputFilePaths.ContainsKey(t))
                {
                    if (_targetClassToOutputFilePaths[t].Length == 1)
                    {
                        GUILayout.FlexibleSpace();
                        if (EditorGUIUtility.currentViewWidth > _settings.DenseViewWidthThreshold)
                            GUILayout.Label(_targetClassToOutputFileNames[t][0], gui_deleteMiniLabel);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                        //if (EditorGUIUtility.currentViewWidth > _settings.DenseViewWidthThreshold)
                        //    GUILayout.Label(gui_multiGeneratorsLabel, gui_deleteMiniLabel);
                    }

                    for (int i = 0; i < _targetClassToOutputFilePaths[t].Length; i++)
                    {
                        if (GUILayout.Button(gui_deleteBtn))
                        {
                            if (File.Exists(_targetClassToOutputFilePaths[t][i])
                            && EditorUtility.DisplayDialog(
                                nameof(UnitySourceGenerator),
                                $"Would you like to delete emitted file?\n" +
                                $"- {_targetClassToOutputFileNames[t][i]}\n" +
                                $"\n" +
                                $"File Path: {_targetClassToOutputFilePaths[t][i]}",
                                "Yes", "cancel"))
                            {
                                File.Delete(_targetClassToOutputFilePaths[t][i]);
                                Debug.Log($"[{nameof(UnitySourceGenerator)}] File is deleted: {_targetClassToOutputFilePaths[t][i]}");
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }


        static Type[] GetReferencingEmitters(Type t)
        {
            _generatorTypeToShowEmittersInGUI = t;
            if (t == null)
                return Array.Empty<Type>();

            // NOTE: self-emit generator can have other target.
            var ret = USGEngine.GeneratorInfoList
                .Where(x => x.Attribute.GeneratorClass == t)
                .Select(static x => x.TargetClass)
                .ToArray();

            if (ret.Length == 1 && ret[0] == t)
                return Array.Empty<Type>();

            return ret;
        }


    }
}

#endif
