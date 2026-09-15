using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Integration;
using UnityEditor.PackageManager;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>Metadata-only boundary for the optional Modular Avatar package. This assembly never references its types.</summary>
    public sealed class ModularAvatarOptionalBackend : IOptionalIntegrationBackendPlanner
    {
        public const string PackageId = "nadena.dev.modular-avatar";
        public const string Id = "modular-avatar";
        public const int Version = 1;

        public string BackendId { get { return Id; } }
        public int BackendVersion { get { return Version; } }

        public OptionalIntegrationBackendAvailability DetectAvailability()
        {
            try
            {
                var packages = PackageInfo.GetAllRegisteredPackages();
                for (int i = 0; i < packages.Length; i++)
                {
                    var package = packages[i];
                    if (package != null && string.Equals(package.name, PackageId, StringComparison.Ordinal))
                    {
                        return EvaluatePackageMetadata(true, package.version);
                    }
                }
            }
            catch (Exception)
            {
                return new OptionalIntegrationBackendAvailability(Id, false, string.Empty, false, "Unity package metadata could not be read.", false);
            }

            return EvaluatePackageMetadata(false, string.Empty);
        }

        public OptionalIntegrationPlan Plan()
        {
            var availability = DetectAvailability();
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (!availability.PackageMetadataAvailable)
            {
                diagnostics.Add(Error("FM-H-MA-METADATA-UNAVAILABLE", "Unity package metadata could not be read.", "Refresh the Package Manager metadata and try again."));
            }
            else if (!availability.PackageInstalled)
            {
                diagnostics.Add(Error("FM-H-MA-NOT-INSTALLED", "Modular Avatar is not installed.", "Install Modular Avatar through the VRChat package workflow, then reopen this window."));
            }
            else if (!availability.HasPackageVersion)
            {
                diagnostics.Add(Error("FM-H-MA-VERSION-UNAVAILABLE", "Modular Avatar was found, but Unity did not report its package version.", "Refresh the Package Manager metadata and try again."));
            }
            else
            {
                diagnostics.Add(Error("FM-H-MA-NOT-IMPLEMENTED", "Modular Avatar " + availability.PackageVersion + " was detected, but this optional backend has no implementation.", "Use Direct integration. No Modular Avatar components or assets were created."));
            }
            return new OptionalIntegrationPlan(Id, availability, diagnostics);
        }

        public static OptionalIntegrationBackendAvailability EvaluatePackageMetadata(bool packageInstalled, string packageVersion)
        {
            if (!packageInstalled) return new OptionalIntegrationBackendAvailability(Id, false, string.Empty, false, "Modular Avatar is not installed.");
            if (string.IsNullOrWhiteSpace(packageVersion)) return new OptionalIntegrationBackendAvailability(Id, true, string.Empty, false, "The installed package did not provide a version.");
            return new OptionalIntegrationBackendAvailability(Id, true, packageVersion.Trim(), false, "The package is installed, but no Modular Avatar backend is implemented.");
        }

        private static FaceMotionDiagnostic Error(string code, string message, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, Id, true, fix);
        }
    }
}
