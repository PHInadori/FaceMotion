using System.IO;
using FaceMotion.Diagnostics;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.Tests
{
    public sealed class ArchitectureSmokeTests
    {
        [Test]
        public void Versions_StartAtDefinedValues()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(FaceMotionVersions).Assembly);
            Assert.That(package, Is.Not.Null, "FaceMotion package metadata must be discoverable from its assembly.");
            string manifestPath = Path.Combine(package.resolvedPath, "package.json");
            var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));

            Assert.That(FaceMotionVersions.ToolVersion, Is.EqualTo(manifest.version));
            Assert.That(FaceMotionVersions.ProjectSchemaVersion, Is.EqualTo(1));
            Assert.That(FaceMotionVersions.IntegrationManifestVersion, Is.EqualTo(1));
            Assert.That(FaceMotionVersions.IntegrationBackendVersion, Is.EqualTo(1));
            Assert.That(FaceMotionVersions.MappingProfileSchemaVersion, Is.EqualTo(1));
            Assert.That(FaceMotionVersions.AvatarFingerprintAlgorithmVersion, Is.EqualTo(1));
            Assert.That(FaceMotionVersions.AvatarFingerprintFormatVersion, Is.EqualTo(1));
        }

        [System.Serializable]
        private sealed class PackageManifest
        {
            public string version;
        }

        [Test]
        public void Diagnostic_PreservesArchitectureFields()
        {
            var diagnostic = new FaceMotionDiagnostic(
                "FM-CORE-0001",
                FaceMotionDiagnosticSeverity.Warning,
                "Message",
                "context-id",
                true,
                "Suggested fix");

            Assert.That(diagnostic.Code, Is.EqualTo("FM-CORE-0001"));
            Assert.That(diagnostic.Severity, Is.EqualTo(FaceMotionDiagnosticSeverity.Warning));
            Assert.That(diagnostic.Message, Is.EqualTo("Message"));
            Assert.That(diagnostic.ContextId, Is.EqualTo("context-id"));
            Assert.That(diagnostic.Blocking, Is.True);
            Assert.That(diagnostic.SuggestedFix, Is.EqualTo("Suggested fix"));
        }

        [Test]
        public void VrcSdkContract_MatchesPinnedSdk()
        {
            Assert.That(typeof(VRCAvatarDescriptor.CustomAnimLayer).IsValueType, Is.True);
            Assert.That((int)VRCAvatarDescriptor.AnimLayerType.FX, Is.EqualTo(5));
            Assert.That(
                typeof(VRCAvatarDescriptor).GetField(nameof(VRCAvatarDescriptor.baseAnimationLayers)),
                Is.Not.Null);

            Assert.That(VRCExpressionParameters.MAX_PARAMETER_COST, Is.EqualTo(256));
            Assert.That(VRCExpressionParameters.MAX_PARAMETER_COUNT, Is.EqualTo(8192));
            Assert.That(
                VRCExpressionParameters.TypeCost(VRCExpressionParameters.ValueType.Bool),
                Is.EqualTo(1));
            Assert.That(
                VRCExpressionParameters.TypeCost(VRCExpressionParameters.ValueType.Int),
                Is.EqualTo(8));
            Assert.That(
                VRCExpressionParameters.TypeCost(VRCExpressionParameters.ValueType.Float),
                Is.EqualTo(8));

            Assert.That(VRCExpressionsMenu.MAX_CONTROLS, Is.EqualTo(8));
            Assert.That(
                (int)VRCExpressionsMenu.Control.ControlType.SubMenu,
                Is.EqualTo(103));
        }
    }
}
