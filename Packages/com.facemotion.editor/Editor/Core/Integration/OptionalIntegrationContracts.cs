using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Integration
{
    public enum IntegrationBackendSelection
    {
        Direct = 0,
        ModularAvatar = 1
    }

    /// <summary>SDK-neutral availability state for an optional integration backend.</summary>
    public sealed class OptionalIntegrationBackendAvailability
    {
        public OptionalIntegrationBackendAvailability(string backendId, bool packageInstalled, string packageVersion, bool backendAvailable, string reason, bool packageMetadataAvailable = true)
        {
            BackendId = backendId ?? string.Empty;
            PackageInstalled = packageInstalled;
            PackageVersion = packageVersion ?? string.Empty;
            BackendAvailable = backendAvailable;
            Reason = reason ?? string.Empty;
            PackageMetadataAvailable = packageMetadataAvailable;
        }

        public string BackendId { get; }
        public bool PackageInstalled { get; }
        public bool PackageMetadataAvailable { get; }
        public string PackageVersion { get; }
        public bool HasPackageVersion { get { return !string.IsNullOrWhiteSpace(PackageVersion); } }
        public bool BackendAvailable { get; }
        public string Reason { get; }
    }

    /// <summary>Read-only result from an optional backend planner. Applying is intentionally not part of this contract.</summary>
    public sealed class OptionalIntegrationPlan
    {
        public OptionalIntegrationPlan(string backendId, OptionalIntegrationBackendAvailability availability, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            BackendId = backendId ?? string.Empty;
            Availability = availability ?? throw new ArgumentNullException(nameof(availability));
            Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>();
        }

        public string BackendId { get; }
        public OptionalIntegrationBackendAvailability Availability { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++) if (Diagnostics[i].Blocking) return false;
                return Availability.BackendAvailable;
            }
        }
    }

    public interface IOptionalIntegrationBackendPlanner
    {
        string BackendId { get; }
        int BackendVersion { get; }
        OptionalIntegrationBackendAvailability DetectAvailability();
        OptionalIntegrationPlan Plan();
    }
}
