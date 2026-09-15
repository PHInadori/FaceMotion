using FaceMotion.Editor.UI.Preview;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class PreviewCameraStateTests
    {
        [Test]
        public void Fit_UsesTheLimitingViewportAxis()
        {
            var state = new PreviewCameraState();
            var bounds = new Bounds(Vector3.zero, new Vector3(2f, 4f, 2f));

            state.Fit(bounds, 60f, 0.5f);

            Assert.That(state.Pivot, Is.EqualTo(bounds.center));
            Assert.That(state.Distance, Is.GreaterThan(bounds.extents.magnitude));
        }

        [Test]
        public void OrbitPanAndZoom_KeepNavigationInSafeRanges()
        {
            var state = new PreviewCameraState();
            state.Reset(new Bounds(Vector3.zero, Vector3.one));
            state.Orbit(new Vector2(100f, 1000f));
            state.Pan(new Vector2(20f, -10f), 200f);
            state.Zoom(10000f);

            Assert.That(state.Pitch, Is.EqualTo(-80f));
            Assert.That(state.Pivot, Is.Not.EqualTo(Vector3.zero));
            Assert.That(state.Distance, Is.LessThanOrEqualTo(10000f));
        }

        [Test]
        public void Bounds_WithoutRenderers_FallsBackToRoot()
        {
            var root = new GameObject("Preview bounds test");
            root.transform.position = new Vector3(1f, 2f, 3f);
            try
            {
                Assert.That(PreviewCameraState.TryCalculateBounds(root, out Bounds bounds), Is.False);
                Assert.That(bounds.center, Is.EqualTo(root.transform.position));
                Assert.That(bounds.size, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
