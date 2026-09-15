using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Avatar
{
    public enum MappingProfileStatus
    {
        Valid = 0,
        StaleButValid = 1,
        StaleWithMissing = 2,
        Invalid = 3
    }

    /// <summary>Result of evaluating a profile against the avatar it was built for.</summary>
    public sealed class MappingProfileEvaluation
    {
        public MappingProfileEvaluation(
            MappingProfileStatus status,
            IReadOnlyList<EntryResolution> resolutions,
            bool fingerprintMatches,
            IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            Status = status;
            Resolutions = resolutions;
            FingerprintMatches = fingerprintMatches;
            Diagnostics = diagnostics ?? new List<FaceMotionDiagnostic>();
        }

        public MappingProfileStatus Status { get; }

        public IReadOnlyList<EntryResolution> Resolutions { get; }

        public bool FingerprintMatches { get; }

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>
    /// Foundation of the remapping flow: given a previously-built profile and a fresh avatar
    /// index, produce a per-binding validation result. A fingerprint mismatch never voids
    /// the profile; staleness is reported and every binding is re-validated individually.
    /// </summary>
    public static class AvatarMappingEvaluator
    {
        public static MappingProfileEvaluation Evaluate(AvatarMappingProfile profile, AvatarIndex index)
        {
            if (profile == null)
            {
                return new MappingProfileEvaluation(
                    MappingProfileStatus.Invalid,
                    new List<EntryResolution>(),
                    false,
                    new List<FaceMotionDiagnostic>
                    {
                        new FaceMotionDiagnostic(
                            FaceMotionDiagnosticCodes.NullMappingProfile,
                            FaceMotionDiagnosticSeverity.Error,
                            "The mapping profile is null.",
                            string.Empty,
                            true,
                            "Load a valid mapping profile.")
                    });
            }

            if (index == null)
            {
                return new MappingProfileEvaluation(
                    MappingProfileStatus.Invalid,
                    new List<EntryResolution>(),
                    false,
                    new List<FaceMotionDiagnostic>
                    {
                        new FaceMotionDiagnostic(
                            FaceMotionDiagnosticCodes.InvalidBinding,
                            FaceMotionDiagnosticSeverity.Error,
                            "No avatar index is available for evaluation.",
                            profile.ProfileId,
                            true,
                            "Scan the avatar first.")
                    });
            }

            bool fingerprintMatches = profile.AvatarFingerprint != null
                && index.Fingerprint != null
                && profile.AvatarFingerprint.EqualsValue(index.Fingerprint);

            var resolutions = new List<EntryResolution>(profile.Mappings.Count);
            var diagnostics = new List<FaceMotionDiagnostic>();
            bool hasMissing = false;
            bool hasAmbiguous = false;
            bool hasInvalid = false;

            foreach (var entry in profile.Mappings)
            {
                EntryResolution resolution = AvatarMappingResolver.Resolve(entry, index);
                resolutions.Add(resolution);
                if (resolution.Diagnostic != null)
                {
                    diagnostics.Add(resolution.Diagnostic);
                }

                switch (resolution.Status)
                {
                    case MappingResolutionStatus.MissingRenderer:
                    case MappingResolutionStatus.MissingBlendShape:
                    case MappingResolutionStatus.MissingTransform:
                    case MappingResolutionStatus.Missing:
                        hasMissing = true;
                        break;
                    case MappingResolutionStatus.Ambiguous:
                        hasAmbiguous = true;
                        break;
                    case MappingResolutionStatus.Invalid:
                        hasInvalid = true;
                        break;
                }
            }

            if (!fingerprintMatches)
            {
                diagnostics.Add(new FaceMotionDiagnostic(
                    FaceMotionDiagnosticCodes.ProfileFingerprintMismatch,
                    FaceMotionDiagnosticSeverity.Warning,
                    "The avatar fingerprint changed since this profile was built; treat the profile as stale.",
                    profile.ProfileId,
                    false,
                    "Re-validate each binding, then rebind the fingerprint."));
            }

            MappingProfileStatus status;
            if (hasInvalid)
            {
                status = MappingProfileStatus.Invalid;
            }
            else if (hasMissing || hasAmbiguous)
            {
                status = fingerprintMatches ? MappingProfileStatus.Invalid : MappingProfileStatus.StaleWithMissing;
            }
            else
            {
                status = fingerprintMatches ? MappingProfileStatus.Valid : MappingProfileStatus.StaleButValid;

                if (!fingerprintMatches)
                {
                    diagnostics.Add(new FaceMotionDiagnostic(
                        FaceMotionDiagnosticCodes.StaleMappingButValid,
                        FaceMotionDiagnosticSeverity.Warning,
                        "Every binding still resolves exactly, but the fingerprint differs.",
                        profile.ProfileId,
                        false,
                        "Accept the scanner result to rebind the fingerprint."));
                }
            }

            return new MappingProfileEvaluation(status, resolutions, fingerprintMatches, diagnostics);
        }
    }
}