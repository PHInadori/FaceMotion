using FaceMotion.Timeline;

namespace FaceMotion.Data
{
    /// <summary>
    /// Entry point for safe, mechanical data repairs. The pipeline order is clone,
    /// migrate, normalize, validate; a project that is never normalized is reported by
    /// validation rather than silently repaired by evaluators.
    /// </summary>
    public static class ProjectNormalizer
    {
        /// <summary>
        /// Repairs null collections, null entries, missing payload objects for the active
        /// kind, and out-of-order keys. Never fabricates user values such as duration or
        /// frame rate and never resolves kind/payload mismatches.
        /// </summary>
        public static void Normalize(FaceMotionProject project)
        {
            if (project == null)
            {
                return;
            }

            project.NormalizeStructure();
        }
    }
}