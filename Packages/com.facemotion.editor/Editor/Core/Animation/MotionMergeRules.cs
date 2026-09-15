using FaceMotion.Timeline;

namespace FaceMotion.Animation
{
    /// <summary>Outcome of a generated-vs-existing key collision.</summary>
    public enum MotionMergeDecision
    {
        Reject = 0,
        Replace = 1
    }

    /// <summary>
    /// Phase A policy for applying generated motion onto already-authored keys. The
    /// default is conservative: existing authored state is always preserved, and a manual
    /// key is never overwritten. Full merge policy is defined here so future generators
    /// share one rule instead of inventing their own.
    /// </summary>
    public static class MotionMergeRules
    {
        public const string Spec = "Phase A default: Reject every collision. Manual keys are never overwritten; generated keys never replace existing generated keys either.";

        public static MotionMergeDecision Decide(KeyOrigin existing, KeyOrigin incoming)
        {
            return MotionMergeDecision.Reject;
        }

        /// <summary>True when the existing key must be preserved because it is manually authored.</summary>
        public static bool ManualKeyIsProtected(KeyOrigin existing)
        {
            return existing.Kind == OriginKind.Manual;
        }

        /// <summary>True when the incoming key should be skipped for the existing key.</summary>
        public static bool ShouldSkipIncoming(KeyOrigin existing, KeyOrigin incoming)
        {
            return Decide(existing, incoming) == MotionMergeDecision.Reject;
        }
    }
}