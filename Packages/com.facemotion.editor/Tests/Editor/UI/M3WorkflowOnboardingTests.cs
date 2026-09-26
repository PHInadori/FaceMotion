using System.Linq;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Guidance;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class M3WorkflowOnboardingTests
    {
        private FaceMotionEditorSession _session;
        private ProjectController _projects;
        private AvatarController _avatar;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TimelineInputHandler _timelineInput;
        private TempFaceMotionAsset _temp;
        private int _previousHotControl;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _previousHotControl = GUIUtility.hotControl;
            GUIUtility.hotControl = 0;
            _session = new FaceMotionEditorSession();
            _projects = new ProjectController(_session);
            _avatar = new AvatarController(_session);
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _timelineInput = new TimelineInputHandler(_session, _keys, _tracks);
            _temp = new TempFaceMotionAsset();
        }

        [TearDown]
        public void TearDown()
        {
            GUIUtility.hotControl = _previousHotControl;
            _temp?.Dispose();
            Undo.ClearAll();
        }

        [Test] public void TEST1_NewProject_GetsExactlyOneInitialAnimation() { FaceMotionProject project = CreateProject(); Assert.That(project.Animations.Count, Is.EqualTo(1)); }
        [Test] public void TEST2_InitialAnimation_UsesCanonicalDefaults() { FaceMotionAnimationData animation = CreateProject().Animations[0]; FaceTimelineData defaults = FaceTimelineData.CreateDefault(); Assert.That(animation.AnimationId, Is.Not.Empty); Assert.That(animation.DisplayName, Is.EqualTo("Animation 1")); Assert.That(animation.Timeline.Duration, Is.EqualTo(defaults.Duration)); Assert.That(animation.Timeline.FrameRate, Is.EqualTo(defaults.FrameRate)); }
        [Test] public void TEST3_InitialAnimation_BecomesSelected() { FaceMotionProject project = CreateProject(); Assert.That(_session.SelectedAnimationId, Is.EqualTo(project.Animations[0].AnimationId)); }
        [Test] public void TEST4_SecondAnimation_UsesCanonicalNonDuplicateName() { CreateProject(); string id = _animations.Add(); Assert.That(_session.GetSelectedAnimation().AnimationId, Is.EqualTo(id)); Assert.That(_session.GetSelectedAnimation().DisplayName, Is.EqualTo("Animation 2")); }

        [Test]
        public void TEST5_ExistingLoadedZeroAnimationProject_RemainsZero()
        {
            FaceMotionProject project = CreateExistingEmptyProject();
            _projects.LoadProject(project);
            Assert.That(project.Animations.Count, Is.Zero);
        }

        [Test]
        public void TEST6_DeleteLastAnimation_DoesNotRecreateIt()
        {
            FaceMotionProject project = CreateProject();
            Assert.That(_animations.Delete(), Is.True);
            Assert.That(project.Animations.Count, Is.Zero);
        }

        [Test]
        public void TEST7_SessionRefresh_DoesNotRecreateDeletedAnimation()
        {
            FaceMotionProject project = CreateProject();
            _animations.Delete();
            _session.RefreshAll(); _session.ValidateSelections();
            Assert.That(project.Animations.Count, Is.Zero);
        }

        [Test] public void TEST8_ExistingZeroAnimation_GuidanceRequestsCreation() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, false, false, false, false, false), FaceMotionUxState.CreateAnimation); }
        [Test] public void TEST9_EmptyInitialAnimation_IsNotVrchatReady() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, false, false, true, true), FaceMotionUxState.AddTrack); }
        [Test] public void TEST10_AnimationWithoutTrack_GuidanceRequestsTrack() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, false, false, false, false), FaceMotionUxState.AddTrack); }
        [Test] public void TEST11_TrackWithoutKey_GuidanceRequestsKeyAuthoring() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, false, false, false), FaceMotionUxState.AddKey); }
        [Test] public void TEST12_AuthoredAnimation_GuidanceAdvancesToPreview() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, true, false, false), FaceMotionUxState.StartPreview); }
        [Test] public void TEST13_VrchatGuidance_DoesNotPrecedeAuthoredContent() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, false, true, true), FaceMotionUxState.AddKey); }
        [Test] public void TEST14_VrchatSelection_ProducesFinalUpdateGuidance() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, true, true, true), FaceMotionUxState.PreviewAndIntegrate); }
        [Test] public void TEST15_GuidanceChangesAfterTrackAndKey() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, false, false, false, false), FaceMotionUxState.AddTrack); AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, false, false, false), FaceMotionUxState.AddKey); AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, true, false, false), FaceMotionUxState.StartPreview); }
        [Test] public void TEST16_GuidanceChangesAfterDeletingContent() { AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, true, false, false), FaceMotionUxState.StartPreview); AssertStage(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, false, false, false), FaceMotionUxState.AddKey); }

        [Test]
        public void TEST17_GuidanceEvaluation_IsReadOnly()
        {
            FaceMotionProject project = CreateProject();
            int version = _session.Version;
            EditorUtility.ClearDirty(project);
            FaceMotionWorkflowHintService.Evaluate(_session, false);
            Assert.That(project.Animations.Count, Is.EqualTo(1));
            Assert.That(_session.Version, Is.EqualTo(version));
            Assert.That(EditorUtility.IsDirty(project), Is.False);
        }

        [Test]
        public void TEST18_ExistingZeroAnimation_SurvivesSaveReload()
        {
            FaceMotionProject project = CreateExistingEmptyProject();
            AssetDatabase.SaveAssets();
            _projects.LoadProject(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(_temp.AssetPath("M3ExistingEmpty")));
            Assert.That(project.Animations.Count, Is.Zero);
        }

        [Test]
        public void TEST19_UndoRedoDeleteLastAnimation_PreservesIntent()
        {
            FaceMotionProject project = CreateProject();
            _animations.Delete();
            Undo.PerformUndo(); _session.RefreshAfterUndo();
            Assert.That(project.Animations.Count, Is.EqualTo(1));
            Undo.PerformRedo(); _session.RefreshAfterUndo();
            Assert.That(project.Animations.Count, Is.Zero);
        }

        [Test]
        public void TEST20_M2AddAndQuickKey_WorkflowsRemainAvailable()
        {
            CreateProject();
            _tracks.AddBlendShapeTrack("Face", "Smile");
            _session.SetCurrentTime(0.5f);
            Assert.That(_keys.AddKeyAtCurrentTime(100f, Vector3.zero, InterpolationType.Linear), Is.Not.Null);
            Assert.That(_keys.ApplyQuickKey(50f), Is.Not.Null);
        }

        [Test]
        public void TEST21_BlendShapeCandidateBaseline_RequiresUserKeyAndReturnsAfterDelete()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateProject();
                fixture.FaceRenderer.SetBlendShapeWeight(0, 20f);
                _avatar.SetDescriptor(fixture.Descriptor);
                var candidate = _session.Candidates.BlendShapes.First(c => c.BlendShapeName == "Mouth_Smile");
                Assert.That(_tracks.AddBlendShapeTrack(candidate), Is.True);
                Assert.That(_session.GetSelectedTrack().BlendShape.Keys.Single().Origin.IsBaseline, Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.AddKey);

                _session.SetCurrentTime(0.5f);
                Assert.That(_keys.AddKeyAtCurrentTime(50f, Vector3.zero, InterpolationType.Linear), Is.Not.Null);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
                Assert.That(_keys.DeleteSelectedKeys(), Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.AddKey);
            }
        }

        [Test]
        public void TEST22_BlendShapeBaselineValueEdit_PromotesItToAuthoredContent()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateProject();
                _avatar.SetDescriptor(fixture.Descriptor);
                var candidate = _session.Candidates.BlendShapes.First(c => c.BlendShapeName == "Mouth_Smile");
                _tracks.AddBlendShapeTrack(candidate);
                string keyId = _session.GetSelectedTrack().BlendShape.Keys.Single().KeyId;
                Assert.That(_keys.SetKeyValue(keyId, 40f, Vector3.zero), Is.True);
                Assert.That(_session.GetSelectedTrack().BlendShape.Keys.Single().Origin.Kind, Is.EqualTo(OriginKind.Manual));
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
            }
        }

        [Test]
        public void TEST23_TransformCandidate_RequiresUserKeyAndReturnsAfterDelete()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateProject();
                _avatar.SetDescriptor(fixture.Descriptor);
                var candidate = _session.Candidates.FilterTransforms(string.Empty).First();
                Assert.That(_tracks.AddTransformTrack(TrackKind.TransformPosition, candidate), Is.True);
                Assert.That(_session.GetSelectedTrack().Transform.Keys, Is.Empty);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.AddKey);

                _session.SetCurrentTime(0.5f);
                Assert.That(_keys.AddKeyAtCurrentTime(0f, new Vector3(1f, 0f, 0f), InterpolationType.Linear), Is.Not.Null);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
                Assert.That(_keys.DeleteSelectedKeys(), Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.AddKey);
            }
        }

        [Test]
        public void TEST24_ActivePreviewCloneWithoutPlay_DoesNotCompleteReview()
        {
            using (var fixture = AvatarFixture.Create())
            using (var preview = new PreviewSession())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                preview.EnsureAvatar(fixture.Root);
                Assert.That(preview.IsActive, Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, preview.IsActive), FaceMotionUxState.StartPreview);
            }
        }

        [Test]
        public void TEST25_PlayReviewsCurrentAnimation_AndStopKeepsReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                var playback = StartPreviewReview();
                Assert.That(playback.IsPlaying, Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.SelectForVrchat);
                playback.Stop();
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.SelectForVrchat);
            }
        }

        [Test]
        public void TEST26_VrchatSelectionCannotBypassPreview_ThenBecomesReadyAfterPlay()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                _session.SetBatchSelected(_session.SelectedAnimationId, true);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
                StartPreviewReview();
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.PreviewAndIntegrate);
            }
        }

        [Test]
        public void TEST27_AuthoringChangesInvalidatePreviewReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string keyId = CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.SelectForVrchat);
                Assert.That(_keys.SetKeyValue(keyId, 75f, Vector3.zero), Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
                StartPreviewReview();
                _session.SetCurrentTime(0.75f);
                Assert.That(_keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear), Is.Not.Null);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
                Assert.That(_keys.DeleteSelectedKeys(), Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
            }
        }

        [Test]
        public void TEST28_ReviewIsScopedToCurrentAnimation()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                Assert.That(_animations.Add(), Is.Not.Null);
                _tracks.AddBlendShapeTrack("Face", "Frown");
                _session.SetCurrentTime(0.5f);
                _keys.AddKeyAtCurrentTime(30f, Vector3.zero, InterpolationType.Linear);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
            }
        }

        [Test]
        public void TEST29_ReviewA_EditB_ReturnA_KeepsAReviewed()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                string a = _session.SelectedAnimationId;
                StartPreviewReview();
                string b = AddSecondAuthoredAnimation();
                Assert.That(_animations.Select(a), Is.EqualTo(a));
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.SelectForVrchat);
                Assert.That(b, Is.Not.EqualTo(a));
            }
        }

        [Test]
        public void TEST30_ReviewA_ReviewB_ReturnA_KeepsBothReviews()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                string a = _session.SelectedAnimationId;
                StartPreviewReview();
                AddSecondAuthoredAnimation();
                StartPreviewReview();
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
                _animations.Select(a);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
            }
        }

        [Test]
        public void TEST31_DeleteReviewedAnimation_ClearsItsReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                string a = _session.SelectedAnimationId;
                StartPreviewReview();
                Assert.That(_animations.Delete(), Is.True);
                Undo.PerformUndo(); _session.RefreshAfterUndo();
                _animations.Select(a);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            }
        }

        [Test]
        public void TEST32_ProjectSwitch_ClearsReviewState()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                FaceMotionProject other = FaceMotionProject.CreateNew();
                AssetDatabase.CreateAsset(other, _temp.AssetPath("M3OtherProject"));
                _session.SetActiveProject(other, AssetDatabase.GetAssetPath(other));
                _animations.Add();
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            }
        }

        [Test]
        public void TEST33_CrossAnimationUndo_ClearsAllSessionReviews()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string aKey = CreateAuthoredBlendShapeAnimation(fixture);
                string a = _session.SelectedAnimationId;
                string b = AddSecondAuthoredAnimation();
                StartPreviewReview();
                _animations.Select(a);
                StartPreviewReview();
                Assert.That(_keys.SetKeyValue(aKey, 75f, Vector3.zero), Is.True);
                _animations.Select(b);
                Undo.PerformUndo(); _session.RefreshAfterUndo();
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
                _animations.Select(a);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            }
        }

        [Test]
        public void TEST34_CrossAnimationRedo_ClearsAllSessionReviews()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string aKey = CreateAuthoredBlendShapeAnimation(fixture);
                string a = _session.SelectedAnimationId;
                string b = AddSecondAuthoredAnimation();
                StartPreviewReview();
                _animations.Select(a);
                StartPreviewReview();
                Assert.That(_keys.SetKeyValue(aKey, 75f, Vector3.zero), Is.True);
                _animations.Select(b);
                Undo.PerformUndo(); _session.RefreshAfterUndo();
                Undo.PerformRedo(); _session.RefreshAfterUndo();
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
                _animations.Select(a);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            }
        }

        [Test]
        public void TEST35_TimelineSingleKeyDrag_InvalidatesReviewOnlyWhenTimeChanges()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string keyId = CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                float before = TimeOf(keyId);
                DragTimelineKey(keyId, 40f);
                Assert.That(TimeOf(keyId), Is.Not.EqualTo(before));
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.StartPreview);
            }
        }

        [Test]
        public void TEST36_TimelineNoOpDrag_KeepsReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string keyId = CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                DragTimelineKey(keyId, 0f);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);

                _session.ViewState.SnapEnabled = true;
                _session.GetSelectedAnimation().Timeline.FrameRate = 30f;
                DragTimelineKey(keyId, 0.1f);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
            }
        }

        [Test]
        public void TEST37_TimelineSelectedGroupDrag_InvalidatesReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string first = CreateAuthoredBlendShapeAnimation(fixture);
                _session.SetCurrentTime(0.75f);
                string second = _keys.AddKeyAtCurrentTime(20f, Vector3.zero, InterpolationType.Linear);
                _session.Selection.SetSelection(new[] { first, second });
                StartPreviewReview();
                DragTimelineKey(first, 40f);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            }
        }

        [Test]
        public void TEST38_PlayheadScrub_KeepsReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string keyId = CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                RouteTimeline(Pointer(EventType.MouseDown, TimelineX(0.25f), TimelineRowY(keyId)));
                RouteTimeline(Pointer(EventType.MouseUp, TimelineX(0.25f), TimelineRowY(keyId)));
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
            }
        }

        [Test]
        public void TEST39_NudgeAndInspectorMove_InvalidateReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                string keyId = CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                Assert.That(_keys.NudgeSelectedKeys(1), Is.True);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
                StartPreviewReview();
                Assert.That(_keys.SetKeyTime(keyId, 0.75f), Is.True);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            }
        }

        [Test]
        public void TEST40_BaselineNoOpTimelineDrag_DoesNotPromoteToManual()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateProject();
                _avatar.SetDescriptor(fixture.Descriptor);
                var candidate = _session.Candidates.BlendShapes.First(c => c.BlendShapeName == "Mouth_Smile");
                Assert.That(_tracks.AddBlendShapeTrack(candidate), Is.True);
                var key = _session.GetSelectedTrack().BlendShape.Keys.Single();
                _session.ViewState.SnapEnabled = true;
                DragTimelineKey(key.KeyId, 0.1f);
                Assert.That(key.Time, Is.Zero);
                Assert.That(key.Origin.IsBaseline, Is.True);
                AssertStage(FaceMotionWorkflowHintService.Evaluate(_session, false), FaceMotionUxState.AddKey);
            }
        }

        [Test]
        public void TEST41_RejectedAuthoringCommand_DoesNotInvalidateReview()
        {
            using (var fixture = AvatarFixture.Create())
            {
                CreateAuthoredBlendShapeAnimation(fixture);
                StartPreviewReview();
                var track = _session.GetSelectedTrack();
                Assert.That(UICommandRunner.Run(_session,
                    new UpdateFloatKeyValueCommand(_session.SelectedAnimationId, track.TrackId, "missing-key", 75f)).Succeeded,
                    Is.False);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
            }
        }

        private FaceMotionProject CreateProject()
        {
            OperationResult result = _projects.CreateProjectAtPath(_temp.AssetPath("M3NewProject"));
            Assert.That(result.Succeeded, Is.True);
            return _session.ActiveProject;
        }

        private string CreateAuthoredBlendShapeAnimation(AvatarFixture fixture)
        {
            CreateProject();
            _avatar.SetDescriptor(fixture.Descriptor);
            var candidate = _session.Candidates.BlendShapes.First(c => c.BlendShapeName == "Mouth_Smile");
            _tracks.AddBlendShapeTrack(candidate);
            _session.SetCurrentTime(0.5f);
            return _keys.AddKeyAtCurrentTime(50f, Vector3.zero, InterpolationType.Linear);
        }

        private PreviewPlaybackController StartPreviewReview()
        {
            var playback = new PreviewPlaybackController(_session);
            var panel = new PreviewPanel(_session, null, null, playback);
            Assert.That(panel.StartPlayback(), Is.True);
            return playback;
        }

        private string AddSecondAuthoredAnimation()
        {
            string id = _animations.Add();
            Assert.That(id, Is.Not.Null);
            Assert.That(_tracks.AddBlendShapeTrack("Face", "Frown"), Is.True);
            _session.SetCurrentTime(0.5f);
            Assert.That(_keys.AddKeyAtCurrentTime(30f, Vector3.zero, InterpolationType.Linear), Is.Not.Null);
            return id;
        }

        private void DragTimelineKey(string keyId, float pixelDelta)
        {
            if (!_session.Selection.Contains(keyId))
            {
                _session.Selection.SetSingle(keyId);
            }
            float x = TimelineX(TimeOf(keyId));
            float y = TimelineRowY(keyId);
            RouteTimeline(Pointer(EventType.MouseDown, x, y));
            if (pixelDelta != 0f)
            {
                RouteTimeline(Pointer(EventType.MouseDrag, x + pixelDelta, y));
            }

            RouteTimeline(Pointer(EventType.MouseUp, x + pixelDelta, y));
        }

        private bool RouteTimeline(Event e)
        {
            FaceMotionAnimationData animation = _session.GetSelectedAnimation();
            TimelineLayoutSnapshot layout = TimelineLayoutBuilder.Build(
                animation,
                null,
                180f,
                TimelineGeometry.RulerHeight,
                _session.ViewState.ScrollTime,
                _session.ViewState.Zoom,
                animation.Timeline.Duration);
            Rect plot = new Rect(layout.PlotLeft, 0f, 800f, 300f);
            return TimelineView.RoutePointerEvent(e, 9517, new Rect(0f, 0f, 980f, 300f), _timelineInput, _session.ViewState, plot, layout, false);
        }

        private float TimelineX(float time)
        {
            FaceMotionAnimationData animation = _session.GetSelectedAnimation();
            return TimelineGeometry.TimeToPixel(time, _session.ViewState.ScrollTime, _session.ViewState.PixelsPerSecond, 180f);
        }

        private float TimelineRowY(string keyId)
        {
            FaceMotionAnimationData animation = _session.GetSelectedAnimation();
            TimelineLayoutSnapshot layout = TimelineLayoutBuilder.Build(animation, null, 180f, TimelineGeometry.RulerHeight, _session.ViewState.ScrollTime, _session.ViewState.Zoom, animation.Timeline.Duration);
            FaceTrackData track = animation.Timeline.Tracks.Single(t => (t.BlendShape != null && t.BlendShape.Keys.Any(k => k.KeyId == keyId)) || (t.Transform != null && t.Transform.Keys.Any(k => k.KeyId == keyId)));
            return layout.Rows.Single(row => row.Track.TrackId == track.TrackId).Y + TimelineGeometry.RowHeight * 0.5f;
        }

        private float TimeOf(string keyId)
        {
            FaceMotionAnimationData animation = _session.GetSelectedAnimation();
            foreach (FaceTrackData track in animation.Timeline.Tracks)
            {
                if (track.BlendShape != null)
                {
                    var key = track.BlendShape.Keys.SingleOrDefault(k => k.KeyId == keyId);
                    if (key != null) return key.Time;
                }
            }

            throw new AssertionException("Key was not found.");
        }

        private static Event Pointer(EventType type, float x, float y)
        {
            return new Event { type = type, button = 0, mousePosition = new Vector2(x, y) };
        }

        private FaceMotionProject CreateExistingEmptyProject()
        {
            FaceMotionProject project = FaceMotionProject.CreateNew();
            AssetDatabase.CreateAsset(project, _temp.AssetPath("M3ExistingEmpty"));
            return project;
        }

        private static void AssertStage(FaceMotionGuidanceModel model, FaceMotionUxState expected)
        {
            Assert.That(model.State, Is.EqualTo(expected));
            Assert.That(model.StepNumber, Is.EqualTo((int)expected));
        }
    }
}
