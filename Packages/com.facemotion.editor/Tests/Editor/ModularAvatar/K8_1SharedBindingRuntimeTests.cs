using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Export;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Empirical proof of the generated Modular Avatar runtime semantics for two FaceMotion
    /// animations that share one BlendShape binding. The fixture builds exactly what
    /// <see cref="ModularAvatarIntegrationBackend"/> generates (verified against the real
    /// backend output) and evaluates the layers in both merge orders, because MA appends
    /// layers in avatar hierarchy order.
    ///
    /// Required semantics when the two generated layers both target Body/Face + Mouth_Smile:
    /// all off => baseline; exactly one on => the active animation visibly controls the
    /// binding; both on => deterministic later-layer precedence.
    /// </summary>
    public sealed class K8_1SharedBindingRuntimeTests
    {
        private const string Folder = "Assets/__FaceMotionTests_SB";
        private const string BindingPath = "Body/Face";
        private const string ShapeName = "Mouth_Smile";
        private const string ParamA = "FaceMotion_AnimA";
        private const string ParamB = "FaceMotion_AnimB";
        private const float ValueA = 100f;
        private const float ValueB = 50f;

        private GameObject _root;
        private SkinnedMeshRenderer _renderer;
        private Mesh _mesh;
        private VRCAvatarDescriptor _avatar;
        private AnimationClip _clipA;
        private AnimationClip _clipB;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_SB");
            _root = new GameObject("AvatarRoot");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            var body = new GameObject("Body");
            body.transform.SetParent(_root.transform, false);
            var face = new GameObject("Face");
            face.transform.SetParent(body.transform, false);
            _mesh = CreateMesh(ShapeName);
            _renderer = face.AddComponent<SkinnedMeshRenderer>();
            _renderer.sharedMesh = _mesh;
            _clipA = CreateClip("anim-a.anim", ValueA);
            _clipB = CreateClip("anim-b.anim", ValueB);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_mesh);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------
        // Architecture documentation (real backend output)
        // ------------------------------------------------------------------

        [Test]
        public void Architecture_RealBackend_GeneratesOffResetOnAuthoredWdFalseWeightOneBoolLayer()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(
                new ModularAvatarIntegrationRequest(_avatar, _clipA, Folder, "AnimA")));

            Assert.That(result.Succeeded, Is.True, DiagnosticsOf(result));
            string root = Folder + "/FaceMotionMA_AnimA";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "/FX.controller");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters, Has.Length.EqualTo(1));
            Assert.That(controller.parameters[0].name, Is.EqualTo(ParamA));
            Assert.That(controller.parameters[0].type, Is.EqualTo(AnimatorControllerParameterType.Bool));
            // CreateAnimatorControllerAtPath leaves an empty default layer; the generated
            // FaceMotion layer is appended after it.
            Assert.That(controller.layers, Has.Length.EqualTo(2));
            Assert.That(controller.layers[0].stateMachine.states, Is.Empty);
            Assert.That(controller.layers[1].defaultWeight, Is.EqualTo(1f));

            var machine = controller.layers[1].stateMachine;
            var off = machine.states[0].state;
            var on = machine.states[1].state;
            Assert.That(machine.defaultState, Is.SameAs(off));
            Assert.That(off.name, Is.EqualTo("Off"));
            Assert.That(on.name, Is.EqualTo("On"));
            Assert.That(off.writeDefaultValues, Is.False);
            Assert.That(on.writeDefaultValues, Is.False);
            Assert.That(on.motion, Is.SameAs(_clipA));

            var reset = off.motion as AnimationClip;
            Assert.That(reset, Is.Not.Null, "Off state must hold the generated Reset clip");
            Assert.That(reset.name, Is.EqualTo("Reset"));
            var curves = AnimationUtility.GetCurveBindings(reset);
            Assert.That(curves, Has.Length.EqualTo(1));
            Assert.That(curves[0].path, Is.EqualTo(BindingPath));
            Assert.That(curves[0].propertyName, Is.EqualTo("blendShape." + ShapeName));
            Assert.That(AnimationUtility.GetEditorCurve(reset, curves[0]).Evaluate(0f), Is.EqualTo(0f).Within(0.001f));

            Assert.That(off.transitions, Has.Length.EqualTo(1));
            Assert.That(off.transitions[0].destinationState, Is.SameAs(on));
            Assert.That(off.transitions[0].duration, Is.EqualTo(0f));
            Assert.That(off.transitions[0].hasExitTime, Is.False);
            Assert.That(on.transitions, Has.Length.EqualTo(1));
            Assert.That(on.transitions[0].destinationState, Is.SameAs(off));
            Assert.That(on.transitions[0].duration, Is.EqualTo(0f));
            Assert.That(on.transitions[0].hasExitTime, Is.False);

            var node = _root.transform.Find("FaceMotion MA AnimA");
            Assert.That(node, Is.Not.Null);
            var parameters = node.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarParameters>();
            Assert.That(parameters.parameters[0].saved, Is.False);
            Assert.That(parameters.parameters[0].defaultValue, Is.EqualTo(0f));
            Assert.That(node.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator>().layerType,
                Is.EqualTo(VRCAvatarDescriptor.AnimLayerType.FX));
        }

        [Test]
        public void Architecture_RuntimeFixture_MatchesRealBackendGeneratedController()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(
                new ModularAvatarIntegrationRequest(_avatar, _clipA, Folder, "AnimA")));
            Assert.That(result.Succeeded, Is.True, DiagnosticsOf(result));
            var real = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimA/FX.controller");

            var built = BuildCombinedController(Folder + "/fixture.controller", true);

            Assert.That(built.layers.Length, Is.EqualTo(real.layers.Length + 1), "fixture adds only the second animation layer");
            Assert.That(Describe(built.layers[1]), Is.EqualTo(Describe(real.layers[1])),
                "the fixture's FaceMotion layer must be structurally identical to the backend output");
        }

        // ------------------------------------------------------------------
        // Runtime semantics: 4 cases x 2 merge orders (real backend output)
        // ------------------------------------------------------------------

        [Test]
        public void Runtime_RequiredSemantics_AFirstOrder()
        {
            ApplySharedPair(out var a, out var b);
            var combined = Combine("a-first", a, b);
            AssertCase(SampleCombined(combined, false, false), 0f, "A first, both off => baseline");
            AssertCase(SampleCombined(combined, true, false), ValueA, "A first, only A on => A controls");
            AssertCase(SampleCombined(combined, false, true), ValueB, "A first, only B on => B controls");
            AssertCase(SampleCombined(combined, true, true), ValueB, "A first, both on => later layer (B) wins");
        }

        [Test]
        public void Runtime_RequiredSemantics_BFirstOrder()
        {
            ApplySharedPair(out var a, out var b);
            var combined = Combine("b-first", b, a);
            AssertCase(SampleCombined(combined, false, false), 0f, "B first, both off => baseline");
            AssertCase(SampleCombined(combined, true, false), ValueA, "B first, only A on => A controls");
            AssertCase(SampleCombined(combined, false, true), ValueB, "B first, only B on => B controls");
            AssertCase(SampleCombined(combined, true, true), ValueA, "B first, both on => later layer (A) wins");
        }

        [Test]
        public void Policy_SharedBinding_PlanWarnsWithoutBlocking()
        {
            _renderer.SetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName), 0f);
            var backend = new ModularAvatarIntegrationBackend();
            var first = backend.ApplyBatch(backend.PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, _clipA, Folder, "AnimA")
            }));
            Assert.That(first.Succeeded, Is.True, BatchDiagnosticsOf(first));

            var planB = backend.PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, _clipB, Folder, "AnimB")
            }).Items[0];

            Assert.That(planB.IsValid, Is.True, PlanDiagnosticsOf(planB));
            var warning = FindDiagnostic(planB.Diagnostics, "FM-H-MA-SHARED-BINDING");
            Assert.That(warning, Is.Not.Null, "sharing a FaceMotion-managed binding must warn, not block");
            Assert.That(warning.Blocking, Is.False);
            Assert.That(warning.Severity, Is.EqualTo(FaceMotionDiagnosticSeverity.Warning));
            Assert.That(FindDiagnostic(planB.Diagnostics, "FM-H-MA-BINDING-CONFLICT"), Is.Null,
                "a FaceMotion-managed partner must not emit the blocking conflict code");
            Assert.That(planB.PartnerParameters, Does.Contain(ParamA));
            Assert.That(planB.SharedBindings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Architecture_SharedIntegration_GeneratesPartnerAwareThreeStateMachine()
        {
            ApplySharedPair(out var a, out var b);
            AssertThreeState(a, ParamA, ParamB, _clipA);
            AssertThreeState(b, ParamB, ParamA, _clipB);

            var resetA = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_AnimA/Reset.anim");
            var uniqueA = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_AnimA/ResetUnique.anim");
            Assert.That(resetA, Is.Not.Null);
            Assert.That(uniqueA, Is.Not.Null, "partner-aware machines ship a unique-only reset clip");
            Assert.That(AnimationUtility.GetCurveBindings(resetA), Has.Length.EqualTo(1));
            Assert.That(AnimationUtility.GetCurveBindings(uniqueA), Has.Length.EqualTo(0),
                "the shared binding is stripped from the unique reset so an inactive partner never clobbers the active layer");
        }

        [Test]
        public void Regen_AddingSharedPartner_RegeneratesTheEarlierIntegration()
        {
            _renderer.SetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName), 0f);
            var backend = new ModularAvatarIntegrationBackend();
            var first = backend.ApplyBatch(backend.PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, _clipA, Folder, "AnimA")
            }));
            Assert.That(first.Succeeded, Is.True, BatchDiagnosticsOf(first));
            var before = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimA/FX.controller");
            Assert.That(before.parameters, Has.Length.EqualTo(1), "an animation without partners stays a two-state machine");

            var second = backend.ApplyBatch(backend.PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, _clipB, Folder, "AnimB")
            }));
            Assert.That(second.Succeeded, Is.True, BatchDiagnosticsOf(second));

            var regenerated = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimA/FX.controller");
            Assert.That(ParameterNames(regenerated), Is.EquivalentTo(new[] { ParamA, ParamB }),
                "adding a sharing partner must regenerate the earlier integration with partner conditions");
            AssertThreeState(regenerated, ParamA, ParamB, _clipA);
            var b = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimB/FX.controller");
            Assert.That(ParameterNames(b), Is.EquivalentTo(new[] { ParamA, ParamB }));
        }

        [Test]
        public void Regen_RemovingSharedPartner_DropsTheStaleParameter()
        {
            ApplySharedPair(out _, out _);
            var backend = new ModularAvatarIntegrationBackend();
            var removed = backend.RemoveAnimation(_avatar, ParamB);
            Assert.That(removed.Succeeded, Is.True, DiagnosticsOf(removed));
            Assert.That(_root.transform.Find("FaceMotion MA AnimB"), Is.Null);

            var regenerated = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimA/FX.controller");
            Assert.That(ParameterNames(regenerated), Is.EquivalentTo(new[] { ParamA }),
                "removing a sharing partner must drop the stale partner parameter (missing params evaluate FALSE)");
            var machine = regenerated.layers[1].stateMachine;
            var names = new List<string>();
            foreach (var child in machine.states) names.Add(child.state.name);
            Assert.That(names, Is.EquivalentTo(new[] { "Off", "On" }));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_AnimA/ResetUnique.anim"), Is.Null,
                "the unique reset asset is deleted with the regeneration");
        }

        // ------------------------------------------------------------------
        // Unity layer-semantics probes (architecture-decision evidence)
        // ------------------------------------------------------------------

        [Test]
        public void Probe_HigherLayerActiveStateWithoutSharedCurve_PassesLowerLayerValueThrough()
        {
            Assert.That(SamplePassThrough(useNullMotion: true), Is.EqualTo(ValueA).Within(0.5f),
                "a higher layer's null-motion state must not overwrite the lower layer's value");
            Assert.That(SamplePassThrough(useNullMotion: false), Is.EqualTo(ValueA).Within(0.5f),
                "a higher layer's curve-less clip state must not overwrite the lower layer's value");
        }

        [Test]
        public void Probe_CandidateArchitecture_BaselineLayerPlusEmptyOff_AllFourCases()
        {
            // Layers: baseline (lowest) -> animation layer(s) with empty Off states.
            AssertCase(SampleCandidate(false, false, true), 0f, "all off => baseline layer controls");
            AssertCase(SampleCandidate(true, false, true), ValueA, "A on, B off => A controls");
            AssertCase(SampleCandidate(false, true, true), ValueB, "B on, A off => B controls");
            AssertCase(SampleCandidate(true, true, true), ValueB, "both on => later layer (B) wins");
        }

        [Test]
        public void Probe_CandidateArchitecture_BaselineLayerPlusEmptyOff_ReverseOrder()
        {
            AssertCase(SampleCandidate(false, false, false), 0f, "all off => baseline layer controls");
            AssertCase(SampleCandidate(true, false, false), ValueA, "A on, B off => A controls");
            AssertCase(SampleCandidate(false, true, false), ValueB, "B on, A off => B controls");
            AssertCase(SampleCandidate(true, true, false), ValueA, "both on => later layer (A) wins");
        }

        /// <summary>Lower layer holds ValueA; the higher layer is active but has no shared curve.</summary>
        private float SamplePassThrough(bool useNullMotion)
        {
            _renderer.SetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName), 0f);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                AssetDatabase.GenerateUniqueAssetPath(Folder + "/probe-pass-through.controller"));

            var hold = new AnimatorStateMachine { name = "Hold" };
            AssetDatabase.AddObjectToAsset(hold, controller);
            var holdState = hold.AddState("On");
            holdState.motion = _clipA;
            holdState.writeDefaultValues = false;
            controller.AddLayer(new AnimatorControllerLayer { name = "Hold", defaultWeight = 1f, stateMachine = hold });

            var empty = new AnimatorStateMachine { name = "NoCurve" };
            AssetDatabase.AddObjectToAsset(empty, controller);
            var emptyState = empty.AddState("Idle");
            emptyState.writeDefaultValues = false;
            if (!useNullMotion)
            {
                var curveLess = new AnimationClip { frameRate = 60 };
                AssetDatabase.CreateAsset(curveLess, AssetDatabase.GenerateUniqueAssetPath(Folder + "/curve-less.anim"));
                emptyState.motion = curveLess;
            }

            controller.AddLayer(new AnimatorControllerLayer { name = "NoCurve", defaultWeight = 1f, stateMachine = empty });

            return SampleWith(controller, null);
        }

        /// <summary>Candidate architecture: one lowest baseline layer, per-animation layers with empty Off states.</summary>
        private float SampleCandidate(bool aOn, bool bOn, bool aFirst)
        {
            _renderer.SetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName), 0f);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                Folder + "/candidate-" + Guid.NewGuid().ToString("N") + ".controller");
            controller.AddParameter(ParamA, AnimatorControllerParameterType.Bool);
            controller.AddParameter(ParamB, AnimatorControllerParameterType.Bool);

            var baseline = new AnimatorStateMachine { name = "FaceMotion Baseline" };
            AssetDatabase.AddObjectToAsset(baseline, controller);
            var baselineState = baseline.AddState("Baseline");
            baselineState.motion = CreateResetClip();
            baselineState.writeDefaultValues = false;
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "FaceMotion Baseline",
                defaultWeight = 1f,
                stateMachine = baseline
            });

            if (aFirst)
            {
                AddEmptyOffLayer(controller, _clipA, ParamA, "FaceMotion MA AnimA");
                AddEmptyOffLayer(controller, _clipB, ParamB, "FaceMotion MA AnimB");
            }
            else
            {
                AddEmptyOffLayer(controller, _clipB, ParamB, "FaceMotion MA AnimB");
                AddEmptyOffLayer(controller, _clipA, ParamA, "FaceMotion MA AnimA");
            }

            return SampleWith(controller, new bool?[] { aOn, bOn });
        }

        private static void AddEmptyOffLayer(
            AnimatorController controller,
            AnimationClip clip,
            string parameterName,
            string layerName)
        {
            var machine = new AnimatorStateMachine { name = layerName };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var off = machine.AddState("Off");
            off.writeDefaultValues = false;
            var on = machine.AddState("On");
            on.motion = clip;
            on.writeDefaultValues = false;
            var toOn = off.AddTransition(on);
            toOn.hasExitTime = false;
            toOn.duration = 0f;
            toOn.AddCondition(AnimatorConditionMode.If, 0, parameterName);
            var toOff = on.AddTransition(off);
            toOff.hasExitTime = false;
            toOff.duration = 0f;
            toOff.AddCondition(AnimatorConditionMode.IfNot, 0, parameterName);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = machine
            });
        }

        private float SampleWith(AnimatorController controller, bool?[] parameterStates)
        {
            var animator = _root.AddComponent<Animator>();
            try
            {
                animator.runtimeAnimatorController = controller;
                if (parameterStates != null)
                {
                    animator.SetBool(ParamA, parameterStates[0] == true);
                    animator.SetBool(ParamB, parameterStates[1] == true);
                }

                animator.Update(0.1f);
                animator.Update(0.1f);
                animator.Update(0.1f);
                return _renderer.GetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(animator);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void AssertCase(float actual, float expected, string message)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(0.5f), message);
        }

        /// <summary>
        /// Plans and applies both sharing animations through the real backend in one batch,
        /// which is how desired-state reconciliation creates them together.
        /// </summary>
        private void ApplySharedPair(out AnimatorController controllerA, out AnimatorController controllerB)
        {
            _renderer.SetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName), 0f);
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.ApplyBatch(backend.PlanBatch(new[]
            {
                new ModularAvatarIntegrationRequest(_avatar, _clipA, Folder, "AnimA"),
                new ModularAvatarIntegrationRequest(_avatar, _clipB, Folder, "AnimB")
            }));
            Assert.That(result.Succeeded, Is.True, BatchDiagnosticsOf(result));
            controllerA = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimA/FX.controller");
            controllerB = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_AnimB/FX.controller");
            Assert.That(controllerA, Is.Not.Null, "animation A must be generated");
            Assert.That(controllerB, Is.Not.Null, "animation B must be generated");
        }

        /// <summary>
        /// One controller holding both generated layers, appended in the order Modular Avatar
        /// merges them (avatar hierarchy order).
        /// </summary>
        private AnimatorController Combine(string tag, AnimatorController first, AnimatorController second)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/combined-" + tag + ".controller");
            controller.AddParameter(ParamA, AnimatorControllerParameterType.Bool);
            controller.AddParameter(ParamB, AnimatorControllerParameterType.Bool);
            AppendGeneratedLayer(controller, first);
            AppendGeneratedLayer(controller, second);
            return controller;
        }

        /// <summary>
        /// Copies the source controller's generated layers into the target controller through the
        /// public animator API (Instantiate on a sub-asset state machine trips Unity's internal
        /// PPtr assertions). Every state, transition, condition, and motion is read from the real
        /// backend output; the copy hardcodes no generation knowledge.
        /// </summary>
        private static void AppendGeneratedLayer(AnimatorController target, AnimatorController source)
        {
            var layers = source.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                var sourceMachine = layers[i].stateMachine;
                if (sourceMachine == null || sourceMachine.states == null || sourceMachine.states.Length == 0) continue;
                var machine = new AnimatorStateMachine { name = sourceMachine.name };
                AssetDatabase.AddObjectToAsset(machine, target);

                var sourceStates = sourceMachine.states;
                var copies = new Dictionary<AnimatorState, AnimatorState>();
                for (var s = 0; s < sourceStates.Length; s++)
                {
                    var sourceState = sourceStates[s].state;
                    var copy = machine.AddState(sourceState.name);
                    copy.motion = sourceState.motion;
                    copy.writeDefaultValues = sourceState.writeDefaultValues;
                    copies[sourceState] = copy;
                }
                machine.defaultState = copies[sourceMachine.defaultState];

                for (var s = 0; s < sourceStates.Length; s++)
                {
                    var sourceState = sourceStates[s].state;
                    var sourceTransitions = sourceState.transitions;
                    for (var t = 0; t < sourceTransitions.Length; t++)
                    {
                        var sourceTransition = sourceTransitions[t];
                        var copy = copies[sourceState].AddTransition(copies[sourceTransition.destinationState]);
                        copy.hasExitTime = sourceTransition.hasExitTime;
                        copy.duration = sourceTransition.duration;
                        var conditions = sourceTransition.conditions;
                        for (var c = 0; c < conditions.Length; c++)
                            copy.AddCondition(conditions[c].mode, conditions[c].threshold, conditions[c].parameter);
                    }
                }

                target.AddLayer(new AnimatorControllerLayer
                {
                    name = layers[i].name,
                    defaultWeight = layers[i].defaultWeight,
                    blendingMode = layers[i].blendingMode,
                    stateMachine = machine
                });
            }
        }

        private float SampleCombined(AnimatorController controller, bool aOn, bool bOn)
        {
            _renderer.SetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName), 0f);
            var animator = _root.AddComponent<Animator>();
            try
            {
                animator.runtimeAnimatorController = controller;
                animator.SetBool(ParamA, aOn);
                animator.SetBool(ParamB, bOn);
                animator.Update(0.1f);
                animator.Update(0.1f);
                animator.Update(0.1f);
                animator.Update(0.1f);
                return _renderer.GetBlendShapeWeight(_mesh.GetBlendShapeIndex(ShapeName));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(animator);
            }
        }

        private void AssertThreeState(AnimatorController controller, string own, string partner, AnimationClip clip)
        {
            Assert.That(ParameterNames(controller), Is.EquivalentTo(new[] { own, partner }),
                "the controller carries its own parameter plus every partner parameter");
            var machine = FindGeneratedLayer(controller).stateMachine;
            Assert.That(machine, Is.Not.Null, "the generated FaceMotion layer must exist");
            var names = new List<string>();
            foreach (var child in machine.states) names.Add(child.state.name);
            Assert.That(names, Is.EqualTo(new[] { "OffUnique", "OffAll", "On" }),
                "partner-aware machines contain the unique reset, the full reset, and the clip state");

            var offUnique = machine.states[0].state;
            var offAll = machine.states[1].state;
            var on = machine.states[2].state;
            Assert.That(machine.defaultState, Is.SameAs(offUnique));
            Assert.That(offUnique.writeDefaultValues, Is.False);
            Assert.That(offAll.writeDefaultValues, Is.False);
            Assert.That(on.writeDefaultValues, Is.False);
            Assert.That(on.motion, Is.SameAs(clip));
            Assert.That((offAll.motion as AnimationClip)?.name, Is.EqualTo("Reset"));
            Assert.That((offUnique.motion as AnimationClip)?.name,
                Is.EqualTo("ResetUnique").Or.EqualTo("Reset Unique"));

            AssertTransition(offUnique.transitions[0], on, "If:" + own);
            AssertTransition(offUnique.transitions[1], offAll, "IfNot:" + partner);
            AssertTransition(offAll.transitions[0], on, "If:" + own);
            AssertTransition(offAll.transitions[1], offUnique, "If:" + partner);
            AssertTransition(on.transitions[0], offAll, "IfNot:" + own + ";IfNot:" + partner);
            AssertTransition(on.transitions[1], offUnique, "IfNot:" + own + ";If:" + partner);
        }

        /// <summary>Conditions are asserted in order as "If:Param;IfNot:Param".</summary>
        private static void AssertTransition(AnimatorStateTransition transition, AnimatorState expectedDestination, string expectedConditions)
        {
            Assert.That(transition.destinationState, Is.SameAs(expectedDestination));
            Assert.That(transition.duration, Is.EqualTo(0f));
            Assert.That(transition.hasExitTime, Is.False);
            var expected = expectedConditions.Split(';');
            Assert.That(transition.conditions, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                var separator = expected[i].IndexOf(':');
                var mode = (AnimatorConditionMode)Enum.Parse(typeof(AnimatorConditionMode), expected[i].Substring(0, separator));
                var parameter = expected[i].Substring(separator + 1);
                Assert.That(transition.conditions[i].mode, Is.EqualTo(mode), "condition " + i + " mode");
                Assert.That(transition.conditions[i].parameter, Is.EqualTo(parameter), "condition " + i + " parameter");
            }
        }

        private static AnimatorControllerLayer FindGeneratedLayer(AnimatorController controller)
        {
            foreach (var layer in controller.layers)
                if (layer.stateMachine != null && layer.stateMachine.states != null && layer.stateMachine.states.Length > 0)
                    return layer;
            return default;
        }

        private static string[] ParameterNames(AnimatorController controller)
        {
            var parameters = controller.parameters;
            var names = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) names[i] = parameters[i].name;
            return names;
        }

        private static FaceMotionDiagnostic FindDiagnostic(IReadOnlyList<FaceMotionDiagnostic> diagnostics, string code)
        {
            if (diagnostics == null) return null;
            for (var i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i] != null && string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return diagnostics[i];
            return null;
        }

        private static string BatchDiagnosticsOf(ModularAvatarIntegrationBatchResult result)
        {
            if (result == null) return "no batch result";
            if (result.Diagnostics == null || result.Diagnostics.Count == 0) return "no diagnostics";
            var parts = new List<string>();
            for (int i = 0; i < result.Diagnostics.Count; i++) parts.Add(result.Diagnostics[i].Code + ": " + result.Diagnostics[i].Message);
            return string.Join(" | ", parts);
        }

        private static string PlanDiagnosticsOf(ModularAvatarIntegrationPlan plan)
        {
            if (plan == null) return "no plan";
            if (plan.Diagnostics == null || plan.Diagnostics.Count == 0) return "no diagnostics";
            var parts = new List<string>();
            for (int i = 0; i < plan.Diagnostics.Count; i++) parts.Add(plan.Diagnostics[i].Code + ": " + plan.Diagnostics[i].Message);
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// One controller holding both generated layers, appended in the order Modular Avatar
        /// would merge them (hierarchy order).
        /// </summary>
        private AnimatorController BuildCombinedController(string path, bool aFirst)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(ParamA, AnimatorControllerParameterType.Bool);
            controller.AddParameter(ParamB, AnimatorControllerParameterType.Bool);
            AnimationClip reset = CreateResetClip();
            if (aFirst)
            {
                AddFaceMotionLayer(controller, _clipA, ParamA, "FaceMotion MA AnimA", reset);
                AddFaceMotionLayer(controller, _clipB, ParamB, "FaceMotion MA AnimB", reset);
            }
            else
            {
                AddFaceMotionLayer(controller, _clipB, ParamB, "FaceMotion MA AnimB", reset);
                AddFaceMotionLayer(controller, _clipA, ParamA, "FaceMotion MA AnimA", reset);
            }

            return controller;
        }

        private AnimationClip CreateResetClip()
        {
            var reset = ResetClipBuilder.Create(_avatar, _clipA);
            reset.name = "Reset";
            AssetDatabase.CreateAsset(reset, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Reset.anim"));
            return reset;
        }

        /// <summary>Mirrors ModularAvatarIntegrationBackend.Apply lines that build FX.controller.</summary>
        private static void AddFaceMotionLayer(
            AnimatorController controller,
            AnimationClip clip,
            string parameterName,
            string layerName,
            AnimationClip reset)
        {
            var machine = new AnimatorStateMachine { name = layerName };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var off = machine.AddState("Off");
            off.motion = reset;
            off.writeDefaultValues = false;
            var on = machine.AddState("On");
            on.motion = clip;
            on.writeDefaultValues = false;
            var toOn = off.AddTransition(on);
            toOn.hasExitTime = false;
            toOn.duration = 0f;
            toOn.AddCondition(AnimatorConditionMode.If, 0, parameterName);
            var toOff = on.AddTransition(off);
            toOff.hasExitTime = false;
            toOff.duration = 0f;
            toOff.AddCondition(AnimatorConditionMode.IfNot, 0, parameterName);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = machine
            });
        }

        private static string Describe(AnimatorControllerLayer layer)
        {
            var machine = layer.stateMachine;
            var off = machine.states[0].state;
            var on = machine.states[1].state;
            var toOn = off.transitions[0];
            var toOff = on.transitions[0];
            return string.Join("|",
                "name=" + layer.name,
                "weight=" + layer.defaultWeight,
                "default=" + machine.defaultState.name,
                "off=" + off.name + ",wd=" + off.writeDefaultValues + ",motion=" + MotionName(off.motion),
                "on=" + on.name + ",wd=" + on.writeDefaultValues + ",motion=" + MotionName(on.motion),
                "toOn=" + toOn.duration + ",exit=" + toOn.hasExitTime
                    + "," + toOn.conditions[0].mode + "," + toOn.conditions[0].parameter,
                "toOff=" + toOff.duration + ",exit=" + toOff.hasExitTime
                    + "," + toOff.conditions[0].mode + "," + toOff.conditions[0].parameter);
        }

        private static string MotionName(Motion motion)
        {
            return motion == null ? "<none>" : motion.name;
        }

        private static string DiagnosticsOf(ModularAvatarIntegrationResult result)
        {
            if (result == null || result.Diagnostics == null || result.Diagnostics.Count == 0) return "no diagnostics";
            var parts = new List<string>();
            for (int i = 0; i < result.Diagnostics.Count; i++) parts.Add(result.Diagnostics[i].Code + ": " + result.Diagnostics[i].Message);
            return string.Join(" | ", parts);
        }

        private static Mesh CreateMesh(string blendShape)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.AddBlendShapeFrame(blendShape, 0f, new Vector3[3], null, null);
            return mesh;
        }

        private AnimationClip CreateClip(string assetName, float value)
        {
            var clip = new AnimationClip { frameRate = 60 };
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(BindingPath, typeof(SkinnedMeshRenderer), "blendShape." + ShapeName),
                AnimationCurve.Constant(0f, 1f / 60f, value));
            AssetDatabase.CreateAsset(clip, Folder + "/" + assetName);
            return clip;
        }
    }
}
