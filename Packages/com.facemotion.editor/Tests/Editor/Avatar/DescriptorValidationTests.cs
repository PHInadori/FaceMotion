using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.VRChat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    public sealed class DescriptorValidationTests
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
        public void FindAvatarRoot_FromNestedChild_FindsDescriptor()
        {
            _fixture = AvatarFixture.Create();
            VRCAvatarDescriptor found = VRCAvatarDescriptorAdapter.FindAvatarRoot(_fixture.LeftEar.gameObject);
            Assert.That(found, Is.SameAs(_fixture.Descriptor));
        }

        [Test]
        public void FindAvatarRoot_WithoutDescriptor_ReturnsNull()
        {
            var root = new GameObject("PlainRoot");
            try
            {
                Assert.That(VRCAvatarDescriptorAdapter.FindAvatarRoot(root), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validate_NullDescriptor_IsBlockingDescriptorMissing()
        {
            DescriptorValidation validation = VRCAvatarDescriptorAdapter.Validate(null);
            Assert.That(validation.HasBlocking, Is.True);
            Assert.That(TestHelpers.FindDiagnostic(validation.Diagnostics, FaceMotionDiagnosticCodes.DescriptorMissing), Is.Not.Null);
        }

        [Test]
        public void Validate_SceneAvatar_ReportsSceneInstanceContextAndNoBlocking()
        {
            _fixture = AvatarFixture.Create();
            DescriptorValidation validation = VRCAvatarDescriptorAdapter.Validate(_fixture.Descriptor);
            Assert.That(validation.ObjectContext, Is.EqualTo(AvatarObjectContext.SceneInstance));
            Assert.That(validation.AvatarRoot, Is.SameAs(_fixture.Root));
            Assert.That(validation.HasBlocking, Is.False);
        }

        [Test]
        public void Validate_InactiveRoot_ReportsNonBlockingWarning()
        {
            _fixture = AvatarFixture.Create();
            _fixture.Root.SetActive(false);
            DescriptorValidation validation = VRCAvatarDescriptorAdapter.Validate(_fixture.Descriptor);
            Assert.That(validation.HasBlocking, Is.False);
            Assert.That(TestHelpers.FindDiagnostic(validation.Diagnostics, FaceMotionDiagnosticCodes.InactiveAvatarRoot), Is.Not.Null);
        }

        [Test]
        public void Validate_MissingAnimator_ReportsNonBlockingWarning()
        {
            _fixture = AvatarFixture.Create();
            Assert.That(_fixture.Root.GetComponent<Animator>(), Is.Null);
            DescriptorValidation validation = VRCAvatarDescriptorAdapter.Validate(_fixture.Descriptor);
            Assert.That(validation.HasBlocking, Is.False);
            Assert.That(TestHelpers.FindDiagnostic(validation.Diagnostics, FaceMotionDiagnosticCodes.MissingAnimator), Is.Not.Null);
        }

        [Test]
        public void Validate_AnimatorWithoutAvatar_ReportsNonBlockingWarning()
        {
            _fixture = AvatarFixture.Create();
            _fixture.Root.AddComponent<Animator>();
            DescriptorValidation validation = VRCAvatarDescriptorAdapter.Validate(_fixture.Descriptor);
            Assert.That(validation.HasBlocking, Is.False);
            Assert.That(TestHelpers.FindDiagnostic(validation.Diagnostics, FaceMotionDiagnosticCodes.AnimatorAvatarNull), Is.Not.Null);
        }

        [Test]
        public void DetectObjectContext_PrefabAssetAndInstance_AreDetectedSeparately()
        {
            var plainRoot = new GameObject("PrefabSource");
            plainRoot.AddComponent<VRCAvatarDescriptor>();
            var child = new GameObject("Child");
            child.transform.SetParent(plainRoot.transform, worldPositionStays: false);
            GameObject instance = null;
            try
            {
                using (var temp = new TempFaceMotionAsset())
                {
                    string prefabPath = temp.Folder + "/source.prefab";
                    AssetDatabase.DeleteAsset(prefabPath);
                    if (!PrefabUtility.SaveAsPrefabAsset(plainRoot, prefabPath))
                    {
                        Assert.Fail("Could not save prefab asset for context test.");
                    }

                    var prefabSource = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Assert.That(prefabSource, Is.Not.Null, "Saved prefab failed to load.");
                    Assert.That(VRCAvatarDescriptorAdapter.DetectObjectContext(prefabSource), Is.EqualTo(AvatarObjectContext.PrefabAsset));

                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
                    Assert.That(VRCAvatarDescriptorAdapter.DetectObjectContext(instance), Is.EqualTo(AvatarObjectContext.PrefabInstance));
                }
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                Object.DestroyImmediate(plainRoot);
            }
        }

        [Test]
        public void ScanAvatar_WithDescriptor_ReturnsIndexWithoutBlocking()
        {
            _fixture = AvatarFixture.Create();
            AvatarScanReport report = VRCAvatarDescriptorAdapter.ScanAvatar(_fixture.Descriptor);
            Assert.That(report.HasBlocking, Is.False);
            Assert.That(report.Index, Is.Not.Null);
            Assert.That(report.Index.HasTransform(AvatarFixture.HeadPath), Is.True);
        }

        [Test]
        public void ScanAvatar_NullDescriptor_ReturnsBlockingReport()
        {
            AvatarScanReport report = VRCAvatarDescriptorAdapter.ScanAvatar(null);
            Assert.That(report.HasBlocking, Is.True);
            Assert.That(report.Index, Is.Null);
        }
    }
}