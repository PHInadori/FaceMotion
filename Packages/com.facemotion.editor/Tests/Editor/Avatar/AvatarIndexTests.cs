using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Support;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class AvatarIndexTests
    {
        [TearDown]
        public void TearDown()
        {
            if (_fixture != null)
            {
                _fixture.Dispose();
                _fixture = null;
            }
        }

        private AvatarFixture _fixture;

        private AvatarIndex ScanStandard()
        {
            _fixture = AvatarFixture.Create();
            return UnityAvatarScanner.Scan(_fixture.Root).Index;
        }

        [Test]
        public void Scan_IncludesInactiveTransform()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.HasTransform(AvatarFixture.HiddenRendererPath), Is.True);
        }

        [Test]
        public void Scan_RootTransform_HasEmptyRelativePath()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.Transforms.Count, Is.GreaterThan(0));
            Assert.That(index.Transforms[0].RelativePath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Scan_NestedTransform_HasSlashSeparatedRelativePath()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.HasTransform(AvatarFixture.HeadPath), Is.True);
            TransformIndexEntry head = index.ResolveTransform(AvatarFixture.HeadPath).Entry;
            Assert.That(head.Name, Is.EqualTo("Head"));
            Assert.That(head.Depth, Is.EqualTo(4));
            Assert.That(head.ParentPath, Is.EqualTo("Armature/Hips/Spine"));
        }

        [Test]
        public void Scan_ListsRenderers()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.RendererCount, Is.EqualTo(4));
            Assert.That(index.HasRenderer(AvatarFixture.FaceRendererPath), Is.True);
            Assert.That(index.HasRenderer(AvatarFixture.CheekRendererPath), Is.True);
            Assert.That(index.HasRenderer(AvatarFixture.HiddenRendererPath), Is.True);
            Assert.That(index.HasRenderer(AvatarFixture.NoMeshRendererPath), Is.True);
        }

        [Test]
        public void Scan_RendererWithoutSharedMesh_IsSafeAndListed()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.TryFindRenderer(AvatarFixture.NoMeshRendererPath, out RendererIndexEntry entry), Is.True);
            Assert.That(entry.HasMesh, Is.False);
            Assert.That(entry.BlendShapeCount, Is.EqualTo(0));
        }

        [Test]
        public void Scan_RendererWithMesh_HasBlendShapeCountAndName()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.TryFindRenderer(AvatarFixture.FaceRendererPath, out RendererIndexEntry entry), Is.True);
            Assert.That(entry.HasMesh, Is.True);
            Assert.That(entry.BlendShapeCount, Is.EqualTo(4));
            Assert.That(entry.MeshName, Is.EqualTo("FaceMesh"));
        }

        [Test]
        public void Scan_ListsBlendShapesWithRendererContext()
        {
            AvatarIndex index = ScanStandard();
            var smile = new BlendShapeBinding(AvatarFixture.CheekRendererPath, "Smile");
            var shape = index.ResolveBlendShape(smile);
            Assert.That(shape.Status, Is.EqualTo(NameResolutionStatus.Unique));
            Assert.That(shape.Entry.RendererPath, Is.EqualTo(AvatarFixture.CheekRendererPath));
            Assert.That(shape.Entry.BlendShapeName, Is.EqualTo("Smile"));
            Assert.That(shape.Entry.CurrentIndex, Is.EqualTo(0));
        }

        [Test]
        public void Scan_SameBlendShapeNameOnDifferentRenderers_AreDistinct()
        {
            AvatarIndex index = ScanStandard();
            var faceSmile = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Smile");
            var cheekSmile = new BlendShapeBinding(AvatarFixture.CheekRendererPath, "Smile");
            Assert.That(index.ResolveBlendShape(faceSmile).Status, Is.EqualTo(NameResolutionStatus.Unique));
            Assert.That(index.ResolveBlendShape(cheekSmile).Status, Is.EqualTo(NameResolutionStatus.Unique));
            Assert.That(index.ResolveBlendShape(faceSmile).Entry.CurrentIndex, Is.EqualTo(3));
            Assert.That(index.ResolveBlendShape(cheekSmile).Entry.CurrentIndex, Is.EqualTo(0));
        }

        [Test]
        public void Scan_ZeroDeltaBlendShape_IsRetainedForExistingBindingsButHiddenFromCandidates()
        {
            _fixture = AvatarFixture.Create();
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("===Custom", 100f, new[] { Vector3.zero }, new[] { Vector3.zero }, new[] { Vector3.zero });
            mesh.AddBlendShapeFrame("NormalOnly", 100f, new[] { Vector3.zero }, new[] { Vector3.up }, new[] { Vector3.zero });
            _fixture.FaceRenderer.sharedMesh = mesh;

            AvatarIndex index = UnityAvatarScanner.Scan(_fixture.Root).Index;
            BlendShapeResolution empty = index.ResolveBlendShape(new BlendShapeBinding(AvatarFixture.FaceRendererPath, "===Custom"));
            BlendShapeResolution normalOnly = index.ResolveBlendShape(new BlendShapeBinding(AvatarFixture.FaceRendererPath, "NormalOnly"));

            Assert.That(empty.Status, Is.EqualTo(NameResolutionStatus.Unique));
            Assert.That(empty.Entry.HasVisibleDelta, Is.False);
            Assert.That(normalOnly.Entry.HasVisibleDelta, Is.True);
            Assert.That(AvatarCandidateSnapshot.Build(index).FilterBlendShapes(string.Empty), Has.None.Matches<AvatarCandidateSnapshot.BlendShapeCandidate>(candidate => candidate.BlendShapeName == "===Custom"));
            Assert.That(AvatarCandidateSnapshot.Build(index).FilterBlendShapes(string.Empty), Has.Some.Matches<AvatarCandidateSnapshot.BlendShapeCandidate>(candidate => candidate.BlendShapeName == "NormalOnly"));

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ResolveTransform_UniquePath_FindsSingleMatch()
        {
            AvatarIndex index = ScanStandard();
            TransformResolution resolution = index.ResolveTransform(AvatarFixture.LeftEarPath);
            Assert.That(resolution.Status, Is.EqualTo(NameResolutionStatus.Unique));
            Assert.That(resolution.Count, Is.EqualTo(1));
            Assert.That(resolution.Entry.Name, Is.EqualTo("LeftEar"));
        }

        [Test]
        public void ResolveTransform_MissingPath_IsNotFound()
        {
            AvatarIndex index = ScanStandard();
            Assert.That(index.ResolveTransform("Armature/Boot").Status, Is.EqualTo(NameResolutionStatus.NotFound));
        }

        [Test]
        public void ResolveBlendShape_MissingBlendShape_IsNotFound()
        {
            AvatarIndex index = ScanStandard();
            var binding = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Frown");
            Assert.That(index.ResolveBlendShape(binding).Status, Is.EqualTo(NameResolutionStatus.NotFound));
        }

        [Test]
        public void ResolveBlendShape_MissingRenderer_IsNotFound()
        {
            AvatarIndex index = ScanStandard();
            var binding = new BlendShapeBinding("Body/Ghost", "Smile");
            Assert.That(index.ResolveBlendShape(binding).Status, Is.EqualTo(NameResolutionStatus.NotFound));
        }

        [Test]
        public void Scan_DuplicateTransformPath_ReportsBlockingDiagnosticAndIsAmbiguous()
        {
            _fixture = AvatarFixture.Create();
            _fixture.AddFork("Extra");
            _fixture.AddFork("Extra");

            AvatarScanReport report = UnityAvatarScanner.Scan(_fixture.Root);
            Assert.That(TestHelpers.FindDiagnostic(report.Diagnostics, FaceMotionDiagnosticCodes.DuplicateTransformPath), Is.Not.Null);
            Assert.That(report.HasBlocking, Is.True);
            Assert.That(report.Index.ResolveTransform(AvatarFixture.HeadPath + "/Extra").Status,
                Is.EqualTo(NameResolutionStatus.Ambiguous));
        }

        [Test]
        public void Scan_LargeHierarchy_PerformsLookups()
        {
            _fixture = AvatarFixture.Create();
            Transform parent = _fixture.Head;
            const int count = 1000;
            var paths = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Chain_" + i.ToString("D4"));
                go.transform.SetParent(parent, worldPositionStays: false);
                parent = go.transform;
                paths.Add(RelativePathUtility.GetRelativePath(_fixture.Root.transform, go.transform));
            }

            AvatarIndex index = UnityAvatarScanner.Scan(_fixture.Root).Index;
            Assert.That(index.TransformCount, Is.GreaterThanOrEqualTo(count));
            foreach (string path in paths)
            {
                Assert.That(index.ResolveTransform(path).Status, Is.EqualTo(NameResolutionStatus.Unique));
            }
        }
    }
}
