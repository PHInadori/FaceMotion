using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using FaceMotion.Timeline;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class TrackBindingValidationTests
    {
        [TearDown]
        public void TearDown()
        {
            if (_fixture != null)
            {
                _fixture.Dispose();
                _fixture = null;
            }
        }

        private AvatarFixture _fixture;

        private AvatarIndex ScanStandard()
        {
            _fixture = AvatarFixture.Create();
            return UnityAvatarScanner.Scan(_fixture.Root).Index;
        }

        [Test]
        public void Validate_BoundBlendShapeTrack_IsResolved()
        {
            AvatarIndex index = ScanStandard();
            var animation = FaceMotionAnimationData.Create("Face");
            animation.Timeline.AddTrack(FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Smile"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            IReadOnlyList<TrackBindingValidation> results = AvatarTrackBindingValidator.ValidateTimeline(project, index);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Status, Is.EqualTo(MappingResolutionStatus.Resolved));
            Assert.That(results[0].Diagnostic, Is.Null);
        }

        [Test]
        public void Validate_BoundTransformTracks_AreResolved()
        {
            AvatarIndex index = ScanStandard();
            var animation = FaceMotionAnimationData.Create("Motion");
            animation.Timeline.AddTrack(FaceTrackData.CreateTransform(TrackKind.TransformPosition, AvatarFixture.HeadPath));
            animation.Timeline.AddTrack(FaceTrackData.CreateTransform(TrackKind.TransformRotation, AvatarFixture.HeadPath));
            animation.Timeline.AddTrack(FaceTrackData.CreateTransform(TrackKind.TransformScale, AvatarFixture.HeadPath));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            IReadOnlyList<TrackBindingValidation> results = AvatarTrackBindingValidator.ValidateTimeline(project, index);
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Status, Is.EqualTo(MappingResolutionStatus.Resolved));
            Assert.That(results[1].Status, Is.EqualTo(MappingResolutionStatus.Resolved));
            Assert.That(results[2].Status, Is.EqualTo(MappingResolutionStatus.Resolved));
        }

        [Test]
        public void Validate_MissingRendererTrack_IsMissingRenderer()
        {
            AvatarIndex index = ScanStandard();
            var animation = FaceMotionAnimationData.Create("Ghost");
            animation.Timeline.AddTrack(FaceTrackData.CreateBlendShape("Body/Ghost", "Smile"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            Assert.That(AvatarTrackBindingValidator.ValidateTimeline(project, index)[0].Status,
                Is.EqualTo(MappingResolutionStatus.MissingRenderer));
        }

        [Test]
        public void Validate_MissingBlendShapeTrack_IsMissingBlendShape()
        {
            AvatarIndex index = ScanStandard();
            var animation = FaceMotionAnimationData.Create("Frown");
            animation.Timeline.AddTrack(FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Frown"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            Assert.That(AvatarTrackBindingValidator.ValidateTimeline(project, index)[0].Status,
                Is.EqualTo(MappingResolutionStatus.MissingBlendShape));
        }

        [Test]
        public void Validate_MissingTransformTrack_IsMissingTransform()
        {
            AvatarIndex index = ScanStandard();
            var animation = FaceMotionAnimationData.Create("Boot");
            animation.Timeline.AddTrack(FaceTrackData.CreateTransform(TrackKind.TransformPosition, "Armature/Boot"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            Assert.That(AvatarTrackBindingValidator.ValidateTimeline(project, index)[0].Status,
                Is.EqualTo(MappingResolutionStatus.MissingTransform));
        }

        [Test]
        public void Validate_AmbiguousTransformTrack_IsAmbiguous()
        {
            _fixture = AvatarFixture.Create();
            _fixture.AddFork("Extra");
            _fixture.AddFork("Extra");
            AvatarIndex index = UnityAvatarScanner.Scan(_fixture.Root).Index;

            var animation = FaceMotionAnimationData.Create("Fork");
            animation.Timeline.AddTrack(FaceTrackData.CreateTransform(TrackKind.TransformPosition, AvatarFixture.HeadPath + "/Extra"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            Assert.That(AvatarTrackBindingValidator.ValidateTimeline(project, index)[0].Status,
                Is.EqualTo(MappingResolutionStatus.Ambiguous));
        }

        [Test]
        public void Validate_NullIndex_ReturnsEmpty()
        {
            var animation = FaceMotionAnimationData.Create("Face");
            animation.Timeline.AddTrack(FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Smile"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            Assert.That(AvatarTrackBindingValidator.ValidateTimeline(project, null).Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_DoesNotMutateTheProject()
        {
            AvatarIndex index = ScanStandard();
            var animation = FaceMotionAnimationData.Create("Face");
            animation.Timeline.AddTrack(FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Smile"));
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);
            string pathBefore = project.Animations[0].Timeline.Tracks[0].BlendShape.RendererPath;
            string nameBefore = project.Animations[0].Timeline.Tracks[0].BlendShape.BlendShapeName;

            AvatarTrackBindingValidator.ValidateTimeline(project, index);

            Assert.That(project.Animations[0].Timeline.Tracks[0].BlendShape.RendererPath, Is.EqualTo(pathBefore));
            Assert.That(project.Animations[0].Timeline.Tracks[0].BlendShape.BlendShapeName, Is.EqualTo(nameBefore));
        }
    }
}