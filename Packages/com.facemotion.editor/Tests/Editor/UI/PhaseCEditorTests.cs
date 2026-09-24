using System;
using System.Collections.Generic;
using System.Linq;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Editor.VRChat;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    // Phase C: Editor window, session, controllers, timeline
    public sealed class PhaseCEditorTests
    {
        private FaceMotionEditorSession _session;
        private ProjectController _project;
        private AvatarController _avatar;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TempFaceMotionAsset _temp;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _session = new FaceMotionEditorSession();
            _project = new ProjectController(_session);
            _avatar = new AvatarController(_session);
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _temp = new TempFaceMotionAsset();
        }

        [TearDown]
        public void TearDown()
        {
            if (_temp != null)
            {
                _temp.Dispose();
                _temp = null;
            }

            Undo.ClearAll();
        }

        // ----- 1. Window / smoke ---------------------------------------------------------

        [Test]
        public void EditorWindow_OpensWindow_NotNull()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.That(typeof(FaceMotionWindow).IsSubclassOf(typeof(EditorWindow)), Is.True);
                return;
            }

            var window = EditorWindow.GetWindow<FaceMotionWindow>();
            Assert.That(window, Is.Not.Null);
            window.Close();
        }

        // ----- 2. Session ----------------------------------------------------------------

        [Test]
        public void Session_NoProject_ByDefault()
        {
            Assert.That(_session.ActiveProject, Is.Null);
            Assert.That(_session.ActiveProjectAssetPath, Is.Null);
        }

        [Test]
        public void Session_SetActiveProject_ExposesProjectAndPath()
        {
            var project = CreateProject(_temp, "SessionSetActive");
            _session.SetActiveProject(project, "Assets/x.asset");
            Assert.That(_session.ActiveProject, Is.SameAs(project));
            Assert.That(_session.ActiveProjectAssetPath, Is.EqualTo("Assets/x.asset"));
        }

        [Test]
        public void Session_SetActiveProject_ResetsSelection()
        {
            var project = CreateProject(_temp, "SessionReset");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            Assert.That(_session.SelectedAnimationId, Is.Not.Null);
            _session.SetActiveProject(CreateProject(_temp, "SessionReset2"), "b.asset");
            Assert.That(_session.SelectedAnimationId, Is.Null);
            Assert.That(_session.SelectedTrackId, Is.Null);
            Assert.That(_session.Selection.Count, Is.Zero);
        }

        [Test]
        public void Session_ClearProject_ResetsProjectSelectionButRetainsAvatar()
        {
            using (var fixture = AvatarFixture.Create())
            {
                var project = CreateProject(_temp, "SessionClear");
                _session.SetActiveProject(project, "a.asset");
                SetAvatar(fixture);
                Assert.That(_session.ActiveAvatarIndex, Is.Not.Null);
                _session.ClearProject();
                Assert.That(_session.ActiveProject, Is.Null);
                Assert.That(_session.ActiveAvatarIndex, Is.Not.Null);
                Assert.That(_session.Selection.Count, Is.Zero);
                Assert.That(_session.Diagnostics, Does.Not.Contain(null));
            }
        }

        [Test]
        public void Session_SetAvatar_MarksIndexNotDirty()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetAvatar(fixture);
                Assert.That(_session.ActiveAvatarIndex, Is.Not.Null);
                Assert.That(_session.AvatarIndexDirty, Is.False);
                Assert.That(_session.Candidates, Is.Not.Null);
                Assert.That(_session.Candidates.BlendShapeCount, Is.GreaterThan(0));
                Assert.That(_session.Candidates.TransformCount, Is.GreaterThan(0));
            }
        }

        [Test]
        public void Session_AvatarHierarchyChange_SetsDirtyFlag()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetAvatar(fixture);
                fixture.AddFork("HierarchyChange");
                _avatar.MarkAvatarDirtyFromHierarchy();
                Assert.That(_session.AvatarIndexDirty, Is.True);
            }
        }

        [Test]
        public void Session_Selection_PrunedWhenAnimationDeleted()
        {
            var project = CreateProject(_temp, "SessionPrune");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            var animation = _session.GetSelectedAnimation();
            var track = FaceTrackData.CreateBlendShape("Body/Face", "Mouth_Smile");
            animation.Timeline.AddTrack(track);
            _session.SelectedTrackId = track.TrackId;
            _session.Selection.SetSingle("dead-key");

            _animations.Delete();
            _session.ValidateSelections();
            Assert.That(_session.SelectedAnimationId, Is.Null);
            Assert.That(_session.Selection.Count, Is.Zero);
        }

        [Test]
        public void AnimationListSnapshot_RemainsReadableWhenTheSourceListShrinks()
        {
            var source = new List<FaceMotionAnimationData> { null, null };

            IReadOnlyList<FaceMotionAnimationData> snapshot = AnimationListPanel.Snapshot(source);
            source.RemoveAt(1);

            Assert.That(snapshot.Count, Is.EqualTo(2));
            Assert.That(snapshot[1], Is.Null);
        }

        [Test]
        public void Session_TrackBindings_EmptyWithoutAvatar()
        {
            var project = CreateProject(_temp, "SessionNoAvatar");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            Assert.That(_session.TrackBindings, Is.Not.Null);
            Assert.That(_session.TrackBindings.Count, Is.Zero);
        }

        // ----- 3. Animation controller ----------------------------------------------------

        [Test]
        public void AnimationController_Add_CreatesAndSelects()
        {
            var project = CreateProject(_temp, "AnimAdd");
            _session.SetActiveProject(project, "a.asset");
            string id = _animations.Add();
            Assert.That(id, Is.Not.Null);
            Assert.That(_session.SelectedAnimationId, Is.EqualTo(id));
            Assert.That(project.Animations.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnimationController_Duplicate_AddsSecondAnimation()
        {
            var project = CreateProject(_temp, "AnimDup");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            var first = _session.GetSelectedAnimation();
            _animations.Duplicate();
            Assert.That(project.Animations.Count, Is.EqualTo(2));
            Assert.That(project.Animations[1].AnimationId, Is.Not.EqualTo(first.AnimationId));
        }

        [Test]
        public void AnimationController_Delete_RemovesSelected()
        {
            var project = CreateProject(_temp, "AnimDelete");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _animations.Add();
            int count = project.Animations.Count;
            _animations.Delete();
            Assert.That(project.Animations.Count, Is.EqualTo(count - 1));
        }

        [Test]
        public void AnimationController_Rename_ChangesDisplayName()
        {
            var project = CreateProject(_temp, "AnimRename");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            Assert.That(_animations.Rename("Renamed"), Is.True);
            Assert.That(_session.GetSelectedAnimation().DisplayName, Is.EqualTo("Renamed"));
        }

        [Test]
        public void AnimationController_SetDuration_RejectsNonPositive()
        {
            var project = CreateProject(_temp, "AnimDuration");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            float initial = _session.GetSelectedAnimation().Timeline.Duration;
            Assert.That(_animations.SetDuration(0f), Is.False);
            Assert.That(_session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(initial));
            Assert.That(_animations.SetDuration(4.5f), Is.True);
            Assert.That(_session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(4.5f));
        }

        [Test]
        public void AnimationController_SetLoop_TogglesLoop()
        {
            var project = CreateProject(_temp, "AnimLoop");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            Assert.That(_animations.SetLoop(true), Is.True);
            Assert.That(_session.GetSelectedAnimation().Timeline.Loop, Is.True);
        }

        // ----- 4. Track controller --------------------------------------------------------

        [Test]
        public void TrackController_AddBlendShape_Succeeds()
        {
            var project = CreateProject(_temp, "TrackBlend");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            var animation = _session.GetSelectedAnimation();
            Assert.That(_tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile"), Is.True);
            Assert.That(animation.Timeline.Tracks.Count, Is.EqualTo(1));
            Assert.That(_session.SelectedTrackId, Is.Not.Null);
        }

        [Test]
        public void TrackController_AddBlendShape_DuplicateRejected()
        {
            var project = CreateProject(_temp, "TrackBlendDup");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            var animation = _session.GetSelectedAnimation();
            Assert.That(_tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile"), Is.True);
            Assert.That(_tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile"), Is.False);
            Assert.That(animation.Timeline.Tracks.Count, Is.EqualTo(1));
        }

        [Test]
        public void TrackController_AddBlendShapeCandidate_UsesCurrentAvatarWeightAsInitialKey()
        {
            using (var fixture = AvatarFixture.Create())
            {
                var project = CreateProject(_temp, "TrackBaseline");
                _session.SetActiveProject(project, "a.asset");
                _animations.Add();
                fixture.FaceRenderer.SetBlendShapeWeight(0, 20.5f);
                _avatar.SetDescriptor(fixture.Descriptor);
                var candidate = _session.Candidates.BlendShapes.First(c => c.RendererPath == AvatarFixture.FaceRendererPath && c.BlendShapeName == "Mouth_Smile");

                Assert.That(_tracks.AddBlendShapeTrack(candidate), Is.True);

                var track = _session.GetSelectedTrack();
                Assert.That(track.BlendShape.Keys, Has.Count.EqualTo(1));
                Assert.That(track.BlendShape.Keys[0].Time, Is.EqualTo(0f));
                Assert.That(track.BlendShape.Keys[0].Value, Is.EqualTo(20.5f));
                Assert.That(fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(20.5f));
            }
        }

        [Test]
        public void TrackController_AddBlendShapeCandidate_DoesNotAddBaselineToExistingTrack()
        {
            using (var fixture = AvatarFixture.Create())
            {
                var project = CreateProject(_temp, "TrackBaselineExisting");
                _session.SetActiveProject(project, "a.asset");
                _animations.Add();
                _avatar.SetDescriptor(fixture.Descriptor);
                var candidate = _session.Candidates.BlendShapes.First(c => c.RendererPath == AvatarFixture.FaceRendererPath && c.BlendShapeName == "Mouth_Smile");
                Assert.That(_tracks.AddBlendShapeTrack(candidate), Is.True);
                var track = _session.GetSelectedTrack();
                Assert.That(track.BlendShape.Keys, Has.Count.EqualTo(1));
                fixture.FaceRenderer.SetBlendShapeWeight(0, 80f);

                Assert.That(_tracks.AddBlendShapeTrack(candidate), Is.False);
                Assert.That(track.BlendShape.Keys, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void TrackController_AddTransform_Succeeds()
        {
            var project = CreateProject(_temp, "TrackTransform");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            var animation = _session.GetSelectedAnimation();
            Assert.That(_tracks.AddTransformTrack(TrackKind.TransformPosition, "Armature/Hips/Spine/Head"), Is.True);
            Assert.That(animation.Timeline.Tracks.Count, Is.EqualTo(1));
            Assert.That(animation.Timeline.Tracks[0].Kind, Is.EqualTo(TrackKind.TransformPosition));
        }

        [Test]
        public void TrackController_AddTransform_DuplicateRejected()
        {
            var project = CreateProject(_temp, "TrackTransformDup");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            Assert.That(_tracks.AddTransformTrack(TrackKind.TransformScale, "Head"), Is.True);
            Assert.That(_tracks.AddTransformTrack(TrackKind.TransformScale, "Head"), Is.False);
            Assert.That(_session.GetSelectedAnimation().Timeline.Tracks.Count, Is.EqualTo(1));
        }

        [Test]
        public void TrackController_RemoveTrack_Succeeds()
        {
            var project = CreateProject(_temp, "TrackRemove");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            string trackId = _session.SelectedTrackId;
            Assert.That(_tracks.RemoveTrack(trackId), Is.True);
            Assert.That(_session.GetSelectedAnimation().Timeline.Tracks.Count, Is.Zero);
        }

        [Test]
        public void TrackController_Select_SetsTrackId()
        {
            var project = CreateProject(_temp, "TrackSelect");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            _tracks.ClearSelection();
            Assert.That(_session.SelectedTrackId, Is.Null);
        }

        // ----- 5. Keyframe controller -----------------------------------------------------

        [Test]
        public void KeyframeController_AddKeyAt_CreatesKey()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            string keyId = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(keyId, Is.Not.Null);
            Assert.That(KeyCount(trackId), Is.EqualTo(1));
            Assert.That(_session.Selection.Count, Is.EqualTo(1));
        }

        [Test]
        public void KeyframeController_AddKeyAt_SameTimeUpdatesNotDuplicates()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.AddKeyAt(0.1f, 0.9f, Vector3.zero, InterpolationType.Linear);
            Assert.That(KeyCount(trackId), Is.EqualTo(1));
            Assert.That(GetSelectedBlendTrack().BlendShape.Keys[0].Value, Is.EqualTo(0.9f).Within(1e-4f));
        }

        [Test]
        public void KeyframeController_DeleteSelectedKeys_Removes()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(_keys.DeleteSelectedKeys(), Is.True);
            Assert.That(KeyCount(trackId), Is.Zero);
            Assert.That(_session.Selection.Count, Is.Zero);
        }

        [Test]
        public void KeyframeController_MoveSelectedKeysBy_AppliesDelta()
        {
            SetupBlendShapeTrack(out _, out _);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            float before = GetSelectedBlendTrack().BlendShape.Keys[0].Time;
            Assert.That(_keys.MoveSelectedKeysBy(0.1f, false), Is.True);
            float after = GetSelectedBlendTrack().BlendShape.Keys[0].Time;
            Assert.That(after, Is.EqualTo(before + 0.1f).Within(1e-3f));
        }

        [Test]
        public void KeyframeController_MoveKeys_SnapsToFrameGrid()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.MoveSelectedKeysBy(0.033333f, true);
            float t = GetSelectedBlendTrack().BlendShape.Keys[0].Time;
            float frame = Mathf.Round(t * 60f);
            Assert.That(Mathf.Abs(t - frame / 60f), Is.LessThan(1e-3f));
            Assert.That(KeyCount(trackId), Is.EqualTo(1));
        }

        [Test]
        public void KeyframeController_Selection_SingleSelect()
        {
            SetupBlendShapeTrack(out _, out _);
            string keyId = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.SelectAllKeys();
            _session.Selection.SetSingle(keyId);
            Assert.That(_session.Selection.Count, Is.EqualTo(1));
            Assert.That(_session.Selection.KeyIds[0], Is.EqualTo(keyId));
        }

        [Test]
        public void KeyframeController_Selection_ToggleSelect()
        {
            SetupBlendShapeTrack(out _, out _);
            string keyId = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);
            _session.Selection.Toggle(keyId);
            Assert.That(_session.Selection.Count, Is.Zero);
            _session.Selection.Select(keyId);
            Assert.That(_session.Selection.Count, Is.EqualTo(1));
        }

        [Test]
        public void KeyframeController_Selection_SelectAll()
        {
            SetupBlendShapeTrack(out _, out _);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.AddKeyAt(0.2f, 0.6f, Vector3.zero, InterpolationType.Linear);
            _keys.SelectAllKeys();
            Assert.That(_session.Selection.Count, Is.EqualTo(2));
        }

        [Test]
        public void KeyframeController_PasteAt_PastesNewKeys()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.CopySelection();
            var result = _keys.PasteAt(0.5f);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(KeyCount(trackId), Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero);
        }

        [Test]
        public void KeyframeController_PasteAt_SkipsOccupiedTimes()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.CopySelection();
            Assert.That(_keys.PasteAt(0.5f).Succeeded, Is.True);
            var second = _keys.PasteAt(0.5f);
            Assert.That(second.Skipped, Is.EqualTo(1));
            Assert.That(KeyCount(trackId), Is.EqualTo(2));
        }

        [Test]
        public void KeyframeController_PasteAt_MissingTrackRejected()
        {
            SetupBlendShapeTrack(out _, out string trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            _keys.CopySelection();
            _tracks.RemoveTrack(trackId);
            var result = _keys.PasteAt(0.5f);
            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void KeyframeController_CopySelection_NoopWithoutSelection()
        {
            SetupBlendShapeTrack(out _, out _);
            _session.Selection.Clear();
            _keys.CopySelection();
            Assert.That(_session.Clipboard.Count, Is.Zero);
        }

        [Test]
        public void KeyframeController_SetKeyValue_BlendShapeUpdates()
        {
            SetupBlendShapeTrack(out _, out _);
            string keyId = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(_keys.SetKeyValue(keyId, 0.8f, Vector3.zero), Is.True);
            Assert.That(GetSelectedBlendTrack().BlendShape.Keys[0].Value, Is.EqualTo(0.8f).Within(1e-4f));
        }

        [Test]
        public void KeyframeController_SetKeyInterpolation_Applies()
        {
            SetupBlendShapeTrack(out _, out _);
            string keyId = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(_keys.SetKeyInterpolation(keyId, InterpolationType.Smooth), Is.True);
            Assert.That(GetSelectedBlendTrack().BlendShape.Keys[0].Interpolation, Is.EqualTo(InterpolationType.Smooth));
        }

        // ----- 6. Planner / drag / undo ----------------------------------------------------

        [Test]
        public void KeyMovePlanner_AppliesDeltaGroupwise()
        {
            float[] result = KeyMovePlanner.PlanTrack(new[] { 0.1f, 0.2f }, Array.Empty<float>(), 0.1f, 1f, 60f, false);
            Assert.That(result[0], Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(result[1], Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void KeyMovePlanner_ClampsToDuration()
        {
            float[] result = KeyMovePlanner.PlanTrack(new[] { 0.9f }, Array.Empty<float>(), 1.0f, 1f, 60f, false);
            Assert.That(result[0], Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void KeyMovePlanner_ResolvesCollision_NoOverlap()
        {
            float[] result = KeyMovePlanner.PlanTrack(new[] { 0.1f }, new[] { 0.15f }, 0.05f, 1f, 60f, false);
            Assert.That(result[0], Is.GreaterThan(0.15f + 1e-4f));
            Assert.That(result[0], Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void KeyMovePlanner_NoSnapKeepsRawDelta()
        {
            float[] result = KeyMovePlanner.PlanTrack(new[] { 0.1f }, Array.Empty<float>(), 0.013f, 1f, 60f, false);
            Assert.That(result[0], Is.EqualTo(0.113f).Within(1e-3f));
        }

        [Test]
        public void DragUndoScope_DragIsOneUndoEntry()
        {
            SetupBlendShapeTrackWithKey(out _, out _);
            _session.Selection.SetSingle(GetSelectedBlendTrack().BlendShape.Keys[0].KeyId);

            _keys.BeginKeyDrag();
            _keys.UpdateKeyDrag(12f, 120f);
            _keys.EndKeyDrag();
            float moved = GetSelectedBlendTrack().BlendShape.Keys[0].Time;

            Undo.PerformUndo();
            Assert.That(GetSelectedBlendTrack().BlendShape.Keys[0].Time, Is.EqualTo(0.1f).Within(1e-3f));
            Assert.That(moved, Is.EqualTo(0.2f).Within(1e-3f));
        }

        [Test]
        public void DragUndoScope_CancelRestoresOriginal()
        {
            SetupBlendShapeTrackWithKey(out _, out _);
            _session.Selection.SetSingle(GetSelectedBlendTrack().BlendShape.Keys[0].KeyId);
            _keys.BeginKeyDrag();
            _keys.UpdateKeyDrag(-6f, 120f);
            _keys.CancelKeyDrag();
            Assert.That(GetSelectedBlendTrack().BlendShape.Keys[0].Time, Is.EqualTo(0.1f).Within(1e-3f));
        }

        [Test]
        public void RemoveTrack_Undo_RestoresTrack()
        {
            var project = CreateProject(_temp, "TrackUndo");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            string trackId = _session.SelectedTrackId;
            _tracks.RemoveTrack(trackId);
            Assert.That(_session.GetSelectedAnimation().Timeline.Tracks.Count, Is.Zero);

            Undo.PerformUndo();
            Assert.That(_session.GetSelectedAnimation().Timeline.Tracks.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddKeys_Undo_RestoresEmptyTimeline()
        {
            var project = CreateProject(_temp, "KeyUndo");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);

            Undo.PerformUndo();
            Assert.That(KeyCount(_session.SelectedTrackId), Is.Zero);
        }

        // ----- 7. Geometry ----------------------------------------------------------------

        [Test]
        public void TimelineGeometry_TimePixel_RoundTrip()
        {
            float scroll = 0.5f;
            float pps = 120f;
            float plotLeft = 100f;
            float x = TimelineGeometry.TimeToPixel(2f, scroll, pps, plotLeft);
            float t = TimelineGeometry.PixelToTime(x, scroll, pps, plotLeft);
            Assert.That(t, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void TimelineGeometry_FitZoom_Clamped()
        {
            Assert.That(TimelineGeometry.FitZoom(10f, 1200f), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(TimelineGeometry.FitZoom(600f, 120f), Is.GreaterThanOrEqualTo(TimelineGeometry.MinZoom));
            Assert.That(TimelineGeometry.FitZoom(0.001f, 120f), Is.GreaterThan(0f));
        }

        [Test]
        public void TimelineGeometry_ClampScroll_Clamps()
        {
            float duration = 10f;
            float pps = 120f;
            Assert.That(TimelineGeometry.ClampScrollTime(99f, duration, 600f, pps), Is.LessThanOrEqualTo(duration));
            Assert.That(TimelineGeometry.ClampScrollTime(-5f, duration, 600f, pps), Is.GreaterThanOrEqualTo(0f));
            Assert.That(TimelineGeometry.ClampScrollTime(2f, duration, 600f, pps), Is.EqualTo(2f).Within(1e-3f));
        }

        [Test]
        public void TimelineGeometry_RulerSteps_Resolve()
        {
            float pps = 120f;
            float major = TimelineGeometry.GetMajorStep(pps);
            float minor = TimelineGeometry.GetMinorStep(pps);
            Assert.That(major, Is.GreaterThan(0f));
            Assert.That(minor, Is.GreaterThan(0f));
            Assert.That(minor, Is.LessThanOrEqualTo(major));
            Assert.That(major * pps, Is.LessThanOrEqualTo(90f + 1e-3f));
        }

        [Test]
        public void TimelineGeometry_ZoomAround_AnchorStable()
        {
            float zoom0 = 1f;
            float zoom1 = 2f;
            float plotLeft = 100f;
            float scroll0 = 0f;
            float anchorX = 340f;
            float anchorTime = TimelineGeometry.PixelToTime(anchorX, scroll0, TimelineGeometry.PixelsPerSecond(zoom0), plotLeft);
            float scroll1 = TimelineGeometry.ComputeZoomedScroll(zoom1, anchorX, anchorTime, plotLeft);
            float after = TimelineGeometry.PixelToTime(anchorX, scroll1, TimelineGeometry.PixelsPerSecond(zoom1), plotLeft);
            Assert.That(after, Is.EqualTo(anchorTime).Within(1e-4f));
        }

        [Test]
        public void TimelineHitTest_FindsKeyAtPixel()
        {
            var project = CreateProject(_temp, "HitTest");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            var animation = _session.GetSelectedAnimation();

            float pps = 120f;
            var layout = TimelineLayoutBuilder.Build(animation, null, 180f, 0f, 0f, 1f, animation.Timeline.Duration);
            float x = TimelineGeometry.TimeToPixel(0.1f, 0f, pps, layout.PlotLeft);
            Assert.That(
                TimelineHitTest.TryFindKeyAt(layout, layout.PlotLeft, x, layout.Rows[0].Y + 11f, out _, out string keyId, out _),
                Is.True);
            Assert.That(keyId, Is.Not.Null);
        }

        // ----- 8. Bindings ----------------------------------------------------------------

        [Test]
        public void TrackBindings_Resolved_WhenAvatarMatches()
        {
            using (var fixture = AvatarFixture.Create())
            {
                var project = CreateProject(_temp, "BindingResolve");
                _session.SetActiveProject(project, "a.asset");
                SetAvatar(fixture);
                _animations.Add();
                _tracks.AddBlendShapeTrack(AvatarFixture.FaceRendererPath, "Mouth_Smile");
                _session.RefreshAll();

                Assert.That(_session.TrackBindings.Count, Is.EqualTo(1));
                foreach (var binding in _session.TrackBindings)
                {
                    Assert.That(binding.Status, Is.EqualTo(MappingResolutionStatus.Resolved));
                }
            }
        }

        [Test]
        public void TrackBindings_Missing_WhenAvatarMissing()
        {
            var project = CreateProject(_temp, "BindingMissing");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "DoesNotExistForReal");
            _session.RefreshAll();
            Assert.That(_session.TrackBindings.Count, Is.EqualTo(0));
        }

        [Test]
        public void CandidateSnapshot_FiltersBlendShapes()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetAvatar(fixture);
                var matches = _session.Candidates.FilterBlendShapes("smile");
                Assert.That(matches.Count, Is.GreaterThan(0));
                foreach (var candidate in matches)
                {
                    Assert.That(
                        candidate.BlendShapeName.IndexOf("smile", StringComparison.OrdinalIgnoreCase) >= 0
                        || candidate.RendererPath.IndexOf("smile", StringComparison.OrdinalIgnoreCase) >= 0,
                        Is.True);
                }
            }
        }

        // ----- Helpers ---------------------------------------------------------------------

        private void SetupBlendShapeTrack(out FaceMotionProject project, out string trackId)
        {
            project = CreateProject(_temp, "BlendTrack");
            _session.SetActiveProject(project, "a.asset");
            _animations.Add();
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            trackId = _session.SelectedTrackId;
            Assert.That(trackId, Is.Not.Null);
        }

        private void SetupBlendShapeTrackWithKey(out FaceMotionProject project, out string trackId)
        {
            SetupBlendShapeTrack(out project, out trackId);
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
        }

        private int KeyCount(string trackId)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null || !animation.Timeline.TryGetTrack(trackId, out var track))
            {
                return 0;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                return track.BlendShape.Keys.Count;
            }

            if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                return track.Transform.Keys.Count;
            }

            return 0;
        }

        private FaceTrackData GetSelectedBlendTrack()
        {
            return _session.GetSelectedTrack();
        }

        private void SetAvatar(AvatarFixture fixture)
        {
            var validation = VRCAvatarDescriptorAdapter.Validate(fixture.Descriptor);
            var cache = new UnityAvatarObjectCache();
            var report = cache.Rebuild(fixture.Root);
            var candidates = AvatarCandidateSnapshot.Build(report.Index);
            _session.SetAvatar(fixture.Descriptor, fixture.Root, validation, report, cache, candidates);
        }

        private static FaceMotionProject CreateProject(TempFaceMotionAsset temp, string fileName)
        {
            var project = FaceMotionProject.CreateNew();
            string assetPath = temp.AssetPath(fileName);
            EditorUtility.SetDirty(project);
            AssetDatabase.CreateAsset(project, assetPath);
            AssetDatabase.SaveAssets();
            return project;
        }
    }
}
