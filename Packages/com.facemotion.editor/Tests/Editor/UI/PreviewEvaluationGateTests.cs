using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Timeline;
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

            gate.Synchronize(animation, 0.5f);

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

            gate.Synchronize(animation, 0.2f);
            gate.Synchronize(animation, 0.5f);
            gate.Synchronize(animation, 0.8f);

            Assert.That(gate.Pending, Is.True);
            Assert.That(gate.FlushCount, Is.Zero);
            Assert.That(flushed, Is.Empty);

            Assert.That(gate.FlushPending(), Is.True);

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

            gate.Synchronize(animation, 0.1f);
            gate.Synchronize(animation, 0.4f);
            gate.Synchronize(animation, 0.9f);
            gate.EndScrub();

            Assert.That(gate.Pending, Is.False);
            Assert.That(gate.FlushCount, Is.EqualTo(1));
            Assert.That(flushed, Has.Count.EqualTo(1));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.9f)));

            gate.EndScrub();

            Assert.That(gate.FlushCount, Is.EqualTo(1));
            Assert.That(flushed, Has.Count.EqualTo(1));
        }

        [Test]
        public void FlushPending_WithoutPendingSample_DoesNothing()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));

            Assert.That(gate.FlushPending(), Is.False);
            Assert.That(gate.FlushCount, Is.Zero);
            Assert.That(flushed, Is.Empty);

            gate.Scrubbing = true;
            Assert.That(gate.FlushPending(), Is.False);
            Assert.That(gate.FlushCount, Is.Zero);
        }

        [Test]
        public void ScrubbedThenEnded_EndOfGestureFlushCountsTheDeferredSampleOnly()
        {
            var flushed = new List<KeyValuePair<string, float>>();
            var gate = new PreviewEvaluationGate(Record(flushed));
            var animation = FaceMotionAnimationData.Create("A");
            gate.Scrubbing = true;
            gate.Synchronize(animation, 0.25f);
            gate.EndScrub();
            gate.Scrubbing = false;
            gate.Synchronize(animation, 0.75f);

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
                gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime);
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
            gate.EndScrub();

            Assert.That(session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(scrubEndedFired, Is.False, "scrubEnded only fires on a synthesized MouseUp, not on drags.");
            Assert.That(gate.Pending, Is.False);
            Assert.That(flushed, Has.Count.EqualTo(1));
            Assert.That(flushed[0], Is.EqualTo(new KeyValuePair<string, float>(animation.AnimationId, 0.75f)));
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
                gate.Synchronize(session.GetSelectedAnimation(), session.ViewState.CurrentTime);
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

        private static bool Drag(TimelineInputHandler input, Rect plotRect, TimelineLayoutSnapshot layout, float x)
        {
            return input.HandleEvent(new Event
            {
                type = EventType.MouseDrag,
                mousePosition = new Vector2(x, 12f),
                button = 0
            }, plotRect, layout, false);
        }

        private static System.Action<FaceMotionAnimationData, float> Record(List<KeyValuePair<string, float>> flushed)
        {
            return (animation, time) => flushed.Add(new KeyValuePair<string, float>(animation.AnimationId, time));
        }
    }
}