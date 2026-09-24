using System;
using FaceMotion.Data;
using FaceMotion.Editor.Diagnostics;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Guidance;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.Preview;
using FaceMotion.Editor.VRChat.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Window
{
    /// <summary>
    /// FaceMotion project + timeline editor. The window stays thin: it owns the session,
    /// controllers, panels, and the timeline view, wires Unity/hierarchy/undo events, and
    /// only paints. All Undo, validation, and mutation logic lives in the controllers.
    /// </summary>
    public sealed class FaceMotionWindow : EditorWindow
    {
        public const string WindowTitle = "FaceMotion";

        private const float ToolbarHeight = 22f;
        internal const float GuidanceHeight = 26f;
        internal const float MinimumWindowWidth = 900f;
        internal const float MinimumWindowHeight = 700f;
        internal const float MinimumLeftColumnWidth = 320f;
        internal const float MaximumLeftColumnRatio = 0.4f;
        internal const float MinimumTimelineWidth = 460f;
        internal const float SplitterWidth = 5f;

        private const float DefaultLeftColumnRatio = 0.36f;
        private const float DefaultPreviewHeightRatio = 0.5f;
        internal const float MinimumPreviewHeight = 280f;
        internal const float MinimumTimelineHeight = 160f;
        internal const float InspectorHeight = 150f;
        internal const float MaximumPreviewHeightRatio = 0.7f;
        private const string LeftColumnRatioKey = "FaceMotion.Window.v1.LeftColumnRatio";
        private const string PreviewHeightRatioKey = "FaceMotion.Window.v1.PreviewHeightRatio";
        private const int MaximumPendingAvatarRestoreAttempts = 8;

        private FaceMotionEditorSession _session;
        private ProjectController _project;
        private AvatarController _avatar;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TimelineView _timelineView;
        private PreviewSession _previewSession;
        private SceneApplySession _sceneApplySession;
        private PreviewPlaybackController _playback;
        private PreviewEvaluationGate _previewGate;
        private bool _previewDrainScheduled;
        private bool _integrationIndexRefreshScheduled;
        private VRCAvatarDescriptor _integrationIndexRefreshAvatar;

        private ProjectPanel _projectPanel;
        private AvatarPanel _avatarPanel;
        private AnimationListPanel _animationListPanel;
        private TrackListPanel _trackListPanel;
        private KeyframeInspectorPanel _inspectorPanel;
        private DiagnosticsPanel _diagnosticsPanel;
        private ExportPanel _exportPanel;
        private OneClickIntegrationPanel _integrationPanel;
        private ModularAvatarManagedStateCache _managedStateCache;
        private PreviewPanel _previewPanel;
        private ShortcutHelpPanel _shortcutHelpPanel;

        private float _leftColumnRatio = DefaultLeftColumnRatio;
        private bool _draggingSplitter;
        private float _previewHeightRatio = DefaultPreviewHeightRatio;
        private bool _draggingPreviewSplitter;
        internal const string AdvancedFoldoutKey = "FaceMotion.Window.v3.AdvancedFoldout";
        private bool _advancedFoldout;
        private Vector2 _leftScrollPosition;
        private int _sessionChangedCount;
        private string _pendingAvatarGlobalObjectId;
        private string _pendingAvatarScenePath;
        private int _pendingAvatarRestoreAttempts;
        private string _lastPreviewAnimationId;
        private readonly PreviewRepaintScheduler _previewRepaint = new PreviewRepaintScheduler();
        private readonly PreviewRepaintScheduler _previewDeferredRepaint = new PreviewRepaintScheduler();
        private readonly PlaybackUpdateGate _playbackUpdateGate = new PlaybackUpdateGate();
        private static readonly ProfilerMarker WindowOnGuiMarker = new ProfilerMarker("FaceMotion.Window.OnGUI");

        // Unity's MenuItem attribute requires a compile-time constant, so it uses the Japanese default.
        [MenuItem("Tools/FaceMotion/FaceMotion ウィンドウを開く")]
        public static void OpenWindow()
        {
            var window = GetWindow<FaceMotionWindow>(false, WindowTitle, true);
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            _session = new FaceMotionEditorSession();
            _project = new ProjectController(_session);
            _avatar = new AvatarController(_session);
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _timelineView = new TimelineView(_session, _keys, _tracks, OnScrubEnded);
            _previewSession = new PreviewSession();
            _sceneApplySession = new SceneApplySession();
            _playback = new PreviewPlaybackController(_session);
            _previewGate = new PreviewEvaluationGate(EvaluatePreviewAt);

            _projectPanel = new ProjectPanel(_session, _project);
            _avatarPanel = new AvatarPanel(_session, _avatar);
            _animationListPanel = new AnimationListPanel(_session, _animations);
            _trackListPanel = new TrackListPanel(_session, _tracks, _previewSession.Override, RequestPreviewRepaint);
            _inspectorPanel = new KeyframeInspectorPanel(_session, _keys);
            _diagnosticsPanel = new DiagnosticsPanel(_session);
            _exportPanel = new ExportPanel(_session);
            _managedStateCache = new ModularAvatarManagedStateCache();
            _integrationPanel = new OneClickIntegrationPanel(_session, _managedStateCache, ScheduleIntegrationIndexRefresh);
            _previewPanel = new PreviewPanel(_session, _previewSession, _sceneApplySession, _playback, () => { _avatar.EnsureAvatarIndexCurrent(); });
            _shortcutHelpPanel = new ShortcutHelpPanel();
            _leftColumnRatio = Mathf.Clamp(EditorPrefs.GetFloat(LeftColumnRatioKey, DefaultLeftColumnRatio), 0.1f, MaximumLeftColumnRatio);
            _previewHeightRatio = Mathf.Clamp(EditorPrefs.GetFloat(PreviewHeightRatioKey, DefaultPreviewHeightRatio), 0.1f, MaximumPreviewHeightRatio);
            _advancedFoldout = ReadAdvancedFoldoutPref();
            wantsMouseMove = true;

            RestoreSessionState();
            _lastPreviewAnimationId = _session.SelectedAnimationId;

            _session.Changed += OnSessionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorApplication.delayCall += RestorePendingAvatar;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= FlushPreviewRepaint;
            EditorApplication.delayCall -= DrainPendingPreviewEvaluation;
            EditorApplication.update -= FlushDeferredPreviewRepaint;
            EditorApplication.update -= FlushIntegrationIndexRefresh;
            _previewRepaint.Cancel();
            _previewDeferredRepaint.Cancel();
            _integrationIndexRefreshScheduled = false;
            _integrationIndexRefreshAvatar = null;
            SetPlaybackUpdateActive(false);
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorApplication.delayCall -= RestorePendingAvatar;
            _sceneApplySession?.Dispose();
            _previewSession?.Dispose();

            if (_session != null)
            {
                _session.Changed -= OnSessionChanged;
                FaceMotionSessionStateStore.Save(
                    _session.ActiveProjectAssetPath,
                    _session.SelectedAnimationId,
                    _session.ViewState.CurrentTime,
                    _session.ViewState.Zoom,
                    GetAvatarGlobalObjectId(_session.ActiveDescriptor),
                    GetAvatarScenePath(_session.ActiveDescriptor));
            }

            EditorPrefs.SetFloat(LeftColumnRatioKey, _leftColumnRatio);
            EditorPrefs.SetFloat(PreviewHeightRatioKey, _previewHeightRatio);
            wantsMouseMove = false;
        }

        private void OnGUI()
        {
            if (_session == null)
            {
                return;
            }

            using (WindowOnGuiMarker.Auto())
            {

            Event current = Event.current;
            if (current != null && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag
                || current.type == EventType.MouseUp || current.type == EventType.Layout || current.type == EventType.Repaint))
            {
                FaceMotionPreviewTrace.Trace(
                    "W.OnGUI",
                    "window={0} event={1} focused={2} pending={3}",
                    GetInstanceID(),
                    current.type,
                    hasFocus,
                    _previewRepaint.Pending);
            }

            GUILayout.BeginArea(new Rect(0f, 0f, position.width, ToolbarHeight));
            DrawToolbar();
            GUILayout.EndArea();

            DrawGuidance();

            float contentTop = ToolbarHeight + GuidanceHeight;
            Rect area = new Rect(0f, contentTop, position.width, Mathf.Max(0f, position.height - contentTop));
            float leftWidth = CalculateLeftColumnWidth(position.width, _leftColumnRatio);
            Rect leftRect = new Rect(area.x, area.y, leftWidth, area.height);
            Rect splitterRect = new Rect(leftRect.xMax, area.y, SplitterWidth, area.height);
            Rect rightRect = new Rect(splitterRect.xMax, area.y, Mathf.Max(0f, area.xMax - splitterRect.xMax), area.height);

            DrawLeftColumn(leftRect);
            DrawSplitter(splitterRect, area);
                DrawRightColumn(rightRect);
            }
        }

        /// <summary>Computes an adaptive column width while preserving usable panel and timeline minima.</summary>
        internal static float CalculateLeftColumnWidth(float hostWidth, float ratio)
        {
            float usableWidth = Mathf.Max(0f, hostWidth - SplitterWidth);
            float maxWidth = Mathf.Min(usableWidth * MaximumLeftColumnRatio, usableWidth - MinimumTimelineWidth);
            float minWidth = Mathf.Min(MinimumLeftColumnWidth, Mathf.Max(0f, maxWidth));
            maxWidth = Mathf.Max(minWidth, maxWidth);
            return Mathf.Clamp(usableWidth * Mathf.Clamp01(ratio), minWidth, maxWidth);
        }

        internal static float CalculatePreviewHeight(float hostHeight, float ratio)
        {
            float usableHeight = Mathf.Max(0f, hostHeight - SplitterWidth - InspectorHeight);
            float maxHeight = Mathf.Min(usableHeight * MaximumPreviewHeightRatio, usableHeight - MinimumTimelineHeight);
            float minHeight = Mathf.Min(MinimumPreviewHeight, Mathf.Max(0f, maxHeight));
            maxHeight = Mathf.Max(minHeight, maxHeight);
            return Mathf.Clamp(usableHeight * Mathf.Clamp01(ratio), minHeight, maxHeight);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (EditorGUILayout.DropdownButton(
                new GUIContent(FaceMotionUiText.Get("file"), FaceMotionUiText.Get("fileTooltip")),
                FocusType.Passive,
                EditorStyles.toolbarDropDown,
                GUILayout.Width(54f)))
            {
                ShowFileMenu();
            }

            if (EditorGUILayout.DropdownButton(
                new GUIContent(FaceMotionUiText.Get("avatar"), FaceMotionUiText.Get("avatarTooltip")),
                FocusType.Passive,
                EditorStyles.toolbarDropDown,
                GUILayout.Width(70f)))
            {
                ShowAvatarMenu();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent(_session.ViewState.IsPlaying ? FaceMotionUiText.Get("pause") : FaceMotionUiText.Get("play"), FaceMotionUiText.Get("playTooltip")), EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                TogglePlayback();
            }

            if (GUILayout.Button(new GUIContent(FaceMotionUiText.Get("fit"), FaceMotionUiText.Get("fitTooltip")), EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                FitTimeline();
            }

            if (GUILayout.Button(new GUIContent(FaceMotionUiText.Get("zoomOutLabel"), FaceMotionUiText.Get("zoomOut")), EditorStyles.toolbarButton, GUILayout.Width(54f)))
            {
                StepZoom(-1);
            }

            if (GUILayout.Button(new GUIContent(FaceMotionUiText.Get("zoomInLabel"), FaceMotionUiText.Get("zoomIn")), EditorStyles.toolbarButton, GUILayout.Width(54f)))
            {
                StepZoom(+1);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGuidance()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(_session);
            string step = string.Format(FaceMotionUiText.Get("guidanceStepFormat"), model.StepNumber);
            string hint = FaceMotionUiText.Get(model.HintKey);
            GUILayout.BeginArea(new Rect(0f, ToolbarHeight, position.width, GuidanceHeight));
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(step, EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            GUILayout.Label(FaceMotionUiText.Get("guidanceNextAction"), EditorStyles.miniLabel, GUILayout.Width(88f));
            GUILayout.Label(hint, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (model.IsComplete)
            {
                GUILayout.Label(FaceMotionUiText.Get("guidanceCompleteBadge"), EditorStyles.miniBoldLabel, GUILayout.Width(40f));
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ShowFileMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("createProject")), false, () => _project.CreateProject());
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("openProject")), false, OpenProjectDialog);
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("saveProject")), false, _project.SaveProject);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("validateProject")), false, _project.ValidateProject);
            menu.ShowAsContext();
        }

        private void ShowAvatarMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("selectVrcAvatar")), false, _avatarPanel.ShowSceneSelector);
            menu.ShowAsContext();
        }

        private void DrawLeftColumn(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _leftScrollPosition = GUILayout.BeginScrollView(
                _leftScrollPosition,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar);
            _projectPanel.OnGUI();
            EditorGUILayout.Space();
            _avatarPanel.OnGUI();
            EditorGUILayout.Space();
            _animationListPanel.OnGUI();
            EditorGUILayout.Space();
            _trackListPanel.OnGUI();
            EditorGUILayout.Space();
            _integrationPanel.OnGUI();
            EditorGUILayout.Space();
            _diagnosticsPanel.OnGUI();
            EditorGUILayout.Space();
            bool advanced = EditorGUILayout.Foldout(_advancedFoldout, FaceMotionUiText.Get("advancedSettings"), true);
            if (advanced != _advancedFoldout)
            {
                _advancedFoldout = advanced;
                EditorPrefs.SetBool(AdvancedFoldoutKey, advanced);
            }
            if (_advancedFoldout)
            {
                _exportPanel.OnGUI();
                EditorGUILayout.Space();
                _integrationPanel.DrawAdvanced();
                EditorGUILayout.Space();
                DrawTroubleshooting();
                EditorGUILayout.Space();
            }
            _shortcutHelpPanel.OnGUI();
            EditorGUILayout.Space();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTroubleshooting()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("troubleshooting"), EditorStyles.boldLabel);
            if (GUILayout.Button(FaceMotionUiText.Get("rebuildAvatarIndex")))
            {
                _avatar.RebuildIndex();
            }
        }

        /// <summary>Reads the persisted advanced-foldout preference; defaults to collapsed.</summary>
        internal static bool ReadAdvancedFoldoutPref()
        {
            return EditorPrefs.GetBool(AdvancedFoldoutKey, false);
        }

        private void DrawSplitter(Rect splitterRect, Rect hostRect)
        {
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.25f));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && splitterRect.Contains(current.mousePosition))
            {
                _draggingSplitter = true;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && _draggingSplitter)
            {
                float usableWidth = Mathf.Max(1f, hostRect.width - SplitterWidth);
                _leftColumnRatio = Mathf.Clamp((current.mousePosition.x - hostRect.x) / usableWidth, 0.1f, MaximumLeftColumnRatio);
                Repaint();
                current.Use();
            }
            else if (current.type == EventType.MouseUp && _draggingSplitter)
            {
                _draggingSplitter = false;
                EditorPrefs.SetFloat(LeftColumnRatioKey, _leftColumnRatio);
                current.Use();
            }
        }

        private void DrawRightColumn(Rect rect)
        {
            bool inspectorOwnsKeyboard = KeyframeInspectorPanel.OwnsKeyboardFocus();
            float previewHeight = CalculatePreviewHeight(rect.height, _previewHeightRatio);
            Rect previewRect = new Rect(rect.x, rect.y, rect.width, previewHeight);
            Rect splitterRect = new Rect(rect.x, previewRect.yMax, rect.width, SplitterWidth);
            float timelineHeight = Mathf.Max(0f, rect.height - previewHeight - SplitterWidth - InspectorHeight);
            Rect timelineRect = new Rect(rect.x, splitterRect.yMax, rect.width, timelineHeight);
            Rect inspectorRect = new Rect(rect.x, timelineRect.yMax, rect.width, InspectorHeight);

            GUILayout.BeginArea(previewRect);
            GUI.Box(new Rect(0f, 0f, previewRect.width, previewRect.height), GUIContent.none, EditorStyles.helpBox);
            _previewPanel.OnGUI(new Rect(0f, 0f, previewRect.width, previewRect.height), inspectorOwnsKeyboard);
            GUILayout.EndArea();

            DrawPreviewSplitter(splitterRect, rect);

            GUILayout.BeginArea(inspectorRect);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _inspectorPanel.OnGUI();
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();

            // Inspector focus also blocks preview F-focus and timeline shortcuts.
            GUILayout.BeginArea(timelineRect);
            _timelineView.OnGUI(new Rect(0f, 0f, rect.width, timelineHeight), inspectorOwnsKeyboard);
            GUILayout.EndArea();
        }

        private void DrawPreviewSplitter(Rect splitterRect, Rect hostRect)
        {
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.25f));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && splitterRect.Contains(current.mousePosition))
            {
                _draggingPreviewSplitter = true;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && _draggingPreviewSplitter)
            {
                float usableHeight = Mathf.Max(1f, hostRect.height - SplitterWidth - InspectorHeight);
                _previewHeightRatio = Mathf.Clamp((current.mousePosition.y - hostRect.y) / usableHeight, 0.1f, MaximumPreviewHeightRatio);
                Repaint();
                current.Use();
            }
            else if (current.type == EventType.MouseUp && _draggingPreviewSplitter)
            {
                _draggingPreviewSplitter = false;
                EditorPrefs.SetFloat(PreviewHeightRatioKey, _previewHeightRatio);
                current.Use();
            }
        }

        private void RestoreSessionState()
        {
            FaceMotionSessionStateStore.Load(out string projectPath, out string animationId, out float time, out float zoom, out string avatarGlobalObjectId, out string avatarScenePath);
            if (!string.IsNullOrEmpty(projectPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(projectPath);
                if (asset != null)
                {
                    _project.LoadProject(asset);
                }
            }

            if (_session.ActiveProject != null)
            {
                if (!string.IsNullOrEmpty(animationId))
                {
                    _animations.Select(animationId);
                }

                _session.SetCurrentTime(time);
                _session.ViewState.Zoom = zoom;
            }

            RestoreAvatar(avatarGlobalObjectId, avatarScenePath);
        }

        internal static bool TryResolveAvatarDescriptor(string globalObjectId, out VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            return TryResolveAvatarDescriptor(globalObjectId, string.Empty, out descriptor);
        }

        internal static bool TryResolveAvatarDescriptor(string globalObjectId, string expectedScenePath, out VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrEmpty(globalObjectId)
                || !GlobalObjectId.TryParse(globalObjectId, out var id))
            {
                return false;
            }

            descriptor = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
            if (descriptor == null || !descriptor.gameObject.scene.IsValid() || !descriptor.gameObject.scene.isLoaded
                // A valid avatar may be in a loaded additive scene which is not active.
                || (!string.IsNullOrEmpty(expectedScenePath)
                    && !string.Equals(descriptor.gameObject.scene.path, expectedScenePath, StringComparison.Ordinal)))
            {
                descriptor = null;
                return false;
            }

            return true;
        }

        private static string GetAvatarGlobalObjectId(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || EditorUtility.IsPersistent(descriptor) || !descriptor.gameObject.scene.IsValid())
            {
                return string.Empty;
            }

            return GlobalObjectId.GetGlobalObjectIdSlow(descriptor).ToString();
        }

        private static string GetAvatarScenePath(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            return descriptor == null || !descriptor.gameObject.scene.IsValid()
                ? string.Empty
                : descriptor.gameObject.scene.path;
        }

        private void RestoreAvatar(string globalObjectId, string scenePath)
        {
            if (TryResolveAvatarDescriptor(globalObjectId, scenePath, out var descriptor))
            {
                _avatar.SetDescriptor(descriptor);
                _pendingAvatarGlobalObjectId = null;
                _pendingAvatarScenePath = null;
                _pendingAvatarRestoreAttempts = 0;
                return;
            }

            bool isNewPendingAvatar = !string.Equals(_pendingAvatarGlobalObjectId, globalObjectId, StringComparison.Ordinal)
                || !string.Equals(_pendingAvatarScenePath, scenePath, StringComparison.Ordinal);
            _pendingAvatarGlobalObjectId = globalObjectId;
            _pendingAvatarScenePath = scenePath;
            if (isNewPendingAvatar)
            {
                _pendingAvatarRestoreAttempts = 0;
            }
        }

        private void RestorePendingAvatar()
        {
            if (string.IsNullOrEmpty(_pendingAvatarGlobalObjectId)
                || _session?.ActiveDescriptor != null
                || _pendingAvatarRestoreAttempts >= MaximumPendingAvatarRestoreAttempts)
            {
                return;
            }

            _pendingAvatarRestoreAttempts++;
            RestoreAvatar(_pendingAvatarGlobalObjectId, _pendingAvatarScenePath);
            if (!string.IsNullOrEmpty(_pendingAvatarGlobalObjectId) && _session?.ActiveDescriptor == null)
            {
                // OnEnable can precede hierarchy and AvatarIndex availability.
                EditorApplication.delayCall += RestorePendingAvatar;
            }
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RestorePendingAvatar();
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            RestorePendingAvatar();
        }

        private void OpenProjectDialog()
        {
            string absolute = EditorUtility.OpenFilePanel(FaceMotionUiText.Get("openProjectDialog"), "Assets", "asset");
            if (!string.IsNullOrEmpty(absolute))
            {
                var asset = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(ProjectPanel.ToProjectRelativePath(absolute));
                _project.LoadProject(asset);
            }
        }

        private void TogglePlayback()
        {
            _playback.Toggle();
        }

        private void FitTimeline()
        {
            float leftWidth = CalculateLeftColumnWidth(position.width, _leftColumnRatio);
            float plotWidth = Mathf.Max(1f, position.width - leftWidth - SplitterWidth - TimelineGeometry.DefaultLabelWidth);
            _timelineView.Input.FitToContent(plotWidth);
        }

        private void StepZoom(int sign)
        {
            float leftWidth = CalculateLeftColumnWidth(position.width, _leftColumnRatio);
            Rect plot = new Rect(
                TimelineGeometry.DefaultLabelWidth,
                ToolbarHeight,
                Mathf.Max(1f, position.width - leftWidth - SplitterWidth - TimelineGeometry.DefaultLabelWidth),
                Mathf.Max(MinimumTimelineHeight, position.height - ToolbarHeight - InspectorHeight - MinimumPreviewHeight - SplitterWidth));
            _timelineView.Input.ZoomStep(sign, plot);
        }

        private void OnEditorUpdate()
        {
            _playback?.Tick(EditorApplication.timeSinceStartup);
            SetPlaybackUpdateActive(_playback != null && _playback.IsPlaying);
        }

        private void OnHierarchyChanged()
        {
            _avatar?.MarkAvatarDirtyFromHierarchy();
            RestorePendingAvatar();
        }

        /// <summary>Waits for Unity's hierarchy notification before refreshing an integration-mutated avatar index.</summary>
        private void ScheduleIntegrationIndexRefresh(VRCAvatarDescriptor avatar)
        {
            if (avatar == null) return;
            _integrationIndexRefreshAvatar = avatar;
            if (_integrationIndexRefreshScheduled) return;
            _integrationIndexRefreshScheduled = true;
            EditorApplication.QueuePlayerLoopUpdate();
            EditorApplication.update += FlushIntegrationIndexRefresh;
        }

        private void FlushIntegrationIndexRefresh()
        {
            EditorApplication.update -= FlushIntegrationIndexRefresh;
            _integrationIndexRefreshScheduled = false;
            var avatar = _integrationIndexRefreshAvatar;
            _integrationIndexRefreshAvatar = null;
            _avatar?.RefreshIndexAfterIntegration(avatar);
        }

        private void OnUndoRedoPerformed()
        {
            _session?.RefreshAfterUndo();
        }

        private void OnSessionChanged()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (!string.Equals(_lastPreviewAnimationId, _session.SelectedAnimationId, StringComparison.Ordinal))
            {
                _lastPreviewAnimationId = _session.SelectedAnimationId;
                _previewSession?.Override.Clear();
            }

            _sessionChangedCount++;
            FaceMotionPreviewTrace.Trace(
                "C.OnSessionChanged",
                "window={0} count={1} time={2} event={3}",
                GetInstanceID(),
                _sessionChangedCount,
                _session.ViewState.CurrentTime,
                Event.current == null ? "<none>" : Event.current.type.ToString());
            SynchronizePreviewFromSession();
            SetPlaybackUpdateActive(_playback != null && _playback.IsPlaying);
            RequestPreviewRepaint();
        }

        private void SetPlaybackUpdateActive(bool active)
        {
            if (!_playbackUpdateGate.Synchronize(active))
            {
                return;
            }

            if (_playbackUpdateGate.IsRegistered)
            {
                EditorApplication.update += OnEditorUpdate;
            }
            else
            {
                EditorApplication.update -= OnEditorUpdate;
            }
        }

        private void RequestPreviewRepaint()
        {
            bool scheduled = _previewRepaint.Request();
            FaceMotionPreviewTrace.Trace(
                "R.Request",
                "window={0} requestCount={1} scheduled={2} pending={3} event={4}",
                GetInstanceID(),
                _previewRepaint.RequestCount,
                scheduled,
                _previewRepaint.Pending,
                Event.current == null ? "<none>" : Event.current.type.ToString());
            if (!scheduled)
            {
                return;
            }

            EditorApplication.delayCall += FlushPreviewRepaint;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void FlushPreviewRepaint()
        {
            if (!_previewRepaint.Dispatch())
            {
                return;
            }

            FaceMotionPreviewTrace.Trace(
                "R.Dispatch",
                "window={0} dispatchCount={1} event={2}",
                GetInstanceID(),
                _previewRepaint.DispatchCount,
                Event.current == null ? "<none>" : Event.current.type.ToString());
            Repaint();
        }

        /// <summary>Keeps preview and optional scene apply on the same session playhead.</summary>
        private void SynchronizePreviewFromSession()
        {
            if (_previewSession == null || _session == null)
            {
                return;
            }

            var animation = _session.GetSelectedAnimation();
            FaceMotionPreviewTrace.Trace(
                "D.Synchronize",
                "animationId={0} time={1} previewActive={2} sceneApplyActive={3}",
                animation == null ? "<null>" : animation.AnimationId,
                _session.ViewState.CurrentTime,
                _previewSession.IsActive,
                _sceneApplySession != null && _sceneApplySession.IsActive);
            bool active = _previewSession.IsActive || (_sceneApplySession != null && _sceneApplySession.IsActive);
            if (!active || _previewGate == null)
            {
                return;
            }

            // While the timeline is in an interactive scrub drag, synchronize through the gate:
            // every MouseDrag event requests the latest sample but only the final one evaluates.
            // Direct (non-drag) time changes still evaluate synchronously, one sample per change.
            _previewGate.Scrubbing = _session.ViewState.DragMode == TimelineDragMode.Scrub;
            _previewGate.Synchronize(animation, _session.ViewState.CurrentTime, _session.PreviewRevision);
            float duration = _session.GetSelectedDuration();
            bool endpoint = _previewGate.Scrubbing &&
                (Mathf.Approximately(_session.ViewState.CurrentTime, 0f) ||
                  Mathf.Approximately(_session.ViewState.CurrentTime, duration));
            if (endpoint)
            {
                _previewGate.FlushPending(_session.PreviewRevision);
                RequestPreviewRepaint();
            }
            if (_previewGate.Pending && !_previewDrainScheduled)
            {
                _previewDrainScheduled = true;
                EditorApplication.delayCall += DrainPendingPreviewEvaluation;
            }
        }

        /// <summary>Flushes the latest coalesced scrub sample once, outside the drag gesture.</summary>
        private void DrainPendingPreviewEvaluation()
        {
            _previewDrainScheduled = false;
            if (_previewGate != null && _previewGate.FlushPending(_session == null ? 0 : _session.PreviewRevision))
            {
                RequestPreviewRepaint();
            }
        }

        private void OnScrubEnded()
        {
            _previewGate?.EndScrub(_session == null ? 0 : _session.PreviewRevision);
            RequestPreviewRepaint();
        }

        private void EvaluatePreviewAt(FaceMotionAnimationData animation, float time)
        {
            if (_previewSession != null && _previewSession.IsActive)
            {
                _previewSession.EnsureAvatar(_session == null ? null : _session.ActiveAvatarRoot);
                _previewSession.Evaluate(animation, time);
                ScheduleDeferredPreviewRepaint();
            }

            _sceneApplySession?.Apply(animation, time);
        }

        /// <summary>
        /// Skinned mesh deformation can become visible one editor tick after a preview mutation.
        /// This repaint-only callback never evaluates animation data or changes the preview clone.
        /// </summary>
        private void ScheduleDeferredPreviewRepaint()
        {
            if (!_previewDeferredRepaint.Request())
            {
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            EditorApplication.update += FlushDeferredPreviewRepaint;
        }

        private void FlushDeferredPreviewRepaint()
        {
            EditorApplication.update -= FlushDeferredPreviewRepaint;
            if (!_previewDeferredRepaint.Dispatch() || _session == null)
            {
                return;
            }

            Repaint();
        }

        private void OnBeforeAssemblyReload()
        {
            if (_session != null)
            {
                _session.ViewState.IsPlaying = false;
            }

            _sceneApplySession?.Dispose();
            _previewSession?.Dispose();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_session != null)
                {
                    _session.ViewState.IsPlaying = false;
                }

                _sceneApplySession?.Dispose();
                _previewSession?.Dispose();
            }
        }
    }
}
