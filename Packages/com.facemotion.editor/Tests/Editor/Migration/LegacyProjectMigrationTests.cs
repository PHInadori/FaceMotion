using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Serialization;
using FaceMotion.Serialization;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Phase I.2 coverage for the legacy schema-0 -> current project migration: stable ID
    /// preservation and repair, duplicate handling, numeric corruption recovery, null and
    /// malformed entry handling, provenance preservation, blocking unsafe data, idempotency,
    /// and the asset-level ProjectMigrationService transaction boundary.
    /// </summary>
    public sealed class LegacyProjectMigrationTests
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

        private static ProjectMigrationResult Migrate(FaceMotionProject source)
        {
            return new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[]
            {
                new ProjectLegacyMigrator(), new ProjectV1ToV2Migrator()
            }).Migrate(source);
        }

        private static FaceMotionProject CreateLegacyProject()
        {
            var project = FaceMotionProject.CreateNew();
            ReflectionUtil.SetField(project, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            return project;
        }

        private static FaceTrackData CreateLegacyBlendTrack(string rendererPath = "Body/Renderer", string blendShapeName = "Smile")
        {
            var track = FaceTrackData.CreateBlendShape(rendererPath, blendShapeName);
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            return track;
        }

        [Test]
        public void NewProject_UsesCurrentSchemaV2()
        {
            var project = FaceMotionProject.CreateNew();
            var track = FaceTrackData.CreateBlendShape("Face", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 20f, InterpolationType.Linear, KeyOrigin.Baseline));
            Assert.That(track.BlendShape.Keys[0].Origin.IsBaseline, Is.True);
            Assert.That(project.SchemaVersion, Is.EqualTo(2), "A project able to persist Baseline=5 must identify its new schema.");
        }

        [Test]
        public void Schema1Project_MigratesToV2WithoutChangingExistingKeys()
        {
            var project = FaceMotionProject.CreateNew();
            ReflectionUtil.SetField(project, "_schemaVersion", 1);
            var animation = FaceMotionAnimationData.Create("Published v1");
            var blend = FaceTrackData.CreateBlendShape("Face", "Smile");
            var position = FaceTrackData.CreateTransform(TrackKind.TransformPosition, "Jaw");
            for (int kind = 0; kind <= 4; kind++)
            {
                var origin = KeyOrigin.Manual;
                if (kind > 0)
                {
                    var record = GenerationRecord.Create(animation.AnimationId,
                        (GeneratorType)(kind - 1), "preset", 1, "hash");
                    project.AddGenerationRecord(record);
                    origin = new KeyOrigin((OriginKind)kind, record.GenerationId);
                }

                float time = kind * 0.2f;
                blend.BlendShape.AddKey(FloatKeyframeData.Create(time, 10f + kind * 5f,
                    InterpolationType.EaseIn, origin));
                position.Transform.AddKey(Vector3KeyframeData.Create(time,
                    new Vector3(kind, kind + 1, kind + 2), InterpolationType.Hold, origin));
            }
            animation.Timeline.AddTrack(blend);
            animation.Timeline.AddTrack(position);
            project.AddAnimation(animation);
            string path = Folder + "/schema-v1.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            // Compare the saved v1 asset, not an unsaved in-memory key whose null generation
            // ID Unity normalizes to an empty string during ordinary serialization.
            var before = FaceMotionProject.CreateClone(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));
            try
            {
                var result = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path);

                Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
                Assert.That(loaded.SchemaVersion, Is.EqualTo(2));
                Assert.That(loaded.ProjectId, Is.EqualTo(before.ProjectId));
                Assert.That(loaded.Animations[0].AnimationId, Is.EqualTo(before.Animations[0].AnimationId));
                Assert.That(loaded.Generations.Count, Is.EqualTo(before.Generations.Count));
                for (int trackIndex = 0; trackIndex < 2; trackIndex++)
                    Assert.That(loaded.Animations[0].Timeline.Tracks[trackIndex].TrackId,
                        Is.EqualTo(before.Animations[0].Timeline.Tracks[trackIndex].TrackId));
                for (int i = 0; i < 5; i++)
                {
                    var oldFloat = before.Animations[0].Timeline.Tracks[0].BlendShape.Keys[i];
                    var newFloat = loaded.Animations[0].Timeline.Tracks[0].BlendShape.Keys[i];
                    Assert.That(newFloat.KeyId, Is.EqualTo(oldFloat.KeyId));
                    Assert.That(newFloat.Time, Is.EqualTo(oldFloat.Time));
                    Assert.That(newFloat.Value, Is.EqualTo(oldFloat.Value));
                    Assert.That(newFloat.Interpolation, Is.EqualTo(oldFloat.Interpolation));
                    Assert.That(newFloat.Origin.Kind, Is.EqualTo(oldFloat.Origin.Kind));
                    // Unity's asset overwrite can turn an absent manual GenerationId from
                    // null into empty. Both represent "no generation"; populated IDs must match.
                    Assert.That(newFloat.Origin.GenerationId ?? string.Empty,
                        Is.EqualTo(oldFloat.Origin.GenerationId ?? string.Empty));
                    Assert.That(newFloat.Origin.IsBaseline, Is.False);

                    var oldVector = before.Animations[0].Timeline.Tracks[1].Transform.Keys[i];
                    var newVector = loaded.Animations[0].Timeline.Tracks[1].Transform.Keys[i];
                    Assert.That(newVector.KeyId, Is.EqualTo(oldVector.KeyId));
                    Assert.That(newVector.Time, Is.EqualTo(oldVector.Time));
                    Assert.That(newVector.Value, Is.EqualTo(oldVector.Value));
                    Assert.That(newVector.Interpolation, Is.EqualTo(oldVector.Interpolation));
                    Assert.That(newVector.Origin.Kind, Is.EqualTo(oldVector.Origin.Kind));
                    Assert.That(newVector.Origin.GenerationId ?? string.Empty,
                        Is.EqualTo(oldVector.Origin.GenerationId ?? string.Empty));
                    Assert.That(newVector.Origin.IsBaseline, Is.False);
                }
                Assert.That(loaded.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Time, Is.Zero);
                Assert.That(loaded.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Origin.Kind,
                    Is.EqualTo(OriginKind.Manual), "A v1 key at zero is not a generated baseline.");
            }
            finally
            {
                Object.DestroyImmediate(before);
            }
        }

        [TestCase(OriginKind.Manual)]
        [TestCase(OriginKind.Preset)]
        public void Schema1Migration_DoesNotGuessBaseline(OriginKind oldKind)
        {
            var project = FaceMotionProject.CreateNew();
            ReflectionUtil.SetField(project, "_schemaVersion", 1);
            var animation = FaceMotionAnimationData.Create("Old zero key");
            var track = FaceTrackData.CreateBlendShape("Face", "Smile");
            string generationId = null;
            if (oldKind == OriginKind.Preset)
            {
                var record = GenerationRecord.Create(animation.AnimationId, GeneratorType.Preset, "preset", 1, "hash");
                project.AddGenerationRecord(record);
                generationId = record.GenerationId;
            }
            var key = FloatKeyframeData.Create(0f, 37f, InterpolationType.Smooth,
                new KeyOrigin(oldKind, generationId));
            track.BlendShape.AddKey(key);
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var migrated = Migrate(project);

            Assert.That(migrated.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            var preserved = migrated.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0];
            Assert.That(preserved.KeyId, Is.EqualTo(key.KeyId));
            Assert.That(preserved.Time, Is.Zero);
            Assert.That(preserved.Value, Is.EqualTo(37f));
            Assert.That(preserved.Interpolation, Is.EqualTo(InterpolationType.Smooth));
            Assert.That(preserved.Origin.Kind, Is.EqualTo(oldKind));
            Assert.That(preserved.Origin.GenerationId, Is.EqualTo(generationId));
            Assert.That(preserved.Origin.IsBaseline, Is.False);
        }

        [Test]
        public void Schema0Project_ChainsThroughLegacyAndV2()
        {
            var project = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy chain");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            project.AddAnimation(animation);
            var legacy = new ProjectLegacyMigrator();
            var v2 = new ProjectV1ToV2Migrator();

            var migrated = Migrate(project);

            Assert.That(legacy.FromVersion, Is.Zero);
            Assert.That(legacy.ToVersion, Is.EqualTo(1));
            Assert.That(v2.FromVersion, Is.EqualTo(legacy.ToVersion));
            Assert.That(v2.ToVersion, Is.EqualTo(2));
            Assert.That(migrated.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            Assert.That(migrated.Project.SchemaVersion, Is.EqualTo(2));
            Assert.That(migrated.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId,
                Is.EqualTo(project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId));
            Assert.That(project.SchemaVersion, Is.Zero);
        }

        [Test]
        public void MigrationToV2_IsIdempotent()
        {
            var project = FaceMotionProject.CreateNew();
            ReflectionUtil.SetField(project, "_schemaVersion", 1);
            var animation = FaceMotionAnimationData.Create("Old project");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            project.AddAnimation(animation);

            var first = Migrate(project);
            var second = Migrate(first.Project);

            Assert.That(first.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            Assert.That(second.Status, Is.EqualTo(ProjectMigrationStatus.NoMigrationNeeded));
            Assert.That(second.Project.SchemaVersion, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(second.Project), Is.EqualTo(JsonUtility.ToJson(first.Project)));
            Assert.That(project.SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void MigrateLegacy_CurrentSkeleton_UpgradesAndPreservesValidIds()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = CreateLegacyBlendTrack();
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            string projectId = source.ProjectId;
            string animationId = animation.AnimationId;
            string trackId = track.TrackId;
            string keyId = track.BlendShape.Keys[0].KeyId;

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            Assert.That(result.Project.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion));
            Assert.That(result.Project.ProjectId, Is.EqualTo(projectId));
            Assert.That(result.Project.Animations[0].AnimationId, Is.EqualTo(animationId));
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].TrackId, Is.EqualTo(trackId));
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId, Is.EqualTo(keyId));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.UpgradedSchema), Is.Not.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.IdRepaired), Is.Null);
            Assert.That(source.SchemaVersion, Is.EqualTo(FaceMotionVersions.LegacySchemaVersion), "The source must never be mutated.");
        }

        [Test]
        public void MigrateLegacy_MissingProjectId_GeneratesStableId()
        {
            var source = CreateLegacyProject();
            ReflectionUtil.SetField(source, "_projectId", string.Empty);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(StableId.IsValid(result.Project.ProjectId), Is.True);
            Assert.That(result.Project.ProjectId, Is.Not.EqualTo(source.ProjectId));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.IdRepaired), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_MissingAnimationTrackAndKeyIds_AreGenerated()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = CreateLegacyBlendTrack();
            var key = track.BlendShape.Keys[0];
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            ReflectionUtil.SetField(animation, "_animationId", string.Empty);
            ReflectionUtil.SetField(track, "_trackId", string.Empty);
            ReflectionUtil.SetField(key, "_keyId", string.Empty);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            var migrated = result.Project;
            Assert.That(StableId.IsValid(migrated.Animations[0].AnimationId), Is.True);
            Assert.That(StableId.IsValid(migrated.Animations[0].Timeline.Tracks[0].TrackId), Is.True);
            Assert.That(StableId.IsValid(migrated.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId), Is.True);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.IdRepaired), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_DuplicateAnimationId_FirstKeepsItsId()
        {
            var source = CreateLegacyProject();
            var first = FaceMotionAnimationData.Create("First");
            var second = FaceMotionAnimationData.Create("Second");
            first.Timeline.AddTrack(CreateLegacyBlendTrack());
            second.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(first);
            source.AddAnimation(second);
            string sharedId = first.AnimationId;
            ReflectionUtil.SetField(second, "_animationId", sharedId);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].AnimationId, Is.EqualTo(sharedId));
            Assert.That(result.Project.Animations[1].AnimationId, Is.Not.EqualTo(sharedId));
            Assert.That(StableId.IsValid(result.Project.Animations[1].AnimationId), Is.True);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.DuplicateId), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_DuplicateTrackId_FirstKeepsItsId()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var first = CreateLegacyBlendTrack("Body/A", "Smile");
            var second = CreateLegacyBlendTrack("Body/B", "Frown");
            animation.Timeline.AddTrack(first);
            animation.Timeline.AddTrack(second);
            source.AddAnimation(animation);
            string sharedId = first.TrackId;
            ReflectionUtil.SetField(second, "_trackId", sharedId);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            var tracks = result.Project.Animations[0].Timeline.Tracks;
            Assert.That(tracks[0].TrackId, Is.EqualTo(sharedId));
            Assert.That(tracks[1].TrackId, Is.Not.EqualTo(sharedId));
            Assert.That(StableId.IsValid(tracks[1].TrackId), Is.True);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.DuplicateId), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_DuplicateKeyId_FirstKeepsItsId()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            var first = FloatKeyframeData.Create(0f, 0f);
            var second = FloatKeyframeData.Create(1f, 1f);
            track.BlendShape.AddKey(first);
            track.BlendShape.AddKey(second);
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            string sharedId = first.KeyId;
            ReflectionUtil.SetField(second, "_keyId", sharedId);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            var keys = result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys;
            Assert.That(keys[0].KeyId, Is.EqualTo(sharedId));
            Assert.That(keys[1].KeyId, Is.Not.EqualTo(sharedId));
            Assert.That(StableId.IsValid(keys[1].KeyId), Is.True);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.DuplicateId), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_NonFiniteDurationAndFrameRate_ResetToDefaults()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(animation);
            animation.Timeline.Duration = float.NaN;
            animation.Timeline.FrameRate = float.PositiveInfinity;

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Duration, Is.EqualTo(1f));
            Assert.That(result.Project.Animations[0].Timeline.FrameRate, Is.EqualTo(60f));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.PartialRecovery), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_NonPositiveDurationAndFrameRate_ResetToDefaults()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.Duration = 0f;
            animation.Timeline.FrameRate = -5f;
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(animation);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Duration, Is.EqualTo(1f));
            Assert.That(result.Project.Animations[0].Timeline.FrameRate, Is.EqualTo(60f));
        }

        [Test]
        public void MigrateLegacy_NonFiniteOrNegativeKeyTimes_ResetToZero()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = CreateLegacyBlendTrack();
            track.BlendShape.Keys[0].Time = float.NegativeInfinity;
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            var second = FaceMotionAnimationData.Create("Rotation");
            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Head/Bone");
            rotation.Transform.AddKey(Vector3KeyframeData.Create(float.NaN, Vector3.up));
            second.Timeline.AddTrack(rotation);
            source.AddAnimation(second);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Time, Is.EqualTo(0f));
            Assert.That(result.Project.Animations[1].Timeline.Tracks[0].Transform.Keys[0].Time, Is.EqualTo(0f));
        }

        [Test]
        public void MigrateLegacy_NonFiniteKeyValues_ResetToZero()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = CreateLegacyBlendTrack();
            track.BlendShape.Keys[0].Value = float.NaN;
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            var transform = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head/Bone");
            transform.Transform.AddKey(Vector3KeyframeData.Create(0f, new Vector3(float.PositiveInfinity, 0f, 1f)));
            var second = FaceMotionAnimationData.Create("Scale");
            second.Timeline.AddTrack(transform);
            source.AddAnimation(second);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Value, Is.EqualTo(0f));
            Assert.That(result.Project.Animations[1].Timeline.Tracks[0].Transform.Keys[0].Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MigrateLegacy_NegativeKeyTime_ResetToZero()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(animation);
            animation.Timeline.Tracks[0].BlendShape.Keys[0].Time = -0.5f;

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Time, Is.EqualTo(0f));
        }

        [Test]
        public void MigrateLegacy_UnsortedKeys_AreStableSorted()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            var late = FloatKeyframeData.Create(1f, 5f);
            var early = FloatKeyframeData.Create(0f, 0f);
            track.BlendShape.AddKey(late);
            track.BlendShape.AddKey(early);
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            var keys = result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys;
            Assert.That(keys.Count, Is.EqualTo(2));
            Assert.That(keys[0].Time, Is.EqualTo(0f));
            Assert.That(keys[1].Time, Is.EqualTo(1f));
            Assert.That(keys[0].KeyId, Is.EqualTo(early.KeyId));
            Assert.That(keys[1].KeyId, Is.EqualTo(late.KeyId));
        }

        [Test]
        public void MigrateLegacy_OverlappingKeyTimes_AreRetainedWithoutBlocking()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 1f));
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys.Count, Is.EqualTo(2));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.DuplicateKeyTime), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_InvalidTrackKind_IsBlockingAndLeftUntouched()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = CreateLegacyBlendTrack();
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            ReflectionUtil.SetField(track, "_kind", (TrackKind)99);
            string trackId = track.TrackId;

            var result = Migrate(source);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.ValidationFailed));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.MalformedData), Is.Not.Null);
            Assert.That(track.Kind, Is.EqualTo((TrackKind)99), "The malformed track must never be touched.");
            Assert.That(track.TrackId, Is.EqualTo(trackId), "The malformed track's ID must never be repaired before blocking.");
        }

        [Test]
        public void MigrateLegacy_UnsupportedRotationMode_IsBlockingAndLeftUntouched()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Head/Bone");
            rotation.Transform.RotationMode = MotionRotationMode.EulerContinuous;
            rotation.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero));
            animation.Timeline.AddTrack(rotation);
            source.AddAnimation(animation);

            var result = Migrate(source);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.ValidationFailed));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.MalformedData), Is.Not.Null);
            Assert.That(rotation.Transform.RotationMode, Is.EqualTo(MotionRotationMode.EulerContinuous));
        }

        [Test]
        public void MigrateLegacy_NullTimelineAndNullLists_AreInitializedWithoutBlocking()
        {
            var source = CreateLegacyProject();
            ReflectionUtil.SetField(source, "_animations", (List<FaceMotionAnimationData>)null);
            ReflectionUtil.SetField(source, "_generations", (List<GenerationRecord>)null);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations, Is.Empty);
            Assert.That(result.Project.Generations, Is.Empty);
        }

        [Test]
        public void MigrateLegacy_NullAnimationTimeline_IsReplacedWithADefaultTimeline()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            source.AddAnimation(animation);
            ReflectionUtil.SetField(animation, "_timeline", (FaceTimelineData)null);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline, Is.Not.Null);
            Assert.That(result.Project.Animations[0].Timeline.Duration, Is.EqualTo(1f));
        }

        [Test]
        public void MigrateLegacy_NullTrackPayload_IsReplacedWithAnEmptyPayload()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            ReflectionUtil.SetField(track, "_blendShape", (BlendShapeTrackPayload)null);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape, Is.Not.Null);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys, Is.Empty);
        }

        [Test]
        public void MigrateLegacy_NullKeyEntries_AreRemovedByNormalization()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(1f, 1f));
            ((List<FloatKeyframeData>)ReflectionUtil.GetFieldValue(track.BlendShape, "_keys")).Insert(1, null);
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            var keys = result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys;
            Assert.That(keys.Count, Is.EqualTo(2));
            Assert.That(keys[0].Time, Is.EqualTo(0f));
            Assert.That(keys[1].Time, Is.EqualTo(1f));
        }

        [Test]
        public void MigrateLegacy_NullAnimationAndTrackEntries_AreRemoved_GenerationEntriesArePreserved()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(animation);
            var nullAnimationList = new List<FaceMotionAnimationData> { animation, null };
            ReflectionUtil.SetField(source, "_animations", nullAnimationList);
            var nullGenerationList = new List<GenerationRecord> { null };
            ReflectionUtil.SetField(source, "_generations", nullGenerationList);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations.Count, Is.EqualTo(1));
            Assert.That(result.Project.Generations.Count, Is.EqualTo(1));
            Assert.That(result.Project.Generations[0], Is.Null);
        }

        [Test]
        public void MigrateLegacy_GeneratedProvenance_IsPreservedAndAlgorithmZeroIsNotLifted()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            string generationId = StableId.New();
            var key = FloatKeyframeData.Create(0f, 1f, InterpolationType.Linear, new KeyOrigin(OriginKind.Blink, generationId));
            track.BlendShape.AddKey(key);
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);
            const string snapshot = "{\"formatVersion\":1,\"generatorType\":1,\"sourcePresetId\":\"builtin.blink\",\"seed\":7}";
            var record = GenerationRecord.Create(animation.AnimationId, GeneratorType.Blink, "builtin.blink", 0, "hash", generationId, snapshot);
            source.AddGenerationRecord(record);

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            var migratedAnimation = result.Project.Animations[0];
            var migratedKey = migratedAnimation.Timeline.Tracks[0].BlendShape.Keys[0];
            Assert.That(migratedKey.Origin.Kind, Is.EqualTo(OriginKind.Blink), "Generated provenance must survive migration.");
            Assert.That(migratedKey.Origin.GenerationId, Is.EqualTo(generationId));
            Assert.That(result.Project.Generations.Count, Is.EqualTo(1));
            var migratedRecord = result.Project.Generations[0];
            Assert.That(migratedRecord.GenerationId, Is.EqualTo(generationId));
            Assert.That(migratedRecord.AlgorithmVersion, Is.EqualTo(0));
            Assert.That(migratedRecord.SettingsSnapshot, Is.EqualTo(snapshot));
        }

        [Test]
        public void MigrateLegacy_KeysBeyondDuration_AreRetainedWithWarningsOnly()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(animation);
            animation.Timeline.Duration = 1f;
            animation.Timeline.Tracks[0].BlendShape.Keys[0].Time = 2f;

            var result = Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Time, Is.EqualTo(2f));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.KeyBeyondDuration), Is.Not.Null);
        }

        [Test]
        public void MigrateLegacy_SourceProject_IsNeverMutated()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.Duration = float.NaN;
            var track = CreateLegacyBlendTrack();
            track.BlendShape.Keys[0].Time = -1f;
            animation.Timeline.AddTrack(track);
            source.AddAnimation(animation);

            Migrate(source);

            Assert.That(source.SchemaVersion, Is.EqualTo(FaceMotionVersions.LegacySchemaVersion));
            Assert.That(source.Animations[0].Timeline.Duration, Is.EqualTo(float.NaN));
            Assert.That(source.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].Time, Is.EqualTo(-1f));
        }

        [Test]
        public void MigrateLegacy_IsIdempotent_AcrossSuccessiveEligiblePasses()
        {
            var source = CreateLegacyProject();
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            source.AddAnimation(animation);

            var first = Migrate(source);
            Assert.That(first.Success, Is.True);

            var second = Migrate(first.Project);

            Assert.That(second.Success, Is.True);
            Assert.That(second.Status, Is.EqualTo(ProjectMigrationStatus.NoMigrationNeeded), "The current schema is a content no-op.");
            var migrated = second.Project;
            Assert.That(migrated.ProjectId, Is.EqualTo(first.Project.ProjectId));
            Assert.That(migrated.Animations[0].AnimationId, Is.EqualTo(first.Project.Animations[0].AnimationId));
            Assert.That(migrated.Animations[0].Timeline.Tracks[0].TrackId, Is.EqualTo(first.Project.Animations[0].Timeline.Tracks[0].TrackId));
            Assert.That(TestHelpers.FindDiagnostic(second.Diagnostics, FaceMotionDiagnosticCodes.UpgradedSchema), Is.Null);
        }

        [Test]
        public void Service_LegacyProject_CommitsTheMigrationToTheAsset()
        {
            var project = ScriptableObject.CreateInstance<FaceMotionProject>();
            ReflectionUtil.SetField(project, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            ReflectionUtil.SetField(project, "_projectId", string.Empty);
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            project.AddAnimation(animation);
            string path = Folder + "/legacy.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            var result = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            var reloaded = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path);
            Assert.That(reloaded.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion));
            Assert.That(StableId.IsValid(reloaded.ProjectId), Is.True);
            Assert.That(StableId.IsValid(reloaded.Animations[0].AnimationId), Is.True);
            Assert.That(StableId.IsValid(reloaded.Animations[0].Timeline.Tracks[0].TrackId), Is.True);
            Assert.That(StableId.IsValid(reloaded.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId), Is.True);
        }

        [Test]
        public void Schema1Project_MigrationPreservesAssetObjectName()
        {
            var project = FaceMotionProject.CreateNew();
            ReflectionUtil.SetField(project, "_schemaVersion", 1);
            project.AddAnimation(FaceMotionAnimationData.Create("Published v1"));
            string path = Folder + "/NamedProject.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var before = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path);
            Assert.That(before.name, Is.EqualTo("NamedProject"), "Unity derives the asset object name from the filename.");

            var result = ProjectMigrationService.TryMigrateOnLoad(before);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var after = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path);

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            Assert.That(after.SchemaVersion, Is.EqualTo(2));
            Assert.That(after.name, Is.EqualTo("NamedProject"),
                "The Unity object name is outside the FaceMotion schema and must survive the migration commit.");
        }

        [Test]
        public void Service_CurrentSchema_NoOpDoesNotRewriteTheAsset()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Current");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            project.AddAnimation(animation);
            string path = Folder + "/current.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            var result = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.NoMigrationNeeded));
            Assert.That(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path).SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion));
        }

        [Test]
        public void Service_FutureSchema_IsRefusedAndNeverWritten()
        {
            var project = ScriptableObject.CreateInstance<FaceMotionProject>();
            ReflectionUtil.SetField(project, "_schemaVersion", FaceMotionVersions.ProjectSchemaVersion + 1);
            ReflectionUtil.SetField(project, "_projectId", StableId.New());
            string path = Folder + "/future.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            var result = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.FutureSchema));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.FutureSchema), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path).SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion + 1));
        }

        [Test]
        public void Service_UninitializedSchema_IsRefusedAndNeverWritten()
        {
            var project = ScriptableObject.CreateInstance<FaceMotionProject>();
            ReflectionUtil.SetField(project, "_schemaVersion", -1);
            ReflectionUtil.SetField(project, "_projectId", StableId.New());
            string path = Folder + "/uninitialized.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            var result = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.Uninitialized));
            Assert.That(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path).SchemaVersion, Is.EqualTo(-1));
        }

        [Test]
        public void Service_NullProject_MapsToMigrationFailed()
        {
            var result = ProjectMigrationService.TryMigrateOnLoad(null);

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.MigrationFailed));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.NullProject), Is.Not.Null);
        }

        [Test]
        public void Service_UnsafeLegacyData_DoesNotWriteTheAsset()
        {
            var project = ScriptableObject.CreateInstance<FaceMotionProject>();
            ReflectionUtil.SetField(project, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            ReflectionUtil.SetField(project, "_projectId", StableId.New());
            var animation = FaceMotionAnimationData.Create("Legacy");
            var track = CreateLegacyBlendTrack();
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);
            ReflectionUtil.SetField(track, "_kind", (TrackKind)99);
            string path = Folder + "/unsafe.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            var result = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.ValidationFailed));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.MalformedData), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path).SchemaVersion, Is.EqualTo(FaceMotionVersions.LegacySchemaVersion));
        }

        [Test]
        public void Service_AfterMigration_SecondLoadIsEquivalentToANoOp()
        {
            var project = ScriptableObject.CreateInstance<FaceMotionProject>();
            ReflectionUtil.SetField(project, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            ReflectionUtil.SetField(project, "_projectId", string.Empty);
            var animation = FaceMotionAnimationData.Create("Legacy");
            animation.Timeline.AddTrack(CreateLegacyBlendTrack());
            project.AddAnimation(animation);
            string path = Folder + "/roundtrip.asset";
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();

            var first = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));
            var second = ProjectMigrationService.TryMigrateOnLoad(AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path));

            Assert.That(first.Status, Is.EqualTo(ProjectMigrationStatus.Migrated));
            Assert.That(second.Status, Is.EqualTo(ProjectMigrationStatus.NoMigrationNeeded));
            var reloaded = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(path);
            Assert.That(reloaded.ProjectId, Is.EqualTo(first.Project.ProjectId));
            Assert.That(reloaded.Animations[0].AnimationId, Is.EqualTo(first.Project.Animations[0].AnimationId));
            Assert.That(reloaded.Animations[0].Timeline.Tracks[0].TrackId, Is.EqualTo(first.Project.Animations[0].Timeline.Tracks[0].TrackId));
            Assert.That(reloaded.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId, Is.EqualTo(first.Project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId));
        }
    }
}
