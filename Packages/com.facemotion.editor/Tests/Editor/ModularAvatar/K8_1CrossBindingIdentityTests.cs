using System.Collections.Generic;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Cross-binding identity is Unity's exact float-curve identity: relative path,
    /// component type, and property. Matching BlendShape names on separate renderers
    /// are independent; a foreign FX writer on the exact same binding remains blocking.
    /// </summary>
    public sealed class K8_1CrossBindingIdentityTests
    {
        private const string Folder = "Assets/__FaceMotionTests_CrossBinding";
        private const string Shape = "eye_O_O";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_CrossBinding");
            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            new GameObject("Body").transform.SetParent(_root.transform, false);
            new GameObject("OtherMesh").transform.SetParent(_root.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void DifferentRendererPathsWithSameBlendShapeName_DoNotBlockAgainstForeignFx()
        {
            var candidate = CreateBlendShapeClip("candidate-body.anim", "Body");
            var foreign = CreateBlendShapeClip("foreign-other.anim", "OtherMesh");
            SetFx(foreign);

            var plan = new ModularAvatarIntegrationBackend().Plan(
                new ModularAvatarIntegrationRequest(_avatar, candidate, Folder, "Candidate"));

            Assert.That(plan.IsValid, Is.True, DiagnosticsOf(plan));
            Assert.That(HasCode(plan.Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.False);
        }

        [Test]
        public void ExactRendererPathComponentAndPropertyAgainstForeignFx_RemainBlocking()
        {
            var candidate = CreateBlendShapeClip("candidate-body.anim", "Body");
            var foreign = CreateBlendShapeClip("foreign-body.anim", "Body");
            SetFx(foreign);

            var plan = new ModularAvatarIntegrationBackend().Plan(
                new ModularAvatarIntegrationRequest(_avatar, candidate, Folder, "Candidate"));

            var conflict = Find(plan.Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT");
            Assert.That(plan.IsValid, Is.False);
            Assert.That(conflict, Is.Not.Null);
            Assert.That(conflict.Details[FaceMotion.Diagnostics.FaceMotionDiagnosticDetailKeys.BindingPath], Is.EqualTo("Body"));
            Assert.That(conflict.Details[FaceMotion.Diagnostics.FaceMotionDiagnosticDetailKeys.BindingProperty], Is.EqualTo("blendShape." + Shape));
            Assert.That(conflict.Details[FaceMotion.Diagnostics.FaceMotionDiagnosticDetailKeys.BindingType], Is.EqualTo(nameof(SkinnedMeshRenderer)));
        }

        [Test]
        public void TwoFaceMotionAnimationsOnTheExactBinding_UseSharedBindingPolicyNotCrossBindingConflict()
        {
            var first = CreateBlendShapeClip("first-body.anim", "Body");
            var second = CreateBlendShapeClip("second-body.anim", "Body");

            var plan = new ModularAvatarIntegrationBackend().PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, first, Folder, "First"),
                new ModularAvatarIntegrationRequest(_avatar, second, Folder, "Second")
            });

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Items[0].PartnerParameters, Has.Count.EqualTo(1));
            Assert.That(plan.Items[1].PartnerParameters, Has.Count.EqualTo(1));
            Assert.That(plan.Items[0].SharedBindings, Has.Count.EqualTo(1));
            Assert.That(plan.Items[1].SharedBindings, Has.Count.EqualTo(1));
            Assert.That(HasCode(plan.Items[0].Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.False);
            Assert.That(HasCode(plan.Items[1].Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.False);
        }

        [Test]
        public void LegacyDirectBatchOwnedClip_RemainsRecognizedByCurrentMaPlanner()
        {
            var persisted = ApplyLegacyDirectBatch(out var owned, out var candidate);
            Assert.That(DirectVRChatIntegration.FindOwningParameterForClip(_avatar, owned), Is.EqualTo("FaceMotion_Owned"));
            AnimatorControllerLayer ownedLayer = null;
            foreach (var layer in persisted.GeneratedFx.layers)
                if (layer.name == persisted.Items[0].LayerName) ownedLayer = layer;
            Assert.That(ownedLayer, Is.Not.Null);
            AnimationClip reset = null;
            foreach (var state in ownedLayer.stateMachine.states)
                if (state.state.name == "Off") reset = state.state.motion as AnimationClip;
            Assert.That(reset, Is.Not.Null);
            Assert.That(persisted.OwnedAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(reset)));
            Assert.That(DirectVRChatIntegration.FindOwningParameterForClip(_avatar, reset),
                Is.EqualTo("FaceMotion_Owned"));

            var plan = new ModularAvatarIntegrationBackend().Plan(
                new ModularAvatarIntegrationRequest(_avatar, candidate, Folder, "Candidate"));

            Assert.That(plan.IsValid, Is.True, DiagnosticsOf(plan));
            Assert.That(plan.PartnerParameters, Does.Contain("FaceMotion_Owned"));
            Assert.That(plan.SharedBindings.Count, Is.GreaterThan(0));
            Assert.That(HasCode(plan.Diagnostics, "FM-H-MA-SHARED-BINDING"), Is.True);
            Assert.That(HasCode(plan.Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.False);
        }

        [Test]
        public void LegacyBatch_SameNameForeignClip_RemainsBlockingForMa()
        {
            var persisted = ApplyLegacyDirectBatch(out _, out var candidate);
            var reset = ResetFrom(persisted);
            var foreign = AddForeignFxClip(persisted, Folder + "/foreign.anim", reset.name);
            Assert.That(foreign.name, Is.EqualTo(reset.name));
            Assert.That(persisted.GeneratedFx.animationClips, Does.Contain(foreign));
            Assert.That(AnimationUtility.GetCurveBindings(foreign), Is.EquivalentTo(AnimationUtility.GetCurveBindings(candidate)));
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(persisted.GeneratedFx));
            Assert.That(DirectVRChatIntegration.FindOwningParameterForClip(_avatar, foreign), Is.Null);

            var plan = new ModularAvatarIntegrationBackend().Plan(
                new ModularAvatarIntegrationRequest(_avatar, candidate, Folder, "Candidate"));

            Assert.That(HasCode(plan.Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.True, DiagnosticsOf(plan));
            Assert.That(plan.IsValid, Is.False);
        }

        [Test]
        public void LegacyBatch_ListedClipInNonOwnedLayer_RemainsForeign()
        {
            var persisted = ApplyLegacyDirectBatch(out _, out var candidate);
            string foreignPath = Folder + "/FaceMotion_Batch/foreign.anim";
            var foreign = AddForeignFxClip(persisted, foreignPath, "Reset_Owned");
            var paths = new List<string>(persisted.OwnedAssetPaths) { foreignPath };
            persisted.OwnedAssetPaths = paths.ToArray();
            EditorUtility.SetDirty(persisted);
            AssetDatabase.SaveAssets();
            Assert.That(DirectVRChatIntegration.FindOwningParameterForClip(_avatar, foreign), Is.Null,
                "Even a listed clip in the generated folder is not owned without a matching manifest item layer.");

            var plan = new ModularAvatarIntegrationBackend().Plan(
                new ModularAvatarIntegrationRequest(_avatar, candidate, Folder, "Candidate"));
            Assert.That(plan.IsValid, Is.False);
            Assert.That(HasCode(plan.Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.True);
        }

        private DirectBatchIntegrationManifest ApplyLegacyDirectBatch(out AnimationClip owned, out AnimationClip candidate)
        {
            var originalFx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            _avatar.baseAnimationLayers = new[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    isDefault = false,
                    animatorController = originalFx
                }
            };
            owned = new AnimationClip();
            AssetDatabase.CreateAsset(owned, Folder + "/owned.anim");
            AnimationUtility.SetEditorCurve(owned,
                EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Constant(0f, 1f, 1f));
            candidate = new AnimationClip();
            AssetDatabase.CreateAsset(candidate, Folder + "/candidate.anim");
            AnimationUtility.SetEditorCurve(candidate,
                EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Constant(0f, 1f, 2f));
            EditorUtility.SetDirty(owned);
            EditorUtility.SetDirty(candidate);
            AssetDatabase.SaveAssets();

            var direct = DirectVRChatIntegration.ApplyBatch(DirectVRChatIntegration.PlanBatch(
                new DirectIntegrationBatchRequest(_avatar, new[]
                {
                    new DirectIntegrationBatchItemRequest(owned, "Owned")
                }, Folder)));
            Assert.That(direct.Succeeded, Is.True);
            string manifestPath = Folder + "/FaceMotion_Batch/BatchManifest.asset";
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceUpdate);
            var persisted = AssetDatabase.LoadAssetAtPath<DirectBatchIntegrationManifest>(manifestPath);
            Assert.That(persisted, Is.Not.Null);
            return persisted;
        }

        private static AnimationClip ResetFrom(DirectBatchIntegrationManifest manifest)
        {
            foreach (var layer in manifest.GeneratedFx.layers)
            {
                if (layer.name != manifest.Items[0].LayerName) continue;
                foreach (var state in layer.stateMachine.states)
                    if (state.state.name == "Off") return state.state.motion as AnimationClip;
            }
            throw new AssertionException("No generated reset in the item-owned layer.");
        }

        private static AnimationClip AddForeignFxClip(DirectBatchIntegrationManifest manifest, string path, string displayName)
        {
            var foreign = new AnimationClip();
            AssetDatabase.CreateAsset(foreign, path);
            foreign.name = displayName;
            EditorUtility.SetDirty(foreign);
            AnimationUtility.SetEditorCurve(foreign,
                EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Constant(0f, 1f, 3f));
            var machine = new AnimatorStateMachine { name = "User FX machine" };
            AssetDatabase.AddObjectToAsset(machine, manifest.GeneratedFx);
            machine.AddState("Off").motion = foreign;
            manifest.GeneratedFx.AddLayer(new AnimatorControllerLayer
            {
                name = "User FX layer",
                defaultWeight = 1f,
                stateMachine = machine
            });
            EditorUtility.SetDirty(manifest.GeneratedFx);
            AssetDatabase.SaveAssets();
            return foreign;
        }

        [Test]
        public void TwoFaceMotionAnimationsOnDifferentRendererPaths_AreIndependent()
        {
            var body = CreateBlendShapeClip("first-body.anim", "Body");
            var other = CreateBlendShapeClip("second-other.anim", "OtherMesh");

            var plan = new ModularAvatarIntegrationBackend().PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, body, Folder, "Body"),
                new ModularAvatarIntegrationRequest(_avatar, other, Folder, "Other")
            });

            Assert.That(plan.IsValid, Is.True);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                Assert.That(Find(plan.Items[i].Diagnostics, "FM-H-MA-SHARED-BINDING"), Is.Null);
                Assert.That(HasCode(plan.Items[i].Diagnostics, "FM-H-MA-CROSS-BINDING-CONFLICT"), Is.False);
                Assert.That(plan.Items[i].PartnerParameters, Is.Empty);
            }
        }

        [Test]
        public void DesiredStateResultsAndOperationDiagnostics_DoNotOutliveChangedInputs()
        {
            var resultAvatar = new GameObject("ResultAvatar");
            var resultProject = new GameObject("ResultProject");
            var nextAvatar = new GameObject("NextAvatar");
            try
            {
                string both = OneClickIntegrationPanel.DesiredSelectionSignature(new[] { "A", "B" });
                Assert.That(OneClickIntegrationPanel.DesiredResultMatchesInputs(
                    resultAvatar, resultProject, both, resultAvatar, resultProject, new[] { "A", "B" }), Is.True);
                Assert.That(OneClickIntegrationPanel.DesiredResultMatchesInputs(
                    resultAvatar, resultProject, both, resultAvatar, resultProject, new[] { "B" }), Is.False,
                    "A+B result must not remain visible after selecting B only");
                Assert.That(OneClickIntegrationPanel.DesiredResultMatchesInputs(
                    resultAvatar, resultProject, both, nextAvatar, resultProject, new[] { "A", "B" }), Is.False,
                    "a result from another avatar must not remain visible");

                var session = new FaceMotionEditorSession();
                session.SetLastOperationDiagnostic(new FaceMotion.Diagnostics.FaceMotionDiagnostic(
                    "test", FaceMotion.Diagnostics.FaceMotionDiagnosticSeverity.Error, "old", string.Empty, true, string.Empty));
                Assert.That(session.SetBatchSelected("B", true), Is.True);
                Assert.That(session.LastOperationDiagnostic, Is.Null, "changing desired selection clears the old operation diagnostic");

                session.SetLastOperationDiagnostic(new FaceMotion.Diagnostics.FaceMotionDiagnostic(
                    "test", FaceMotion.Diagnostics.FaceMotionDiagnosticSeverity.Error, "old", string.Empty, true, string.Empty));
                session.SetAvatar(_avatar, _root, null, null, null, null);
                Assert.That(session.LastOperationDiagnostic, Is.Null, "changing avatar clears the old operation diagnostic");
            }
            finally
            {
                Object.DestroyImmediate(resultAvatar);
                Object.DestroyImmediate(resultProject);
                Object.DestroyImmediate(nextAvatar);
            }
        }

        private AnimationClip CreateBlendShapeClip(string name, string path)
        {
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, Folder + "/" + name);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + Shape),
                AnimationCurve.Constant(0f, 1f, 1f));
            return clip;
        }

        private void SetFx(AnimationClip clip)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/fx.controller");
            controller.layers[0].stateMachine.AddState("State").motion = clip;
            _avatar.baseAnimationLayers = new[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    animatorController = controller
                }
            };
        }

        private static FaceMotion.Diagnostics.FaceMotionDiagnostic Find(
            IReadOnlyList<FaceMotion.Diagnostics.FaceMotionDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Code == code) return diagnostics[i];
            }

            return null;
        }

        private static bool HasCode(IReadOnlyList<FaceMotion.Diagnostics.FaceMotionDiagnostic> diagnostics, string code)
        {
            return Find(diagnostics, code) != null;
        }

        private static string DiagnosticsOf(ModularAvatarIntegrationPlan plan)
        {
            var messages = new List<string>();
            for (int i = 0; i < plan.Diagnostics.Count; i++) messages.Add(plan.Diagnostics[i].Code + ": " + plan.Diagnostics[i].Message);
            return string.Join("\n", messages);
        }
    }
}
