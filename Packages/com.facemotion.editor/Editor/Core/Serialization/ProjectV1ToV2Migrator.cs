using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Serialization
{
    /// <summary>
    /// Schema v2 adds Baseline key provenance. No v1 key may be inferred to be a baseline
    /// from its time or value: all existing IDs, values, interpolation and origins survive.
    /// The pipeline advances the detached copy's schema after this explicit step.
    /// </summary>
    public sealed class ProjectV1ToV2Migrator : IProjectMigrator<FaceMotionProject>
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public bool CanMigrate(int version) => version == FromVersion;

        public bool TryMigrate(
            FaceMotionProject sourceCopy,
            out FaceMotionProject migratedCopy,
            ICollection<FaceMotionDiagnostic> diagnostics)
        {
            migratedCopy = sourceCopy;
            return sourceCopy != null && CanMigrate(sourceCopy.SchemaVersion);
        }
    }
}
