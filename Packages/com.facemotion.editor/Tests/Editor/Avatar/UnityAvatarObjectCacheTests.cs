using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class UnityAvatarObjectCacheTests
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

        [Test]
        public void Rebuild_StandardFixture_PopulatesLookups()
        {
            _fixture = AvatarFixture.Create();
            var cache = new UnityAvatarObjectCache();
            AvatarScanReport report = cache.Rebuild(_fixture.Root);

            Assert.That(report.HasBlocking, Is.False);
            Assert.That(cache.TryGetTransform(AvatarFixture.HeadPath, out Transform head), Is.True);
            Assert.That(head, Is.SameAs(_fixture.Head));
            Assert.That(cache.TryGetRenderer(AvatarFixture.FaceRendererPath, out SkinnedMeshRenderer face), Is.True);
            Assert.That(face, Is.SameAs(_fixture.FaceRenderer));

            var identity = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            Assert.That(cache.TryGetBlendShapeIndex(identity, out int index), Is.True);
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void Rebuild_IncrementsRevision()
        {
            _fixture = AvatarFixture.Create();
            var cache = new UnityAvatarObjectCache();
            Assert.That(cache.Revision, Is.EqualTo(0));
            cache.Rebuild(_fixture.Root);
            Assert.That(cache.Revision, Is.EqualTo(1));
            cache.Rebuild(_fixture.Root);
            Assert.That(cache.Revision, Is.EqualTo(2));
        }

        [Test]
        public void Rebuild_AfterStructuralChange_ForgetsOldObjects()
        {
            _fixture = AvatarFixture.Create();
            var cache = new UnityAvatarObjectCache();
            cache.Rebuild(_fixture.Root);

            _fixture.Face.name = "FaceNew";

            AvatarScanReport report = cache.Rebuild(_fixture.Root);
            Assert.That(cache.TryGetRenderer(AvatarFixture.FaceRendererPath, out _), Is.False);
            Assert.That(cache.TryGetTransform(AvatarFixture.FaceRendererPath, out _), Is.False);
            Assert.That(cache.TryGetRenderer("Body/FaceNew", out SkinnedMeshRenderer renamed), Is.True);
            Assert.That(renamed, Is.SameAs(_fixture.FaceRenderer));
        }

        [Test]
        public void Rebuild_AmbiguousPath_ReportsFalseNotArbitraryMatch()
        {
            _fixture = AvatarFixture.Create();
            _fixture.AddFork("Extra");
            _fixture.AddFork("Extra");
            var cache = new UnityAvatarObjectCache();
            cache.Rebuild(_fixture.Root);

            Assert.That(cache.TryGetTransform(AvatarFixture.HeadPath + "/Extra", out _), Is.False);
        }

        [Test]
        public void Rebuild_AmbiguousBlendShape_ReportsFalse()
        {
            _fixture = AvatarFixture.Create();
            var cache = new UnityAvatarObjectCache();
            cache.Rebuild(_fixture.Root);

            var smiling = new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            Assert.That(cache.TryGetBlendShapeIndex(smiling, out int index), Is.True);
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void Clear_ResetsLookups()
        {
            _fixture = AvatarFixture.Create();
            var cache = new UnityAvatarObjectCache();
            cache.Rebuild(_fixture.Root);
            Assert.That(cache.TryGetTransform(AvatarFixture.HeadPath, out _), Is.True);

            cache.Clear();
            Assert.That(cache.TryGetTransform(AvatarFixture.HeadPath, out _), Is.False);
            Assert.That(cache.TryGetRenderer(AvatarFixture.FaceRendererPath, out _), Is.False);
            Assert.That(cache.TryGetBlendShapeIndex(new BlendShapeBinding(AvatarFixture.FaceRendererPath, "Mouth_Smile"), out _), Is.False);
        }
    }
}