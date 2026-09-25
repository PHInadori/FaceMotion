using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Section 8 playback/scrub performance counts over fixed intervals. The harness mirrors
    /// the FaceMotionWindow wiring (session.Changed -> SynchronizePreviewFromSession ->
    /// PreviewEvaluationGate -> EvaluatePreviewAt with the deferred repaint scheduler, plus
    /// the end-of-tick delayCall drain and repaint dispatch) while driving the real playback
    /// controller, real gate, real session, and real PreviewSession evaluation. Held-scrub
    /// counts are reported separately from plain playback because the endpoint/drain
    /// workaround (FlushPending + RequestPreviewRepaint) only exists for scrub gestures.
    /// </summary>
    public sealed class K8_1PlaybackPerfCountTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private PreviewPlaybackController _playback;
        private PreviewEvaluationGate _gate;
        private PreviewSession _preview;
        private TempFaceMotionAsset _temp;
        private AvatarFixture _avatar;
        private FaceMotionAnimationData _animation;
        private PreviewRepaintScheduler _requestRepaint;
        private PreviewRepaintScheduler _deferred;
        private int _playbackTicks;
        private int _sessionChanged;
        private int _gateSyncs;
        private int _requestRepaintCalls;
        private int _deferredSchedules;
        private int _drains;
        private int _requestDispatches;
        private int _deferredDispatches;
        private bool _drainScheduled;

        [SetUp]
        public void SetUp()
        {
            _avatar = AvatarFixture.Create();
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _playback = new PreviewPlaybackController(_session);
            _temp = new TempFaceMotionAsset();
            _preview = new PreviewSession();
            _preview.EnsureAvatar(_avatar.Root);
            ResetCounts();

            var project = FaceMotionProject.CreateNew();
            string path = _temp.AssetPath("K8PlaybackPerf");
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, path);
            Assert.That(_animations.Add(), Is.Not.Null);

            _animation = _session.GetSelectedAnimation();
            _animation.Timeline.Duration = 1f;
            _animation.Timeline.Loop = false;
            var track = FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f, InterpolationType.Linear));
            track.BlendShape.AddKey(FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear));
            _animation.Timeline.AddTrack(track);

            _gate = new PreviewEvaluationGate(OnGateFlush);
            _session.Changed += OnSessionChanged;
        }

        [TearDown]
        public void TearDown()
        {
            _session.Changed -= OnSessionChanged;
            _preview?.Dispose();
            _temp?.Dispose();
            _avatar?.Dispose();
        }

        private void ResetCounts()
        {
            _requestRepaint = new PreviewRepaintScheduler();
            _deferred = new PreviewRepaintScheduler();
            _playbackTicks = 0;
            _sessionChanged = 0;
            _gateSyncs = 0;
            _requestRepaintCalls = 0;
            _deferredSchedules = 0;
            _drains = 0;
            _requestDispatches = 0;
            _deferredDispatches = 0;
            _drainScheduled = false;
            _drainPending = false;
            _evalBaseline = _preview != null ? _preview.EvaluateCallCount : 0;
        }

        /// <summary>Mirror of FaceMotionWindow.EvaluatePreviewAt for the preview-active path.</summary>
        private void OnGateFlush(FaceMotionAnimationData animation, float time)
        {
            _preview.EnsureAvatar(_avatar.Root);
            _preview.Evaluate(animation, time);
            if (_deferred.Request())
            {
                _deferredSchedules++;
            }
        }

        /// <summary>Mirror of FaceMotionWindow.RequestPreviewRepaint (scheduler only).</summary>
        private void RequestPreviewRepaint()
        {
            _requestRepaintCalls++;
            _requestRepaint.Request();
        }

        /// <summary>Mirror of FaceMotionWindow.SynchronizePreviewFromSession.</summary>
        private void OnSessionChanged()
        {
            _sessionChanged++;
            var animation = _session.GetSelectedAnimation();
            if (animation == null || !_preview.IsActive)
            {
                return;
            }

            _gateSyncs++;
            _gate.Scrubbing = _session.ViewState.DragMode == TimelineDragMode.Scrub;
            _gate.Synchronize(animation, _session.ViewState.CurrentTime, _session.PreviewRevision);
            float duration = _session.GetSelectedDuration();
            bool endpoint = _gate.Scrubbing &&
                (Mathf.Approximately(_session.ViewState.CurrentTime, 0f) ||
                 Mathf.Approximately(_session.ViewState.CurrentTime, duration));
            if (endpoint)
            {
                _gate.FlushPending(_session.PreviewRevision);
                RequestPreviewRepaint();
            }

            if (_gate.Pending && !_drainScheduled)
            {
                _drainScheduled = true;
                _drainPending = true;
            }
        }

        private bool _drainPending;
        private int _evalBaseline;

        /// <summary>Mirror of DrainPendingPreviewEvaluation.</summary>
        private void DrainPendingPreviewEvaluation()
        {
            _drainScheduled = false;
            _drainPending = false;
            _drains++;
            if (_gate.FlushPending(_session.PreviewRevision))
            {
                RequestPreviewRepaint();
            }
        }

        /// <summary>
        /// Mirror of one editor update tick: the delayCall drain runs first, then the two
        /// repaint schedulers dispatch their coalesced repaint (EditorApplication.update /
        /// delayCall delivery order approximated deterministically).
        /// </summary>
        private void EndOfEditorTick()
        {
            if (_drainPending)
            {
                DrainPendingPreviewEvaluation();
            }

            if (_requestRepaint.Dispatch())
            {
                _requestDispatches++;
            }

            if (_deferred.Dispatch())
            {
                _deferredDispatches++;
            }
        }

        private void PlaybackTick(double realtimeNow)
        {
            _playbackTicks++;
            _playback.Tick(realtimeNow);
            EndOfEditorTick();
        }

        [Test]
        public void PlaybackInterval_CountsTickToRenderPipelineOverFixedInterval()
        {
            _playback.Play();
            EndOfEditorTick();
            PlaybackTick(10d);
            for (int i = 1; i <= 10; i++)
            {
                PlaybackTick(10d + i * 0.1d);
            }

            Assert.That(_playback.IsPlaying, Is.False, "non-loop playback stops on the endpoint");
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(1f));
            Assert.That(_playbackTicks, Is.EqualTo(11));
            Assert.That(_sessionChanged, Is.EqualTo(11), "one change from Play plus one per tick");
            Assert.That(_gateSyncs, Is.EqualTo(11));
            Assert.That(_preview.EvaluateCallCount, Is.EqualTo(11), "one evaluation per change, none coalesced away");
            Assert.That(_requestRepaintCalls, Is.EqualTo(0), "playback never uses the scrub repaint path");
            Assert.That(_deferredDispatches, Is.EqualTo(_deferredSchedules), "every deferred schedule dispatches once");
            Assert.That(_requestDispatches + _deferredDispatches, Is.EqualTo(11), "one render pass per change");
        }

        [Test]
        public void HeldScrubInterval_CountsCoalescedEvaluations_SeparatelyFromPlayback()
        {
            _session.ViewState.DragMode = TimelineDragMode.Scrub;
            for (int i = 1; i <= 5; i++)
            {
                _session.SetCurrentTime(i * 0.1f);
            }

            EndOfEditorTick();
            for (int i = 6; i <= 10; i++)
            {
                _session.SetCurrentTime(i * 0.1f);
            }

            EndOfEditorTick();
            _session.ViewState.DragMode = TimelineDragMode.None;
            _gate.EndScrub(_session.PreviewRevision);
            RequestPreviewRepaint();
            EndOfEditorTick();

            Assert.That(_sessionChanged, Is.EqualTo(10), "every drag published a new time");
            Assert.That(_gateSyncs, Is.EqualTo(10), "every drag synchronized through the gate");
            Assert.That(_gate.Pending, Is.False, "released gesture leaves nothing pending");
            Assert.That(_preview.EvaluateCallCount, Is.EqualTo(2), "one evaluation per editor tick, latest sample wins");
            Assert.That(_drains, Is.EqualTo(2), "the delayCall drain runs once per editor tick");
            Assert.That(_requestRepaintCalls, Is.EqualTo(3), "two drains plus the scrub-end repaint");
            Assert.That(_requestDispatches, Is.EqualTo(3), "coalesced repaints dispatch once each");
        }

        [Test]
        public void HeldScrubAtEndpoint_HeldEventsClampWithoutExtraWork()
        {
            _session.SetCurrentTime(0.9f);
            EndOfEditorTick();
            ResetCounts();
            int evalBaseline = _preview.EvaluateCallCount;

            _session.ViewState.DragMode = TimelineDragMode.Scrub;
            for (int i = 0; i < 10; i++)
            {
                _session.SetCurrentTime(1f);
            }

            EndOfEditorTick();
            _session.ViewState.DragMode = TimelineDragMode.None;
            _gate.EndScrub(_session.PreviewRevision);
            RequestPreviewRepaint();
            EndOfEditorTick();

            Assert.That(_sessionChanged, Is.EqualTo(1), "only the first clamped drag changes state");
            Assert.That(_gateSyncs, Is.EqualTo(1));
            Assert.That(_preview.EvaluateCallCount - evalBaseline, Is.EqualTo(1), "endpoint evaluated exactly once");
            Assert.That(_requestRepaintCalls, Is.EqualTo(2), "endpoint workaround plus scrub-end repaint");
            Assert.That(_requestDispatches, Is.EqualTo(2));
        }
    }
}
