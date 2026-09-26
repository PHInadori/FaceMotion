using FaceMotion.Animation;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class L1TimelineDurationUxTests
    {
        [TestCase(1f, 1.5f)]
        [TestCase(10f, 11f)]
        [TestCase(0.2f, 0.7f)]
        [TestCase(30f, 31f)]
        public void FitVisibleRange_AddsDurationAwareTrailingContext(float duration, float expectedEnd)
        {
            Vector2 range = TimelineGeometry.FitVisibleRange(duration);

            Assert.That(range.x, Is.Zero);
            Assert.That(range.y, Is.EqualTo(expectedEnd).Within(1e-4f));
        }

        [Test]
        public void EditableDomain_IncludesOnlyDurationEndpoints()
        {
            const float duration = 1f;

            Assert.That(TimelineTimeDomain.IsEditable(-0.01f, duration), Is.False);
            Assert.That(TimelineTimeDomain.IsEditable(0f, duration), Is.True);
            Assert.That(TimelineTimeDomain.IsEditable(0.5f, duration), Is.True);
            Assert.That(TimelineTimeDomain.IsEditable(duration, duration), Is.True);
            Assert.That(TimelineTimeDomain.IsEditable(1.01f, duration), Is.False);
        }

        [Test]
        public void ConstrainGroupDelta_ClampsSingleKeyAtBothBoundaries()
        {
            Assert.That(TimelineTimeDomain.ConstrainGroupDelta(new[] { 0.1f }, -1f, 1f), Is.EqualTo(-0.1f).Within(1e-4f));
            Assert.That(TimelineTimeDomain.ConstrainGroupDelta(new[] { 0.9f }, 1f, 1f), Is.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void KeyMovePlanner_PreservesGroupSpacingAtBoundaries()
        {
            float[] left = KeyMovePlanner.PlanTrack(new[] { 0.2f, 0.4f }, null, -1f, 1f, 60f, false);
            float[] right = KeyMovePlanner.PlanTrack(new[] { 0.6f, 0.8f }, null, 1f, 1f, 60f, false);

            Assert.That(left[0], Is.EqualTo(0f).Within(1e-4f));
            Assert.That(left[1] - left[0], Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(right[1], Is.EqualTo(1f).Within(1e-4f));
            Assert.That(right[1] - right[0], Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void FrameSnapper_ClampsPastEndpointAndKeepsEndpointReachable()
        {
            Assert.That(FrameSnapper.Snap(1.0166667f, 60f, 1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(FrameSnapper.Snap(1f, 60f, 1f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Viewport_CanShowTrailingGreyContextWithoutExpandingEditableDomain()
        {
            float pps = TimelineGeometry.PixelsPerSecond(TimelineGeometry.FitZoom(1f, 180f));
            float visibleEnd = TimelineGeometry.VisibleDuration(180f, pps);

            Assert.That(visibleEnd, Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(visibleEnd, Is.GreaterThan(1f));
            Assert.That(TimelineTimeDomain.IsEditable(visibleEnd, 1f), Is.False);
        }

        [Test]
        public void ManualViewport_IsPreservedUntilAnimationChanges()
        {
            var state = new TimelineViewState();
            state.MarkAutoFit(1f);
            state.Zoom = 2f;
            state.ScrollTime = 0.25f;
            state.MarkManualViewport();

            Assert.That(state.NeedsAutoFit(2f), Is.False);
            Assert.That(state.Zoom, Is.EqualTo(2f));
            Assert.That(state.ScrollTime, Is.EqualTo(0.25f));

            state.ResetAutoFit();
            Assert.That(state.NeedsAutoFit(2f), Is.True);
        }
    }
}
