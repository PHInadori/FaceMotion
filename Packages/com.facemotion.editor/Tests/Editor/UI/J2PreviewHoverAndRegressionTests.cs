using FaceMotion.Data;
using FaceMotion.Editor.Preview;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class J2PreviewHoverTests
    {
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
        public void Hover_AppliesHundredToPreviewCloneOnly()
        {
            var binding = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            _fixture.FaceRenderer.SetBlendShapeWeight(0, 12f);
            _preview.Override.SetHover(binding);

            _preview.Evaluate(null, 0f);

            Assert.That(CloneWeight(binding), Is.EqualTo(100f));
            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(12f));
        }

        [Test]
        public void ClearHover_RestoresTimelineEvaluatedValue()
        {
            var binding = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            FaceMotionAnimationData animation = CreateAnimation(binding, 30f);

            _preview.Evaluate(animation, 0f);
            _preview.Override.SetHover(binding);
            _preview.Evaluate(animation, 0f);
            Assert.That(CloneWeight(binding), Is.EqualTo(100f));

            _preview.Override.Clear();
            _preview.Evaluate(animation, 0f);

            Assert.That(CloneWeight(binding), Is.EqualTo(30f));
        }

        [Test]
        public void Hover_DoesNotModifySourceAvatar()
        {
            var binding = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            _fixture.FaceRenderer.SetBlendShapeWeight(0, 24f);
            _preview.Override.SetHover(binding);

            _preview.Evaluate(null, 0f);

            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(24f));
        }

        [Test]
        public void SetHover_SameBinding_DoesNotIncrementChangeCount()
        {
            var binding = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");

            _preview.Override.SetHover(binding);
            int count = _preview.Override.ChangeCount;
            _preview.Override.SetHover(binding);

            Assert.That(_preview.Override.ChangeCount, Is.EqualTo(count));
        }

        [Test]
        public void ClearHover_MarksOverrideInactive()
        {
            var binding = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            _preview.Override.SetHover(binding);
            int count = _preview.Override.ChangeCount;

            _preview.Override.Clear();

            Assert.That(_preview.Override.HasActive, Is.False);
            Assert.That(_preview.Override.ChangeCount, Is.EqualTo(count + 1));
        }

        [Test]
        public void UnresolvableHoverBinding_IsSafe()
        {
            _preview.Override.SetHover(new BlendShapeBinding("Missing/Renderer", "Unknown"));

            Assert.DoesNotThrow(() => _preview.Evaluate(null, 0f));
            Assert.That(_preview.Override.HasActive, Is.True);
        }

        [Test]
        public void Hover_DoesNotChangeSameNamedShapeOnAnotherRenderer()
        {
            var face = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Smile");
            var cheek = new BlendShapeBinding(AvatarFixture.CheekRendererPath, "Smile");
            _preview.Override.SetHover(face);

            _preview.Evaluate(null, 0f);

            Assert.That(CloneWeight(face), Is.EqualTo(100f));
            Assert.That(CloneWeight(cheek), Is.EqualTo(0f));
        }

        [Test]
        public void Dispose_ClearsHoverOverride()
        {
            _preview.Override.SetHover(new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile"));

            _preview.Dispose();

            Assert.That(_preview.Override.HasActive, Is.False);
        }

        private float CloneWeight(BlendShapeBinding binding)
        {
            Assert.That(_preview.Cache.TryGetBlendShape(binding, out var renderer, out int index), Is.True);
            return renderer.GetBlendShapeWeight(index);
        }

        private static FaceMotionAnimationData CreateAnimation(BlendShapeBinding binding, float value)
        {
            FaceMotionAnimationData animation = FaceMotionAnimationData.Create("J2 Hover");
            FaceTrackData track = FaceTrackData.CreateBlendShape(binding.RendererPath, binding.BlendShapeName);
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, value));
            animation.Timeline.AddTrack(track);
            return animation;
        }
    }

    public sealed class J2PreviewRegressionTests
    {
        [Test]
        public void PreviewPanelLayout_OrdersHeaderControlsAndRender()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 600f, 400f));

            Assert.That(layout.HeaderRect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.y));
            Assert.That(layout.ControlsRect.yMax, Is.LessThanOrEqualTo(layout.RenderRect.y));
            Assert.That(layout.RenderRect.height, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void CameraInput_OutsidePreviewRect_IsIgnored()
        {
            var fixture = AvatarFixture.Create();
            var preview = new PreviewSession();
            try
            {
                preview.EnsureAvatar(fixture.Root);
                float yaw = preview.Camera.Yaw;
                preview.HandleCameraEvent(new Event
                {
                    type = EventType.MouseDrag,
                    button = 0,
                    alt = true,
                    mousePosition = new Vector2(401f, 120f),
                    delta = new Vector2(20f, 0f)
                }, new Rect(0f, 0f, 320f, 240f), false);

                Assert.That(preview.Camera.Yaw, Is.EqualTo(yaw));
            }
            finally
            {
                preview.Dispose();
                fixture.Dispose();
            }
        }

        [Test]
        public void EnsureAvatar_SameSource_ReusesPreviewClone()
        {
            var fixture = AvatarFixture.Create();
            var preview = new PreviewSession();
            try
            {
                preview.EnsureAvatar(fixture.Root);
                preview.EnsureAvatar(fixture.Root);

                Assert.That(preview.CloneCreationCount, Is.EqualTo(1));
            }
            finally
            {
                preview.Dispose();
                fixture.Dispose();
            }
        }

        [Test]
        public void FKey_OutsidePreviewRect_IsIgnored()
        {
            var fixture = AvatarFixture.Create();
            var preview = new PreviewSession();
            try
            {
                preview.EnsureAvatar(fixture.Root);
                Vector3 pivot = preview.Camera.Pivot;
                preview.HandleCameraEvent(new Event
                {
                    type = EventType.KeyDown,
                    keyCode = KeyCode.F,
                    mousePosition = new Vector2(401f, 120f)
                }, new Rect(0f, 0f, 320f, 240f), false);

                Assert.That(preview.Camera.Pivot, Is.EqualTo(pivot));
            }
            finally
            {
                preview.Dispose();
                fixture.Dispose();
            }
        }

        [Test]
        public void Playback_ScrubWhilePlaying_ContinuesFromScrubbedPosition()
        {
            var temp = new TempFaceMotionAsset();
            try
            {
                var session = new FaceMotionEditorSession();
                var animations = new AnimationController(session);
                var playback = new PreviewPlaybackController(session);
                var project = FaceMotionProject.CreateNew();
                string path = temp.AssetPath("J2Scrub");
                AssetDatabase.CreateAsset(project, path);
                AssetDatabase.SaveAssets();
                session.SetActiveProject(project, path);
                Assert.That(animations.Add(), Is.Not.Null);
                session.GetSelectedAnimation().Timeline.Duration = 1f;

                playback.Play();
                playback.Tick(10d);
                session.SetCurrentTime(0.2f);
                playback.Tick(10.1d);

                Assert.That(playback.IsPlaying, Is.True);
                Assert.That(session.ViewState.CurrentTime, Is.EqualTo(0.3f).Within(1e-5f));
            }
            finally
            {
                temp.Dispose();
            }
        }
    }
}
