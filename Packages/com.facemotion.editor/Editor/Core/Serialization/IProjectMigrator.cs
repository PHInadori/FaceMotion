using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Serialization
{
    public interface IProjectMigrator<TProject>
    {
        int FromVersion { get; }

        int ToVersion { get; }

        bool CanMigrate(int version);

        bool TryMigrate(
            TProject sourceCopy,
            out TProject migratedCopy,
            ICollection<FaceMotionDiagnostic> diagnostics);
    }
}
