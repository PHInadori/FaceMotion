using FaceMotion.Avatar;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class MappingProfileTests
    {
        [Test]
        public void CreateNew_ProducesValidProfile()
        {
            var profile = AvatarMappingProfile.CreateNew("My Avatar");
            Assert.That(profile.ProfileId, Is.Not.Null);
            Assert.That(AvatarMappingProfileValidator.Validate(profile).HasBlocking, Is.False);
            Assert.That(profile.SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void Validate_NullProfile_IsBlocking()
        {
            Assert.That(AvatarMappingProfileValidator.Validate(null).HasBlocking, Is.True);
        }

        [Test]
        public void Validate_InvalidProfileId_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            ReflectionUtil.SetField(profile, "_profileId", "not-a-stable-id");
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.InvalidProfileId), Is.Not.Null);
        }

        [Test]
        public void Validate_FutureSchema_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            ReflectionUtil.SetField(profile, "_schemaVersion", 999);
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.FutureProfileSchema), Is.Not.Null);
        }

        [Test]
        public void Validate_UninitializedSchema_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            ReflectionUtil.SetField(profile, "_schemaVersion", 0);
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.UninitializedProfileSchema), Is.Not.Null);
        }

        [Test]
        public void Validate_DuplicateEntryId_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            var first = BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile");
            profile.AddMapping(first);
            profile.AddMapping(first.Duplicate(first.EntryId));
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.DuplicateEntryId), Is.Not.Null);
        }

        [Test]
        public void Validate_MissingLogicalTargetId_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            var entry = BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile");
            ReflectionUtil.SetField(entry, "_logicalTargetId", string.Empty);
            profile.AddMapping(entry);
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.MissingLogicalTargetId), Is.Not.Null);
        }

        [Test]
        public void Validate_DuplicateLogicalTarget_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile"));
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Cheek", "Smile"));
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.DuplicateLogicalTarget), Is.Not.Null);
        }

        [Test]
        public void Validate_MissingBinding_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            var entry = BindingMappingEntry.CreateTransform("head.rotate", "Armature/Hips");
            ReflectionUtil.SetField(entry, "_transformPath", string.Empty);
            profile.AddMapping(entry);
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.MissingBinding), Is.Not.Null);
        }

        [Test]
        public void Validate_InvalidFingerprint_IsBlocking()
        {
            var profile = AvatarMappingProfile.CreateNew();
            var fingerprint = AvatarFingerprint.Create(1, 1, new string('a', 64));
            ReflectionUtil.SetField(fingerprint, "_hash", "ZZ");
            ReflectionUtil.SetField(profile, "_avatarFingerprint", fingerprint);
            Assert.That(Find(profile, FaceMotionDiagnosticCodes.InvalidFingerprint), Is.Not.Null);
        }

        [Test]
        public void Clone_PreservesEveryId()
        {
            var profile = AvatarMappingProfile.CreateNew("Source");
            var fingerprint = AvatarFingerprint.Create(1, 1, new string('b', 64));
            profile.SetAvatarFingerprint(fingerprint);
            var entry = BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile");
            profile.AddMapping(entry);

            var clone = AvatarMappingProfile.CreateClone(profile);
            Assert.That(clone.ProfileId, Is.EqualTo(profile.ProfileId));
            Assert.That(clone.DisplayName, Is.EqualTo("Source"));
            Assert.That(clone.AvatarFingerprint.EqualsValue(fingerprint), Is.True);
            Assert.That(clone.Mappings.Count, Is.EqualTo(1));
            Assert.That(clone.Mappings[0].EntryId, Is.EqualTo(entry.EntryId));
            Assert.That(clone.Mappings[0].LogicalTargetId, Is.EqualTo("mouth.smile"));
            Assert.That(clone.Mappings[0].BlendShape.Value.BlendShapeName, Is.EqualTo("Mouth_Smile"));
        }

        [Test]
        public void Duplicate_AssignsFreshProfileAndEntryIds()
        {
            var profile = AvatarMappingProfile.CreateNew("Source");
            var fingerprint = AvatarFingerprint.Create(1, 1, new string('c', 64));
            profile.SetAvatarFingerprint(fingerprint);
            var entry = BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile");
            profile.AddMapping(entry);

            var duplicate = AvatarMappingProfile.CreateDuplicate(profile);
            Assert.That(duplicate.ProfileId, Is.Not.EqualTo(profile.ProfileId));
            Assert.That(duplicate.Mappings.Count, Is.EqualTo(1));
            Assert.That(duplicate.Mappings[0].EntryId, Is.Not.EqualTo(entry.EntryId));
            Assert.That(duplicate.Mappings[0].LogicalTargetId, Is.EqualTo("mouth.smile"));
            Assert.That(duplicate.AvatarFingerprint.EqualsValue(fingerprint), Is.True);
            Assert.That(duplicate.DisplayName, Is.EqualTo("Source"));
        }

        [Test]
        public void RoundTrip_PreservesProfileAndEntries()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var profile = AvatarMappingProfile.CreateNew("RoundTrip");
                profile.SetAvatarFingerprint(AvatarFingerprint.Create(1, 1, new string('d', 64)));
                profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile"));
                profile.AddMapping(BindingMappingEntry.CreateTransform("head.rotate", "Armature/Hips/Spine/Head"));

                string assetPath = temp.AssetPath("profile");
                MappingProfilePersistence.CreateAsset(profile, assetPath);
                var loaded = MappingProfilePersistence.ReloadCopy(profile, temp.AssetPath("profile_copy"));

                Assert.That(loaded.ProfileId, Is.EqualTo(profile.ProfileId));
                Assert.That(loaded.SchemaVersion, Is.EqualTo(1));
                Assert.That(loaded.DisplayName, Is.EqualTo("RoundTrip"));
                Assert.That(loaded.AvatarFingerprint.EqualsValue(profile.AvatarFingerprint), Is.True);
                Assert.That(loaded.Mappings.Count, Is.EqualTo(2));
                Assert.That(loaded.Mappings[0].EntryId, Is.EqualTo(profile.Mappings[0].EntryId));
                Assert.That(loaded.Mappings[0].LogicalTargetId, Is.EqualTo("mouth.smile"));
                Assert.That(loaded.Mappings[0].BlendShape.Value.RendererPath, Is.EqualTo("Body/Face"));
                Assert.That(loaded.Mappings[0].BlendShape.Value.BlendShapeName, Is.EqualTo("Mouth_Smile"));
                Assert.That(loaded.Mappings[1].Transform.Value.TransformPath, Is.EqualTo("Armature/Hips/Spine/Head"));
                Assert.That(AvatarMappingProfileValidator.Validate(loaded).HasBlocking, Is.False);
            }
        }

        [Test]
        public void NormalizeStructure_RemovesNullEntries()
        {
            var profile = AvatarMappingProfile.CreateNew();
            profile.AddMapping(BindingMappingEntry.CreateBlendShape("mouth.smile", "Body/Face", "Mouth_Smile"));
            ReflectionUtil.SetField(profile, "_mappings", null);
            profile.NormalizeStructure();
            Assert.That(profile.Mappings.Count, Is.EqualTo(0));
        }

        private static FaceMotionDiagnostic Find(
            AvatarMappingProfile profile,
            string code)
        {
            var report = AvatarMappingProfileValidator.Validate(profile);
            return TestHelpers.FindDiagnostic(report.Diagnostics, code);
        }
    }
}