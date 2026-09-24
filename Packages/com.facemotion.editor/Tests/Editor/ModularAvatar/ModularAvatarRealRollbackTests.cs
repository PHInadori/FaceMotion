using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// K7-3C: real ModularAvatarIntegrationBackend rollback atomicity. Every test drives the
    /// reconciliation service against the REAL backend wrapped in a delegating fault backend that
    /// performs a genuine mutation and then reports failure. Restore must return the independent
    /// baseline (managed state, generated files + meta/GUIDs, hierarchy, foreign content) exactly.
    /// </summary>
    public sealed class ModularAvatarRealRollbackTests
    {
        private const string Folder = "Assets/__FM_K73_RollbackTest";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private FaceMotionProject _project;
        private FaceMotionAnimationData _animA;
        private FaceMotionAnimationData _animB;
        private FaceMotionAnimationData _animC;
        private AnimationClip _seedClip;

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__FM_K73_RollbackTest");
            AssetDatabase.Refresh();
            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _seedClip = new AnimationClip();
            AssetDatabase.CreateAsset(_seedClip, Folder + "/seed.anim");
            _project = FaceMotionProject.CreateNew();
            _animA = FaceMotionAnimationData.Create("Alpha");
            _project.AddAnimation(_animA);
            _animB = FaceMotionAnimationData.Create("Beta");
            _project.AddAnimation(_animB);
            _animC = FaceMotionAnimationData.Create("Gamma");
            _project.AddAnimation(_animC);
        }

        [TearDown]
        public void TearDown()
        {
            SetRollbackFailureInjector(null);
            ClearSessionManifests();
            UnityEngine.Object.DestroyImmediate(_root);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void RealMidApply_RemovesLeakedIntegrationAndRestoresBaseline()
        {
            var wrapper = new RealRollbackFaultBackend { ApplyFailAfterItem = 1 };
            var invalidations = 0;
            string baseline = CaptureBaseline(wrapper);
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, new[] { _animA.AnimationId, _animB.AnimationId }, Folder, wrapper, _ => invalidations++);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            Assert.That(invalidations, Is.EqualTo(1));
            AssertBaseline(baseline, "Mid-apply rollback did not restore the baseline exactly.");
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Alpha"), Is.False, "Leaked integration folder survived rollback.");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Alpha.anim"), Is.Null, "Exported clip leaked after rollback.");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Beta.anim"), Is.Null, "Exported clip leaked after rollback.");
        }

        [Test]
        public void RealMidRemove_RestoresEveryRemovedIntegrationAndPreservesOthers()
        {
            var wrapper = new RealRollbackFaultBackend { RemoveFailAfterItem = 1 };
            Assert.That(SeedMany(wrapper, ("Alpha", _animA.AnimationId), ("Beta", _animB.AnimationId), ("Gamma", _animC.AnimationId)).Succeeded, Is.True, "Seed integrations: " + Describe(SeedLast));
            string baseline = CaptureBaseline(wrapper);
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, new[] { _animA.AnimationId }, Folder, wrapper);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            AssertBaseline(baseline, "Mid-remove rollback did not restore the baseline exactly.");
            Assert.That(_root.transform.Find("FaceMotion MA Beta"), Is.Not.Null, "Removed integration hierarchy was not restored.");
            Assert.That(_root.transform.Find("FaceMotion MA Gamma"), Is.Not.Null, "Removed integration hierarchy was not restored.");
            Assert.That(_root.transform.Find("FaceMotion MA Alpha"), Is.Not.Null, "Kept integration was disturbed.");
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Beta/Manifest.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Gamma/Manifest.asset"), Is.Not.Null);
        }

        [Test]
        public void RealEmptyBaseline_LeavesNothingBehindAfterRollback()
        {
            var wrapper = new RealRollbackFaultBackend { ApplyFailAfterItem = 1 };
            string baseline = CaptureBaseline(wrapper);
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, new[] { _animA.AnimationId }, Folder, wrapper);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            AssertBaseline(baseline, "Empty-baseline rollback did not remove every traced mutation.");
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Alpha"), Is.False, "Leaked integration folder survived rollback.");
            Assert.That(_root.transform.Find("FaceMotion MA Alpha"), Is.Null, "Leaked integration node survived rollback.");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Alpha.anim"), Is.Null, "Exported clip leaked after rollback.");
        }

        [Test]
        public void RealRemoveRollback_RestoresOwnedAssetsAndManifest()
        {
            var wrapper = new RealRollbackFaultBackend { RemoveFailAfterItem = 1 };
            Assert.That(SeedMany(wrapper, ("Alpha", _animA.AnimationId)).Succeeded, Is.True, "Seed: " + Describe(SeedLast));
            string baseline = CaptureBaseline(wrapper);
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), Folder, wrapper);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            AssertBaseline(baseline, "Remove rollback did not restore owned assets and the manifest.");
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Alpha/Manifest.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Folder + "/FaceMotionMA_Alpha/FX.controller"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_Alpha/Reset.anim"), Is.Not.Null);
        }

        [Test]
        public void RealRollback_PreservesAssetGuidsAndMetaFiles()
        {
            var wrapper = new RealRollbackFaultBackend { RemoveFailAfterItem = 1 };
            Assert.That(SeedMany(wrapper, ("Alpha", _animA.AnimationId)).Succeeded, Is.True, "Seed: " + Describe(SeedLast));
            var before = CaptureGuidsAndMetas();
            string baseline = CaptureBaseline(wrapper);
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), Folder, wrapper);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            var after = CaptureGuidsAndMetas();
            Assert.That(after, Is.EqualTo(before), "Rollback changed generated asset GUIDs or .meta content.");
            AssertBaseline(baseline, "GUID/meta stability rollback did not restore the baseline.");
        }

        [Test]
        public void RealRollback_PreservesForeignSceneAndFileContent()
        {
            var wrapper = new RealRollbackFaultBackend { RemoveFailAfterItem = 1 };
            Assert.That(SeedMany(wrapper, ("Alpha", _animA.AnimationId)).Succeeded, Is.True, "Seed: " + Describe(SeedLast));
            var foreign = new GameObject("Foreign", typeof(MeshRenderer), typeof(MeshFilter));
            foreign.transform.SetParent(_root.transform);
            var userFile = new AnimationClip();
            AssetDatabase.CreateAsset(userFile, Folder + "/FaceMotionMA_Alpha/user-root.anim");
            var userTop = new AnimationClip();
            AssetDatabase.CreateAsset(userTop, Folder + "/user-top.anim");
            string baseline = CaptureBaseline(wrapper);
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), Folder, wrapper);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            AssertBaseline(baseline, "Rollback disturbed foreign scene objects or non-canonical files.");
            Assert.That(_root.transform.Find("Foreign"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_Alpha/user-root.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/user-top.anim"), Is.Not.Null);
        }

        [Test]
        public void RealRollbackFailure_ReportsFailedRollbackFailed()
        {
            SetRollbackFailureInjector(_ => throw new InvalidOperationException("real rollback failure"));
            var wrapper = new RealRollbackFaultBackend { ApplyFailAfterItem = 1 };
            var invalidations = 0;
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, new[] { _animA.AnimationId }, Folder, wrapper, _ => invalidations++);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRollbackFailed));
            Assert.That(invalidations, Is.EqualTo(1));
        }

        private ModularAvatarIntegrationBatchResult SeedMany(IModularAvatarIntegrationBackend backend, params (string Name, string AnimationId)[] specs)
        {
            var requests = new List<ModularAvatarIntegrationRequest>();
            for (var i = 0; i < specs.Length; i++) requests.Add(new ModularAvatarIntegrationRequest(_avatar, _seedClip, Folder, specs[i].Name, specs[i].AnimationId));
            SeedLast = backend.ApplyBatch(backend.PlanBatch(requests));
            return SeedLast;
        }

        private ModularAvatarIntegrationBatchResult SeedLast;

        private static string Describe(ModularAvatarIntegrationBatchResult result)
        {
            if (result == null) return "(null result)";
            var sb = new StringBuilder();
            sb.Append("Succeeded=").Append(result.Succeeded);
            if (result.Diagnostics != null)
            {
                for (var d = 0; d < result.Diagnostics.Count; d++)
                {
                    sb.Append(" / ").Append(result.Diagnostics[d].Code).Append(": ").Append(result.Diagnostics[d].Message);
                }
            }
            return sb.ToString();
        }

        private void AssertBaseline(string expected, string message)
        {
            string actual = CaptureBaseline(new RealRollbackFaultBackend());
            if (actual == expected) return;
            var inspect = new RealRollbackFaultBackend().InspectManagedState(_avatar);
            var dump = new StringBuilder();
            for (var i = 0; i < inspect.Diagnostics.Count; i++)
                dump.Append('[').Append(inspect.Diagnostics[i].Code).Append(": ").Append(inspect.Diagnostics[i].Message).Append("] ");
            var manifests = AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest");
            Assert.That(actual, Is.EqualTo(expected), message + " | MANAGED-DIAG: " + dump + " | MANIFESTS-FOUND: " + manifests.Length);
        }

        private string CaptureBaseline(IModularAvatarIntegrationBackend backend)
        {
            var builder = new StringBuilder();
            string fullFolder = FullPath(Folder);
            builder.AppendLine("FILE-TREE");
            var files = new List<string>(Directory.GetFiles(fullFolder, "*", SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Count; i++)
            {
                string rel = files[i].Substring(fullFolder.Length).Replace('\\', '/');
                string assetPath = Folder + rel;
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                builder.AppendLine("F " + rel + " " + (string.IsNullOrEmpty(guid) ? "-" : "[" + guid + "]") + " " + Hash(File.ReadAllBytes(files[i])));
            }
            var dirs = new List<string>(Directory.GetDirectories(fullFolder, "*", SearchOption.AllDirectories));
            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < dirs.Count; i++)
            {
                string rel = dirs[i].Substring(fullFolder.Length).Replace('\\', '/');
                builder.AppendLine("D " + rel);
            }
            builder.AppendLine("SCENE");
            var nodes = new List<Transform>();
            CollectChildren(_avatar.transform, nodes);
            nodes.Sort((a, b) => string.CompareOrdinal(RelativePath(a), RelativePath(b)));
            for (var i = 0; i < nodes.Count; i++)
            {
                Transform node = nodes[i];
                var components = new List<string>(node.GetComponents<Component>().Length);
                foreach (var component in node.GetComponents<Component>())
                {
                    if (component == null) components.Add("<null>");
                    else components.Add(component.GetType().Name);
                }
                components.Sort(StringComparer.Ordinal);
                builder.AppendLine("N " + RelativePath(node) + " [" + string.Join(",", components.ToArray()) + "]");
            }
            builder.AppendLine("MANAGED");
            var managed = new List<string>();
            var inspected = backend.InspectManagedState(_avatar);
            for (var i = 0; i < inspected.Items.Count; i++) managed.Add(inspected.Items[i].ParameterName + "=" + inspected.Items[i].AnimationId);
            managed.Sort(StringComparer.Ordinal);
            builder.AppendLine(string.Join("|", managed.ToArray()));
            return builder.ToString();
        }

        private string CaptureGuidsAndMetas()
        {
            var builder = new StringBuilder();
            string fullFolder = FullPath(Folder);
            var files = new List<string>(Directory.GetFiles(fullFolder, "*", SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Count; i++)
            {
                string rel = files[i].Substring(fullFolder.Length).Replace('\\', '/');
                if (rel.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string assetPath = Folder + rel;
                builder.AppendLine(assetPath + " -> " + AssetDatabase.AssetPathToGUID(assetPath) + " meta=" + (File.Exists(fullFolder + rel + ".meta") ? Hash(File.ReadAllBytes(fullFolder + rel + ".meta")) : "-"));
            }
            return builder.ToString();
        }

        private static void CollectChildren(Transform parent, List<Transform> result)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                result.Add(child);
                CollectChildren(child, result);
            }
        }

        private string RelativePath(Transform child)
        {
            var names = new List<string>();
            var cursor = child;
            while (cursor != null && cursor != _avatar.transform)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }
            names.Reverse();
            return "/" + string.Join("/", names.ToArray());
        }

        private static string FullPath(string assetPath)
        {
            var normalized = assetPath.Replace('\\', '/');
            var data = Application.dataPath.Replace('\\', '/');
            var rel = normalized.Substring("Assets/".Length);
            return data + "/" + rel;
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        private static void SetRollbackFailureInjector(Action<string> value)
        {
            var field = typeof(ModularAvatarIntegrationBackend)
                .GetField("RollbackFailureInjector", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(null, value);
        }

        private static void ClearSessionManifests()
        {
            var field = typeof(ModularAvatarIntegrationBackend)
                .GetField("SessionManifests", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            ((System.Collections.IDictionary)field.GetValue(null)).Clear();
        }

        /// <summary>
        /// Test-only delegating wrapper around the REAL ModularAvatarIntegrationBackend. It runs
        /// genuine Apply/RemoveAnimation mutations and, after a configurable number of completed
        /// items, reports failure so the reconciliation service must roll the transaction back.
        /// </summary>
        private sealed class RealRollbackFaultBackend : IModularAvatarIntegrationBackend, IModularAvatarIntegrationRollbackBackend, IModularAvatarDesiredStateBackend
        {
            private readonly ModularAvatarIntegrationBackend _inner = new ModularAvatarIntegrationBackend();
            public int ApplyFailAfterItem = int.MaxValue;
            public int RemoveFailAfterItem = int.MaxValue;
            private int _applied;
            private int _removed;

            public bool HasExistingIntegration(VRCAvatarDescriptor avatar) => _inner.HasExistingIntegration(avatar);
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter) => _inner.HasExistingIntegration(avatar, parameter);
            public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request) => _inner.Plan(request);
            public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan) => _inner.Apply(plan);
            public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar) => _inner.Remove(avatar);
            public ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter) => _inner.RemoveAnimation(avatar, parameter);
            public ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests) => _inner.PlanBatch(requests);
            public ModularAvatarIntegrationBatchPlan PlanFinalState(IReadOnlyList<ModularAvatarIntegrationRequest> requests, IReadOnlyList<string> removingParameters) => _inner.PlanFinalState(requests, removingParameters);
            public ModularAvatarManagedStateSnapshot InspectManagedState(VRCAvatarDescriptor avatar) => _inner.InspectManagedState(avatar);
            public ModularAvatarManagedStateSnapshot ValidateRemovals(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) => _inner.ValidateRemovals(avatar, parameters);
            public object CaptureRollbackSnapshot(VRCAvatarDescriptor avatar) => _inner.CaptureRollbackSnapshot(avatar);
            public ModularAvatarIntegrationBatchResult RestoreRollbackSnapshot(object snapshot) => _inner.RestoreRollbackSnapshot(snapshot);

            public ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan)
            {
                var results = new List<ModularAvatarIntegrationResult>();
                var diagnostics = new List<FaceMotionDiagnostic>();
                for (var i = 0; i < plan.Items.Count; i++)
                {
                    var result = _inner.Apply(plan.Items[i]);
                    results.Add(result);
                    for (var d = 0; d < result.Diagnostics.Count; d++) diagnostics.Add(result.Diagnostics[d]);
                    _applied++;
                    if (_applied >= ApplyFailAfterItem)
                    {
                        diagnostics.Add(new FaceMotionDiagnostic("FM-TEST", FaceMotionDiagnosticSeverity.Error, "Simulated real-backend failure during modular avatar apply batch.", string.Empty, true, string.Empty));
                        return new ModularAvatarIntegrationBatchResult(false, results, diagnostics);
                    }
                }
                return new ModularAvatarIntegrationBatchResult(true, results, diagnostics);
            }

            public ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters)
            {
                var results = new List<ModularAvatarIntegrationResult>();
                var diagnostics = new List<FaceMotionDiagnostic>();
                if (avatar == null || parameters == null || parameters.Count == 0) return _inner.RemoveAnimations(avatar, parameters);
                for (var i = 0; i < parameters.Count; i++)
                {
                    var result = _inner.RemoveAnimation(avatar, parameters[i]);
                    results.Add(result);
                    for (var d = 0; d < result.Diagnostics.Count; d++) diagnostics.Add(result.Diagnostics[d]);
                    _removed++;
                    if (_removed >= RemoveFailAfterItem)
                    {
                        diagnostics.Add(new FaceMotionDiagnostic("FM-TEST", FaceMotionDiagnosticSeverity.Error, "Simulated real-backend failure during modular avatar removal.", string.Empty, true, string.Empty));
                        return new ModularAvatarIntegrationBatchResult(false, results, diagnostics);
                    }
                }
                var succeeded = true;
                for (var i = 0; i < results.Count; i++) if (!results[i].Succeeded) succeeded = false;
                return new ModularAvatarIntegrationBatchResult(succeeded, results, diagnostics);
            }
        }
    }
}