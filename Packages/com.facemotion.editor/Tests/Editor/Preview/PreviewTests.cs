using FaceMotion.Data;
using FaceMotion.Editor.Preview;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class PreviewTests
    {
        private AvatarFixture _fixture;

        [TearDown]
        public void TearDown()
        {
            _fixture?.Dispose();
            _fixture = null;
        }

        [Test]
        public void MotionApplier_UsesTimelineValuesAndRestoresBaseline()
        {
            _fixture = AvatarFixture.Create();
            _fixture.FaceRenderer.SetBlendShapeWeight(0, 12f);
            _fixture.Head.localPosition = new Vector3(1f, 2f, 3f);
            FaceMotionAnimationData animation = CreateAnimation();
            var cache = new PreviewObjectCache(_fixture.Root);
            var baseline = new PreviewBaseline();

            PreviewMotionApplier.Apply(animation, cache, baseline, 0f);

            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(75f));
            Assert.That(_fixture.Head.localPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));

            baseline.Restore();

            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(12f));
            Assert.That(_fixture.Head.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void SceneApplySession_DisposeRestoresSceneAvatar()
        {
            _fixture = AvatarFixture.Create();
            _fixture.FaceRenderer.SetBlendShapeWeight(0, 20f);
            FaceMotionAnimationData animation = CreateAnimation();
            var sceneApply = new SceneApplySession();

            sceneApply.Start(_fixture.Root);
            sceneApply.Apply(animation, 0f);
            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(75f));

            sceneApply.Dispose();
            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(20f));
        }

        [Test]
        public void SceneApplySession_ReplacingAvatarRestoresPreviousAvatar()
        {
            _fixture = AvatarFixture.Create();
            _fixture.FaceRenderer.SetBlendShapeWeight(0, 20f);
            var other = AvatarFixture.Create();
            other.FaceRenderer.SetBlendShapeWeight(0, 30f);
            var sceneApply = new SceneApplySession();

            sceneApply.Start(_fixture.Root);
            sceneApply.Apply(CreateAnimation(), 0f);
            sceneApply.Start(other.Root);

            Assert.That(_fixture.FaceRenderer.GetBlendShapeWeight(0), Is.EqualTo(20f));
            Assert.That(sceneApply.Root, Is.SameAs(other.Root));
            sceneApply.Dispose();
            other.Dispose();
        }

        [Test]
        public void PreviewAvatarClone_DoesNotUseSourceObjectAndCleansUp()
        {
            _fixture = AvatarFixture.Create();
            var clone = new PreviewAvatarClone(_fixture.Root);

            Assert.That(clone.Root, Is.Not.SameAs(_fixture.Root));
            Assert.That(clone.Root.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));

            clone.Dispose();
            Assert.That(clone.Root, Is.Null);
        }

        private static FaceMotionAnimationData CreateAnimation()
        {
            FaceMotionAnimationData animation = FaceMotionAnimationData.Create("Preview");
            FaceTrackData blendShape = FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            blendShape.BlendShape.AddKey(FloatKeyframeData.Create(0f, 75f));
            animation.Timeline.AddTrack(blendShape);

            FaceTrackData position = FaceTrackData.CreateTransform(TrackKind.TransformPosition, AvatarFixture.HeadPath);
            position.Transform.AddKey(Vector3KeyframeData.Create(0f, new Vector3(4f, 5f, 6f)));
            animation.Timeline.AddTrack(position);
            return animation;
        }
    }
}
