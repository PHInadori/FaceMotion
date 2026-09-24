using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>K7-2A: scrub-drag evaluation is coalesced while per-request evaluation stays synchronous.</summary>
    public sealed class PreviewEvaluationGateTests
    {
        [Test]
        public void Synchronize_WithoutScrubbing_FlushesImmediately()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            var animation = FaceMotionAnimationData.Create("A");
            gate.Scrubbing = false;

            gate.Synchronize(animation, 0.5f, 0);

            Assert.That(gate.Pending, Is.False);
            Assert.That(flushed, Has.Count.EqualTo(1));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.5f)));
        }

        [Test]
        public void Synchronize_WhileScrubbing_DefersAndKeepsOnlyTheLatestSample()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            gate.Scrubbing = true;
            var animation = FaceMotionAnimationData.Create("A");

            gate.Synchronize(animation, 0.2f, 0);
            gate.Synchronize(animation, 0.5f, 0);
            gate.Synchronize(animation, 0.8f, 0);

            Assert.That(gate.Pending, Is.True);
            Assert.That(gate.FlushCount, Is.Zero);
            Assert.That(flushed, Is.Empty);

            Assert.That(gate.FlushPending(0), Is.True);

            Assert.That(gate.Pending, Is.False);
            Assert.That(gate.FlushCount, Is.EqualTo(1));
            Assert.That(flushed, Has.Count.EqualTo(1));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.8f)));
        }

        [Test]
        public void EndScrub_FlushesTheFinalPendingSampleExactlyOnce()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            gate.Scrubbing = true;
            var animation = FaceMotionAnimationData.Create("A");

            gate.Synchronize(animation, 0.1f, 0);
            gate.Synchronize(animation, 0.4f, 0);
            gate.Synchronize(animation, 0.9f, 0);
            gate.EndScrub(0);

            Assert.That(gate.Pending, Is.False);
            Assert.That(gate.FlushCount, Is.EqualTo(1));
            Assert.That(flushed, Has.Count.EqualTo(1));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.9f)));

            gate.EndScrub(0);

            Assert.That(gate.FlushCount, Is.EqualTo(1));
            Assert.That(flushed, Has.Count.EqualTo(1));
        }

        [Test]
        public void FlushPending_WithoutPendingSample_DoesNothing()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));

            Assert.That(gate.FlushPending(0), Is.False);
            Assert.That(gate.FlushCount, Is.Zero);
            Assert.That(flushed, Is.Empty);

            gate.Scrubbing = true;
            Assert.That(gate.FlushPending(0), Is.False);
            Assert.That(gate.FlushCount, Is.Zero);
        }

        [Test]
        public void ScrubbedThenEnded_EndOfGestureFlushCountsTheDeferredSampleOnly()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            var animation = FaceMotionAnimationData.Create("A");
            gate.Scrubbing = true;
            gate.Synchronize(animation, 0.25f, 0);
            gate.EndScrub(0);
            gate.Scrubbing = false;
            gate.Synchronize(animation, 0.75f, 0);

            Assert.That(gate.FlushCount, Is.EqualTo(1));
            Assert.That(flushed, Has.Count.EqualTo(2));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.25f)));
            Assert.That(flushed[1], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.75f)));
        }

        [Test]
        public void RealDragGesture_HoversPauseSamplesAndTheGestureEndFlushesTheFinalTimeOnce()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            session.ViewState.SnapEnabled = false;

            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            bool scrubEndedFired = false;
            var input = new TimelineInputHandler(session, keys, tracks, () => scrubEndedFired = true);
            session.Changed += () =>
            {
                gate.Scrubbing = session.ViewState.DragMode == TimelineDragMode.Scrub;
                gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime, session.PreviewRevision);
            };

            var animation = session.GetSelectedAnimation();
            Assert.That(animation, Is.Not.Null);
            Assert.That(session.ViewState.PixelsPerSecond, Is.EqualTo(TimelineGeometry.BasePixelsPerSecond));
            var plotRect = new Rect(TimelineGeometry.DefaultLabelWidth, 0f, 400f, 200f);
            var layout = TimelineLayoutBuilder.Build(
                animation,
                session.TrackBindings,
                TimelineGeometry.DefaultLabelWidth,
                TimelineGeometry.RulerHeight,
                session.ViewState.ScrollTime,
                session.ViewState.Zoom,
                animation.Timeline.Duration);

            // A MouseDown on the ruler starts the scrub gesture (synthetic mouse buttons do
            // not marshal through Unity, so drive the gesture state the same way the handler does).
            session.ViewState.DragMode = TimelineDragMode.Scrub;

            Drag(input, plotRect, layout, TimelineGeometry.DefaultLabelWidth + 120f);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(gate.Pending, Is.True);
            Assert.That(flushed, Is.Empty);

            Drag(input, plotRect, layout, TimelineGeometry.DefaultLabelWidth + 90f);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(gate.Pending, Is.True);
            Assert.That(flushed, Is.Empty);

            // MouseUp ends the gesture; the window's OnScrubEnded performs the end-of-gesture flush.
            session.ViewState.DragMode = TimelineDragMode.None;
            gate.EndScrub(session.PreviewRevision);

            Assert.That(session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(scrubEndedFired, Is.False, "scrubEnded only fires on a synthesized MouseUp, not on drags.");
            Assert.That(gate.Pending, Is.False);
            Assert.That(flushed, Has.Count.EqualTo(1));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.75f)));
        }

        [Test]
        public void ScrubDragBeyondPlotEdges_ClampsTheActualSessionTimeToBothEndpoints()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            session.ViewState.SnapEnabled = false;
            var input = new TimelineInputHandler(session, keys, tracks);
            var animation = session.GetSelectedAnimation();
            var plotRect = new Rect(TimelineGeometry.DefaultLabelWidth, 0f, 120f, 200f);
            var layout = TimelineLayoutBuilder.Build(animation, session.TrackBindings, TimelineGeometry.DefaultLabelWidth,
                TimelineGeometry.RulerHeight, session.ViewState.ScrollTime, session.ViewState.Zoom, animation.Timeline.Duration);

            session.ViewState.DragMode = TimelineDragMode.Scrub;
            Assert.That(Drag(input, plotRect, layout, plotRect.xMax + 500f), Is.True);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(animation.Timeline.Duration).Within(1e-4f));

            Assert.That(Drag(input, plotRect, layout, plotRect.xMin - 500f), Is.True);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void NonScrubDragLeavesTheGateIdle()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack(AvatarFixture.FaceRendererPath, "Mouth_Smile");

            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            var input = new TimelineInputHandler(session, keys, tracks);
            session.Changed += () =>
            {
                gate.Scrubbing = session.ViewState.DragMode == TimelineDragMode.Scrub;
                gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime, session.PreviewRevision);
            };
            var animation = session.GetSelectedAnimation();
            var plotRect = new Rect(TimelineGeometry.DefaultLabelWidth, 0f, 400f, 200f);
            var layout = TimelineLayoutBuilder.Build(
                animation,
                session.TrackBindings,
                TimelineGeometry.DefaultLabelWidth,
                TimelineGeometry.RulerHeight,
                session.ViewState.ScrollTime,
                session.ViewState.Zoom,
                animation.Timeline.Duration);

            bool handled = Drag(input, plotRect, layout, TimelineGeometry.DefaultLabelWidth + 90f);

            Assert.That(handled, Is.False);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(gate.Pending, Is.False);
            Assert.That(flushed, Is.Empty);
        }

        [Test]
        public void RevisionIdentity_DedupesSameRequestButReevaluatesNewRevision()
        {
            var evaluated = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(evaluated));
            var animation = FaceMotionAnimationData.Create("A");
            gate.Synchronize(animation, 0.5f, 1);
            gate.Synchronize(animation, 0.5f, 1);
            gate.Synchronize(animation, 0.5f, 2);
            Assert.That(evaluated, Has.Count.EqualTo(2), "Callback count, not FlushCount, is the total evaluation count.");
        }

        [Test]
        public void PendingRevision_RejectsStaleAndAcceptsReplacement()
        {
            var evaluated = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(evaluated)) { Scrubbing = true };
            var animation = FaceMotionAnimationData.Create("A");
            gate.Synchronize(animation, 0.8f, 1);
            Assert.That(gate.FlushPending(2), Is.False);
            Assert.That(evaluated, Is.Empty);
            Assert.That(gate.Pending, Is.False);
            gate.Synchronize(animation, 0.8f, 1);
            gate.Synchronize(animation, 0.8f, 2);
            Assert.That(gate.FlushPending(2), Is.True);
            Assert.That(evaluated, Has.Count.EqualTo(1));
        }

        [Test]
        public void HeldBoundaryDrag_EvaluatesBothEndpointsBeforeMouseUpAndDedupesRepeats()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session); var keys = new KeyframeController(session); var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null); animations.Add(); session.ViewState.SnapEnabled = false;
            var evaluated = new List<KeyValuePair<string, float>>(); var gate = new PreviewEvaluationGate(Record(evaluated));
            var animation = session.GetSelectedAnimation(); var plot = new Rect(TimelineGeometry.DefaultLabelWidth, 0f, 120f, 200f);
            var layout = TimelineLayoutBuilder.Build(animation, session.TrackBindings, TimelineGeometry.DefaultLabelWidth, TimelineGeometry.RulerHeight, 0f, 1f, animation.Timeline.Duration);
            session.Changed += () =>
            {
                gate.Scrubbing = session.ViewState.DragMode == TimelineDragMode.Scrub;
                gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime, session.PreviewRevision);
                if (gate.Scrubbing && (Mathf.Approximately(session.ViewState.CurrentTime, 0f) || Mathf.Approximately(session.ViewState.CurrentTime, session.GetSelectedDuration()))) gate.FlushPending(session.PreviewRevision);
            };
            var input = new TimelineInputHandler(session, keys, tracks); session.ViewState.DragMode = TimelineDragMode.Scrub;
            int revision = session.PreviewRevision;
            Drag(input, plot, layout, plot.xMax + 500f);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(1f)); Assert.That(evaluated[evaluated.Count - 1].Value, Is.EqualTo(1f));
            for (int i = 0; i < 20; i++) Drag(input, plot, layout, plot.xMax + 500f);
            Assert.That(evaluated, Has.Count.EqualTo(1)); Assert.That(session.PreviewRevision, Is.EqualTo(revision));
            gate.EndScrub(session.PreviewRevision);
            Assert.That(evaluated, Has.Count.EqualTo(1), "MouseUp is only a safety flush after an already-evaluated endpoint.");
            Drag(input, plot, layout, plot.xMin - 500f);
            Assert.That(session.ViewState.CurrentTime, Is.EqualTo(0f)); Assert.That(evaluated[evaluated.Count - 1].Value, Is.EqualTo(0f));
            for (int i = 0; i < 20; i++) Drag(input, plot, layout, plot.xMin - 500f);
            Assert.That(evaluated, Has.Count.EqualTo(2));
            gate.EndScrub(session.PreviewRevision);
            Assert.That(evaluated, Has.Count.EqualTo(2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HeldBoundaryDrag_UsesMouseCaptureUntilOutsideMouseUp(bool rightBoundary)
        {
            GUIUtility.hotControl = 0;
            try
            {
                var session = new FaceMotionEditorSession();
                var animations = new AnimationController(session);
                var keys = new KeyframeController(session);
                var tracks = new TrackController(session);
                session.SetActiveProject(FaceMotionProject.CreateNew(), null);
                animations.Add();
                session.ViewState.SnapEnabled = false;

                var evaluated = new List<KeyValuePair<string, float>>();
                var gate = new PreviewEvaluationGate(Record(evaluated));
                var input = new TimelineInputHandler(session, keys, tracks, () => gate.EndScrub(session.PreviewRevision));
                var animation = session.GetSelectedAnimation();
                var plot = new Rect(TimelineGeometry.DefaultLabelWidth, 0f, 120f, 200f);
                var timeline = new Rect(0f, 0f, plot.xMax + 20f, plot.height);
                var layout = TimelineLayoutBuilder.Build(animation, session.TrackBindings, TimelineGeometry.DefaultLabelWidth,
                    TimelineGeometry.RulerHeight, 0f, 1f, animation.Timeline.Duration);
                const int controlId = 7321;

                session.Changed += () =>
                {
                    gate.Scrubbing = session.ViewState.DragMode == TimelineDragMode.Scrub;
                    gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime, session.PreviewRevision);
                    if (gate.Scrubbing && (Mathf.Approximately(session.ViewState.CurrentTime, 0f)
                        || Mathf.Approximately(session.ViewState.CurrentTime, session.GetSelectedDuration())))
                    {
                        gate.FlushPending(session.PreviewRevision);
                    }
                };

                Event mouseDown = Mouse(EventType.MouseDown, 0, plot.x + 60f, 5f);
                Assert.That(mouseDown.rawType, Is.EqualTo(EventType.MouseDown));
                Assert.That(timeline.Contains(mouseDown.mousePosition), Is.True);
                Assert.That(TimelineView.RoutePointerEvent(mouseDown, controlId,
                    timeline, input, session.ViewState, plot, layout, false), Is.True);
                Assert.That(session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
                Assert.That(GUIUtility.hotControl, Is.EqualTo(controlId));

                float outsideX = rightBoundary ? plot.xMax + 500f : plot.xMin - 500f;
                float endpoint = rightBoundary ? animation.Timeline.Duration : 0f;
                Assert.That(TimelineView.RoutePointerEvent(Mouse(EventType.MouseDrag, 0, outsideX, 5f), controlId,
                    timeline, input, session.ViewState, plot, layout, false), Is.True);
                Assert.That(session.ViewState.CurrentTime, Is.EqualTo(endpoint).Within(1e-4f));
                Assert.That(evaluated, Has.Count.EqualTo(1));
                Assert.That(evaluated[0].Value, Is.EqualTo(endpoint).Within(1e-4f));

                Assert.That(TimelineView.RoutePointerEvent(Mouse(EventType.MouseDrag, 0, outsideX, 5f), controlId,
                    timeline, input, session.ViewState, plot, layout, false), Is.True);
                Assert.That(evaluated, Has.Count.EqualTo(1), "Repeated endpoint drags must remain deduped.");

                Assert.That(TimelineView.RoutePointerEvent(Mouse(EventType.MouseUp, 0, outsideX, 5f), controlId,
                    timeline, input, session.ViewState, plot, layout, false), Is.True);
                Assert.That(session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
                Assert.That(GUIUtility.hotControl, Is.Zero);
                Assert.That(evaluated, Has.Count.EqualTo(1), "MouseUp only releases capture after the endpoint has flushed.");
            }
            finally
            {
                GUIUtility.hotControl = 0;
            }
        }

        [Test]
        public void PoseNotification_ReplacesStalePendingRequestAtTheCurrentTime()
        {
            var session = new FaceMotionEditorSession(); var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null); animations.Add(); session.SetCurrentTime(0.8f);
            var evaluated = new List<KeyValuePair<string, float>>(); var submittedRevisions = new List<int>();
            var gate = new PreviewEvaluationGate((animation, time) => evaluated.Add(new KeyValuePair<string, float>(animation.AnimationId, time)));
            session.ViewState.DragMode = TimelineDragMode.Scrub;
            session.Changed += () =>
            {
                gate.Scrubbing = session.ViewState.DragMode == TimelineDragMode.Scrub;
                submittedRevisions.Add(session.PreviewRevision);
                gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime, session.PreviewRevision);
            };
            int staleRevision = session.PreviewRevision;
            session.NotifyChanged(); // Creates the initial rev-N pending request through the real session callback.
            session.NotifyPoseChanged();
            Assert.That(session.PreviewRevision, Is.EqualTo(staleRevision + 1));
            Assert.That(gate.FlushPending(session.PreviewRevision), Is.True);
            Assert.That(evaluated, Has.Count.EqualTo(1));
            Assert.That(evaluated[0].Value, Is.EqualTo(0.8f));
            Assert.That(submittedRevisions.Exists(value => value == staleRevision + 2), Is.False);
            Assert.That(submittedRevisions[submittedRevisions.Count - 1], Is.EqualTo(session.PreviewRevision));
        }

        [Test]
        public void PoseNotification_ReevaluatesTheSameEndpointAtTheNewRevision()
        {
            var session = new FaceMotionEditorSession(); var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null); animations.Add(); session.SetCurrentTime(1f);
            var evaluated = new List<KeyValuePair<string, float>>(); var gate = new PreviewEvaluationGate(Record(evaluated));
            session.Changed += () => gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime, session.PreviewRevision);
            session.NotifyChanged();
            int revision = session.PreviewRevision;
            session.NotifyPoseChanged();
            Assert.That(session.PreviewRevision, Is.EqualTo(revision + 1));
            Assert.That(evaluated, Has.Count.EqualTo(2));
            Assert.That(evaluated[0].Value, Is.EqualTo(1f)); Assert.That(evaluated[1].Value, Is.EqualTo(1f));
        }

        [Test]
        public void DeferredPreviewRepaintScheduler_RegistersOnceAndConsumesOneCallback()
        {
            var scheduler = new PreviewRepaintScheduler();

            Assert.That(scheduler.Request(), Is.True, "The first request registers the update callback.");
            Assert.That(scheduler.Request(), Is.False, "A pending callback prevents duplicate update subscriptions.");
            Assert.That(scheduler.Request(), Is.False);
            Assert.That(scheduler.Pending, Is.True);

            Assert.That(scheduler.Dispatch(), Is.True, "The callback consumes the pending repaint only.");
            Assert.That(scheduler.Pending, Is.False);
            Assert.That(scheduler.Dispatch(), Is.False, "The callback is one-shot after it has been consumed.");
        }

        private static bool Drag(TimelineInputHandler input, Rect plotRect, TimelineLayoutSnapshot layout, float x)
        {
            return input.HandleEvent(new Event
            {
                type = EventType.MouseDrag,
                mousePosition = new Vector2(x, 12f),
                button = 0
            }, plotRect, layout, false);
        }

        private static Event Mouse(EventType type, int button, float x, float y)
        {
            return new Event { type = type, button = button, mousePosition = new Vector2(x, y) };
        }

        private static System.Action<FaceMotionAnimationData, float> Record(List<KeyValuePair<string, float>> flushed)
        {
            return (animation, time) => flushed.Add(new KeyValuePair<string, float>(animation.AnimationId, time));
        }
    }
}
