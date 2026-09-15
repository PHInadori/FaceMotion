using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Phase I.2 coverage for the AvatarMappingProfile compatibility migration: additive
    /// schema upgrade, stable ID preservation, duplicate handling, malformed fingerprint
    /// recovery flags, blocking future/uninitialized/null states, and the asset-level
    /// MappingProfileMigrationService transaction boundary.
    /// </summary>
    public sealed class MappingProfileMigrationTests
    {
        private const string Folder = "Assets/__FaceMotionTests_I2";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_I2");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.Refresh();
            }
        }

        private static AvatarMappingProfile CreateLegacyProfile(string displayName = "Legacy")
        {
            var profile = AvatarMappingProfile.CreateNew(displayName);
            ReflectionUtil.SetField(profile, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            return profile;
        }

        private static BindingMappingEntry CreateEntry(string logicalTargetId)
        {
            return BindingMappingEntry.CreateBlendShape(logicalTargetId, "Body/Renderer", "Smile");
        }

        private static AvatarMappingProfileMigrationResult Migrate(AvatarMappingProfile source)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            var result = AvatarMappingProfileMigration.TryMigrate(source, out var migrated, diagnostics);
            return new AvatarMappingProfileMigrationResult(result, migrated, diagnostics);
        }

        private sealed class AvatarMappingProfileMigrationResult
        {
            public AvatarMappingProfileMigrationResult(MigrationResult result, AvatarMappingProfile migrated, List<FaceMotionDiagnostic> diagnostics)
            {
                Result = result;
                Migrated = migrated;
                Diagnostics = diagnostics;
            }

            public MigrationResult Result { get; }

            public AvatarMappingProfile Migrated { get; }

            public List<FaceMotionDiagnostic> Diagnostics { get; }
        }

        [Test]
        public void MigrateLegacyProfile_UpgradesAndPreservesValidIds()
        {
            var source = CreateLegacyProfile();
            var entry = CreateEntry("idle.blink");
            source.AddMapping(entry);
            string profileId = source.ProfileId;
            string entryId = entry.EntryId;

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(result.Result.Blocked, Is.False);
            Assert.That(result.Migrated.SchemaVersion, Is.EqualTo(FaceMotionVersions.MappingProfileSchemaVersion));
            Assert.That(result.Migrated.ProfileId, Is.EqualTo(profileId));
            Assert.That(result.Migrated.Mappings[0].EntryId, Is.EqualTo(entryId));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.UpgradedSchema), Is.Not.Null);
            Assert.That(source.SchemaVersion, Is.EqualTo(FaceMotionVersions.LegacySchemaVersion), "The source must never be mutated.");
        }

        [Test]
        public void MigrateLegacyProfile_MissingProfileAndEntryIds_AreRepaired()
        {
            var source = CreateLegacyProfile();
            var entry = CreateEntry("idle.blink");
            source.AddMapping(entry);
            ReflectionUtil.SetField(source, "_profileId", string.Empty);
            ReflectionUtil.SetField(entry, "_entryId", string.Empty);

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(StableId.IsValid(result.Migrated.ProfileId), Is.True);
            Assert.That(StableId.IsValid(result.Migrated.Mappings[0].EntryId), Is.True);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.IdRepaired), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacyProfile_MalformedEntryIds_AreRegenerated()
        {
            var source = CreateLegacyProfile();
            var entry = CreateEntry("idle.blink");
            source.AddMapping(entry);
            ReflectionUtil.SetField(entry, "_entryId", "not-a-stable-id");

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(StableId.IsValid(result.Migrated.Mappings[0].EntryId), Is.True);
            Assert.That(result.Migrated.Mappings[0].EntryId, Is.Not.EqualTo("not-a-stable-id"));
        }

        [Test]
        public void MigrateLegacyProfile_DuplicateEntryId_FirstKeepsItsId()
        {
            var source = CreateLegacyProfile();
            var first = CreateEntry("idle.blink");
            var second = CreateEntry("idle.frown");
            source.AddMapping(first);
            source.AddMapping(second);
            ReflectionUtil.SetField(second, "_entryId", first.EntryId);

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(result.Migrated.Mappings[0].EntryId, Is.EqualTo(first.EntryId));
            Assert.That(result.Migrated.Mappings[1].EntryId, Is.Not.EqualTo(first.EntryId));
            Assert.That(StableId.IsValid(result.Migrated.Mappings[1].EntryId), Is.True);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.DuplicateId), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacyProfile_NullMappingEntries_AreRemoved()
        {
            var source = CreateLegacyProfile();
            source.AddMapping(CreateEntry("idle.blink"));
            var withNulls = new List<BindingMappingEntry> { CreateEntry("idle.frown"), null };
            ReflectionUtil.SetField(source, "_mappings", withNulls);

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(result.Migrated.Mappings.Count, Is.EqualTo(1));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.PartialRecovery), Is.Not.Null);
        }

        [Test]
        public void MigrateProfile_CurrentSchema_IsANoOpThatReturnsAClone()
        {
            var source = AvatarMappingProfile.CreateNew("Current");
            var entry = CreateEntry("idle.blink");
            source.AddMapping(entry);
            string profileId = source.ProfileId;
            string entryId = entry.EntryId;

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.False);
            Assert.That(result.Result.Blocked, Is.False);
            Assert.That(result.Migrated, Is.Not.SameAs(source));
            Assert.That(result.Migrated.ProfileId, Is.EqualTo(profileId));
            Assert.That(result.Migrated.Mappings[0].EntryId, Is.EqualTo(entryId));
            Assert.That(result.Migrated.Mappings[0].LogicalTargetId, Is.EqualTo("idle.blink"));
        }

        [Test]
        public void MigrateProfile_FutureSchema_IsBlocking()
        {
            var source = AvatarMappingProfile.CreateNew("Future");
            ReflectionUtil.SetField(source, "_schemaVersion", FaceMotionVersions.MappingProfileSchemaVersion + 1);

            var result = Migrate(source);

            Assert.That(result.Result.Blocked, Is.True);
            Assert.That(result.Migrated, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.FutureProfileSchema), Is.Not.Null);
        }

        [Test]
        public void MigrateProfile_UninitializedSchema_IsBlocking()
        {
            var source = AvatarMappingProfile.CreateNew("Uninitialized");
            ReflectionUtil.SetField(source, "_schemaVersion", -1);

            var result = Migrate(source);

            Assert.That(result.Result.Blocked, Is.True);
            Assert.That(result.Migrated, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.UninitializedProfileSchema), Is.Not.Null);
        }

        [Test]
        public void MigrateProfile_NullProfile_IsBlocking()
        {
            var result = Migrate(null);

            Assert.That(result.Result.Blocked, Is.True);
            Assert.That(result.Migrated, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.NullMappingProfile), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacyProfile_MalformedStoredFingerprint_IsPreservedWithAWarning()
        {
            var source = CreateLegacyProfile();
            var fingerprint = AvatarFingerprint.Create(1, 1, new string('a', AvatarFingerprint.HashLength));
            ReflectionUtil.SetField(fingerprint, "_hash", "not-a-hexhash");
            ReflectionUtil.SetField(source, "_avatarFingerprint", fingerprint);

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(result.Result.Blocked, Is.False);
            Assert.That(result.Migrated.AvatarFingerprint, Is.Not.Null);
            Assert.That(result.Migrated.AvatarFingerprint.Hash, Is.EqualTo("not-a-hexhash"));
            Assert.That(result.Migrated.AvatarFingerprint.IsValid, Is.False);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.PartialRecovery), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacyProfile_NullMappingsList_IsInitialized()
        {
            var source = CreateLegacyProfile();
            ReflectionUtil.SetField(source, "_mappings", (List<BindingMappingEntry>)null);

            var result = Migrate(source);

            Assert.That(result.Result.Applied, Is.True);
            Assert.That(result.Migrated.Mappings, Is.Empty);
        }

        [Test]
        public void MigrateLegacyProfile_IsIdempotent_AcrossMigrations()
        {
            var source = CreateLegacyProfile();
            var entry = CreateEntry("idle.blink");
            source.AddMapping(entry);
            string repairedProfileId;
            string repairedEntryId;

            var first = Migrate(source);
            Assert.That(first.Result.Applied, Is.True);
            repairedProfileId = first.Migrated.ProfileId;
            repairedEntryId = first.Migrated.Mappings[0].EntryId;

            var second = Migrate(first.Migrated);

            Assert.That(second.Result.Applied, Is.False, "The current schema is a content no-op.");
            Assert.That(second.Migrated.ProfileId, Is.EqualTo(repairedProfileId));
            Assert.That(second.Migrated.Mappings[0].EntryId, Is.EqualTo(repairedEntryId));
        }

        [Test]
        public void Service_LegacyProfile_CommitsTheMigrationToTheAsset()
        {
            var profile = ScriptableObject.CreateInstance<AvatarMappingProfile>();
            ReflectionUtil.SetField(profile, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            ReflectionUtil.SetField(profile, "_profileId", string.Empty);
            profile.AddMapping(CreateEntry("idle.blink"));
            string path = Folder + "/legacy-profile.asset";
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            var result = MappingProfileMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(path));

            Assert.That(result.Applied, Is.True);
            Assert.That(result.Blocked, Is.False);
            var reloaded = AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(path);
            Assert.That(reloaded.SchemaVersion, Is.EqualTo(FaceMotionVersions.MappingProfileSchemaVersion));
            Assert.That(StableId.IsValid(reloaded.ProfileId), Is.True);
            Assert.That(StableId.IsValid(reloaded.Mappings[0].EntryId), Is.True);
            Assert.That(reloaded.Mappings[0].LogicalTargetId, Is.EqualTo("idle.blink"));
        }

        [Test]
        public void Service_FutureProfile_IsRefusedAndNeverWritten()
        {
            var profile = ScriptableObject.CreateInstance<AvatarMappingProfile>();
            ReflectionUtil.SetField(profile, "_schemaVersion", FaceMotionVersions.MappingProfileSchemaVersion + 1);
            ReflectionUtil.SetField(profile, "_profileId", StableId.New());
            string path = Folder + "/future-profile.asset";
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            var result = MappingProfileMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(path));

            Assert.That(result.Blocked, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(path).SchemaVersion, Is.EqualTo(FaceMotionVersions.MappingProfileSchemaVersion + 1));
        }

        [Test]
        public void Service_CurrentProfile_IsANoOp()
        {
            var profile = AvatarMappingProfile.CreateNew("Current");
            profile.AddMapping(CreateEntry("idle.blink"));
            string path = Folder + "/current-profile.asset";
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            var result = MappingProfileMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(path));

            Assert.That(result.Applied, Is.False);
            Assert.That(result.Blocked, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(path).SchemaVersion, Is.EqualTo(FaceMotionVersions.MappingProfileSchemaVersion));
        }
    }
}