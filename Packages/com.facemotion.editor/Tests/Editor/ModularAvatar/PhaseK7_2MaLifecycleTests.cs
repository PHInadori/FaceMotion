using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.VRChat.Integration;
using facemotion_diagnostics = FaceMotion.Diagnostics.FaceMotionDiagnostic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>K7-2B: removal now deletes generated state; per-animation and batch removal are safe and idempotent.</summary>
    public sealed class PhaseK7_2MaLifecycleTests
    {
        private const string Folder = "Assets/__FaceMotionTests_K72";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_K72");
            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _clip = new AnimationClip();
            AssetDatabase.CreateAsset(_clip, Folder + "/motion.anim");
        }

        [TearDown]
        public void TearDown()
        {
            ClearSessionManifests();
            Object.DestroyImmediate(_root);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void RemoveAnimation_DeletesOnlyTheRequestedParameter()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Assert.That(Apply(backend, "Sad").Succeeded, Is.True);
            string smileParam = Parameter(backend, "Smile");
            string sadParam = Parameter(backend, "Sad");
            Assert.That(sadParam, Is.Not.EqualTo(smileParam));

            var removal = backend.RemoveAnimation(_avatar, smileParam);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(HasCode(removal.Diagnostics, "FM-H-MA-REMOVED"), Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Sad"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Folder + "/FaceMotionMA_Smile/FX.controller"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Sad/Manifest.asset"), Is.Not.Null);
            Assert.That(backend.HasExistingIntegration(_avatar, smileParam), Is.False);
            Assert.That(backend.HasExistingIntegration(_avatar, sadParam), Is.True);
        }

        [Test]
        public void RemoveAnimation_WithoutExistingParameter_IsAnIdempotentNoOpSuccess()
        {
            var removal = new ModularAvatarIntegrationBackend().RemoveAnimation(_avatar, "FaceMotion_DoesNotExist");

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(removal.Manifest, Is.Null);
            Assert.That(HasCode(removal.Diagnostics, "FM-H-MA-NOTHING-TO-REMOVE"), Is.True);
        }

        [Test]
        public void RemoveAnimations_RemovesOnlyTheSelectedParameters()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Assert.That(Apply(backend, "Sad").Succeeded, Is.True);
            Assert.That(Apply(backend, "Wink").Succeeded, Is.True);

            var removal = backend.RemoveAnimations(_avatar, new[] { Parameter(backend, "Smile"), Parameter(backend, "Sad") });

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Sad"), Is.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Wink"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Wink/Manifest.asset"), Is.Not.Null);
            Assert.That(backend.HasExistingIntegration(_avatar), Is.True);
        }

        [Test]
        public void RemoveAnimations_WithNoMatchingParameters_IsAnIdempotentNoOp()
        {
            var removal = new ModularAvatarIntegrationBackend().RemoveAnimations(_avatar, new List<string> { "FaceMotion_DoesNotExist" });

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(HasCode(removal.Diagnostics, "FM-H-MA-NOTHING-TO-REMOVE"), Is.True);
        }

        [Test]
        public void Remove_All_RemovesEveryManagedIntegration()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Assert.That(Apply(backend, "Sad").Succeeded, Is.True);

            var removal = backend.Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Sad"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Sad/Manifest.asset"), Is.Null);
            Assert.That(backend.HasExistingIntegration(_avatar), Is.False);
        }

        [Test]
        public void Remove_WhenNothingIsManaged_IsAnIdempotentNoOpSuccess()
        {
            var removal = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(HasCode(removal.Diagnostics, "FM-H-MA-NOTHING-TO-REMOVE"), Is.True);
        }

        [Test]
        public void Remove_DetachedCleanManifest_IsStaleCleanupSuccess()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Object.DestroyImmediate(_root.transform.Find("FaceMotion MA Smile").gameObject);

            var removal = backend.Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Null);
            Assert.That(HasCode(removal.Diagnostics, "FM-H-MA-REMOVED"), Is.True);
        }

        [Test]
        public void Remove_WithForeignObjectOnTheIntegrationObjectName_IsBlockedAndPreservesEverything()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Object.DestroyImmediate(_root.transform.Find("FaceMotion MA Smile").gameObject);
            var foreign = new GameObject("FaceMotion MA Smile");
            foreign.transform.SetParent(_root.transform);

            var removal = backend.Remove(_avatar);

            Assert.That(removal.Succeeded, Is.False);
            Assert.That(HasCode(removal.Diagnostics, "FM-H-MA-OWNERSHIP"), Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.SameAs(foreign.transform));
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Not.Null);
            Object.DestroyImmediate(foreign);
        }

        [Test]
        public void Remove_PreservesUnrelatedUserAssetsInsideTheGeneratedFolder()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            string userPath = Folder + "/FaceMotionMA_Smile/user.anim";
            var user = new AnimationClip();
            AssetDatabase.CreateAsset(user, userPath);

            var removal = backend.Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(userPath), Is.Not.Null);
        }

        [Test]
        public void ReinstateAfterRemoveAll_CreatesOneCleanIntegration()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Assert.That(backend.Remove(_avatar).Succeeded, Is.True);

            var reapplied = Apply(backend, "Smile");

            Assert.That(reapplied.Succeeded, Is.True);
            Assert.That(_root.GetComponentsInChildren<nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Not.Null);
            Assert.That(backend.HasExistingIntegration(_avatar), Is.True);
        }

        [Test]
        public void BatchCycle_AddRemoveAndReaddAcrossAnimations_EndsClean()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Assert.That(Apply(backend, "Sad").Succeeded, Is.True);

            Assert.That(backend.RemoveAnimation(_avatar, Parameter(backend, "Smile")).Succeeded, Is.True);
            Assert.That(Apply(backend, "Smile").Succeeded, Is.True);
            Assert.That(backend.RemoveAnimation(_avatar, Parameter(backend, "Sad")).Succeeded, Is.True);
            Assert.That(Apply(backend, "Sad").Succeeded, Is.True);

            Assert.That(backend.Remove(_avatar).Succeeded, Is.True);

            Assert.That(backend.HasExistingIntegration(_avatar), Is.False);
            Assert.That(_root.GetComponentsInChildren<nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(0));

            var reinstated = Apply(backend, "Smile");
            Assert.That(reinstated.Succeeded, Is.True);
            Assert.That(_root.GetComponentsInChildren<nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(1));
        }

        [Test]
        public void PresenceCache_HasExistingIntegration_ScansTheBackendOncePerAvatarAndPerParameter()
        {
            var session = new FaceMotionEditorSession();
            var backend = new CountingBackend();
            var cache = new ModularAvatarIntegrationPresenceCache(backend, session);

            Assert.That(cache.HasExistingIntegration(_avatar), Is.True);
            Assert.That(cache.HasExistingIntegration(_avatar), Is.True);
            Assert.That(cache.HasExistingIntegration(_avatar), Is.True);
            Assert.That(backend.AllScans, Is.EqualTo(1));

            Assert.That(cache.HasExistingIntegration(_avatar, "FaceMotion_Smile"), Is.True);
            Assert.That(cache.HasExistingIntegration(_avatar, "FaceMotion_Smile"), Is.True);
            Assert.That(backend.ParameterScans, Is.EqualTo(1));

            Assert.That(cache.HasExistingIntegration(_avatar, "FaceMotion_Sad"), Is.True);
            Assert.That(backend.ParameterScans, Is.EqualTo(2));

            var other = new GameObject("OtherAvatar").AddComponent<VRCAvatarDescriptor>();
            try
            {
                Assert.That(cache.HasExistingIntegration(other), Is.True);
                Assert.That(backend.AllScans, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void PresenceCache_NullBackendOrNullInputs_ShortCircuitToFalse()
        {
            var session = new FaceMotionEditorSession();
            var cache = new ModularAvatarIntegrationPresenceCache(null, session);

            Assert.That(cache.HasExistingIntegration(_avatar), Is.False);
            Assert.That(cache.HasExistingIntegration(_avatar, "FaceMotion_Smile"), Is.False);

            var liveBackend = new CountingBackend();
            var live = new ModularAvatarIntegrationPresenceCache(liveBackend, session);
            Assert.That(live.HasExistingIntegration(null), Is.False);
            Assert.That(live.HasExistingIntegration(_avatar, null), Is.False);
            Assert.That(live.HasExistingIntegration(_avatar, ""), Is.False);
            Assert.That(liveBackend.AllScans, Is.Zero);
            Assert.That(liveBackend.ParameterScans, Is.Zero);
        }

        private ModularAvatarIntegrationResult Apply(IModularAvatarIntegrationBackend backend, string stem)
        {
            var batch = backend.PlanBatch(new[] { new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, stem) });
            return backend.ApplyBatch(batch).Items[0];
        }

        private string Parameter(IModularAvatarIntegrationBackend backend, string stem)
        {
            return backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, stem)).ParameterName;
        }

        private static bool HasCode(IReadOnlyList<facemotion_diagnostics> diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Code == code) return true;
            }
            return false;
        }

        private static void ClearSessionManifests()
        {
            var field = typeof(ModularAvatarIntegrationBackend)
                .GetField("SessionManifests", BindingFlags.Static | BindingFlags.NonPublic);
            ((IDictionary)field.GetValue(null)).Clear();
        }

        private sealed class CountingBackend : IModularAvatarIntegrationBackend
        {
            public int AllScans;
            public int ParameterScans;

            public bool HasExistingIntegration(VRCAvatarDescriptor avatar) { AllScans++; return true; }
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter) { ParameterScans++; return true; }
            public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request) => throw new System.NotImplementedException();
            public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan) => throw new System.NotImplementedException();
            public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar) => throw new System.NotImplementedException();
            public ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter) => throw new System.NotImplementedException();
            public ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) => throw new System.NotImplementedException();
            public ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests) => throw new System.NotImplementedException();
            public ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan) => throw new System.NotImplementedException();
        }
    }
}