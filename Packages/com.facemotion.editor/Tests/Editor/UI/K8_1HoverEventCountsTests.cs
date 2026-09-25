using FaceMotion.Data;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Section 7 hover pipeline event counts. The harness mirrors one window OnGUI event
    /// pass exactly as FaceMotionWindow wires it: left column TrackListPanel hover handling
    /// (IsCandidateHoverEvent / ShouldClearHoverPreview / ApplyHoverPreviewChange, candidates
    /// first then the clear check) runs before the right column PreviewPanel override
    /// ConsumeOverrideChange poll, the hover repaint request goes through the production
    /// ShouldScheduleHoverRepaint predicate, and pending requests are flushed at the end of
    /// the event like EditorApplication.delayCall does. Targets: a new candidate is one
    /// SetHover change, one evaluation and one repaint pass; repeated MouseMove over the
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
        private PreviewRepaintScheduler _scheduler;
        private Event _currentEvent;
        private int _lastOverrideChangeCount;
        private int _setChanges;
        private int _clears;
        private int _evaluations;
        private int _scheduledRepaints;
        private int _samePassRepaints;
        private bool _pendingFlush;

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
            _scheduler = new PreviewRepaintScheduler();
            _currentEvent = null;
            _setChanges = 0;
            _clears = 0;
            _evaluations = 0;
            _scheduledRepaints = 0;
            _samePassRepaints = 0;
            _pendingFlush = false;
        }

        /// <summary>Mirrors one window event pass through both columns plus the delayCall flush.</summary>
        private void SimulateEvent(EventType type, BlendShapeBinding? candidateUnderCursor, bool flush = true)
        {
            _currentEvent = new Event { type = type };
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

            if (TrackListPanel.ShouldClearHoverPreview(_currentEvent, candidateUnderCursor.HasValue))
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

            if (flush)
            {
                FlushScheduledRepaint();
            }
        }

        private void OnHoverOverrideChanged()
        {
            if (FaceMotionWindow.ShouldScheduleHoverRepaint(_currentEvent) && _scheduler.Request())
            {
                _pendingFlush = true;
            }
        }

        private void FlushScheduledRepaint()
        {
            if (_pendingFlush && _scheduler.Dispatch())
            {
                _pendingFlush = false;
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
        public void NewCandidateDuringRepaintPass_ConsumesSamePass_WithoutSecondRepaint()
        {
            SimulateEvent(EventType.Repaint, FaceBinding);

            Assert.That(_setChanges, Is.EqualTo(1));
            Assert.That(_evaluations, Is.EqualTo(1));
            Assert.That(_samePassRepaints, Is.EqualTo(1), "consumed and rendered in the same pass");
            Assert.That(_scheduledRepaints, Is.EqualTo(0), "no redundant repaint is scheduled");
            Assert.That(
                _scheduledRepaints + _samePassRepaints,
                Is.EqualTo(1),
                "the hover change is rendered exactly once");
            Assert.That(CloneWeight(FaceBinding), Is.EqualTo(100f));
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
        public void CoalescedRequests_FlushOnce()
        {
            SimulateEvent(EventType.MouseMove, FaceBinding, flush: false);
            SimulateEvent(EventType.MouseMove, CheekBinding, flush: false);
            FlushScheduledRepaint();

            Assert.That(_setChanges, Is.EqualTo(2), "both hover targets changed");
            Assert.That(_scheduledRepaints, Is.EqualTo(1), "requests coalesce into one scheduled repaint");
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
