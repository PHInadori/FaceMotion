using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Phase I.2 coverage for the Modular Avatar integration manifest: additive schema
    /// upgrade at the use boundary, stable integration ID preservation across reapplies,
    /// state preservation, future-schema blocking, ambiguous ownership blocking, and
    /// legacy detached reconnection. Compiled only when Modular Avatar 1.18.7 is installed.
    /// </summary>
    public sealed class ManifestMigrationTests
    {
        private const string Folder = "Assets/__FaceMotionTests_I2";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_I2");
            }

            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _clip = new AnimationClip();
            AssetDatabase.CreateAsset(_clip, Folder + "/motion.anim");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.Refresh();
            }
        }

        private static void SetLegacySchema(ModularAvatarIntegrationManifest manifest)
        {
            manifest.SchemaVersion = FaceMotionVersions.LegacySchemaVersion;
            manifest.BackendId = string.Empty;
            manifest.IntegrationId = string.Empty;
        }

        private static bool HasCode(IReadOnlyList<FaceMotionDiagnostic> diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i] != null && diagnostics[i].Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        private ModularAvatarIntegrationResult MaApply(string name = "Smile")
        {
            var backend = new ModularAvatarIntegrationBackend();
            return backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, name)));
        }

        [Test]
        public void MaApply_LegacyManifestWithOwnedObject_UpgradesAndPreservesIntegrationId()
        {
            Assert.That(MaApply().Succeeded, Is.True);
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            SetLegacySchema(manifest);

            var reapplied = MaApply();

            Assert.That(reapplied.Succeeded, Is.True);
            var migrated = (ModularAvatarIntegrationManifest)reapplied.Manifest;
            Assert.That(migrated.SchemaVersion, Is.EqualTo(FaceMotionVersions.IntegrationManifestVersion));
            Assert.That(migrated.BackendId, Is.EqualTo(ModularAvatarIntegrationBackend.BackendId));
            Assert.That(string.IsNullOrEmpty(migrated.IntegrationId), Is.False);
            Assert.That(StableId.IsValid(migrated.IntegrationId), Is.True);
            Assert.That(migrated.State, Is.EqualTo(IntegrationState.Attached));
            var node = _root.transform.Find("FaceMotion MA Smile");
            var controller = node.GetComponent<ModularAvatarMergeAnimator>().animator;
            Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo(Folder + "/FaceMotionMA_Smile/FX.controller"), "A reapply must reuse the generated folder.");
        }

        [Test]
        public void MaApply_LegacyDetachedManifestWithForeignObject_IsBlockedAmbiguous()
        {
            Assert.That(MaApply().Succeeded, Is.True);
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            SetLegacySchema(manifest);
            var foreign = new GameObject("FaceMotion MA Smile");
            foreign.transform.SetParent(_root.transform);

            var reapplied = MaApply();

            Assert.That(reapplied.Succeeded, Is.False);
            Assert.That(HasCode(reapplied.Diagnostics, "FM-OWNERSHIP-TAMPERED"), Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.SameAs(foreign.transform));
            Assert.That(_root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(0));
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Not.Null);
            Object.DestroyImmediate(foreign);
        }

        [Test]
        public void MaApply_LegacyDetachedManifest_ReconnectsReusingAssetsAndCarriesFields()
        {
            Assert.That(MaApply().Succeeded, Is.True);
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            manifest.AnimationId = "ANIM-2";
            SetLegacySchema(manifest);

            var reapplied = MaApply();

            Assert.That(reapplied.Succeeded, Is.True);
            var migrated = (ModularAvatarIntegrationManifest)reapplied.Manifest;
            Assert.That(migrated.State, Is.EqualTo(IntegrationState.Attached));
            Assert.That(migrated.AnimationId, Is.EqualTo("ANIM-2"));
            Assert.That(migrated.SchemaVersion, Is.EqualTo(FaceMotionVersions.IntegrationManifestVersion));
            var node = _root.transform.Find("FaceMotion MA Smile");
            Assert.That(node, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(node.GetComponent<ModularAvatarMergeAnimator>().animator), Is.EqualTo(Folder + "/FaceMotionMA_Smile/FX.controller"));
        }

        [Test]
        public void MaApply_FutureSchemaManifest_IsBlockedWithoutMutation()
        {
            Assert.That(MaApply().Succeeded, Is.True);
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            manifest.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion + 1;

            var reapplied = MaApply();

            Assert.That(reapplied.Succeeded, Is.False);
            Assert.That(HasCode(reapplied.Diagnostics, FaceMotionDiagnosticCodes.FutureSchemaBlocked), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset").SchemaVersion, Is.EqualTo(FaceMotionVersions.IntegrationManifestVersion + 1));
            Assert.That(_root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(0));
        }

        [Test]
        public void MaTryMigrate_RecordedState_IsNeverRecomputed()
        {
            Assert.That(MaApply().Succeeded, Is.True);
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            SetLegacySchema(manifest);
            manifest.State = IntegrationState.Detached;

            var result = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(manifest, _avatar);

            Assert.That(result.Applied, Is.True);
            Assert.That(result.Blocked, Is.False);
            Assert.That(manifest.State, Is.EqualTo(IntegrationState.Detached), "A recorded state must never be recomputed from the live hierarchy.");
        }

        [Test]
        public void MaTryMigrate_NullManifest_IsBlocked()
        {
            var result = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(null, _avatar);

            Assert.That(result.Blocked, Is.True);
            Assert.That(HasCode(result.Diagnostics, "FM-H-MA-MANIFEST"), Is.True);
        }

        [Test]
        public void MaTryMigrate_UnresolvableAvatar_IsBlockedAmbiguous()
        {
            var manifest = ScriptableObject.CreateInstance<ModularAvatarIntegrationManifest>();
            SetLegacySchema(manifest);
            manifest.AvatarGlobalId = string.Empty;
            manifest.IntegrationObjectName = "FaceMotion MA X";

            var result = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(manifest, null);

            Assert.That(result.Blocked, Is.True);
            Assert.That(HasCode(result.Diagnostics, FaceMotionDiagnosticCodes.IntegrationAmbiguous), Is.True);
        }

        [Test]
        public void MaRemove_LegacyDetachedManifest_BlocksWithoutOwnedObject()
        {
            Assert.That(MaApply().Succeeded, Is.True);
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            SetLegacySchema(manifest);

            var removal = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(removal.Succeeded, Is.False);
            Assert.That(removal.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-OWNERSHIP"));
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Not.Null);
        }
    }
}