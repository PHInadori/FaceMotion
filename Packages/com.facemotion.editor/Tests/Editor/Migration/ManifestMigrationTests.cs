using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Phase I.2 coverage for the Direct and Modular Avatar integration manifests: additive
    /// schema upgrade at the use boundary, stable integration ID preservation across
    /// reapplies, state reconstruction, future-schema blocking, ambiguous ownership
    /// blocking, and legacy detached reconnection.
    /// </summary>
    public sealed class ManifestMigrationTests
    {
        private const string Folder = "Assets/__FaceMotionTests_I2";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimatorController _fx;
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
            _fx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = _fx } };
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

        private static void SetLegacySchema(DirectIntegrationManifest manifest)
        {
            manifest.SchemaVersion = FaceMotionVersions.LegacySchemaVersion;
            manifest.BackendId = string.Empty;
            manifest.IntegrationId = string.Empty;
        }

        private DirectIntegrationPlan Plan(string name = "Smile")
        {
            return DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, Folder, name));
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

        // ---- Direct integration ----

        [Test]
        public void DirectApply_ReapplyOfLegacyManifest_UpgradesAndPreservesIntegrationId()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            Assert.That(first.Succeeded, Is.True);
            SetLegacySchema(first.Manifest);

            var reapplied = DirectVRChatIntegration.Apply(Plan());

            Assert.That(reapplied.Succeeded, Is.True);
            Assert.That(reapplied.Manifest.SchemaVersion, Is.EqualTo(FaceMotionVersions.IntegrationManifestVersion));
            Assert.That(reapplied.Manifest.BackendId, Is.EqualTo(DirectVRChatIntegration.BackendId));
            Assert.That(string.IsNullOrEmpty(reapplied.Manifest.IntegrationId), Is.False);
            Assert.That(StableId.IsValid(reapplied.Manifest.IntegrationId), Is.True);
            Assert.That(HasCode(reapplied.Diagnostics, FaceMotionDiagnosticCodes.UpgradedSchema), Is.True);
        }

        [Test]
        public void DirectApply_ConsecutiveReapplies_PreserveTheIntegrationId()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            SetLegacySchema(first.Manifest);
            var second = DirectVRChatIntegration.Apply(Plan());

            var third = DirectVRChatIntegration.Apply(Plan());

            Assert.That(third.Succeeded, Is.True);
            Assert.That(third.Manifest.IntegrationId, Is.EqualTo(second.Manifest.IntegrationId), "The integration ID must be preserved across reapplies.");
            Assert.That(string.IsNullOrEmpty(third.Manifest.IntegrationId), Is.False);
        }

        [Test]
        public void DirectRollback_ThenFreshApply_StartsANewIntegrationId()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            string firstId = first.Manifest.IntegrationId;

            Assert.That(DirectVRChatIntegration.Rollback(first.Manifest, out _), Is.True);
            var fresh = DirectVRChatIntegration.Apply(Plan());

            Assert.That(fresh.Succeeded, Is.True);
            Assert.That(fresh.Manifest.IntegrationId, Is.Not.EqualTo(firstId), "A full rollback ends the integration; a fresh apply must start a new ID.");
        }

        [Test]
        public void DirectApply_LegacyManifestWithCarryFields_KeepsAnimationIdAndFingerprint()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            first.Manifest.AnimationId = "ANIM-1";
            first.Manifest.AvatarFingerprint = "FP-1";
            SetLegacySchema(first.Manifest);

            var reapplied = DirectVRChatIntegration.Apply(Plan());

            Assert.That(reapplied.Succeeded, Is.True);
            Assert.That(reapplied.Manifest.AnimationId, Is.EqualTo("ANIM-1"));
            Assert.That(reapplied.Manifest.AvatarFingerprint, Is.EqualTo("FP-1"));
        }

        [Test]
        public void DirectApply_FutureSchemaManifest_IsBlockedWithoutMutation()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            var generatedFx = first.Manifest.GeneratedFx;
            first.Manifest.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion + 1;

            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(HasCode(result.Diagnostics, FaceMotionDiagnosticCodes.FutureSchemaBlocked), Is.True);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(generatedFx), "A future-schema integration must never be rolled back or mutated.");
            Assert.That(first.Manifest.SchemaVersion, Is.EqualTo(FaceMotionVersions.IntegrationManifestVersion + 1));
        }

        [Test]
        public void DirectTryMigrate_LegacyManifestWithoutAvatar_IsBlockedAmbiguous()
        {
            var manifest = ScriptableObject.CreateInstance<DirectIntegrationManifest>();
            SetLegacySchema(manifest);

            var result = DirectIntegrationManifestMigration.TryMigrateOnUse(manifest);

            Assert.That(result.Blocked, Is.True);
            Assert.That(HasCode(result.Diagnostics, FaceMotionDiagnosticCodes.IntegrationAmbiguous), Is.True);
        }

        [Test]
        public void DirectRollback_LegacyManifestWithForeignOwnedPath_KeepsTheForeignAsset()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            string root = Folder + "/FaceMotion_Smile";
            var foreign = new AnimationClip();
            AssetDatabase.CreateAsset(foreign, root + "/UserMemo.anim");
            first.Manifest.OwnedAssetPaths = new[] { root + "/FX.controller", root + "/UserMemo.anim" };
            SetLegacySchema(first.Manifest);

            var rolledBack = DirectVRChatIntegration.Rollback(first.Manifest, out var diagnostics);

            Assert.That(rolledBack, Is.True);
            Assert.That(HasCode(diagnostics, "FM-G-OWNERSHIP"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/UserMemo.anim"), Is.Not.Null, "A foreign owned path must not be deleted.");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "/FX.controller"), Is.Null, "A canonical generated path must still be deleted.");
        }

        [Test]
        public void DirectApply_LegacyDetachedManifest_ReconnectsAndRecordsAttached()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            SetLegacySchema(first.Manifest);
            SetFx(_avatar, _fx);
            _avatar.expressionParameters = null;
            _avatar.expressionsMenu = null;

            var reapplied = DirectVRChatIntegration.Apply(Plan());

            Assert.That(reapplied.Succeeded, Is.True);
            Assert.That(HasCode(reapplied.Diagnostics, FaceMotionDiagnosticCodes.UpgradedSchema), Is.True);
            Assert.That(reapplied.Manifest.State, Is.EqualTo(IntegrationState.Attached));
            Assert.That(_avatar.expressionParameters, Is.Not.Null);
            Assert.That(_avatar.expressionsMenu, Is.Not.Null);
        }

        private static void SetFx(VRCAvatarDescriptor avatar, RuntimeAnimatorController controller)
        {
            var layers = avatar.baseAnimationLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    layers[i].animatorController = controller;
                    avatar.baseAnimationLayers = layers;
                    return;
                }
            }
        }
    }
}