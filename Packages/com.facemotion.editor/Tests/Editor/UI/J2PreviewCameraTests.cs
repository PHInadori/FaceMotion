using FaceMotion.Editor.UI.Preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class J2PreviewCameraTests
    {
        private readonly Rect _rect = new Rect(0f, 0f, 320f, 240f);
        private AvatarFixture _fixture;
        private PreviewSession _preview;

        [SetUp]
        public void SetUp()
        {
            _fixture = AvatarFixture.Create();
            _preview = new PreviewSession();
            _preview.EnsureAvatar(_fixture.Root);
        }

        [TearDown]
        public void TearDown()
        {
            _preview?.Dispose();
            _fixture?.Dispose();
        }

        [Test]
        public void AltLeftDrag_OrbitsCamera()
        {
            float yaw = _preview.Camera.Yaw;

            _preview.HandleCameraEvent(Drag(0, true, new Vector2(16f, 4f)), _rect, false);

            Assert.That(_preview.Camera.Yaw, Is.Not.EqualTo(yaw));
        }

        [Test]
        public void PlainLeftDrag_DoesNotOrbitCamera()
        {
            float yaw = _preview.Camera.Yaw;
            float pitch = _preview.Camera.Pitch;

            _preview.HandleCameraEvent(Drag(0, false, new Vector2(16f, 4f)), _rect, false);

            Assert.That(_preview.Camera.Yaw, Is.EqualTo(yaw));
            Assert.That(_preview.Camera.Pitch, Is.EqualTo(pitch));
        }

        [Test]
        public void MiddleDrag_PansCamera()
        {
            Vector3 pivot = _preview.Camera.Pivot;

            _preview.HandleCameraEvent(Drag(2, false, new Vector2(16f, 4f)), _rect, false);

            Assert.That(_preview.Camera.Pivot, Is.Not.EqualTo(pivot));
        }

        [Test]
        public void ScrollWheel_ZoomsCamera()
        {
            float distance = _preview.Camera.Distance;
            var input = new Event
            {
                type = EventType.ScrollWheel,
                mousePosition = _rect.center,
                delta = new Vector2(0f, 1f)
            };

            _preview.HandleCameraEvent(input, _rect, false);

            Assert.That(_preview.Camera.Distance, Is.Not.EqualTo(distance));
        }

        [Test]
        public void FKey_FocusesAvatarBounds()
        {
            _preview.HandleCameraEvent(Drag(2, false, new Vector2(25f, 10f)), _rect, false);
            Bounds bounds;
            PreviewCameraState.TryCalculateBounds(_preview.CloneRoot, out bounds);
            var input = new Event { type = EventType.KeyDown, keyCode = KeyCode.F, mousePosition = _rect.center };

            _preview.HandleCameraEvent(input, _rect, false);

            Assert.That(_preview.Camera.Pivot, Is.EqualTo(bounds.center));
            Assert.That(_preview.Camera.Distance, Is.GreaterThan(0f));
        }

        [Test]
        public void FKey_IsIgnoredWhileEditingText()
        {
            Vector3 pivot = _preview.Camera.Pivot;
            bool previous = EditorGUIUtility.editingTextField;
            try
            {
                EditorGUIUtility.editingTextField = true;
                var input = new Event { type = EventType.KeyDown, keyCode = KeyCode.F, mousePosition = _rect.center };

                _preview.HandleCameraEvent(input, _rect, true);
            }
            finally
            {
                EditorGUIUtility.editingTextField = previous;
            }

            Assert.That(_preview.Camera.Pivot, Is.EqualTo(pivot));
        }

        private Event Drag(int button, bool alt, Vector2 delta)
        {
            return new Event
            {
                type = EventType.MouseDrag,
                button = button,
                alt = alt,
                mousePosition = _rect.center,
                delta = delta
            };
        }
    }
}
