using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterDataCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.NullProject, FaceMotionDiagnosticActionLevel.Recommended, "data");
            Add(defs, FaceMotionDiagnosticCodes.InvalidProjectId, FaceMotionDiagnosticActionLevel.Recommended, "data");
            Add(defs, FaceMotionDiagnosticCodes.InvalidAnimationId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateAnimationId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NullAnimation, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NullTimeline, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.InvalidDuration, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.InvalidFrameRate, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.InvalidTrackId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateTrackId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NullTrack, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.PayloadMismatch, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.UnsupportedRotationMode, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NullPayload, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateTrackBinding, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.InvalidKeyId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateKeyId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NullKey, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateKeyTime, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.UnsortedKeys, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NegativeKeyTime, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.KeyBeyondDuration, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NonFiniteKeyValue, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.NonFiniteKeyTime, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.ManualKeyWithGenerationId, FaceMotionDiagnosticActionLevel.Recommended, "timeline");
            Add(defs, FaceMotionDiagnosticCodes.OrphanedGenerationKey, FaceMotionDiagnosticActionLevel.Recommended, "generation");
            Add(defs, FaceMotionDiagnosticCodes.NullGenerationRecord, FaceMotionDiagnosticActionLevel.Recommended, "generation");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateGenerationId, FaceMotionDiagnosticActionLevel.Recommended, "generation");
            Add(defs, FaceMotionDiagnosticCodes.ManualKeyProtected, FaceMotionDiagnosticActionLevel.Recommended, "generation");
            Add(defs, FaceMotionDiagnosticCodes.ForeignContentInFolder, FaceMotionDiagnosticActionLevel.Recommended, "ownership");
            Add(defs, FaceMotionDiagnosticCodes.TamperedOwnedPaths, FaceMotionDiagnosticActionLevel.Recommended, "ownership");
        }
    }
}