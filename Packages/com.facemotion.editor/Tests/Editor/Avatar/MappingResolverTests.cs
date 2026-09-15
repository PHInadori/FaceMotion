using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class MappingResolverTests
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
        public void Resolve_BlendShapeExactBinding_IsResolved()
        {
            AvatarIndex index = ScanStandard();
            var entry = BindingMappingEntry.CreateBlendShape("mouth.smile", AvatarFixture.FaceRendererPath, "Mouth_Smile");
            EntryResolution resolution = AvatarMappingResolver.Resolve(entry, index);
            Assert.That(resolution.Status, Is.EqualTo(MappingResolutionStatus.Resolved));
            Assert.That(resolution.Diagnostic, Is.Null);
        }

        [Test]
        public void Resolve_SameBlendShapeNameOnDifferentRenderer_IsIndependent()
        {
            AvatarIndex index = ScanStandard();
            var face = BindingMappingEntry.CreateBlendShape("face.smile", AvatarFixture.FaceRendererPath, "Smile");
            var cheek = BindingMappingEntry.CreateBlendShape("cheek.smile", AvatarFixture.CheekRendererPath, "Smile");
            Assert.That(AvatarMappingResolver.Resolve(face, index).Status, Is.EqualTo(MappingResolutionStatus.Resolved));
            Assert.That(AvatarMappingResolver.Resolve(cheek, index).Status, Is.EqualTo(MappingResolutionStatus.Resolved));
        }

        [Test]
        public void Resolve_MissingRenderer_IsMissingRenderer()
        {
            AvatarIndex index = ScanStandard();
            var entry = BindingMappingEntry.CreateBlendShape("ghost.smile", "Body/Ghost", "Smile");
            Assert.That(AvatarMappingResolver.Resolve(entry, index).Status,
                Is.EqualTo(MappingResolutionStatus.MissingRenderer));
        }

        [Test]
        public void Resolve_MissingBlendShape_IsMissingBlendShape()
        {
            AvatarIndex index = ScanStandard();
            var entry = BindingMappingEntry.CreateBlendShape("mouth.frown", AvatarFixture.FaceRendererPath, "Mouth_Frown");
            Assert.That(AvatarMappingResolver.Resolve(entry, index).Status,
                Is.EqualTo(MappingResolutionStatus.MissingBlendShape));
        }

        [Test]
        public void Resolve_TransformExactBinding_IsResolved()
        {
            AvatarIndex index = ScanStandard();
            var entry = BindingMappingEntry.CreateTransform("ear.pos", AvatarFixture.LeftEarPath);
            EntryResolution resolution = AvatarMappingResolver.Resolve(entry, index);
            Assert.That(resolution.Status, Is.EqualTo(MappingResolutionStatus.Resolved));
        }

        [Test]
        public void Resolve_MissingTransform_IsMissingTransform()
        {
            AvatarIndex index = ScanStandard();
            var entry = BindingMappingEntry.CreateTransform("boot.pos", "Armature/Boot");
            Assert.That(AvatarMappingResolver.Resolve(entry, index).Status,
                Is.EqualTo(MappingResolutionStatus.MissingTransform));
        }

        [Test]
        public void Resolve_AmbiguousTransform_IsAmbiguous()
        {
            _fixture = AvatarFixture.Create();
            _fixture.AddFork("Extra");
            _fixture.AddFork("Extra");
            AvatarIndex index = UnityAvatarScanner.Scan(_fixture.Root).Index;

            var entry = BindingMappingEntry.CreateTransform("fork.pos", AvatarFixture.HeadPath + "/Extra");
            EntryResolution resolution = AvatarMappingResolver.Resolve(entry, index);
            Assert.That(resolution.Status, Is.EqualTo(MappingResolutionStatus.Ambiguous));
            Assert.That(resolution.Diagnostic.Code, Is.EqualTo(FaceMotion.Diagnostics.FaceMotionDiagnosticCodes.AmbiguousBinding));
        }

        [Test]
        public void Resolve_NullEntry_IsInvalid()
        {
            AvatarIndex index = ScanStandard();
            EntryResolution resolution = AvatarMappingResolver.Resolve(null, index);
            Assert.That(resolution.Status, Is.EqualTo(MappingResolutionStatus.Invalid));
        }

        [Test]
        public void Resolve_NullIndex_IsInvalid()
        {
            var entry = BindingMappingEntry.CreateTransform("ear.pos", AvatarFixture.LeftEarPath);
            Assert.That(AvatarMappingResolver.Resolve(entry, null).Status, Is.EqualTo(MappingResolutionStatus.Invalid));
        }

        [Test]
        public void Evaluate_AllBindingsResolved_WithMatchingFingerprint_IsValid()
        {
            AvatarIndex index = ScanStandard();
            var profile = AvatarMappingProfile.CreateNew("Face");
            profile.SetAvatarFingerprint(index.Fingerprint);
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", AvatarFixture.FaceRendererPath, "Mouth_Smile"));
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("cheek.smile", AvatarFixture.CheekRendererPath, "Smile"));
            profile.AddMapping(BindingMappingEntry.CreateTransform("head.pos", AvatarFixture.HeadPath));

            MappingProfileEvaluation evaluation = AvatarMappingEvaluator.Evaluate(profile, index);
            Assert.That(evaluation.Status, Is.EqualTo(MappingProfileStatus.Valid));
            Assert.That(evaluation.FingerprintMatches, Is.True);
            Assert.That(evaluation.Resolutions.Count, Is.EqualTo(3));
        }

        [Test]
        public void Evaluate_StaleFingerprint_ButEveryBindingStillResolves_IsStaleButValid()
        {
            AvatarIndex index = ScanStandard();
            var profile = AvatarMappingProfile.CreateNew("Face");
            profile.SetAvatarFingerprint(index.Fingerprint);
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", AvatarFixture.FaceRendererPath, "Mouth_Smile"));
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("cheek.smile", AvatarFixture.CheekRendererPath, "Smile"));

            _fixture.Wrist.name = "WristRenamedNotBound";
            AvatarIndex changed = UnityAvatarScanner.Scan(_fixture.Root).Index;

            MappingProfileEvaluation evaluation = AvatarMappingEvaluator.Evaluate(profile, changed);
            Assert.That(evaluation.FingerprintMatches, Is.False);
            Assert.That(evaluation.Status, Is.EqualTo(MappingProfileStatus.StaleButValid));
        }

        [Test]
        public void Evaluate_StaleFingerprint_WithMissingBinding_IsStaleWithMissing()
        {
            AvatarIndex index = ScanStandard();
            var profile = AvatarMappingProfile.CreateNew("Face");
            profile.SetAvatarFingerprint(index.Fingerprint);
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", AvatarFixture.FaceRendererPath, "Mouth_Smile"));

            _fixture.Face.name = "FaceRenamed";
            AvatarIndex changed = UnityAvatarScanner.Scan(_fixture.Root).Index;

            MappingProfileEvaluation evaluation = AvatarMappingEvaluator.Evaluate(profile, changed);
            Assert.That(evaluation.FingerprintMatches, Is.False);
            Assert.That(evaluation.Status, Is.EqualTo(MappingProfileStatus.StaleWithMissing));
        }

        [Test]
        public void Evaluate_AmbiguousWithMatchingFingerprint_IsInvalid()
        {
            _fixture = AvatarFixture.Create();
            _fixture.AddFork("Extra");
            _fixture.AddFork("Extra");
            AvatarIndex index = UnityAvatarScanner.Scan(_fixture.Root).Index;

            var profile = AvatarMappingProfile.CreateNew("Face");
            profile.SetAvatarFingerprint(index.Fingerprint);
            profile.AddMapping(BindingMappingEntry.CreateTransform("fork.pos", AvatarFixture.HeadPath + "/Extra"));

            MappingProfileEvaluation evaluation = AvatarMappingEvaluator.Evaluate(profile, index);
            Assert.That(evaluation.FingerprintMatches, Is.True);
            Assert.That(evaluation.Status, Is.EqualTo(MappingProfileStatus.Invalid));
        }

        [Test]
        public void Evaluate_NullProfile_IsInvalid()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(AvatarMappingEvaluator.Evaluate(null, index).Status, Is.EqualTo(MappingProfileStatus.Invalid));
        }

        [Test]
        public void Evaluate_NullIndex_IsInvalid()
        {
            var profile = AvatarMappingProfile.CreateNew();
            Assert.That(AvatarMappingEvaluator.Evaluate(profile, null).Status, Is.EqualTo(MappingProfileStatus.Invalid));
        }
    }
}