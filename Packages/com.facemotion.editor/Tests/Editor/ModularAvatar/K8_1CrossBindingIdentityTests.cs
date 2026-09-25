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
