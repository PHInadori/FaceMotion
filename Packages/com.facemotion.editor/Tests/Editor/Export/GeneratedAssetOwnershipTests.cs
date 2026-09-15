using System.IO;
using FaceMotion.Editor.Export;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class GeneratedAssetOwnershipTests
    {
        private const string Folder = "Assets/__FaceMotionTests_Owned";
        private string _root;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_Owned");
            var name = "Gen_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
            _root = Folder + "/" + name;
            AssetDatabase.CreateFolder(Folder, name);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void IsSafeOwnedAssetPath_RejectsTraversalSegments()
        {
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(_root + "/../escape.asset", _root), Is.False);
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(_root + "/a/../../escape.asset", _root), Is.False);
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(_root + "/./fx.controller", _root), Is.False);
        }

        [Test]
        public void IsSafeOwnedAssetPath_RejectsPathsOutsideTheRootFolder()
        {
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(Folder + "/outside.asset", _root), Is.False);
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath("Assets/other/FX.controller", _root), Is.False);
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(_root, _root), Is.False);
        }

        [Test]
        public void IsSafeOwnedAssetPath_AcceptsOwnedAssetsInsideTheRoot()
        {
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(_root + "/FX.controller", _root), Is.True);
            Assert.That(GeneratedAssetOwnership.IsSafeOwnedAssetPath(_root + "/Reset.anim", _root), Is.True);
        }

        [Test]
        public void IsCanonicalGeneratedFileName_RecognizesTheGeneratedSet()
        {
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("FX.controller"), Is.True);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("Parameters.asset"), Is.True);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("Menu.asset"), Is.True);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("FaceMotion.menu.asset"), Is.True);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("Reset.anim"), Is.True);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("Manifest.asset"), Is.True);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName("MyUserMemo.anim"), Is.False);
            Assert.That(GeneratedAssetOwnership.IsCanonicalGeneratedFileName(string.Empty), Is.False);
        }

        [Test]
        public void FolderHasForeignContent_IgnoresHiddenMetaFiles()
        {
            var meta = FullPath(_root) + "/stray.meta";
            try
            {
                File.WriteAllText(meta, "orphan meta must not block folder deletion");
                Assert.That(GeneratedAssetOwnership.FolderHasForeignContent(_root, null, out _), Is.False);
            }
            finally
            {
                if (File.Exists(meta)) File.Delete(meta);
            }
        }

        [Test]
        public void FolderHasForeignContent_DetectsForeignAssetsAndSubfolders()
        {
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, _root + "/user.anim");
            Assert.That(GeneratedAssetOwnership.FolderHasForeignContent(_root, null, out var file), Is.True);
            Assert.That(file, Is.EqualTo(_root + "/user.anim"));
            AssetDatabase.DeleteAsset(_root + "/user.anim");

            AssetDatabase.CreateFolder(_root, "UserData");
            Assert.That(GeneratedAssetOwnership.FolderHasForeignContent(_root, null, out _), Is.True);
            Assert.That(GeneratedAssetOwnership.FolderHasForeignContent(_root, new[] { _root + "/FX.controller" }, out _), Is.True);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void TryDeleteEmptyOwnedFolder_DeletesOnlyAnEmptyOwnedFolder()
        {
            Assert.That(GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(_root, null), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(_root), Is.False);
        }

        [Test]
        public void TryDeleteEmptyOwnedFolder_KeepsAFolderWithForeignContent()
        {
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, _root + "/user.anim");
            Assert.That(GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(_root, null), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(_root), Is.True);
            AssetDatabase.DeleteAsset(_root + "/user.anim");
        }

        private static string FullPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }
    }
}