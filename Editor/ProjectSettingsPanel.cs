#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;


namespace SatorImaging.UnitySourceGenerator
{
    internal class ProjectSettingsPanel : SettingsProvider
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
        Editor _cachedEditor;

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
            Editor _cachedEditor;

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
        static string[] _generatorPaths = Array.Empty<string>();
        static bool[] _isEmitterExists = Array.Empty<bool>();
        static string[] _emitterPaths = Array.Empty<string>();
        static string _generatorPathToShowEmitters = null;
        static Dictionary<string, string> _generatorNameToOutputFileName = new();
        static GUIContent gui_emitterBtn;
        static GUIContent gui_deleteBtn;
        static GUIContent gui_generateBtn;
        static GUIContent gui_goGeneratedBtn;
        static GUILayoutOption gui_toggleWidth;
        static GUILayoutOption gui_buttonWidth;
        static GUIStyle gui_noBGButtonStyle;
        static GUIStyle gui_deleteMiniLabel;

        // NOTE: class is reference type and reference type variable is "passed by value".
        //       to take reference to newly created object, need `ref` chain.
        static void Wakeup(ref Editor cachedEditor)
        {
            // GUI classes cannot be initialized on field definition.
            gui_emitterBtn ??= new(EditorGUIUtility.IconContent("d_icon dropdown"));
            gui_deleteBtn ??= new(EditorGUIUtility.IconContent("d_TreeEditor.Trash"));
            gui_generateBtn ??= new(EditorGUIUtility.IconContent("PlayButton On"));//d_playLoopOff
            gui_goGeneratedBtn ??= new(EditorGUIUtility.IconContent("d_Linked"));//SavePassive/SaveActive/SaveFromPlay/d_pick/
            gui_toggleWidth ??= GUILayout.Width(16);
            gui_buttonWidth ??= GUILayout.Width(32);
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

            _generatorPaths = USGEngine.TypeNameToInfo
                // remove emitter class. it can be access thru referencing emitter list.
                .Where(static x => x.Value.TargetClass == null || x.Value.Attribute.GeneratorClass == null)
                .Select(static x => USGUtility.GetAssetPathByName(x.Key))
                //.Where(static x => x.StartsWith("Assets/"))
                .ToArray();
            _isEmitterExists = _generatorPaths
                .Select(static x => GetEmitters(x).Length > 0)
                .ToArray();
            _emitterPaths = GetEmitters(null);  //clear
            _generatorNameToOutputFileName = USGEngine.TypeNameToInfo
                .ToDictionary(static x => x.Key, static x => x.Value.OutputFileName);

            Editor.CreateCachedEditor(_settings, null, ref cachedEditor);
        }


        static bool _debugFoldout;
        static void DrawEditor(Editor cachedEditor, ref Vector2 currentScroll)
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
                    var suspendAutoReload = EditorGUILayout.ToggleLeft(" Suspend Auto Reload while Unity Editor in Background  *experimental", _settings.SuspendAutoReloadWhileEditorInBackground);
                    var autoEmit = EditorGUILayout.ToggleLeft(" Auto Run Generators on Script Update / Reimport", _settings.AutoEmitOnScriptUpdate);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _settings.SuspendAutoReloadWhileEditorInBackground = suspendAutoReload;
                        _settings.AutoEmitOnScriptUpdate = autoEmit;
                        _settings.Save();
                    }
                    //EditorGUILayout.Space();

                    EditorGUILayout.LabelField("On    Run", EditorStyles.miniLabel);
                    if (_generatorPaths.Length == 0)
                        EditorGUILayout.LabelField("NO SOURCE GENERATORS IN PROJECT");
                    else
                        for (int i = 0; i < _generatorPaths.Length; i++)
                        {
                            DrawGenerator(_generatorPaths[i], _isEmitterExists[i]);
                        }
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Referencing Emitters");
                    if (_generatorPathToShowEmitters != null)
                    {
                        for (int i = 0; i < _emitterPaths.Length; i++)
                        {
                            DrawGenerator(_emitterPaths[i], false);
                        }
                    }
                    EditorGUILayout.Space();

                    GUILayout.FlexibleSpace();
                    _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, "DEBUG", true);
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


        static void DrawGenerator(string filePath, bool showEmitterBtn)
        {
            EditorGUILayout.BeginHorizontal();
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

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
                if (GUILayout.Button(gui_generateBtn, gui_buttonWidth))
                {
                    Debug.Log($"[{nameof(UnitySourceGenerator)}] Generator running: {fileName}");
                    USGUtility.ForceGenerateByName(fileName, false);
                }

                //label
                if (EditorGUILayout.LinkButton(fileName))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(filePath));
                }
                //emitter??
                if (showEmitterBtn)
                {
                    if (GUILayout.Button(gui_emitterBtn, gui_noBGButtonStyle))
                    {
                        _emitterPaths = GetEmitters(filePath);
                    }
                }
                //goGenerated??
                else
                {
                    if (GUILayout.Button(gui_goGeneratedBtn, gui_noBGButtonStyle))
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(
                            USGEngine.GetGeneratorOutputPath(filePath, _generatorNameToOutputFileName[fileName])));
                    }
                }

                //deleteBtn
                if (_generatorNameToOutputFileName[fileName]?.Length is > 0)
                {
                    GUILayout.FlexibleSpace();
                    if (EditorGUIUtility.currentViewWidth > _settings.DenseViewWidthThreshold)
                        GUILayout.Label(_generatorNameToOutputFileName[fileName], gui_deleteMiniLabel);
                    if (GUILayout.Button(gui_deleteBtn))//, gui_noBGButtonStyle))
                    {
                        var genFullPath = USGEngine.GetGeneratorOutputPath(filePath, _generatorNameToOutputFileName[fileName]);
                        if (EditorUtility.DisplayDialog(nameof(UnitySourceGenerator),
                            $"Would you like to delete emitted file?\n" +
                            $"- {_generatorNameToOutputFileName[fileName]}\n" +
                            $"\n" +
                            $"File Path: {genFullPath}",
                            "Yes", "cancel"))
                        {
                            File.Delete(genFullPath);
                            Debug.Log($"[{nameof(UnitySourceGenerator)}] File is deleted: {genFullPath}");
                            AssetDatabase.Refresh();
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }


        static string[] GetEmitters(string generatorPath)
        {
            _generatorPathToShowEmitters = generatorPath;
            if (string.IsNullOrEmpty(generatorPath))
                return Array.Empty<string>();

            var clsName = Path.GetFileNameWithoutExtension(generatorPath);
            return USGEngine.TypeNameToInfo
                .Where(x => x.Value.Attribute.GeneratorClass?.Name == clsName || x.Value.TargetClass?.Name == clsName)
                .Select(static x => USGUtility.GetAssetPathByName(x.Key))
                .Where(x => x != generatorPath)  // remove itself
                .ToArray();
        }


    }
}

#endif
