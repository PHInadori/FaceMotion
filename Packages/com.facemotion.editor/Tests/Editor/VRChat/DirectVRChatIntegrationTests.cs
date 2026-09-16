using System.Text.RegularExpressions;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.Tests
{
    public sealed class DirectVRChatIntegrationTests
    {
        private const string Folder = "Assets/__FaceMotionTests_G";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimatorController _fx;
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_G");
            _root = new GameObject("Avatar"); _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _fx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = _fx } };
            _clip = new AnimationClip(); AssetDatabase.CreateAsset(_clip, Folder + "/motion.anim");
        }

        [TearDown]
        public void TearDown()
        {
            DirectVRChatIntegration.ApplyFailureInjector = null;
            DirectVRChatIntegration.PlanFailureInjector = null;
            Object.DestroyImmediate(_root); AssetDatabase.DeleteAsset(Folder); AssetDatabase.Refresh();
        }

        private DirectIntegrationPlan Plan(string name = "Smile")
        {
            return DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, Folder, name));
        }

        private static bool ContainsDiagnostic(DirectIntegrationPlan plan, string code)
        {
            for (int i = 0; i < plan.Diagnostics.Count; i++) if (plan.Diagnostics[i].Code == code) return true;
            return false;
        }

        [Test]
        public void Plan_IsPureAndReportsItsProposedNames()
        {
            var plan = DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, Folder, "Happy Face"));

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.ParameterName, Is.EqualTo("FaceMotion_Happy_Face"));
            Assert.That(_avatar.expressionParameters, Is.Null);
            Assert.That(_avatar.expressionsMenu, Is.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
        }

        [Test]
        public void Plan_UnexpectedExceptionReturnsBlockingDiagnosticWithoutMutation()
        {
            DirectVRChatIntegration.PlanFailureInjector = _ => throw new System.InvalidOperationException("direct planning test exception");
            LogAssert.Expect(LogType.Exception, new Regex("direct planning test exception"));

            var plan = Plan();
            var result = DirectVRChatIntegration.Apply(plan);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-G-PLAN-UNEXPECTED"));
            Assert.That(plan.Diagnostics[0].Message, Does.Contain("InvalidOperationException"));
            Assert.That(plan.Diagnostics[0].Message, Does.Contain("Stack trace:"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(_avatar.expressionParameters, Is.Null);
            Assert.That(_avatar.expressionsMenu, Is.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile"), Is.False);
        }

        [Test]
        public void Apply_CopyOnWritesAndRollbackRestoresOriginalReferences()
        {
            var plan = DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, Folder, "Smile"));
            var result = DirectVRChatIntegration.Apply(plan);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_avatar.expressionParameters, Is.Not.Null);
            Assert.That(_avatar.expressionsMenu, Is.Not.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.Not.SameAs(_fx));
            Assert.That(result.Manifest.GeneratedParameters.FindParameter("FaceMotion_Smile"), Is.Not.Null);
            Assert.That(result.Manifest.GeneratedFx.parameters, Has.Some.Matches<AnimatorControllerParameter>(p => p.name == "FaceMotion_Smile"));

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out var diagnostics), Is.True);
            Assert.That(_avatar.expressionParameters, Is.Null);
            Assert.That(_avatar.expressionsMenu, Is.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(diagnostics[0].Code, Is.EqualTo("FM-G-ROLLED-BACK"));
        }

        [Test]
        public void Plan_BlocksExpressionBudgetAndNeverCreatesAssets()
        {
            var parameters = ScriptableObject.CreateInstance<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters>();
            parameters.parameters = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter[256];
            for (int i = 0; i < parameters.parameters.Length; i++) parameters.parameters[i] = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter { name = "P" + i, valueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool };
            _avatar.expressionParameters = parameters;
            var plan = DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, Folder, "Budget"));
            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-G-BUDGET"));
            Object.DestroyImmediate(parameters);
        }

        [Test]
        public void Plan_RejectsNullRequestWithoutMutation()
        {
            var plan = DirectVRChatIntegration.Plan(null);
            Assert.That(plan.IsValid, Is.False);
            Assert.That(ContainsDiagnostic(plan, "FM-G-AVATAR"), Is.True);
            Assert.That(ContainsDiagnostic(plan, "FM-G-CLIP"), Is.True);
        }

        [Test]
        public void Plan_RejectsTraversalAndAssetsPrefixLookalikes()
        {
            Assert.That(ContainsDiagnostic(DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, "AssetsBad", "Safe")), "FM-G-PATH"), Is.True);
            Assert.That(ContainsDiagnostic(DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, _clip, "Assets/../Assets", "Safe")), "FM-G-PATH"), Is.True);
        }

        [Test]
        public void Plan_RejectsPrefabAssetsWithoutCreatingIntegrationAssets()
        {
            var prefabPath = Folder + "/Avatar.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(_root, prefabPath);

            var plan = DirectVRChatIntegration.Plan(new DirectIntegrationRequest(prefab.GetComponent<VRCAvatarDescriptor>(), _clip, Folder, "Prefab"));

            Assert.That(ContainsDiagnostic(plan, "FM-G-PREFAB-ASSET"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Prefab"), Is.False);
        }

        [Test]
        public void Plan_RejectsOverlongGeneratedParameter()
        {
            Assert.That(ContainsDiagnostic(Plan(new string('a', 257)), "FM-G-PARAMETER-NAME"), Is.True);
        }

        [Test]
        public void Plan_RejectsExpressionParameterConflict()
        {
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new[] { new VRCExpressionParameters.Parameter { name = "FaceMotion_Smile", valueType = VRCExpressionParameters.ValueType.Bool } };
            _avatar.expressionParameters = parameters;
            Assert.That(ContainsDiagnostic(Plan(), "FM-G-PARAMETER-CONFLICT"), Is.True);
            Object.DestroyImmediate(parameters);
        }

        [Test]
        public void Plan_RejectsAnimatorParameterAndLayerConflicts()
        {
            _fx.AddParameter("FaceMotion_Smile", AnimatorControllerParameterType.Bool);
            var machine = new AnimatorStateMachine { name = "FaceMotion Existing" };
            _fx.AddLayer(new AnimatorControllerLayer { name = "FaceMotion Existing", stateMachine = machine });
            var plan = Plan();
            Assert.That(ContainsDiagnostic(plan, "FM-G-ANIMATOR-PARAMETER-CONFLICT"), Is.True);
            Assert.That(ContainsDiagnostic(plan, "FM-G-LAYER-CONFLICT"), Is.True);
        }

        [Test]
        public void Plan_RejectsWriteDefaultsAndCompetingBindings()
        {
            var machine = new AnimatorStateMachine { name = "Existing" };
            var state = machine.AddState("On"); state.writeDefaultValues = true;
            var existing = new AnimationClip(); AssetDatabase.CreateAsset(existing, Folder + "/existing.anim");
            var binding = EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(_clip, binding, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            AnimationUtility.SetEditorCurve(existing, binding, AnimationCurve.Linear(0f, 1f, 1f, 0f)); state.motion = existing;
            _fx.AddLayer(new AnimatorControllerLayer { name = "Existing", stateMachine = machine });
            var plan = Plan();
            Assert.That(ContainsDiagnostic(plan, "FM-G-WRITE-DEFAULTS"), Is.True);
            Assert.That(ContainsDiagnostic(plan, "FM-G-BINDING-CONFLICT"), Is.True);
        }

        [Test]
        public void Plan_RejectsFullMenuButAcceptsNullControlList()
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new System.Collections.Generic.List<VRCExpressionsMenu.Control>();
            for (int i = 0; i < 8; i++) menu.controls.Add(new VRCExpressionsMenu.Control());
            _avatar.expressionsMenu = menu;
            Assert.That(ContainsDiagnostic(Plan(), "FM-G-MENU-CAPACITY"), Is.True);
            menu.controls = null;
            Assert.That(Plan().IsValid, Is.True);
            Object.DestroyImmediate(menu);
        }

        [Test]
        public void Plan_RejectsUnmanagedGeneratedFolder()
        {
            AssetDatabase.CreateFolder(Folder, "FaceMotion_Smile");
            Assert.That(ContainsDiagnostic(Plan(), "FM-G-OUTPUT-CONFLICT"), Is.True);
        }

        [Test]
        public void Apply_CreatesMenuControlsWhenSourceControlsAreNull()
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); menu.controls = null;
            _avatar.expressionsMenu = menu;
            var result = DirectVRChatIntegration.Apply(Plan());
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Manifest.GeneratedMenu.controls, Has.Count.EqualTo(1));
            Assert.That(result.Manifest.GeneratedMenu.controls[0].type, Is.EqualTo(VRCExpressionsMenu.Control.ControlType.SubMenu));
            Assert.That(result.Manifest.GeneratedMenu.controls[0].subMenu, Is.Not.Null);
        }

        [Test]
        public void Apply_CreatesAnOffResetClipFromTheAvatarBaselineForOwnedBindings()
        {
            var face = new GameObject("Face"); face.transform.SetParent(_root.transform); face.transform.localPosition = new Vector3(1f, 2f, 3f); face.transform.localRotation = Quaternion.Euler(10f, 20f, 30f); face.transform.localScale = new Vector3(2f, 3f, 4f);
            var mesh = new Mesh(); mesh.AddBlendShapeFrame("Smile", 100f, new Vector3[0], new Vector3[0], new Vector3[0]);
            var renderer = face.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.SetBlendShapeWeight(0, 42f);
            SetCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            SetCurve("Face", typeof(Transform), "m_LocalPosition.x"); SetCurve("Face", typeof(Transform), "m_LocalPosition.y"); SetCurve("Face", typeof(Transform), "m_LocalPosition.z");
            SetCurve("Face", typeof(Transform), "m_LocalRotation.x"); SetCurve("Face", typeof(Transform), "m_LocalRotation.y"); SetCurve("Face", typeof(Transform), "m_LocalRotation.z"); SetCurve("Face", typeof(Transform), "m_LocalRotation.w");
            SetCurve("Face", typeof(Transform), "m_LocalScale.x"); SetCurve("Face", typeof(Transform), "m_LocalScale.y"); SetCurve("Face", typeof(Transform), "m_LocalScale.z");

            var result = DirectVRChatIntegration.Apply(Plan());
            var layer = result.Manifest.GeneratedFx.layers[1];
            var off = System.Array.Find(layer.stateMachine.states, state => state.state.name == "Off").state;
            var on = System.Array.Find(layer.stateMachine.states, state => state.state.name == "On").state;
            var reset = off.motion as AnimationClip;

            Assert.That(reset, Is.Not.Null);
            Assert.That(on.motion, Is.SameAs(_clip));
            Assert.That(off.writeDefaultValues, Is.False); Assert.That(on.writeDefaultValues, Is.False);
            Assert.That(result.Manifest.GeneratedFx.parameters, Has.Some.Matches<AnimatorControllerParameter>(p => p.name == "FaceMotion_Smile" && p.type == AnimatorControllerParameterType.Bool && !p.defaultBool));
            Assert.That(off.transitions[0].duration, Is.Zero); Assert.That(off.transitions[0].hasExitTime, Is.False);
            Assert.That(on.transitions[0].duration, Is.Zero); Assert.That(on.transitions[0].hasExitTime, Is.False);
            Assert.That(AnimationUtility.GetCurveBindings(reset), Is.EquivalentTo(AnimationUtility.GetCurveBindings(_clip)));
            Assert.That(CurveValue(reset, "Face", typeof(SkinnedMeshRenderer), "blendShape.Smile"), Is.EqualTo(42f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.x"), Is.EqualTo(1f)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.y"), Is.EqualTo(2f)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.z"), Is.EqualTo(3f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.x"), Is.EqualTo(face.transform.localRotation.x)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.y"), Is.EqualTo(face.transform.localRotation.y)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.z"), Is.EqualTo(face.transform.localRotation.z)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.w"), Is.EqualTo(face.transform.localRotation.w));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalScale.x"), Is.EqualTo(2f)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalScale.y"), Is.EqualTo(3f)); Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalScale.z"), Is.EqualTo(4f));
            Assert.That(AnimationUtility.GetEditorCurve(reset, EditorCurveBinding.FloatCurve("Face", typeof(Transform), "m_LocalEulerAnglesRaw.x")), Is.Null);
            Assert.That(result.Manifest.OwnedAssetPaths, Has.Member(Folder + "/FaceMotion_Smile/Reset.anim"));
        }

        [Test]
        public void Apply_ReapplyRefreshesTheResetBaselineWithoutDuplicatingItsAsset()
        {
            var face = new GameObject("Face"); face.transform.SetParent(_root.transform); face.transform.localPosition = new Vector3(1f, 0f, 0f);
            SetCurve("Face", typeof(Transform), "m_LocalPosition.x");
            var first = DirectVRChatIntegration.Apply(Plan());
            face.transform.localPosition = new Vector3(9f, 0f, 0f);

            var second = DirectVRChatIntegration.Apply(Plan());
            var reset = System.Array.Find(second.Manifest.GeneratedFx.layers[1].stateMachine.states, state => state.state.name == "Off").state.motion as AnimationClip;

            Assert.That(second.Succeeded, Is.True);
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.x"), Is.EqualTo(9f));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/Reset.anim"), Is.SameAs(reset));
            Assert.That(second.Manifest.OwnedAssetPaths, Has.Member(Folder + "/FaceMotion_Smile/Reset.anim"));
            Assert.That(first.Manifest.GeneratedFx, Is.Not.SameAs(second.Manifest.GeneratedFx));
        }

        [Test]
        public void Apply_FailureInjectionCleansAssetsAndKeepsDescriptorReferences()
        {
            DirectVRChatIntegration.ApplyFailureInjector = stage => { if (stage == "after-parameters") throw new System.InvalidOperationException("injected"); };
            var result = DirectVRChatIntegration.Apply(Plan());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile"), Is.False);
            Assert.That(result.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-G-APPLY"));
        }

        [Test]
        public void Apply_FailureAfterResetCreationCleansTheResetAssetAndKeepsDescriptorReferences()
        {
            DirectVRChatIntegration.ApplyFailureInjector = stage => { if (stage == "after-reset") throw new System.InvalidOperationException("injected"); };

            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/Reset.anim"), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile"), Is.False);
        }

        [Test]
        public void Rollback_SkipsTamperedOwnershipOutsideManifestFolder()
        {
            var unrelated = new AnimationClip(); AssetDatabase.CreateAsset(unrelated, Folder + "/unrelated.anim");
            var result = DirectVRChatIntegration.Apply(Plan());
            result.Manifest.OwnedAssetPaths = new[] { AssetDatabase.GetAssetPath(result.Manifest), AssetDatabase.GetAssetPath(unrelated) };
            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out var diagnostics), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/unrelated.anim"), Is.Not.Null);
            Assert.That(diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-G-OWNERSHIP"));
        }

        [Test]
        public void Apply_ReapplyReplacesOwnedAssetsAndKeepsOriginalReferencesForRollback()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            var firstFx = first.Manifest.GeneratedFx;
            var second = DirectVRChatIntegration.Apply(Plan());
            Assert.That(second.Succeeded, Is.True, second.Diagnostics[second.Diagnostics.Count - 1].Message);
            Assert.That(second.Manifest.GeneratedFx, Is.Not.SameAs(firstFx));
            Assert.That(DirectVRChatIntegration.Rollback(second.Manifest, out _), Is.True);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
        }

        [Test]
        public void Rollback_DeletesOwnedFxController()
        {
            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotion_Smile/FX.controller"), Is.Not.Null);
            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotion_Smile/FX.controller"), Is.Null);
        }

        [Test]
        public void Rollback_DeletesOwnedResetClip()
        {
            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/Reset.anim"), Is.Not.Null);
            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/Reset.anim"), Is.Null);
        }

        [Test]
        public void Rollback_DeletesOwnedMenuAndParameters()
        {
            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Folder + "/FaceMotion_Smile/Menu.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(Folder + "/FaceMotion_Smile/Parameters.asset"), Is.Null);
        }

        [Test]
        public void Rollback_DeletesOwnedManifest()
        {
            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(Folder + "/FaceMotion_Smile/Manifest.asset"), Is.Null);
        }

        [Test]
        public void Rollback_KeepsForeignAssetInsideGeneratedRoot()
        {
            var result = DirectVRChatIntegration.Apply(Plan());
            var foreign = new AnimationClip(); AssetDatabase.CreateAsset(foreign, Folder + "/FaceMotion_Smile/MyUserMemo.anim");

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/MyUserMemo.anim"), Is.Not.Null);
            AssetDatabase.DeleteAsset(Folder + "/FaceMotion_Smile/MyUserMemo.anim");
        }

        [Test]
        public void Rollback_KeepsForeignSubfolderInsideGeneratedRoot()
        {
            var result = DirectVRChatIntegration.Apply(Plan());
            AssetDatabase.CreateFolder(Folder + "/FaceMotion_Smile", "UserData");
            var note = new AnimationClip(); AssetDatabase.CreateAsset(note, Folder + "/FaceMotion_Smile/UserData/note.anim");

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile/UserData"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/UserData/note.anim"), Is.Not.Null);
            AssetDatabase.DeleteAsset(Folder + "/FaceMotion_Smile/UserData/note.anim");
        }

        [Test]
        public void Rollback_RetainsRootFolderWhenForeignContentExists()
        {
            var result = DirectVRChatIntegration.Apply(Plan());
            var foreign = new AnimationClip(); AssetDatabase.CreateAsset(foreign, Folder + "/FaceMotion_Smile/MyUserMemo.anim");

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out var diagnostics), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile"), Is.True);
            Assert.That(diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-OWNERSHIP-FOREIGN-CONTENT"));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/MyUserMemo.anim"), Is.Not.Null);
            AssetDatabase.DeleteAsset(Folder + "/FaceMotion_Smile/MyUserMemo.anim");
        }

        [Test]
        public void Rollback_DeletesRootFolderOnlyWhenEmptyAndFaceMotionOwned()
        {
            var result = DirectVRChatIntegration.Apply(Plan());

            Assert.That(DirectVRChatIntegration.Rollback(result.Manifest, out _), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile"), Is.False);
        }

        [Test]
        public void Reapply_PreservesForeignAssetBesideGeneratedAssets()
        {
            var first = DirectVRChatIntegration.Apply(Plan());
            var foreign = new AnimationClip(); AssetDatabase.CreateAsset(foreign, Folder + "/FaceMotion_Smile/MyUserMemo.anim");

            var second = DirectVRChatIntegration.Apply(Plan());

            Assert.That(second.Succeeded, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotion_Smile/MyUserMemo.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(second.Manifest), Is.EqualTo(Folder + "/FaceMotion_Smile/Manifest.asset"));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Smile 1"), Is.False);
            AssetDatabase.DeleteAsset(Folder + "/FaceMotion_Smile/MyUserMemo.anim");
        }

        private void SetCurve(string path, System.Type type, string property) { _clip.SetCurve(path, type, property, AnimationCurve.Constant(0f, 1f, 100f)); }
        private static float CurveValue(AnimationClip clip, string path, System.Type type, string property) { return AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property)).Evaluate(0f); }
    }
}
