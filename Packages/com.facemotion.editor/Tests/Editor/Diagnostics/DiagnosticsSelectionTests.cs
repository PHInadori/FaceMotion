using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Localization;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Contract tests for selection resolution. An ambiguous identity never resolves to one
    /// of the ambiguous matches; the resolver derives the unique parent, then the avatar
    /// root, then no selection. Resolution never mutates the scene or the cache.
    /// </summary>
    public sealed class DiagnosticsSelectionTests
    {
        private AvatarFixture _fixture;
        private UnityAvatarObjectCache _cache;

        [TearDown]
        public void TearDown()
        {
            if (_cache != null)
            {
                _cache.Clear();
                _cache = null;
            }

            if (_fixture != null)
            {
                _fixture.Dispose();
                _fixture = null;
            }
        }

        [Test]
        public void Resolve_SelectionKindNone_ReturnsNull()
        {
            CreateScannedFixture();
            var unknown = CreatePresentation("FM-UNKNOWN-0001", "Any/Path");

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(unknown, _fixture.Root, _cache), Is.Null);
            Assert.That(FaceMotionDiagnosticSelectionResolver.CanResolve(unknown, _fixture.Root, _cache), Is.False);
        }

        [Test]
        public void Resolve_AvatarRoot_ReturnsRoot_OrNullWithoutRoot()
        {
            CreateScannedFixture();
            var diagnostic = CreatePresentation(FaceMotionDiagnosticCodes.DescriptorMissing, string.Empty);

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache),
                Is.SameAs(_fixture.Root.transform));
            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, null, _cache), Is.Null);
        }

        [Test]
        public void Resolve_RelativeRendererPath_ReturnsRendererTransform()
        {
            CreateScannedFixture();
            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.RendererSharedMeshMissing,
                AvatarFixture.FaceRendererPath);

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache),
                Is.SameAs(_fixture.Face));
        }

        [Test]
        public void Resolve_RendererParentPath_SelectsRendererParentNotBlendShape()
        {
            CreateScannedFixture();
            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.DuplicateBlendShapeNameInMesh,
                AvatarFixture.FaceRendererPath + "/Mouth_Smile");

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache),
                Is.SameAs(_fixture.Face));
        }

        [Test]
        public void Resolve_ParentOfAmbiguousPath_UniqueParentSelectedNotDuplicate()
        {
            CreateScannedFixture("Extra", "Extra");

            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                AvatarFixture.HeadPath + "/Extra");

            Assert.That(_cache.TryGetTransform(AvatarFixture.HeadPath + "/Extra", out _), Is.False,
                "The cache must agree the path is ambiguous.");
            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache),
                Is.SameAs(_fixture.Head),
                "The unique parent must be selected, never one of the duplicates.");
        }

        [Test]
        public void Resolve_ParentOfAmbiguousPath_AmbiguousParentFallsBackToAvatarRoot()
        {
            CreateScannedFixture("Extra", "Extra");

            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                AvatarFixture.HeadPath + "/Extra/Child");

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache),
                Is.SameAs(_fixture.Root.transform),
                "When even the parent is ambiguous, the avatar root is the safe anchor.");
        }

        [Test]
        public void Resolve_ParentOfAmbiguousPath_MissingParentFallsBackToAvatarRoot()
        {
            CreateScannedFixture();

            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                "Body/NotPresent/Child");

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache),
                Is.SameAs(_fixture.Root.transform));
        }

        [Test]
        public void Resolve_ParentOfAmbiguousPath_NullRootReturnsNull()
        {
            CreateScannedFixture();
            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                AvatarFixture.HeadPath + "/Extra");

            Assert.That(FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, null, _cache), Is.Null);
        }

        [Test]
        public void Resolve_IsReadOnly_DoesNotMutateCacheOrScene()
        {
            CreateScannedFixture();
            var diagnostic = CreatePresentation(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                AvatarFixture.HeadPath);

            Assert.That(_cache.TryGetTransform(AvatarFixture.HeadPath, out Transform before), Is.True);
            Transform head = _fixture.Head;
            bool beforeActive = head.gameObject.activeInHierarchy;

            FaceMotionDiagnosticSelectionResolver.Resolve(diagnostic, _fixture.Root, _cache);

            Assert.That(_cache.TryGetTransform(AvatarFixture.HeadPath, out Transform after), Is.True);
            Assert.That(after, Is.SameAs(before));
            Assert.That(head.gameObject.activeInHierarchy, Is.EqualTo(beforeActive));
        }

        private void CreateScannedFixture(params string[] forkedChildren)
        {
            _fixture = AvatarFixture.Create();
            if (forkedChildren != null)
            {
                foreach (string childName in forkedChildren)
                {
                    _fixture.AddFork(childName);
                }
            }

            _cache = new UnityAvatarObjectCache();
            _cache.Rebuild(_fixture.Root);
        }

        private static FaceMotionDiagnosticPresentation CreatePresentation(string code, string contextId)
        {
            var diagnostic = new FaceMotionDiagnostic(
                code,
                FaceMotionDiagnosticSeverity.Warning,
                "Message",
                contextId,
                blocking: false,
                "Suggested fix");
            return new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.English);
        }
    }
}