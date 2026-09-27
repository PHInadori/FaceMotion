using FaceMotion.Data;
using System.Linq;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Section 7 hover pipeline event counts. The harness mirrors one window OnGUI event
    /// pass exactly as FaceMotionWindow wires it: left column TrackListPanel hover handling
    /// (IsCandidateHoverEvent / ShouldClearHoverPreview / ApplyHoverPreviewChange, candidates
    /// first then the clear check) runs before the right column PreviewPanel override
    /// ConsumeOverrideChange poll, and each effective hover change requests the next frame
    /// immediately via the production ShouldScheduleHoverRepaint predicate. Targets: a new
    /// candidate is one SetHover change, one evaluation and one repaint request; repeated MouseMove over the
    /// same candidate adds nothing; leaving is one clear, one evaluation, one repaint pass.
    /// </summary>
    public sealed class K8_1HoverEventCountsTests
    {
        private static readonly BlendShapeBinding FaceBinding =
            new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");

        private static readonly BlendShapeBinding CheekBinding =
            new BlendShapeBinding(AvatarFixture.CheekRendererPath, "Smile");

        private AvatarFixture _fixture;
        private PreviewSession _preview;
        private FaceMotionAnimationData _animation;
        private Event _currentEvent;
        private int _lastOverrideChangeCount;
        private int _setChanges;
        private int _clears;
        private int _evaluations;
        private int _scheduledRepaints;
        private int _samePassRepaints;

        [SetUp]
        public void SetUp()
        {
            _fixture = AvatarFixture.Create();
            _preview = new PreviewSession();
            _preview.EnsureAvatar(_fixture.Root);
            _animation = CreateAnimation(FaceBinding, 30f);
            _preview.Evaluate(_animation, 0f);
            ResetCounts();
            _lastOverrideChangeCount = _preview.Override.ChangeCount;
        }

        [TearDown]
        public void TearDown()
        {
            _preview?.Dispose();
            _fixture?.Dispose();
        }

        private void ResetCounts()
        {
            _currentEvent = null;
            _setChanges = 0;
            _clears = 0;
            _evaluations = 0;
            _scheduledRepaints = 0;
            _samePassRepaints = 0;
        }

        /// <summary>Mirrors one window event pass through both columns and the immediate repaint request.</summary>
        private void SimulateEvent(EventType type, BlendShapeBinding? candidateUnderCursor, bool consumed = false)
        {
            _currentEvent = new Event { type = type };
            if (consumed) _currentEvent.Use();
            bool changeThisPass = false;

            if (candidateUnderCursor.HasValue && TrackListPanel.IsCandidateHoverEvent(_currentEvent))
            {
                if (TrackListPanel.ApplyHoverPreviewChange(
                        _preview.Override, candidateUnderCursor.Value, OnHoverOverrideChanged))
                {
                    _setChanges++;
                    changeThisPass = true;
                }
            }

            if (TrackListPanel.ShouldClearHoverPreview(type, candidateUnderCursor.HasValue))
            {
                if (TrackListPanel.ApplyHoverPreviewChange(_preview.Override, null, OnHoverOverrideChanged))
                {
                    _clears++;
                    changeThisPass = true;
                }
            }

            if (_preview.IsActive && PreviewPanel.ConsumeOverrideChange(ref _lastOverrideChangeCount, _preview.Override))
            {
                _evaluations++;
                _preview.Evaluate(_animation, 0f);
            }

            if (type == EventType.Repaint && changeThisPass)
            {
                _samePassRepaints++;
            }

        }

        private void SimulateScrolledEvent(EventType type, Rect rowScreen, Rect viewportScreen,
            Vector2 pointerScreen, BlendShapeBinding binding)
        {
            BlendShapeBinding? target = TrackListPanel.IsVisibleCandidateHit(rowScreen, viewportScreen, pointerScreen)
                ? binding : (BlendShapeBinding?)null;
            SimulateEvent(type, target);
        }

        [Test]
        public void ScrolledCandidate_BlankSpaceAndOffscreenRowNeverActivate_ButVisibleRowDoes()
        {
            var viewport = new Rect(200f, 100f, 180f, 120f);
            var originalRow = new Rect(205f, 106f, 140f, 20f);
            var pointer = new Vector2(220f, 110f);
            SimulateScrolledEvent(EventType.MouseMove, originalRow, viewport, pointer, FaceBinding);
            Assert.That(_preview.Override.Binding, Is.EqualTo(FaceBinding));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(100f));

            var movedDownRow = new Rect(205f, 150f, 140f, 20f);
            SimulateScrolledEvent(EventType.ScrollWheel, movedDownRow, viewport, pointer, FaceBinding);
            Assert.That(_preview.Override.HasActive, Is.False);
            int changesAfterScroll = _preview.Override.ChangeCount;
            SimulateScrolledEvent(EventType.MouseMove, movedDownRow, viewport, pointer, FaceBinding);
            SimulateScrolledEvent(EventType.Repaint, movedDownRow, viewport, pointer, FaceBinding);
            Assert.That(_preview.Override.HasActive, Is.False, "Blank area above the scrolled row is not a candidate.");
            Assert.That(_preview.Override.ChangeCount, Is.EqualTo(changesAfterScroll));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(30f));

            SimulateScrolledEvent(EventType.MouseMove, movedDownRow, viewport,
                new Vector2(movedDownRow.x + 1f, movedDownRow.y + 1f), FaceBinding);
            Assert.That(_preview.Override.Binding, Is.EqualTo(FaceBinding));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(100f));
            Assert.That(_preview.Override.ChangeCount, Is.EqualTo(changesAfterScroll + 1));

            SimulateScrolledEvent(EventType.MouseMove, movedDownRow, viewport,
                new Vector2(movedDownRow.x + 1f, movedDownRow.y - 1f), FaceBinding);
            Assert.That(_preview.Override.HasActive, Is.False);
            int changesAfterExit = _preview.Override.ChangeCount;
            SimulateScrolledEvent(EventType.Repaint, movedDownRow, viewport,
                new Vector2(movedDownRow.x + 1f, movedDownRow.y - 1f), FaceBinding);
            Assert.That(_preview.Override.ChangeCount, Is.EqualTo(changesAfterExit));

            var offscreenRow = new Rect(205f, 225f, 140f, 20f);
            SimulateScrolledEvent(EventType.MouseMove, offscreenRow, viewport,
                new Vector2(offscreenRow.x + 1f, offscreenRow.y + 1f), FaceBinding);
            Assert.That(_preview.Override.HasActive, Is.False, "Offscreen row cannot activate.");
            Assert.That(TrackListPanel.IsVisibleCandidateHit(
                new Rect(205f, 215f, 140f, 20f), viewport, new Vector2(206f, 216f)), Is.True,
                "A visible 1px inset of a clipped row remains hittable.");
            Assert.That(TrackListPanel.IsVisibleCandidateHit(
                new Rect(205f, 215f, 140f, 20f), viewport, new Vector2(206f, 220f)), Is.False,
                "A point outside the viewport must not activate even within the content row.");
            Assert.That(TrackListPanel.IsVisibleCandidateHit(
                movedDownRow, viewport, new Vector2(206f, movedDownRow.y - 1f)), Is.False);
        }

        private void OnHoverOverrideChanged()
        {
            if (FaceMotionWindow.ShouldScheduleHoverRepaint(_currentEvent))
            {
                _scheduledRepaints++;
            }
        }

        [Test]
        public void NewCandidateDuringMouseMove_TriggersOneChangeOneEvaluationOneRepaint()
        {
            int evaluateBaseline = _preview.EvaluateCallCount;

            SimulateEvent(EventType.MouseMove, FaceBinding);
            SimulateEvent(EventType.Repaint, FaceBinding);

            Assert.That(_setChanges, Is.EqualTo(1));
            Assert.That(_clears, Is.EqualTo(0));
            Assert.That(_evaluations, Is.EqualTo(1));
            Assert.That(_preview.EvaluateCallCount - evaluateBaseline, Is.EqualTo(1), "one real evaluation");
            Assert.That(_scheduledRepaints, Is.EqualTo(1));
            Assert.That(_samePassRepaints, Is.EqualTo(0));
            Assert.That(_scheduledRepaints + _samePassRepaints, Is.EqualTo(1), "exactly one repaint pass");
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(100f));
        }

        [Test]
        public void RepaintOverCandidateWithoutMouseMove_DoesNotCreateHover()
        {
            SimulateEvent(EventType.Repaint, FaceBinding);
            SimulateEvent(EventType.Repaint, FaceBinding);

            Assert.That(_setChanges, Is.Zero);
            Assert.That(_evaluations, Is.Zero);
            Assert.That(_samePassRepaints, Is.Zero);
            Assert.That(_scheduledRepaints, Is.Zero);
            Assert.That(_preview.Override.HasActive, Is.False);
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(30f));
        }

        [Test]
        public void RepeatedMouseMoveSameCandidate_TriggersNoAdditionalWork()
        {
            SimulateEvent(EventType.MouseMove, FaceBinding);
            int setAfterFirst = _setChanges;
            int evalAfterFirst = _evaluations;
            int repaintsAfterFirst = _scheduledRepaints + _samePassRepaints;

            SimulateEvent(EventType.MouseMove, FaceBinding);
            SimulateEvent(EventType.Repaint, FaceBinding);

            Assert.That(_setChanges, Is.EqualTo(setAfterFirst));
            Assert.That(_evaluations, Is.EqualTo(evalAfterFirst));
            Assert.That(
                _scheduledRepaints + _samePassRepaints,
                Is.EqualTo(repaintsAfterFirst),
                "repeated hover over the same candidate adds no work");
        }

        [Test]
        public void MouseLeaveWindow_ClearsEvaluatesRepaintsOnce()
        {
            SimulateEvent(EventType.MouseMove, FaceBinding);
            ResetCounts();

            SimulateEvent(EventType.MouseLeaveWindow, null);

            Assert.That(_setChanges, Is.EqualTo(0));
            Assert.That(_clears, Is.EqualTo(1));
            Assert.That(_evaluations, Is.EqualTo(1));
            Assert.That(_scheduledRepaints, Is.EqualTo(1));
            Assert.That(_scheduledRepaints + _samePassRepaints, Is.EqualTo(1));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(30f));
        }

        [Test]
        public void GapMouseMove_ClearsEvaluatesRepaintsOnce()
        {
            SimulateEvent(EventType.MouseMove, FaceBinding);
            ResetCounts();

            SimulateEvent(EventType.MouseMove, null);
            SimulateEvent(EventType.Repaint, null);

            Assert.That(_clears, Is.EqualTo(1));
            Assert.That(_evaluations, Is.EqualTo(1));
            Assert.That(_scheduledRepaints, Is.EqualTo(1));
            Assert.That(_scheduledRepaints + _samePassRepaints, Is.EqualTo(1));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(30f));
        }

        [Test]
        public void TwoDistinctTargetChanges_RequestTheNextFrameOnEachEvent()
        {
            SimulateEvent(EventType.MouseMove, FaceBinding);
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(100f));
            SimulateEvent(EventType.MouseMove, CheekBinding);

            Assert.That(_setChanges, Is.EqualTo(2), "both hover targets changed");
            Assert.That(_evaluations, Is.EqualTo(2));
            Assert.That(_scheduledRepaints, Is.EqualTo(2), "one immediate request per distinct interaction");
            Assert.That(_preview.Override.Binding, Is.EqualTo(CheekBinding));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(30f));
            Assert.That(CloneWeight(CheekBinding), Is.EqualTo(100f));
        }

        [Test]
        public void Scroll_ClearsHoverImmediatelyAndRepeatedRepaintDoesNotClearAgain()
        {
            SimulateEvent(EventType.MouseMove, FaceBinding);
            ResetCounts();

            SimulateEvent(EventType.ScrollWheel, FaceBinding, consumed: true);
            Assert.That(_preview.Override.HasActive, Is.False);
            Assert.That(_clears, Is.EqualTo(1));
            Assert.That(_evaluations, Is.EqualTo(1));
            Assert.That(_scheduledRepaints, Is.EqualTo(1));
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(30f));

            SimulateEvent(EventType.Repaint, FaceBinding);
            SimulateEvent(EventType.Repaint, FaceBinding);
            Assert.That(_clears, Is.EqualTo(1));
            Assert.That(_setChanges, Is.Zero);
            Assert.That(_scheduledRepaints, Is.EqualTo(1));

            SimulateEvent(EventType.MouseMove, CheekBinding);
            Assert.That(_setChanges, Is.EqualTo(1));
            Assert.That(_preview.Override.Binding, Is.EqualTo(CheekBinding));
            Assert.That(CloneWeight(CheekBinding), Is.EqualTo(100f));
        }

        [Test]
        public void FilterChangeAndWarningHelp_ClearWithoutAuthoringOrProjectMutation()
        {
            var project = FaceMotionProject.CreateNew();
            try
            {
                project.AddAnimation(_animation);
                var session = new FaceMotionEditorSession();
                session.SetActiveProject(project, string.Empty);
                session.SelectedAnimationId = _animation.AnimationId;
                session.MarkCurrentAnimationReviewed();
                EditorUtility.ClearDirty(project);
                int previewRevision = session.PreviewRevision;
                int undoGroup = Undo.GetCurrentGroup();
                int repaintRequests = 0;
                var panel = new TrackListPanel(session, new TrackController(session), _preview.Override,
                    () => repaintRequests++);

                _preview.Override.SetHover(FaceBinding);
                panel.OnCandidateFilterChanged(true);
                Assert.That(_preview.Override.HasActive, Is.False);
                Assert.That(repaintRequests, Is.EqualTo(1));
                SimulateEvent(EventType.Repaint, FaceBinding);
                SimulateEvent(EventType.Repaint, FaceBinding);
                Assert.That(_preview.Override.HasActive, Is.False, "Search changes survive passive repaints at the old row.");
                panel.OnCandidateFilterChanged(false);
                Assert.That(repaintRequests, Is.EqualTo(1));

                Assert.That(panel.SetHoverPreview(Warning(FaceBinding)), Is.True);
                Assert.That(repaintRequests, Is.EqualTo(2));
                panel.OnCandidateFilterChanged(false);
                Assert.That(repaintRequests, Is.EqualTo(3));
                SimulateEvent(EventType.Repaint, FaceBinding);
                Assert.That(_preview.Override.HasActive, Is.False, "Category changes survive passive repaint.");

                var first = Warning(FaceBinding);
                var second = Warning(CheekBinding);
                Assert.That(panel.SetHoverPreview(first), Is.True);
                Assert.That(panel.SetHoverPreview(first), Is.False);
                Assert.That(repaintRequests, Is.EqualTo(4), "same candidate does not rebuild the binding or request repaint");
                int changeCount = _preview.Override.ChangeCount;
                Assert.That(panel.SetHoverPreview(second), Is.True);
                Assert.That(_preview.Override.Binding, Is.EqualTo(CheekBinding));
                Assert.That(_preview.Override.ChangeCount, Is.EqualTo(changeCount + 1));
                Assert.That(repaintRequests, Is.EqualTo(5));
                panel.ToggleWarningDetails(first);
                Assert.That(panel.ActiveWarningCandidate, Is.SameAs(first));
                Assert.That(_preview.Override.HasActive, Is.False, "The ? is not part of the candidate hover region.");
                SimulateEvent(EventType.Repaint, CheekBinding);
                SimulateEvent(EventType.Repaint, CheekBinding);
                Assert.That(_preview.Override.HasActive, Is.False, "Warning help clear survives passive repaints.");
                Assert.That(repaintRequests, Is.EqualTo(6));
                panel.ToggleWarningDetails(second);
                Assert.That(panel.ActiveWarningCandidate, Is.SameAs(second));
                Assert.That(repaintRequests, Is.EqualTo(6));
                panel.ToggleWarningDetails(second);
                Assert.That(panel.ActiveWarningCandidate, Is.Null);

                SimulateEvent(EventType.MouseMove, FaceBinding);
                Assert.That(_preview.Override.Binding, Is.EqualTo(FaceBinding));

                Assert.That(session.HasReviewedCurrentAnimation(), Is.True);
                Assert.That(session.PreviewRevision, Is.EqualTo(previewRevision));
                Assert.That(EditorUtility.IsDirty(project), Is.False);
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
                Assert.That(_animation.Timeline.Tracks[0].BlendShape.Keys[0].Value, Is.EqualTo(30f));
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void AddingHoveredCandidate_AddsItsTrackAndClearsTransientOverride()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                project.AddAnimation(_animation);
                string path = temp.AssetPath("K8HoverCandidateAdd");
                AssetDatabase.CreateAsset(project, path);
                var session = new FaceMotionEditorSession();
                session.SetActiveProject(project, path);
                session.SelectedAnimationId = _animation.AnimationId;
                new AvatarController(session).SetDescriptor(_fixture.Descriptor);
                var candidate = session.Candidates.BlendShapes.Single(c =>
                    c.RendererPath == CheekBinding.RendererPath && c.BlendShapeName == CheekBinding.BlendShapeName);
                var tracks = new TrackController(session);
                int repaintRequests = 0;
                var panel = new TrackListPanel(session, tracks, _preview.Override, () => repaintRequests++);
                int trackCount = _animation.Timeline.Tracks.Count;
                SimulateEvent(EventType.MouseMove, candidate.ToBinding());
                Assert.That(CloneWeight(CheekBinding), Is.EqualTo(100f));
                int evaluationsBeforeAdd = _evaluations;

                Assert.That(tracks.AddBlendShapeTrack(candidate), Is.True);
                panel.CloseBlendShapePickerAfterAdd();

                Assert.That(_animation.Timeline.Tracks.Count, Is.EqualTo(trackCount + 1));
                Assert.That(session.GetSelectedTrack().BlendShape.BlendShapeName, Is.EqualTo(candidate.BlendShapeName));
                Assert.That(_preview.Override.HasActive, Is.False);
                Assert.That(repaintRequests, Is.EqualTo(1));
                SimulateEvent(EventType.Repaint, candidate.ToBinding());
                SimulateEvent(EventType.Repaint, candidate.ToBinding());
                Assert.That(_preview.Override.HasActive, Is.False,
                    "Track Add must not resurrect hover on passive repaint at the old row.");
                Assert.That(_evaluations, Is.EqualTo(evaluationsBeforeAdd + 1),
                    "The next repaint consumes the clear exactly once, without a playhead move.");
                Assert.That(CloneWeight(CheekBinding), Is.Zero);

                var other = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "EyeBlink_L");
                SimulateEvent(EventType.MouseMove, other);
                Assert.That(_preview.Override.Binding, Is.EqualTo(other),
                    "A later pointer movement can activate another candidate immediately.");
                Undo.ClearAll();
            }
        }

        private static AvatarCandidateSnapshot.BlendShapeCandidate Warning(BlendShapeBinding binding)
        {
            return new AvatarCandidateSnapshot.BlendShapeCandidate(binding.RendererPath, binding.BlendShapeName,
                conflictStatus: AvatarCandidateSnapshot.BlendShapeConflictStatus.Warning,
                conflictReason: "Existing FX Animator");
        }

        [Test]
        public void ShouldScheduleHoverRepaint_OnlySkipsTheRepaintPass()
        {
            Assert.That(FaceMotionWindow.ShouldScheduleHoverRepaint(new Event { type = EventType.Repaint }), Is.False);
            Assert.That(FaceMotionWindow.ShouldScheduleHoverRepaint(new Event { type = EventType.MouseMove }), Is.True);
            Assert.That(FaceMotionWindow.ShouldScheduleHoverRepaint(new Event { type = EventType.MouseEnterWindow }), Is.True);
            Assert.That(FaceMotionWindow.ShouldScheduleHoverRepaint(new Event { type = EventType.MouseLeaveWindow }), Is.True);
            Assert.That(FaceMotionWindow.ShouldScheduleHoverRepaint(new Event { type = EventType.KeyDown }), Is.True);
            Assert.That(FaceMotionWindow.ShouldScheduleHoverRepaint(null), Is.True);
        }

        [Test]
        public void ConsumeOverrideChange_ConsumesEachChangeAtMostOnce()
        {
            var state = new PreviewOverrideState();
            int last = 0;

            Assert.That(PreviewPanel.ConsumeOverrideChange(ref last, state), Is.False, "no change yet");

            state.SetHover(FaceBinding);
            Assert.That(PreviewPanel.ConsumeOverrideChange(ref last, state), Is.True);
            Assert.That(PreviewPanel.ConsumeOverrideChange(ref last, state), Is.False, "second consume is a no-op");

            state.Clear();
            Assert.That(PreviewPanel.ConsumeOverrideChange(ref last, state), Is.True);
            Assert.That(PreviewPanel.ConsumeOverrideChange(ref last, null), Is.False, "null state is a no-op");
        }

        private float CloneWeight(BlendShapeBinding binding)
        {
            Assert.That(_preview.Cache.TryGetBlendShape(binding, out var renderer, out int index), Is.True);
            return renderer.GetBlendShapeWeight(index);
        }

        private static FaceMotionAnimationData CreateAnimation(BlendShapeBinding binding, float value)
        {
            FaceMotionAnimationData animation = FaceMotionAnimationData.Create("K8-1 Hover Counts");
            FaceTrackData track = FaceTrackData.CreateBlendShape(binding.RendererPath, binding.BlendShapeName);
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, value));
            animation.Timeline.AddTrack(track);
            return animation;
        }
    }
}
