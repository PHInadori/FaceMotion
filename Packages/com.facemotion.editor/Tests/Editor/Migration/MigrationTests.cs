using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Serialization;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class MigrationTests
    {
        /// <summary>
        /// A one-step migrator used to exercise the pipeline mechanics. Each successful run
        /// records its target schema in <see cref="ExecutionLog"/> and advances the copy's
        /// schema version, mirroring what a real migrator would do.
        /// </summary>
        private sealed class FakeMigrator : IProjectMigrator<FaceMotionProject>
        {
            public static readonly List<int> ExecutionLog = new List<int>();

            private readonly int _from;
            private readonly int _to;

            public FakeMigrator(int from, int to)
            {
                _from = from;
                _to = to;
            }

            public int FromVersion => _from;

            public int ToVersion => _to;

            public bool CanMigrate(int version)
            {
                return version == _from;
            }

            public bool TryMigrate(
                FaceMotionProject sourceCopy,
                out FaceMotionProject migratedCopy,
                ICollection<FaceMotionDiagnostic> diagnostics)
            {
                if (!CanMigrate(sourceCopy.SchemaVersion))
                {
                    migratedCopy = null;
                    return false;
                }

                ExecutionLog.Add(_to);
                sourceCopy.SetSchemaVersionForMigration(_to);
                migratedCopy = sourceCopy;
                return true;
            }
        }

        private static FaceMotionProject BuildValidProject()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Migrate");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);
            return project;
        }

        [Test]
        public void Migrate_CurrentSchema_SucceedsOnDetachedClone()
        {
            var source = BuildValidProject();
            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[0]);
            var result = pipeline.Migrate(source);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.NoMigrationNeeded));
            Assert.That(result.Project, Is.Not.SameAs(source));
            Assert.That(result.Project.ProjectId, Is.EqualTo(source.ProjectId));
            Assert.That(source.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion));
        }

        [Test]
        public void Migrate_NullProject_Fails()
        {
            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[0]);
            var result = pipeline.Migrate(null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.MigrationFailed));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.NullProject), Is.Not.Null);
        }

        [Test]
        public void Migrate_UninitializedSchema_IsBlocking()
        {
            var source = BuildValidProject();
            ReflectionUtil.SetField(source, "_schemaVersion", -1); // negative = uninitialized; 0 is the legacy schema
            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[0]);
            var result = pipeline.Migrate(source);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.Uninitialized));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.UninitializedSchema), Is.Not.Null);
        }

        [Test]
        public void Migrate_LegacyDefaultSchema_WithoutMigrator_IsVersionGap()
        {
            var source = BuildValidProject();
            ReflectionUtil.SetField(source, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[0]);
            var result = pipeline.Migrate(source);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.VersionGap));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.MissingMigrationStep), Is.Not.Null);
        }

        [Test]
        public void Migrate_FutureSchema_IsBlocking()
        {
            var source = BuildValidProject();
            ReflectionUtil.SetField(source, "_schemaVersion", FaceMotionVersions.ProjectSchemaVersion + 1);
            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[0]);
            var result = pipeline.Migrate(source);

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.FutureSchema));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.FutureSchema), Is.Not.Null);
        }

        [Test]
        public void Migrate_MissingStep_IsVersionGap()
        {
            var source = BuildValidProject();
            var pipeline = new ProjectMigrationPipeline(
                new IProjectMigrator<FaceMotionProject>[] { new FakeMigrator(2, 3) },
                FaceMotionVersions.ProjectSchemaVersion + 2);
            var result = pipeline.Migrate(source);

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.VersionGap));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.MissingMigrationStep), Is.Not.Null);
        }

        [Test]
        public void Migrate_SkippingMigratorStep_IsVersionGap()
        {
            var source = BuildValidProject();
            var pipeline = new ProjectMigrationPipeline(
                new IProjectMigrator<FaceMotionProject>[] { new FakeMigrator(1, 3) },
                FaceMotionVersions.ProjectSchemaVersion + 2);
            var result = pipeline.Migrate(source);

            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.VersionGap));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.MissingMigrationStep), Is.Not.Null);
        }

        [Test]
        public void Migrate_AppliesMigratorPerStep_OnDetachedCopyOnly()
        {
            FakeMigrator.ExecutionLog.Clear();
            var source = BuildValidProject();
            var pipeline = new ProjectMigrationPipeline(
                new IProjectMigrator<FaceMotionProject>[] { new FakeMigrator(1, 2) },
                FaceMotionVersions.ProjectSchemaVersion + 1);
            var result = pipeline.Migrate(source);

            Assert.That(FakeMigrator.ExecutionLog, Is.EqualTo(new[] { 2 }), "The 1->2 migrator must run exactly once.");
            Assert.That(result.Success, Is.False, "Migrating beyond the tool's current schema must cascade into validation failure.");
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.ValidationFailed));
            Assert.That(result.Project, Is.Null);
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.FutureSchema), Is.Not.Null);
            Assert.That(source.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion), "The source must never be mutated.");
            Assert.That(source.Animations.Count, Is.EqualTo(1));
            Assert.That(source.Animations[0].AnimationId, Is.Not.Null.Or.Not.Empty);
        }

        [Test]
        public void Migrate_ChainedSteps_ApplyInOrder()
        {
            FakeMigrator.ExecutionLog.Clear();
            var source = BuildValidProject();
            var pipeline = new ProjectMigrationPipeline(
                new IProjectMigrator<FaceMotionProject>[] { new FakeMigrator(2, 3), new FakeMigrator(1, 2) },
                FaceMotionVersions.ProjectSchemaVersion + 2);
            var result = pipeline.Migrate(source);

            Assert.That(FakeMigrator.ExecutionLog, Is.EqualTo(new[] { 2, 3 }), "Chained migrators must run in schema order.");
            Assert.That(result.Success, Is.False, "Migrating beyond the tool's current schema must cascade into validation failure.");
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.ValidationFailed));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.FutureSchema), Is.Not.Null);
            Assert.That(source.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion), "The source must never be mutated.");
        }

        [Test]
        public void Migrate_ValidationFailure_SurfacesDiagnostics()
        {
            var source = BuildValidProject();
            ReflectionUtil.SetField(source.Animations[0], "_animationId", "bad-id");
            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[0]);
            var result = pipeline.Migrate(source);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProjectMigrationStatus.ValidationFailed));
            Assert.That(TestHelpers.FindDiagnostic(result.Diagnostics, FaceMotionDiagnosticCodes.InvalidAnimationId), Is.Not.Null);
        }
    }
}