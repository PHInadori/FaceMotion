using System.Linq;
using System.Reflection;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class L2QuickKeyUxTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TempFaceMotionAsset _temp;
        private bool _hadStoredValue;
        private float _storedValue;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _hadStoredValue = EditorPrefs.HasKey(QuickKeyPreferences.ValueKey);
            _storedValue = QuickKeyPreferences.Value;
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _temp = new TempFaceMotionAsset();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadStoredValue)
            {
                EditorPrefs.SetFloat(QuickKeyPreferences.ValueKey, _storedValue);
            }
            else
            {
                EditorPrefs.DeleteKey(QuickKeyPreferences.ValueKey);
            }

            _temp?.Dispose();
            Undo.ClearAll();
        }

        [Test]
        public void QuickKeyValue_DefaultsToOneHundred()
        {
            EditorPrefs.DeleteKey(QuickKeyPreferences.ValueKey);

            Assert.That(QuickKeyPreferences.Value, Is.EqualTo(100f));
        }

        [Test]
        public void QuickKeyValue_ClampsToTheConfiguredRange()
        {
            QuickKeyPreferences.Value = -1f;
            Assert.That(QuickKeyPreferences.Value, Is.Zero);

            QuickKeyPreferences.Value = 101f;
            Assert.That(QuickKeyPreferences.Value, Is.EqualTo(100f));
        }

        [Test]
        public void QuickKeyValue_PersistsAcrossSessionRecreationWithoutDirtyingProject()
        {
            var project = FaceMotionProject.CreateNew();
            AssetDatabase.CreateAsset(project, _temp.AssetPath("QuickKeyPreference"));
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            Assert.That(EditorUtility.IsDirty(project), Is.False);

            QuickKeyPreferences.Value = 50f;
            var recreatedSession = new FaceMotionEditorSession();
            var recreatedController = new KeyframeController(recreatedSession);

            Assert.That(QuickKeyPreferences.Value, Is.EqualTo(50f));
            Assert.That(recreatedController.CanApplyQuickKey(), Is.False);
            Assert.That(EditorUtility.IsDirty(project), Is.False);
        }

        [Test]
        public void QuickKey_InsertsConfiguredBlendShapeValueAtSnappedPlayhead()
        {
            SetupBlendShapeTrack();
            QuickKeyPreferences.Value = 50f;
            _session.SetCurrentTime(0.5f);

            string keyId = _keys.ApplyQuickKey(QuickKeyPreferences.Value);
            var key = GetBlendShapeKeys().Single();

            Assert.That(keyId, Is.EqualTo(key.KeyId));
            Assert.That(key.Time, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(key.Value, Is.EqualTo(50f));
        }

        [Test]
        public void QuickKey_UpdatesExistingKeyInsteadOfCreatingADuplicate()
        {
            SetupBlendShapeTrack();
            _session.SetCurrentTime(0.5f);
            string existing = _keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear);
            Undo.ClearAll();

            string result = _keys.ApplyQuickKey(100f);

            var key = GetBlendShapeKeys().Single();
            Assert.That(result, Is.EqualTo(existing));
            Assert.That(key.KeyId, Is.EqualTo(existing));
            Assert.That(key.Value, Is.EqualTo(100f));
        }

        [Test]
        public void QuickKey_UndoRemovesInsertedKey()
        {
            SetupBlendShapeTrack();
            _session.SetCurrentTime(0.5f);
            Undo.ClearAll();

            Assert.That(_keys.ApplyQuickKey(35f), Is.Not.Null);
            Assert.That(GetBlendShapeKeys().Single().Value, Is.EqualTo(35f));
            Undo.PerformUndo();

            Assert.That(GetBlendShapeKeys(), Is.Empty);
        }

        [Test]
        public void QuickKey_UndoRestoresPriorExistingValue()
        {
            SetupBlendShapeTrack();
            _session.SetCurrentTime(0.5f);
            _keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear);
            Undo.ClearAll();

            Assert.That(_keys.ApplyQuickKey(100f), Is.Not.Null);
            Undo.PerformUndo();

            Assert.That(GetBlendShapeKeys().Single().Value, Is.EqualTo(20f));
        }

        [Test]
        public void QuickKey_RejectsOutOfRangePlayheadAndAllowsDurationEndpoint()
        {
            SetupBlendShapeTrack();
            float duration = _session.GetSelectedAnimation().Timeline.Duration;

            _session.ViewState.CurrentTime = -0.01f;
            Assert.That(_keys.ApplyQuickKey(100f), Is.Null);
            _session.ViewState.CurrentTime = duration + 0.01f;
            Assert.That(_keys.ApplyQuickKey(100f), Is.Null);
            Assert.That(GetBlendShapeKeys(), Is.Empty);

            _session.ViewState.CurrentTime = duration;
            Assert.That(_keys.ApplyQuickKey(100f), Is.Not.Null);
            Assert.That(GetBlendShapeKeys().Single().Time, Is.EqualTo(duration).Within(1e-5f));
        }

        [Test]
        public void QuickKey_LeavesBaselineUnchangedWhenAuthoringElsewhere()
        {
            SetupBlendShapeTrack();
            _keys.AddKeyAt(0f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.SetCurrentTime(0.5f);

            Assert.That(_keys.ApplyQuickKey(100f), Is.Not.Null);

            Assert.That(GetBlendShapeKeys().Single(key => Mathf.Approximately(key.Time, 0f)).Value, Is.EqualTo(20f));
        }

        [Test]
        public void QuickKey_UpdatesBaselineWhenIntentionallyInvokedAtZero()
        {
            SetupBlendShapeTrack();
            _keys.AddKeyAt(0f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.SetCurrentTime(0f);

            Assert.That(_keys.ApplyQuickKey(100f), Is.Not.Null);
            Assert.That(GetBlendShapeKeys(), Has.Count.EqualTo(1));
            Assert.That(GetBlendShapeKeys().Single().Value, Is.EqualTo(100f));
        }

        [Test]
        public void QuickKey_IsUnavailableForInvalidContextAndTransformTracks()
        {
            Assert.That(_keys.ApplyQuickKey(100f), Is.Null);

            var project = CreateProject("QuickKeyInvalidContext");
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            Assert.That(_keys.ApplyQuickKey(100f), Is.Null);

            _animations.Add();
            Assert.That(_keys.ApplyQuickKey(100f), Is.Null);

            _tracks.AddTransformTrack(TrackKind.TransformPosition, "Head");
            Assert.That(_keys.CanApplyQuickKey(), Is.False);
            Assert.That(_keys.ApplyQuickKey(100f), Is.Null);
            Assert.That(_session.GetSelectedTrack().Transform.Keys, Is.Empty);

            _session.SetCurrentTime(0.5f);
            Assert.That(_keys.AddKeyAtCurrentTime(0f, Vector3.zero, InterpolationType.Linear), Is.Not.Null);
            Assert.That(_session.GetSelectedTrack().Transform.Keys.Single().Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void QuickKeyShortcut_GatesAllFaceMotionTextEditing()
        {
            Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(true, null), Is.False);
            Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(false, "FaceMotion.AnimationList.duration"), Is.False);
            Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(false, "FaceMotion.KeyInspector.quickKeyValue"), Is.False);
            Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(false, string.Empty), Is.True);
        }

        [Test]
        public void QuickKeyShortcut_IsRegisteredWithUnityShortcutManager()
        {
            MethodInfo method = typeof(FaceMotionWindow).GetMethod("InvokeQuickKeyShortcut", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetCustomAttributes(typeof(ShortcutAttribute), false).Length, Is.EqualTo(1));
        }

        [Test]
        public void QuickKey_LocalizationUsesJapaneseAndEnglishLabels()
        {
            Assert.That(FaceMotionUiText.Get("quickKey"), Is.EqualTo("クイックキー"));
            Assert.That(FaceMotionUiText.Get("quickKeyValue"), Is.EqualTo("クイックキー値"));
            Assert.That(FaceMotionUiText.Get("quickKey", SystemLanguage.English), Is.EqualTo("Quick Key"));
            Assert.That(FaceMotionUiText.Get("quickKeyValue", SystemLanguage.English), Is.EqualTo("Quick Key Value"));
        }

        [Test]
        public void QuickKeyLayout_StacksOnlyWhenTheInlineControlsWouldNotFit()
        {
            const float labelWidth = 90f;
            const float buttonWidth = 80f;
            float required = labelWidth
                + KeyframeInspectorPanel.MinimumQuickKeyValueWidth
                + buttonWidth
                + KeyframeInspectorPanel.QuickKeyControlSpacing;

            Assert.That(KeyframeInspectorPanel.ShouldStackQuickKeyControls(required, labelWidth, buttonWidth), Is.False);
            Assert.That(KeyframeInspectorPanel.ShouldStackQuickKeyControls(required - 1f, labelWidth, buttonWidth), Is.True);
            Assert.That(FaceMotionWindow.MinimumWindowHeight, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumRequiredWindowHeight));
        }

        [Test]
        public void BackgroundFocusRelease_RequiresAnUnclaimedEditingMouseDown()
        {
            Assert.That(FaceMotionFocusUtility.ShouldReleaseOnBackgroundMouseDown(EventType.MouseDown, 0, 0, true), Is.True);
            Assert.That(FaceMotionFocusUtility.ShouldReleaseOnBackgroundMouseDown(EventType.MouseDown, 0, 15, true), Is.False);
            Assert.That(FaceMotionFocusUtility.ShouldReleaseOnBackgroundMouseDown(EventType.MouseDown, 0, 0, false), Is.False);
            Assert.That(FaceMotionFocusUtility.ShouldReleaseOnBackgroundMouseDown(EventType.MouseUp, 0, 0, true), Is.False);
        }

        [Test]
        public void BackgroundFocusRelease_EndsEditingAndRestoresShortcutAvailability()
        {
            int previousKeyboardControl = GUIUtility.keyboardControl;
            bool previousEditing = EditorGUIUtility.editingTextField;
            try
            {
                GUIUtility.keyboardControl = 4711;
                EditorGUIUtility.editingTextField = true;

                Assert.That(FaceMotionWindow.ReleaseTextFocusOnBackgroundMouseDown(EventType.MouseDown, 0, 0, EditorGUIUtility.editingTextField), Is.True);
                Assert.That(GUIUtility.keyboardControl, Is.Zero);
                Assert.That(EditorGUIUtility.editingTextField, Is.False);
                Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(EditorGUIUtility.editingTextField, string.Empty), Is.True);
            }
            finally
            {
                GUIUtility.keyboardControl = previousKeyboardControl;
                EditorGUIUtility.editingTextField = previousEditing;
            }
        }

        [Test]
        public void QuickKey_BecomesAvailableImmediatelyAfterNormalScrubEnd()
        {
            SetupBlendShapeTrack();
            int scrubEndCount = 0;
            var timeline = new TimelineView(_session, _keys, _tracks, () => scrubEndCount++);
            var rect = new Rect(0f, 0f, 120f, 100f);
            var layout = new TimelineLayoutSnapshot();
            const int controlId = 1842;
            int previousHotControl = GUIUtility.hotControl;
            try
            {
                GUIUtility.hotControl = 0;
                Assert.That(TimelineView.RoutePointerEvent(Pointer(EventType.MouseDown, 60f, 5f), controlId, rect, timeline.Input, _session.ViewState, rect, layout, false), Is.True);
                Assert.That(TimelineView.RoutePointerEvent(Pointer(EventType.MouseDrag, 90f, 5f), controlId, rect, timeline.Input, _session.ViewState, rect, layout, false), Is.True);
                Assert.That(TimelineView.RoutePointerEvent(Pointer(EventType.MouseUp, 90f, 5f), controlId, rect, timeline.Input, _session.ViewState, rect, layout, false), Is.True);

                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
                Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.75f).Within(1e-5f));
                Assert.That(scrubEndCount, Is.EqualTo(1));
                Assert.That(_keys.CanApplyQuickKey(), Is.True);
                Assert.That(_keys.ApplyQuickKey(50f), Is.Not.Null, "Button-style action must work on the scrub-ending state.");
                Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(false, string.Empty), Is.True, "Shortcut-style gating must also be available after scrub end.");
            }
            finally
            {
                GUIUtility.hotControl = previousHotControl;
            }
        }

        [Test]
        public void QuickKey_BecomesAvailableWhenMouseUpArrivesAfterLostTimelineCapture()
        {
            SetupBlendShapeTrack();
            var timeline = new TimelineView(_session, _keys, _tracks);
            var rect = new Rect(0f, 0f, 120f, 100f);
            var layout = new TimelineLayoutSnapshot();
            const int controlId = 1843;
            int previousHotControl = GUIUtility.hotControl;
            try
            {
                GUIUtility.hotControl = 0;
                TimelineView.RoutePointerEvent(Pointer(EventType.MouseDown, 60f, 5f), controlId, rect, timeline.Input, _session.ViewState, rect, layout, false);
                TimelineView.RoutePointerEvent(Pointer(EventType.MouseDrag, 90f, 5f), controlId, rect, timeline.Input, _session.ViewState, rect, layout, false);
                GUIUtility.hotControl = 0;

                Assert.That(TimelineView.RoutePointerEvent(Pointer(EventType.MouseUp, 90f, 5f), controlId, rect, timeline.Input, _session.ViewState, rect, layout, false), Is.False);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
                Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.75f).Within(1e-5f));
                Assert.That(_keys.CanApplyQuickKey(), Is.True);
                Assert.That(_keys.ApplyQuickKey(100f), Is.Not.Null);
            }
            finally
            {
                GUIUtility.hotControl = previousHotControl;
            }
        }

        private void SetupBlendShapeTrack()
        {
            var project = CreateProject("QuickKeyBlendShape");
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            _animations.Add();
            _tracks.AddBlendShapeTrack("Face", "Smile");
        }

        private FaceMotionProject CreateProject(string name)
        {
            var project = FaceMotionProject.CreateNew();
            EditorUtility.SetDirty(project);
            AssetDatabase.CreateAsset(project, _temp.AssetPath(name));
            AssetDatabase.SaveAssets();
            return project;
        }

        private System.Collections.Generic.IReadOnlyList<FloatKeyframeData> GetBlendShapeKeys()
        {
            return _session.GetSelectedTrack().BlendShape.Keys;
        }

        private static Event Pointer(EventType type, float x, float y)
        {
            return new Event
            {
                type = type,
                button = 0,
                mousePosition = new Vector2(x, y)
            };
        }
    }
}
