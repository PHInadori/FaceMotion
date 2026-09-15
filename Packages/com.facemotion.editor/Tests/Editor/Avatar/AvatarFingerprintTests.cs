using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Editor.Avatar;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class AvatarFingerprintTests
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
        public void Fingerprint_IsDeterministic_AcrossScans()
        {
            AvatarIndex first = ScanStandard();
            AvatarFingerprint firstFingerprint = first.Fingerprint;

            AvatarIndex second = UnityAvatarScanner.Scan(_fixture.Root).Index;
            Assert.That(second.Fingerprint.EqualsValue(firstFingerprint), Is.True);
        }

        [Test]
        public void Fingerprint_IsStableAcrossEditorRestarts()
        {
            AvatarIndex index = ScanStandard();
            string hash = index.Fingerprint.Hash;
            Assert.That(hash, Is.Not.Null);
            Assert.That(hash.Length, Is.EqualTo(AvatarFingerprint.HashLength));

            AvatarFingerprint recomputed = RecomputeFromIndex(index);
            Assert.That(recomputed.Hash, Is.EqualTo(hash));
        }

        [Test]
        public void Fingerprint_IgnoresAvatarRootName()
        {
            AvatarIndex index = ScanStandard();
            AvatarFingerprint before = index.Fingerprint;
            _fixture.Root.name = "TotallyDifferentAvatar";

            AvatarIndex renamed = UnityAvatarScanner.Scan(_fixture.Root).Index;
            Assert.That(renamed.Fingerprint.EqualsValue(before), Is.True);
        }

        [Test]
        public void Fingerprint_IgnoresBlendShapeWeight()
        {
            AvatarIndex index = ScanStandard();
            AvatarFingerprint before = index.Fingerprint;
            _fixture.FaceRenderer.SetBlendShapeWeight(0, 85f);

            AvatarIndex weighted = UnityAvatarScanner.Scan(_fixture.Root).Index;
            AvatarFingerprint after = weighted.Fingerprint;
            Assert.That(after.EqualsValue(before), Is.True);
        }

        [Test]
        public void Fingerprint_ChangesWhenTransformPathChanges()
        {
            AvatarFingerprint before = ScanStandard().Fingerprint;
            _fixture.Hips.name = "Pelvis";

            AvatarFingerprint after = UnityAvatarScanner.Scan(_fixture.Root).Index.Fingerprint;
            Assert.That(after.EqualsValue(before), Is.False);
        }

        [Test]
        public void Fingerprint_ChangesWhenBlendShapeAdded()
        {
            AvatarFingerprint before = ScanStandard().Fingerprint;
            var replacement = new Mesh();
            replacement.name = "FaceMesh";
            replacement.vertices = _fixture.FaceMesh.vertices;
            replacement.triangles = _fixture.FaceMesh.triangles;
            replacement.RecalculateNormals();
            replacement.AddBlendShapeFrame("Mouth_Smile", 0f, _fixture.FaceMesh.vertices, null, null);
            replacement.AddBlendShapeFrame("Mouth_Frown", 0f, _fixture.FaceMesh.vertices, null, null);
            _fixture.FaceRenderer.sharedMesh = replacement;

            AvatarFingerprint after = UnityAvatarScanner.Scan(_fixture.Root).Index.Fingerprint;
            Assert.That(after.EqualsValue(before), Is.False);
            UnityEngine.Object.DestroyImmediate(replacement);
        }

        [Test]
        public void Fingerprint_IgnoresMaterialAssignments()
        {
            AvatarFingerprint before = ScanStandard().Fingerprint;
            var material = new Material(Shader.Find("Standard"));
            _fixture.FaceRenderer.sharedMaterials = new[] { material };

            AvatarFingerprint after = UnityAvatarScanner.Scan(_fixture.Root).Index.Fingerprint;
            Assert.That(after.EqualsValue(before), Is.True);
            UnityEngine.Object.DestroyImmediate(material);
        }

        [Test]
        public void FingerprintBuilder_CanonicalLines_AreOrdinalAndShapeTagged()
        {
            var transforms = new List<TransformIndexEntry>
            {
                new TransformIndexEntry("", string.Empty, 0, string.Empty),
                new TransformIndexEntry("Body/Face", "Face", 1, "Body")
            };
            var renderers = new List<RendererIndexEntry>
            {
                new RendererIndexEntry("Body/Face", "Face", true, 1, "FaceMesh")
            };
            var blendShapes = new List<BlendShapeIndexEntry>
            {
                new BlendShapeIndexEntry("Body/Face", "Face", "Mouth_Smile", 0, 0f)
            };

            string hash = AvatarFingerprintBuilder.ComputeHash(transforms, renderers, blendShapes);
            Assert.That(hash, Is.Not.Null);
            Assert.That(hash.Length, Is.EqualTo(AvatarFingerprint.HashLength));

            string shuffled = AvatarFingerprintBuilder.ComputeHash(
                new List<TransformIndexEntry> { transforms[1], transforms[0] },
                renderers,
                blendShapes);
            Assert.That(shuffled, Is.EqualTo(hash));
        }

        [Test]
        public void FingerprintBuilder_LineOrderIndependent()
        {
            var transforms = new List<TransformIndexEntry> { new TransformIndexEntry("T2", "T2", 1, string.Empty) };
            var renderers = new List<RendererIndexEntry>();
            var blendShapes = new List<BlendShapeIndexEntry>();

            string hash = AvatarFingerprintBuilder.ComputeHash(transforms, renderers, blendShapes);
            string other = AvatarFingerprintBuilder.ComputeHash(new List<TransformIndexEntry>(), renderers, blendShapes);
            Assert.That(hash, Is.Not.EqualTo(other));
        }

        private static AvatarFingerprint RecomputeFromIndex(AvatarIndex index)
        {
            return AvatarFingerprintBuilder.Compute(index.Transforms, index.Renderers, index.BlendShapes);
        }
    }
}