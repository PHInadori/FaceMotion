using System;
using System.Linq;
using System.Reflection;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class M2KeyInspectorAutoApplyTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private KeyframeInspectorPanel _panel;
        private TempFaceMotionAsset _temp;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _panel = new KeyframeInspectorPanel(_session, _keys);
            _temp = new TempFaceMotionAsset();
            var project = FaceMotionProject.CreateNew();
            AssetDatabase.CreateAsset(project, _temp.AssetPath("M2AutoApply"));
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            _animations.Add();
            _session.ViewState.SnapEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            _temp?.Dispose();
            Undo.ClearAll();
        }

        [Test] public void TEST1_BlendShapeValue_AppliesImmediately() { string id = AddBlendKey(10f); ApplyFloat(id, 55f); Assert.That(FloatOf(id), Is.EqualTo(55f)); }
        [Test] public void TEST2_BlendShapeValue_ClampsToRange() { string id = AddBlendKey(10f); ApplyFloat(id, 150f); Assert.That(FloatOf(id), Is.EqualTo(100f)); ApplyFloat(id, -1f); Assert.That(FloatOf(id), Is.Zero); }
        [Test] public void TEST3_Position_AppliesImmediately() { string id = AddTransformKey(TrackKind.TransformPosition, Vector3.zero); ApplyVector(id, new Vector3(1f, 2f, 3f)); Assert.That(VectorOf(id), Is.EqualTo(new Vector3(1f, 2f, 3f))); }
        [Test] public void TEST4_Rotation_AppliesImmediately() { string id = AddTransformKey(TrackKind.TransformRotation, Vector3.zero); ApplyVector(id, new Vector3(10f, 20f, 30f)); Assert.That(VectorOf(id), Is.EqualTo(new Vector3(10f, 20f, 30f))); }
        [Test] public void TEST5_Scale_AppliesImmediately() { string id = AddTransformKey(TrackKind.TransformScale, Vector3.one); ApplyVector(id, new Vector3(2f, 3f, 4f)); Assert.That(VectorOf(id), Is.EqualTo(new Vector3(2f, 3f, 4f))); }

        [Test]
        public void TEST6_Interpolation_AppliesImmediately()
        {
            string id = AddBlendKey(10f);
            _session.Selection.SetSingle(id); _panel.Synchronize();
            Set("_interpolationIndex", (int)InterpolationType.Smooth);
            Assert.That(_panel.AutoApplySelectedInterpolation(id, TrackOf(id)), Is.True);
            Assert.That(TrackOf(id).BlendShape.Keys.Single(k => k.KeyId == id).Interpolation, Is.EqualTo(InterpolationType.Smooth));
        }

        [Test]
        public void TEST7_ValueEdit_IncrementsPreviewRevision()
        {
            string id = AddBlendKey(10f); int before = _session.PreviewRevision;
            ApplyFloat(id, 20f);
            Assert.That(_session.PreviewRevision, Is.GreaterThan(before));
        }

        [Test]
        public void TEST8_Undo_RestoresPreviousValue()
        {
            string id = AddBlendKey(10f); ApplyFloat(id, 30f); Undo.PerformUndo(); _session.RefreshAfterUndo();
            Assert.That(FloatOf(id), Is.EqualTo(10f));
        }

        [Test]
        public void TEST9_Redo_RestoresEditedValue()
        {
            string id = AddBlendKey(10f); ApplyFloat(id, 30f); Undo.PerformUndo(); Undo.PerformRedo(); _session.RefreshAfterUndo();
            Assert.That(FloatOf(id), Is.EqualTo(30f));
        }

        [Test]
        public void TEST10_OneAutoApply_UsesOneUndoStep()
        {
            string id = AddBlendKey(10f); ApplyFloat(id, 30f); Undo.PerformUndo(); _session.RefreshAfterUndo();
            Assert.That(FloatOf(id), Is.EqualTo(10f));
        }

        [Test]
        public void TEST11_NoSelectedKey_DoesNotMutateExistingKey()
        {
            string id = AddBlendKey(10f); _session.Selection.Clear(); Set("_floatValue", 80f);
            Assert.That(_panel.AutoApplySelectedValue(null, null), Is.False); Assert.That(FloatOf(id), Is.EqualTo(10f));
        }

        [Test]
        public void TEST12_NoSelectedTrack_AddKeyIsNoOp()
        {
            _session.SelectedTrackId = null;
            Assert.That(_panel.AddKeyFromInspector(), Is.False);
        }

        [Test]
        public void TEST13_BlendShapeTrackLabel_IsHumanReadable()
        {
            string id = AddBlendKey(10f);
            Assert.That(KeyframeInspectorPanel.GetTrackDisplayName(TrackOf(id)), Does.Contain("Smile"));
        }

        [Test]
        public void TEST14_TransformTrackLabel_ShowsChannelAndLeaf()
        {
            string id = AddTransformKey(TrackKind.TransformRotation, Vector3.zero);
            string label = KeyframeInspectorPanel.GetTrackDisplayName(TrackOf(id));
            Assert.That(label, Does.Contain("Head")); Assert.That(label, Does.Contain(FaceMotion.Editor.UI.Localization.FaceMotionUiText.Get("trackRotation")));
        }

        [Test] public void TEST15_AddKey_DefaultBlendShapeValueIs100() { _tracks.AddBlendShapeTrack("Face", "Smile"); Assert.That(_panel.AddKeyFromInspector(), Is.True); Assert.That(_session.GetSelectedTrack().BlendShape.Keys.Single().Value, Is.EqualTo(100f)); }
        [Test] public void TEST16_SameTimeAdd_UpdatesWithoutDuplicate() { string id = AddBlendKey(10f); Set("_floatValue", 70f); Assert.That(_panel.AddKeyFromInspector(), Is.True); Assert.That(TrackOf(id).BlendShape.Keys.Count, Is.EqualTo(1)); Assert.That(FloatOf(id), Is.EqualTo(70f)); }
        [Test] public void TEST17_QuickKey_BehaviorUnchanged() { _tracks.AddBlendShapeTrack("Face", "Smile"); _session.SetCurrentTime(0.5f); string id = _panel.ApplyQuickKey(); Assert.That(id, Is.Not.Null); Assert.That(FloatOf(id), Is.EqualTo(QuickKeyPreferences.Value)); }

        [Test]
        public void TEST18_MultiSelection_DoesNotAutoApplyThroughInspectorUiPath()
        {
            string first = AddBlendKey(10f); _session.SetCurrentTime(0.5f); string second = _keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second }); _panel.Synchronize();
            Assert.That(_session.Selection.Count, Is.EqualTo(2)); Assert.That(FloatOf(first), Is.EqualTo(10f)); Assert.That(FloatOf(second), Is.EqualTo(20f));
        }

        [Test] public void TEST19_InspectorFocus_StillGatesShortcuts() { Assert.That(KeyframeInspectorPanel.OwnsKeyboardFocus("FaceMotion.KeyInspector.blendShape"), Is.True); Assert.That(KeyframeInspectorPanel.OwnsKeyboardFocus("other"), Is.False); }

        [Test]
        public void TEST20_SelectionChange_ReloadsInspectorFields()
        {
            string first = AddBlendKey(10f); _session.SetCurrentTime(0.5f); string second = _keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(first); _panel.Synchronize(); _session.Selection.SetSingle(second); _panel.Synchronize();
            Assert.That(Get<float>("_floatValue"), Is.EqualTo(20f));
        }

        [Test]
        public void TEST21_TimeMove_UsesSnapAndPreservesSelectedIdentity()
        {
            string id = AddBlendKey(10f);
            _session.ViewState.SnapEnabled = true;
            _session.GetSelectedAnimation().Timeline.FrameRate = 60f;
            ApplyTime(id, 0.347f);
            Assert.That(TrackOf(id).BlendShape.Keys.Single(k => k.KeyId == id).Time, Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(_session.Selection.Contains(id), Is.True);
        }

        [Test]
        public void TEST22_TimeMove_ClampsAndUsesSharedCollisionPlanner()
        {
            string first = AddBlendKey(10f);
            _session.SetCurrentTime(0.5f);
            string second = _keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear);
            _session.ViewState.SnapEnabled = true;
            _session.GetSelectedAnimation().Timeline.FrameRate = 60f;
            ApplyTime(first, 4f);
            Assert.That(TrackOf(first).BlendShape.Keys.Single(k => k.KeyId == first).Time, Is.EqualTo(1f));
            ApplyTime(first, 0.5f);
            Assert.That(TrackOf(first).BlendShape.Keys.Count, Is.EqualTo(2));
            Assert.That(TrackOf(first).BlendShape.Keys.Single(k => k.KeyId == first).Time, Is.EqualTo(0.5f + 1f / 60f).Within(1e-5f));
            Assert.That(TrackOf(second).BlendShape.Keys.Single(k => k.KeyId == second).Time, Is.EqualTo(0.5f));
            Assert.That(TrackOf(first).BlendShape.Keys.Select(k => k.Time).Distinct().Count(), Is.EqualTo(2));
            Assert.That(_session.Selection.Contains(first), Is.True);
            Assert.That(_session.Selection.Contains(second), Is.False);
            Assert.That(Get<float>("_time"), Is.EqualTo(0.5f + 1f / 60f).Within(1e-5f));
        }

        [Test]
        public void TEST23_TimeMove_UndoRedoRestoresTime()
        {
            string id = AddBlendKey(10f);
            ApplyTime(id, 0.5f);
            Undo.PerformUndo(); _session.RefreshAfterUndo();
            Assert.That(TrackOf(id).BlendShape.Keys.Single(k => k.KeyId == id).Time, Is.Zero);
            Undo.PerformRedo(); _session.RefreshAfterUndo();
            Assert.That(TrackOf(id).BlendShape.Keys.Single(k => k.KeyId == id).Time, Is.EqualTo(0.5f));
        }

        [Test]
        public void TEST24_ValueAutoApply_CoalescesOneFocusedEditSession()
        {
            string id = AddBlendKey(10f);
            FaceTrackData track = TrackOf(id);
            foreach (float value in new[] { 20f, 30f, 40f, 50f })
            {
                Set("_floatValue", value);
                Assert.That(_panel.AutoApplySelectedValue(id, track, "FaceMotion.KeyInspector.blendShape"), Is.True);
            }

            Undo.PerformUndo(); _session.RefreshAfterUndo();
            Assert.That(FloatOf(id), Is.EqualTo(10f));
            Undo.PerformRedo(); _session.RefreshAfterUndo();
            Assert.That(FloatOf(id), Is.EqualTo(50f));
        }

        [Test]
        public void TEST25_TimeMove_NoOpDoesNotCreateUndoMutation()
        {
            string id = AddBlendKey(10f);
            Undo.ClearAll();
            Assert.That(_keys.SetKeyTime(id, 0f), Is.False);
            Undo.PerformUndo(); _session.RefreshAfterUndo();
            Assert.That(FloatOf(id), Is.EqualTo(10f));
            Assert.That(_session.Selection.Contains(id), Is.True);
        }

        [Test]
        public void TEST26_TransformPositionTrackLabel_ShowsChannelAndLeaf()
        {
            string id = AddTransformKey(TrackKind.TransformPosition, Vector3.zero);
            string label = KeyframeInspectorPanel.GetTrackDisplayName(TrackOf(id));
            Assert.That(label, Does.Contain("Head"));
            Assert.That(label, Does.Contain(FaceMotion.Editor.UI.Localization.FaceMotionUiText.Get("trackPosition")));
        }

        [Test]
        public void TEST27_TransformScaleTrackLabel_ShowsChannelAndLeaf()
        {
            string id = AddTransformKey(TrackKind.TransformScale, Vector3.one);
            string label = KeyframeInspectorPanel.GetTrackDisplayName(TrackOf(id));
            Assert.That(label, Does.Contain("Head"));
            Assert.That(label, Does.Contain(FaceMotion.Editor.UI.Localization.FaceMotionUiText.Get("trackScale")));
        }

        private string AddBlendKey(float value) { _tracks.AddBlendShapeTrack("Face", "Smile"); return _keys.AddKeyAtCurrentTime(value, Vector3.zero, InterpolationType.Linear); }
        private string AddTransformKey(TrackKind kind, Vector3 value) { _tracks.AddTransformTrack(kind, "Body/Head"); return _keys.AddKeyAtCurrentTime(0f, value, InterpolationType.Linear); }
        private FaceTrackData TrackOf(string keyId) { return _session.GetSelectedAnimation().Timeline.Tracks.Single(t => (t.BlendShape != null && t.BlendShape.Keys.Any(k => k.KeyId == keyId)) || (t.Transform != null && t.Transform.Keys.Any(k => k.KeyId == keyId))); }
        private float FloatOf(string id) { return TrackOf(id).BlendShape.Keys.Single(k => k.KeyId == id).Value; }
        private Vector3 VectorOf(string id) { return TrackOf(id).Transform.Keys.Single(k => k.KeyId == id).Value; }
        private void ApplyFloat(string id, float value) { _session.Selection.SetSingle(id); _panel.Synchronize(); Set("_floatValue", value); Assert.That(_panel.AutoApplySelectedValue(id, TrackOf(id)), Is.True); }
        private void ApplyVector(string id, Vector3 value) { _session.Selection.SetSingle(id); _panel.Synchronize(); Set("_vectorValue", value); Assert.That(_panel.AutoApplySelectedValue(id, TrackOf(id)), Is.True); }
        private void ApplyTime(string id, float value) { _session.Selection.SetSingle(id); _panel.Synchronize(); Set("_time", value); Assert.That(_panel.ApplySelectedTime(id), Is.True); }
        private void Set(string field, object value) { typeof(KeyframeInspectorPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_panel, value); }
        private T Get<T>(string field) { return (T)typeof(KeyframeInspectorPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_panel); }
    }
}
