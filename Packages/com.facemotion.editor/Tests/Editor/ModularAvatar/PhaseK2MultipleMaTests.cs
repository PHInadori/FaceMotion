using System;
using System.Collections.Generic;
using System.Reflection;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>K2 backend-only coverage for multiple independent MA integrations on one avatar.</summary>
    public sealed class PhaseK2MultipleMaTests
    {
        private const string Folder = "Assets/__FaceMotionTests_K2MA";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_K2MA");
            _root = new GameObject("Avatar"); _avatar = _root.AddComponent<VRCAvatarDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            SetApplyFailureInjector(null);
            UnityEngine.Object.DestroyImmediate(_root); AssetDatabase.DeleteAsset(Folder); AssetDatabase.Refresh();
        }

        [Test]
        public void Batch_MultipleItemsCoexistWithDistinctStableNames()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var plan = backend.PlanBatch(Requests("Smile", "Smile", "Frown"));
            var result = backend.ApplyBatch(plan);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(plan.Items[0].ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(plan.Items[1].ParameterName, Is.EqualTo("FaceMotion_Smile_2"));
            Assert.That(plan.Items[2].ParameterName, Is.EqualTo("FaceMotion_Frown"));
            Assert.That(plan.Items[0].ObjectName, Is.EqualTo("FaceMotion MA Smile"));
            Assert.That(plan.Items[1].ObjectName, Is.EqualTo("FaceMotion MA Smile_2"));
            Assert.That(plan.Items[0].RootName, Is.EqualTo("FaceMotionMA_Smile"));
            Assert.That(plan.Items[1].RootName, Is.EqualTo("FaceMotionMA_Smile_2"));
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Smile_2"), Is.Not.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Frown"), Is.Not.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMenuInstaller>().menuToAppend.controls[0].name, Is.EqualTo("Smile"));
            Assert.That(_root.transform.Find("FaceMotion MA Smile_2").GetComponent<ModularAvatarMenuInstaller>().menuToAppend.controls[0].name, Is.EqualTo("Smile_2"));
            Assert.That(_root.transform.Find("FaceMotion MA Frown").GetComponent<ModularAvatarMenuInstaller>().menuToAppend.controls[0].name, Is.EqualTo("Frown"));
        }

        [Test]
        public void Batch_ReplanAndApplyAreIdempotent()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(backend.ApplyBatch(backend.PlanBatch(Requests("Smile", "Frown"))).Succeeded, Is.True);
            var replan = backend.PlanBatch(Requests("Smile", "Frown"));

            Assert.That(replan.IsValid, Is.True);
            Assert.That(replan.Items[0].ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(replan.Items[1].ParameterName, Is.EqualTo("FaceMotion_Frown"));
            Assert.That(replan.Items[0].RootName, Is.EqualTo("FaceMotionMA_Smile"));
            Assert.That(replan.Items[1].RootName, Is.EqualTo("FaceMotionMA_Frown"));
            Assert.That(backend.ApplyBatch(replan).Succeeded, Is.True);
            Assert.That(_root.transform.childCount, Is.EqualTo(2));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Smile 1"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Frown 1"), Is.False);
        }

        [Test]
        public void Batch_PlanIsPureAndFailureRollsBackOnlyNewIntegrations()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, Clip("Existing"), Folder, "Existing"))).Succeeded, Is.True);
            var plan = backend.PlanBatch(Requests("Smile", "Frown"));

            Assert.That(_root.transform.childCount, Is.EqualTo(1));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Smile"), Is.False);
            SetApplyFailureInjector(stage => { if (stage == "after-batch-item-1") throw new InvalidOperationException("K2 injected failure"); });
            var result = backend.ApplyBatch(plan);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(_root.transform.Find("FaceMotion MA Existing"), Is.Not.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Frown"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Frown/Manifest.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Folder + "/FaceMotionMA_Smile/FX.controller"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Folder + "/FaceMotionMA_Frown/FX.controller"), Is.Null);
        }

        private IReadOnlyList<ModularAvatarIntegrationRequest> Requests(params string[] names)
        {
            var requests = new List<ModularAvatarIntegrationRequest>();
            for (var i = 0; i < names.Length; i++) requests.Add(new ModularAvatarIntegrationRequest(_avatar, Clip(names[i]), Folder, names[i]));
            return requests;
        }

        private AnimationClip Clip(string name) { var clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, Folder + "/" + name + Guid.NewGuid().ToString("N") + ".anim"); return clip; }
        private static void SetApplyFailureInjector(Action<string> value) { typeof(ModularAvatarIntegrationBackend).GetField("ApplyFailureInjector", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value); }
    }
}
