using System;
using FaceMotion.Data;
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
        internal const float ToolbarHorizontalPadding = 8f;
        internal const float MinimumWindowHeight = 700f;
        internal const float MinimumLeftColumnWidth = 320f;
        internal const float MaximumLeftColumnRatio = 0.4f;
        internal const float MinimumTimelineWidth = 460f;
        internal const float SplitterWidth = 5f;

        /// <summary>
        /// Avatar id scheme for objects in unsaved scenes: those have no stable
        /// GlobalObjectId, so the selection persists as an in-session instance id.
        /// </summary>
        internal const string AvatarInstanceIdScheme = "Instance:";

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
        private string _pendingAvatarGlobalObjectId;
        private string _pendingAvatarScenePath;
        private int _pendingAvatarRestoreAttempts;
        private string _persistedAvatarGlobalObjectId;
        private string _persistedAvatarScenePath;
        private VRCAvatarDescriptor _lastPersistedAvatar;
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
            _trackListPanel = new TrackListPanel(_session, _tracks, _previewSession.Override, RequestHoverPreviewRepaint);
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

                // Persist the tracked avatar identity instead of deriving it from the
                // live descriptor: an unresolved pending restore never transitioned,
                // so its stored id survives the close instead of flattening to empty.
                SynchronizePersistedAvatar();
                StoreSessionState();
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

        private static readonly float[] ToolbarMinimumWidths = { 54f, 70f, 48f, 42f, 54f, 54f };
        private static readonly float[] GuidanceMinimumWidths = { 52f, 88f, 40f, 40f };

        /// <summary>
        /// Distributes toolbar/guidance element widths across the available space. Each element
        /// keeps its preferred (localization-aware) width when there is room, falls back to its
        /// per-item minimum when space is tight, and shrinks proportionally only when even the
        /// minimums cannot fit, so Japanese labels are never clipped by English-era constants.
        /// </summary>
        internal static float[] CalculateToolbarWidths(float[] preferredWidths, float[] minimumWidths, float availableWidth)
        {
            if (preferredWidths == null || preferredWidths.Length == 0)
            {
                return new float[0];
            }

            if (minimumWidths == null || minimumWidths.Length != preferredWidths.Length)
            {
                throw new System.ArgumentException("minimumWidths must match preferredWidths", nameof(minimumWidths));
            }

            var widths = new float[preferredWidths.Length];
            float available = Mathf.Max(0f, availableWidth);
            float preferredSum = 0f;
            float minimumSum = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                float minimum = Mathf.Max(0f, minimumWidths[i]);
                widths[i] = Mathf.Max(minimum, Mathf.Max(0f, preferredWidths[i]));
                preferredSum += widths[i];
                minimumSum += minimum;
            }

            if (preferredSum <= available || preferredSum <= 0f)
            {
                return widths;
            }

            if (available >= minimumSum)
            {
                float remainder = available - minimumSum;
                for (int i = 0; i < widths.Length; i++)
                {
                    float minimum = Mathf.Max(0f, minimumWidths[i]);
                    widths[i] = minimum + remainder * (widths[i] / preferredSum);
                }

                return widths;
            }

            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = available * (widths[i] / preferredSum);
            }

            return widths;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var file = new GUIContent(FaceMotionUiText.Get("file"), FaceMotionUiText.Get("fileTooltip"));
            var avatar = new GUIContent(FaceMotionUiText.Get("avatar"), FaceMotionUiText.Get("avatarTooltip"));
            var play = new GUIContent(
                _session.ViewState.IsPlaying ? FaceMotionUiText.Get("pause") : FaceMotionUiText.Get("play"),
                FaceMotionUiText.Get("playTooltip"));
            var fit = new GUIContent(FaceMotionUiText.Get("fit"), FaceMotionUiText.Get("fitTooltip"));
            var zoomOut = new GUIContent(FaceMotionUiText.Get("zoomOutLabel"), FaceMotionUiText.Get("zoomOut"));
            var zoomIn = new GUIContent(FaceMotionUiText.Get("zoomInLabel"), FaceMotionUiText.Get("zoomIn"));
            float[] widths = CalculateToolbarWidths(
                new[]
                {
                    EditorStyles.toolbarDropDown.CalcSize(file).x,
                    EditorStyles.toolbarDropDown.CalcSize(avatar).x,
                    EditorStyles.toolbarButton.CalcSize(play).x,
                    EditorStyles.toolbarButton.CalcSize(fit).x,
                    EditorStyles.toolbarButton.CalcSize(zoomOut).x,
                    EditorStyles.toolbarButton.CalcSize(zoomIn).x,
                },
                ToolbarMinimumWidths,
                Mathf.Max(0f, position.width - ToolbarHorizontalPadding));

            if (EditorGUILayout.DropdownButton(
                file,
                FocusType.Passive,
                EditorStyles.toolbarDropDown,
                GUILayout.Width(widths[0])))
            {
                ShowFileMenu();
            }

            if (EditorGUILayout.DropdownButton(
                avatar,
                FocusType.Passive,
                EditorStyles.toolbarDropDown,
                GUILayout.Width(widths[1])))
            {
                ShowAvatarMenu();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(play, EditorStyles.toolbarButton, GUILayout.Width(widths[2])))
            {
                TogglePlayback();
            }

            if (GUILayout.Button(fit, EditorStyles.toolbarButton, GUILayout.Width(widths[3])))
            {
                FitTimeline();
            }

            if (GUILayout.Button(zoomOut, EditorStyles.toolbarButton, GUILayout.Width(widths[4])))
            {
                StepZoom(-1);
            }

            if (GUILayout.Button(zoomIn, EditorStyles.toolbarButton, GUILayout.Width(widths[5])))
            {
                StepZoom(+1);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGuidance()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(_session, _previewSession != null && _previewSession.IsActive);
            string step = string.Format(FaceMotionUiText.Get("guidanceStepFormat"), model.StepNumber);
            string hint = FaceMotionUiText.Get(model.HintKey);
            GUILayout.BeginArea(new Rect(0f, ToolbarHeight, position.width, GuidanceHeight));
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var stepContent = new GUIContent(step);
            var nextContent = new GUIContent(FaceMotionUiText.Get("guidanceNextAction"));
            var hintContent = new GUIContent(hint);
            bool complete = model.IsComplete;
            var badgeContent = complete ? new GUIContent(FaceMotionUiText.Get("guidanceCompleteBadge")) : null;
            int count = complete ? 4 : 3;
            var preferred = new float[count];
            var minimums = new float[count];
            preferred[0] = EditorStyles.miniBoldLabel.CalcSize(stepContent).x;
            minimums[0] = GuidanceMinimumWidths[0];
            preferred[1] = EditorStyles.miniLabel.CalcSize(nextContent).x;
            minimums[1] = GuidanceMinimumWidths[1];
            preferred[2] = EditorStyles.boldLabel.CalcSize(hintContent).x;
            minimums[2] = GuidanceMinimumWidths[2];
            if (complete)
            {
                preferred[3] = EditorStyles.miniBoldLabel.CalcSize(badgeContent).x;
                minimums[3] = GuidanceMinimumWidths[3];
            }

            float[] widths = CalculateToolbarWidths(
                preferred,
                minimums,
                Mathf.Max(0f, position.width - ToolbarHorizontalPadding));
            GUILayout.Label(stepContent, EditorStyles.miniBoldLabel, GUILayout.Width(widths[0]));
            GUILayout.Label(nextContent, EditorStyles.miniLabel, GUILayout.Width(widths[1]));
            GUILayout.Label(hintContent, EditorStyles.boldLabel, GUILayout.Width(widths[2]));
            GUILayout.FlexibleSpace();
            if (complete)
            {
                GUILayout.Label(badgeContent, EditorStyles.miniBoldLabel, GUILayout.Width(widths[3]));
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

            // Seed the tracked identity from the store before any restore attempt so a
            // failed (pending) restore keeps the stored id instead of starting empty.
            _persistedAvatarGlobalObjectId = avatarGlobalObjectId ?? string.Empty;
            _persistedAvatarScenePath = avatarScenePath ?? string.Empty;

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

            // The restore itself must never be treated as a selection change.
            _lastPersistedAvatar = NormalizeDescriptor(_session.ActiveDescriptor);
        }

        internal static bool TryResolveAvatarDescriptor(string globalObjectId, out VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            return TryResolveAvatarDescriptor(globalObjectId, string.Empty, out descriptor);
        }

        internal static bool TryResolveAvatarDescriptor(string globalObjectId, string expectedScenePath, out VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrEmpty(globalObjectId))
            {
                return false;
            }

            if (globalObjectId.StartsWith(AvatarInstanceIdScheme, StringComparison.Ordinal))
            {
                // In-session instance id for avatars in unsaved scenes.
                if (!int.TryParse(globalObjectId.Substring(AvatarInstanceIdScheme.Length), out int instanceId))
                {
                    return false;
                }

                descriptor = EditorUtility.InstanceIDToObject(instanceId) as VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
            }
            else
            {
                if (!GlobalObjectId.TryParse(globalObjectId, out var id))
                {
                    return false;
                }

                descriptor = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
            }

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

            // An unsaved scene has no stable GlobalObjectId; persist an in-session
            // instance id so close/reopen still restores the selection.
            if (string.IsNullOrEmpty(descriptor.gameObject.scene.path))
            {
                return AvatarInstanceIdScheme + descriptor.GetInstanceID();
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
            // Runs before the compile/update gate: an avatar change must persist even
            // when the rest of the session work is deferred to a later event.
            SynchronizePersistedAvatar();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (!string.Equals(_lastPreviewAnimationId, _session.SelectedAnimationId, StringComparison.Ordinal))
            {
                _lastPreviewAnimationId = _session.SelectedAnimationId;
                _previewSession?.Override.Clear();
            }

            SynchronizePreviewFromSession();
            SetPlaybackUpdateActive(_playback != null && _playback.IsPlaying);
            RequestPreviewRepaint();
        }

        /// <summary>
        /// Tracks the avatar selection independently of live scene state and writes it
        /// through on every descriptor transition. An explicit clear (or a destroyed
        /// descriptor, treated as a clear) wipes the stored id; a new descriptor
        /// replaces it; an unresolved pending restore never transitions, so its stored
        /// id is kept untouched.
        /// </summary>
        private void SynchronizePersistedAvatar()
        {
            if (_session == null)
            {
                return;
            }

            VRCAvatarDescriptor current = NormalizeDescriptor(_session.ActiveDescriptor);
            if (ReferenceEquals(current, _lastPersistedAvatar))
            {
                return;
            }

            _lastPersistedAvatar = current;
            _pendingAvatarGlobalObjectId = null;
            _pendingAvatarScenePath = null;
            _pendingAvatarRestoreAttempts = 0;

            if (current == null)
            {
                _persistedAvatarGlobalObjectId = string.Empty;
                _persistedAvatarScenePath = string.Empty;
            }
            else
            {
                _persistedAvatarGlobalObjectId = GetAvatarGlobalObjectId(current);
                _persistedAvatarScenePath = GetAvatarScenePath(current);
            }

            StoreSessionState();
        }

        private void StoreSessionState()
        {
            FaceMotionSessionStateStore.Save(
                _session.ActiveProjectAssetPath,
                _session.SelectedAnimationId,
                _session.ViewState.CurrentTime,
                _session.ViewState.Zoom,
                _persistedAvatarGlobalObjectId,
                _persistedAvatarScenePath);
        }

        /// <summary>Normalizes Unity's fake-null (destroyed object) to a real null so identity transitions compare safely.</summary>
        private static VRCAvatarDescriptor NormalizeDescriptor(VRCAvatarDescriptor descriptor)
        {
            return descriptor != null ? descriptor : null;
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

        /// <summary>
        /// Requests a repaint for a preview-override (hover) change. Changes that land
        /// during the Repaint pass are consumed and rendered in that same pass — the left
        /// column hover handling runs before the right column preview — so scheduling
        /// another repaint would render the already-current state a second time.
        /// </summary>
        private void RequestHoverPreviewRepaint()
        {
            if (!ShouldScheduleHoverRepaint(Event.current))
            {
                return;
            }

            RequestPreviewRepaint();
        }

        internal static bool ShouldScheduleHoverRepaint(Event current)
        {
            return current == null || current.type != EventType.Repaint;
        }

        private void RequestPreviewRepaint()
        {
            bool scheduled = _previewRepaint.Request();
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
