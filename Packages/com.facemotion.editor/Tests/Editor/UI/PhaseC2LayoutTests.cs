using FaceMotion.Editor.UI.Window;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class PhaseC2LayoutTests
    {
        [Test]
        public void WindowMinimumSize_IsUsableForTwoColumns()
        {
            Assert.That(FaceMotionWindow.MinimumWindowWidth, Is.EqualTo(900f));
            Assert.That(FaceMotionWindow.MinimumWindowHeight, Is.EqualTo(720f));
            Assert.That(FaceMotionWindow.MinimumWindowHeight, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumRequiredWindowHeight));
            Assert.That(FaceMotionWindow.MinimumLeftColumnWidth, Is.GreaterThanOrEqualTo(320f));
            Assert.That(FaceMotionWindow.MinimumTimelineWidth, Is.GreaterThanOrEqualTo(460f));
        }

        [Test]
        public void LeftColumnWidth_AtMinimumWindowPreservesBothMinima()
        {
            float hostWidth = FaceMotionWindow.MinimumWindowWidth;
            float left = FaceMotionWindow.CalculateLeftColumnWidth(hostWidth, 0.36f);
            float right = hostWidth - left - FaceMotionWindow.SplitterWidth;

            Assert.That(left, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumLeftColumnWidth));
            Assert.That(right, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineWidth));
        }

        [Test]
        public void LeftColumnWidth_RespectsMaximumRatioAsWindowGrows()
        {
            const float hostWidth = 1600f;
            float left = FaceMotionWindow.CalculateLeftColumnWidth(hostWidth, 0.9f);
            float usable = hostWidth - FaceMotionWindow.SplitterWidth;

            Assert.That(left, Is.LessThanOrEqualTo(usable * FaceMotionWindow.MaximumLeftColumnRatio));
            Assert.That(hostWidth - left - FaceMotionWindow.SplitterWidth, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineWidth));
        }

        [Test]
        public void PreviewHeight_PreservesTimelineAndInspectorSpace()
        {
            float hostHeight = FaceMotionWindow.MinimumWindowHeight - 22f;
            float preview = FaceMotionWindow.CalculatePreviewHeight(hostHeight, 0.9f);
            float remaining = hostHeight - preview - FaceMotionWindow.SplitterWidth - FaceMotionWindow.InspectorHeight;

            Assert.That(preview, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumPreviewHeight));
            Assert.That(preview, Is.LessThanOrEqualTo((hostHeight - FaceMotionWindow.SplitterWidth - FaceMotionWindow.InspectorHeight) * FaceMotionWindow.MaximumPreviewHeightRatio));
            Assert.That(remaining, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineHeight));
        }
    }
}
