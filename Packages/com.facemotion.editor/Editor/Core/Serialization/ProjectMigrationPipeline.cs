using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Versioning;

namespace FaceMotion.Serialization
{
    /// <summary>Outcome of a migration attempt.</summary>
    public enum ProjectMigrationStatus
    {
        NoMigrationNeeded = 0,
        Migrated = 1,
        Uninitialized = 2,
        FutureSchema = 3,
        VersionGap = 4,
        MigrationFailed = 5,
        ValidationFailed = 6
    }

    /// <summary>A detached migration result. The source project is never modified.</summary>
    public sealed class ProjectMigrationResult
    {
        public ProjectMigrationResult(ProjectMigrationStatus status, FaceMotionProject project, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            Status = status;
            Project = project;
            Diagnostics = diagnostics ?? new FaceMotionDiagnostic[0];
        }

        public ProjectMigrationStatus Status { get; }

        /// <summary>The migrated detached clone, or null when it is not safe to commit.</summary>
        public FaceMotionProject Project { get; }

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }

        public bool Success => Status == ProjectMigrationStatus.NoMigrationNeeded
            || Status == ProjectMigrationStatus.Migrated;
    }

    /// <summary>
    /// Coordinates schema migration: deep-clones the source, applies exactly one migrator
    /// per schema step, then normalizes and validates the detached copy before committing.
    /// </summary>
    public sealed class ProjectMigrationPipeline
    {
        private readonly IReadOnlyList<IProjectMigrator<FaceMotionProject>> _migrators;
        private readonly int _targetSchemaVersion;

        public ProjectMigrationPipeline(
            IEnumerable<IProjectMigrator<FaceMotionProject>> migrators,
            int targetSchemaVersion = FaceMotionVersions.ProjectSchemaVersion)
        {
            if (migrators == null)
            {
                throw new ArgumentNullException(nameof(migrators));
            }

            var list = new List<IProjectMigrator<FaceMotionProject>>(migrators);
            list.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
            _migrators = list;
            _targetSchemaVersion = targetSchemaVersion;
        }

        public ProjectMigrationResult Migrate(FaceMotionProject source)
        {
            if (source == null)
            {
                var nullDiagnostics = new[]
                {
                    Blocking(FaceMotionDiagnosticCodes.NullProject, "Cannot migrate a null project.", string.Empty, "Load a project first.")
                };
                return new ProjectMigrationResult(ProjectMigrationStatus.MigrationFailed, null, nullDiagnostics);
            }

            if (source.SchemaVersion < 0)
            {
                var diagnostics = new List<FaceMotionDiagnostic>
                {
                    Blocking(FaceMotionDiagnosticCodes.UninitializedSchema, "Project schema version is uninitialized.", source.ProjectId, "Initialize the project through FaceMotionProject.CreateNew().")
                };
                return new ProjectMigrationResult(ProjectMigrationStatus.Uninitialized, null, diagnostics);
            }

            if (source.SchemaVersion > _targetSchemaVersion)
            {
                var diagnostics = new List<FaceMotionDiagnostic>
                {
                    Blocking(FaceMotionDiagnosticCodes.FutureSchema, $"Project uses schema {source.SchemaVersion}, newer than the target {_targetSchemaVersion}.", source.ProjectId, "Upgrade this tool to a version that reads the reported schema.")
                };
                return new ProjectMigrationResult(ProjectMigrationStatus.FutureSchema, null, diagnostics);
            }

            if (source.SchemaVersion == _targetSchemaVersion)
            {
                return CompleteCurrentSchema(source);
            }

            var migrationDiagnostics = new List<FaceMotionDiagnostic>();
            if (!TryBuildChain(source.SchemaVersion, _targetSchemaVersion, out var chain, migrationDiagnostics))
            {
                return new ProjectMigrationResult(ProjectMigrationStatus.VersionGap, null, migrationDiagnostics);
            }

            var copy = FaceMotionProject.CreateClone(source);
            foreach (var migrator in chain)
            {
                if (!migrator.TryMigrate(copy, out var migrated, migrationDiagnostics))
                {
                    migrationDiagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.MigrationFailed,
                        $"Migrator {migrator.FromVersion}->{migrator.ToVersion} failed.",
                        source.ProjectId,
                        "Fix the migrator or the source data."));
                    return new ProjectMigrationResult(ProjectMigrationStatus.MigrationFailed, null, migrationDiagnostics);
                }

                copy = migrated ?? copy;
            }

            copy.SetSchemaVersionForMigration(_targetSchemaVersion);
            ProjectNormalizer.Normalize(copy);

            var validation = ProjectValidator.Validate(copy);
            foreach (var diagnostic in validation.Diagnostics)
            {
                migrationDiagnostics.Add(diagnostic);
            }

            if (validation.HasBlocking)
            {
                migrationDiagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.MigrationValidationFailed,
                    "Migrated project did not pass validation.",
                    copy.ProjectId,
                    "Repair the reported fields."));
                return new ProjectMigrationResult(ProjectMigrationStatus.ValidationFailed, null, migrationDiagnostics);
            }

            migrationDiagnostics.Insert(0, Info(FaceMotionDiagnosticCodes.UpgradedSchema, $"Project schema was upgraded from {source.SchemaVersion} to {_targetSchemaVersion}."));
            return new ProjectMigrationResult(ProjectMigrationStatus.Migrated, copy, migrationDiagnostics);
        }

        private ProjectMigrationResult CompleteCurrentSchema(FaceMotionProject source)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            var clone = FaceMotionProject.CreateClone(source);
            ProjectNormalizer.Normalize(clone);

            var validation = ProjectValidator.Validate(clone);
            foreach (var diagnostic in validation.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

            if (validation.HasBlocking)
            {
                return new ProjectMigrationResult(ProjectMigrationStatus.ValidationFailed, null, diagnostics);
            }

            return new ProjectMigrationResult(ProjectMigrationStatus.NoMigrationNeeded, clone, diagnostics);
        }

        private bool TryBuildChain(
            int fromVersion,
            int targetVersion,
            out List<IProjectMigrator<FaceMotionProject>> chain,
            List<FaceMotionDiagnostic> diagnostics)
        {
            chain = new List<IProjectMigrator<FaceMotionProject>>();
            var byFromVersion = new Dictionary<int, IProjectMigrator<FaceMotionProject>>();
            for (int i = 0; i < _migrators.Count; i++)
            {
                var migrator = _migrators[i];
                if (migrator == null)
                {
                    continue;
                }

                byFromVersion[migrator.FromVersion] = migrator;
            }

            int version = fromVersion;
            while (version < targetVersion)
            {
                if (!byFromVersion.TryGetValue(version, out var migrator))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.MissingMigrationStep,
                        $"No migrator from schema {version} to {version + 1}.",
                        string.Empty,
                        "Provide the missing migration step."));
                    return false;
                }

                if (migrator.ToVersion != version + 1)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.MissingMigrationStep,
                        $"Migrator {migrator.FromVersion}->{migrator.ToVersion} skips a schema step from {version}.",
                        string.Empty,
                        "Each migrator must advance exactly one schema version."));
                    return false;
                }

                chain.Add(migrator);
                version = migrator.ToVersion;
            }

            return true;
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }

        private static FaceMotionDiagnostic Info(string code, string message)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Info, message, string.Empty, false, string.Empty);
        }
    }
}