using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Serialization
{
    /// <summary>Outcome of an in-memory migration of a persisted schema object (manifest, profile).</summary>
    public sealed class MigrationResult
    {
        public MigrationResult(bool applied, bool blocked, ICollection<FaceMotionDiagnostic> diagnostics)
        {
            Applied = applied;
            Blocked = blocked;
            Diagnostics = diagnostics == null ? new FaceMotionDiagnostic[0] : new List<FaceMotionDiagnostic>(diagnostics);
        }

        /// <summary>True when at least one schema field was migrated in memory.</summary>
        public bool Applied { get; }

        /// <summary>True when the object cannot be used safely (future schema, unresolvable ownership).</summary>
        public bool Blocked { get; }

        /// <summary>True when the caller may proceed to use (and mutate) the object.</summary>
        public bool Allowed => !Blocked;

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>Shared deterministic ID repairs for migration. Valid existing IDs are never changed.</summary>
    public static class MigrationIds
    {
        /// <summary>Returns a new stable ID when the value is missing or malformed; otherwise unchanged.</summary>
        public static string Repair(string existing)
        {
            return NeedsRepair(existing) ? StableId.New() : existing;
        }

        public static bool NeedsRepair(string existing)
        {
            return string.IsNullOrEmpty(existing) || StableId.RequiresRepair(existing);
        }

        /// <summary>
        /// Repairs a stable ID and guarantees uniqueness against <paramref name="seen"/>.
        /// The first occurrence of a valid ID is always preserved.
        /// </summary>
        public static string RepairUnique(string existing, ISet<string> seen, out bool repaired, out bool duplicated)
        {
            repaired = false;
            duplicated = false;
            string value = Repair(existing);
            if (!string.Equals(value, existing, StringComparison.Ordinal))
            {
                repaired = true;
                while (!seen.Add(value))
                {
                    value = StableId.New();
                }

                return value;
            }

            if (seen.Add(value))
            {
                return value;
            }

            duplicated = true;
            do
            {
                value = StableId.New();
            }
            while (!seen.Add(value));

            return value;
        }
    }
}