using System.IO;
using System.Reflection;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using MenuItem = UnityEditor.MenuItem;

namespace FaceMotion.Editor.Tests
{
    public sealed class ArchitectureSmokeTests
    {
        [Test]
        public void PerformanceProbe_HasNoPublicBenchmarkMenuItem()
        {
            MethodInfo run = typeof(PerformanceProbe).GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            Assert.That(run, Is.Not.Null);
            Assert.That(run.GetCustomAttributes(typeof(MenuItem), false), Is.Empty,
                "The batch-only probe must not be available from ordinary Unity menus.");
        }

        [Test]
        public void PerformanceProbe_ImplementationStillExists()
        {
            Assert.That(typeof(PerformanceProbe).GetMethod("Run", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(typeof(PerformanceProbe).GetMethod("RunCore", BindingFlags.NonPublic | BindingFlags.Static), Is.Not.Null);
            // Metadata inspection only: invoking Run would exit the Unity Editor.
        }

        [Test]
        public void FaceMotionWindow_StillHasItsMenuEntry()
        {
            MethodInfo open = typeof(FaceMotionWindow).GetMethod("OpenWindow", BindingFlags.Public | BindingFlags.Static);
            Assert.That(open, Is.Not.Null);
            Assert.That(open.GetCustomAttributes(typeof(MenuItem), false), Has.Length.EqualTo(1));
        }

        [Test]
        public void Versions_StartAtDefinedValues()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(FaceMotionVersions).Assembly);
            Assert.That(package, Is.Not.Null, "FaceMotion package metadata must be discoverable from its assembly.");
            string manifestPath = Path.Combine(package.resolvedPath, "package.json");
            var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));

            Assert.That(FaceMotionVersions.ToolVersion, Is.EqualTo(manifest.version));
            Assert.That(FaceMotionVersions.ProjectSchemaVersion, Is.EqualTo(2));
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
