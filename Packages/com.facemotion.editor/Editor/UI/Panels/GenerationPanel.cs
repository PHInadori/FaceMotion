using System;
using FaceMotion.Animation;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Generation;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>Applies Phase E presets and generated motion only after the user explicitly requests it.</summary>
    public sealed class GenerationPanel
    {
        public static System.Collections.Generic.IReadOnlyList<FaceMotionBuiltinPreset> Builtins => FaceMotionBuiltins.Presets;
        private readonly FaceMotionEditorSession _session;
        private int _blendShapeIndex;
        private int _transformIndex;
        private int _seed = 1;
        private float _duration = 2f;
        private float _step = .25f;
        private float _minimum = 0f;
        private float _maximum = 100f;
        private string _outcome;

        public GenerationPanel(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("presetsGeneration"), EditorStyles.boldLabel);
            var animation = _session.GetSelectedAnimation();
            var index = _session.ActiveAvatarIndex;
            if (_session.ActiveProject == null || animation == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("openProjectSelectAnimation"), MessageType.Info);
                return;
            }

            if (index == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAvatarRebuildIndex"), MessageType.Info);
                return;
            }

            DrawMappingProfile(index);
            DrawPresetButtons(animation, index);
            DrawRandomBlendShape(animation, index);
            DrawRandomRotation(animation, index);
            if (!string.IsNullOrEmpty(_outcome)) EditorGUILayout.HelpBox(_outcome, MessageType.None);
        }

        private void DrawPresetButtons(FaceMotionAnimationData animation, AvatarIndex index)
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("builtIns"), EditorStyles.miniBoldLabel);
            foreach (var preset in FaceMotionBuiltins.Presets)
            {
                var bindings = new System.Collections.Generic.Dictionary<string, BlendShapeBinding>();
                bool available = true;
                foreach (var target in preset.Targets)
                {
                    if (LogicalMappingConfirmation.TryResolveConfirmed(_session.ActiveMappingProfile, index, target.LogicalTargetId, out var binding)) bindings.Add(target.LogicalTargetId, binding);
                    else if (!target.Optional) available = false;
                }
                if (bindings.Count == 0) available = false;
                using (new EditorGUI.DisabledScope(!available))
                {
                    if (GUILayout.Button(preset.DisplayName))
                    {
                        var snapshot = GenerationSettingsSnapshot.ForPreset(preset, bindings);
                        Apply(animation, PhaseEGenerators.CreatePreset(StableId.New(), preset, bindings), snapshot);
                    }
                }
                if (!available) EditorGUILayout.LabelField(string.Format(FaceMotionUiText.Get("confirmUniqueMapping"), preset.DisplayName), EditorStyles.miniLabel);
            }

            if (LogicalMappingConfirmation.TryResolveConfirmed(_session.ActiveMappingProfile, index, "eye.blink", out var blinkBinding))
            {
                if (GUILayout.Button(FaceMotionUiText.Get("generateBlink")))
                {
                    var settings = new BlinkGenerationSettings { Seed = _seed, Duration = _duration };
                    Apply(animation, PhaseEGenerators.CreateBlink(StableId.New(), settings, blinkBinding), GenerationSettingsSnapshot.ForBlink(settings, blinkBinding));
                }
            }
        }

        private void DrawMappingProfile(AvatarIndex index)
        {
            var suggestions = LogicalMappingSuggestions.Suggest(index);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("confirmedMapping"), EditorStyles.miniBoldLabel);
            if (_session.ActiveMappingProfile == null && GUILayout.Button(FaceMotionUiText.Get("createMappingProfile")))
            {
                string path = EditorUtility.SaveFilePanelInProject(FaceMotionUiText.Get("createMappingProfileDialog"), "FaceMotionMapping", "asset", FaceMotionUiText.Get("mappingProfilePrompt"));
                if (!string.IsNullOrEmpty(path))
                {
                    var profile = AvatarMappingProfile.CreateNew("FaceMotion Mapping");
                    profile.SetAvatarFingerprint(index.Fingerprint);
                    AssetDatabase.CreateAsset(profile, path);
                    AssetDatabase.SaveAssets();
                    _session.SetMappingProfile(profile);
                }
            }
            if (_session.ActiveMappingProfile != null) EditorGUILayout.LabelField(FaceMotionUiText.Get("profile"), _session.ActiveMappingProfile.name, EditorStyles.miniLabel);
            if (suggestions.Count == 0)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("noLogicalTargets"), EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < suggestions.Count; i++)
            {
                var entry = suggestions[i];
                var binding = entry.BlendShape;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.LogicalTargetId, binding.Value.RendererPath + "/" + binding.Value.BlendShapeName, EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(_session.ActiveMappingProfile == null))
                {
                    if (GUILayout.Button(FaceMotionUiText.Get("confirm"), EditorStyles.miniButton, GUILayout.Width(60f)) && LogicalMappingConfirmation.Confirm(_session.ActiveMappingProfile, index, entry))
                    {
                        EditorUtility.SetDirty(_session.ActiveMappingProfile);
                        AssetDatabase.SaveAssets();
                        _session.RefreshAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRandomBlendShape(FaceMotionAnimationData animation, AvatarIndex index)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("randomBlendShape"), EditorStyles.miniBoldLabel);
            if (index.BlendShapes.Count == 0)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("noBlendShapes"), EditorStyles.miniLabel);
                return;
            }

            _blendShapeIndex = Mathf.Clamp(_blendShapeIndex, 0, index.BlendShapes.Count - 1);
            var names = new string[index.BlendShapes.Count];
            for (int i = 0; i < names.Length; i++) names[i] = index.BlendShapes[i].RendererPath + "/" + index.BlendShapes[i].BlendShapeName;
            _blendShapeIndex = EditorGUILayout.Popup(FaceMotionUiText.Get("target"), _blendShapeIndex, names);
            DrawRandomSettings();
            if (GUILayout.Button(FaceMotionUiText.Get("generateRandomBlendShape")))
            {
                var target = index.BlendShapes[_blendShapeIndex];
                var settings = new RandomBlendShapeGenerationSettings { Seed = _seed, Duration = _duration, Step = _step, MinValue = _minimum, MaxValue = _maximum };
                var binding = new BlendShapeBinding(target.RendererPath, target.BlendShapeName);
                Apply(animation, PhaseEGenerators.CreateRandomBlendShape(StableId.New(), settings, binding), GenerationSettingsSnapshot.ForRandomBlendShape(settings, binding));
            }
        }

        private void DrawRandomRotation(FaceMotionAnimationData animation, AvatarIndex index)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("randomRotation"), EditorStyles.miniBoldLabel);
            if (index.Transforms.Count == 0) return;
            _transformIndex = Mathf.Clamp(_transformIndex, 0, index.Transforms.Count - 1);
            var names = new string[index.Transforms.Count];
            for (int i = 0; i < names.Length; i++) names[i] = string.IsNullOrEmpty(index.Transforms[i].RelativePath) ? FaceMotionUiText.Get("avatarRoot") : index.Transforms[i].RelativePath;
            _transformIndex = EditorGUILayout.Popup(FaceMotionUiText.Get("target"), _transformIndex, names);
            DrawRandomSettings();
            if (GUILayout.Button(FaceMotionUiText.Get("generateRandomRotation")))
            {
                var settings = new RandomRotationGenerationSettings { Seed = _seed, Duration = _duration, Step = _step, MinEuler = new Vector3(_minimum, _minimum, _minimum), MaxEuler = new Vector3(_maximum, _maximum, _maximum), TransformPath = index.Transforms[_transformIndex].RelativePath };
                Apply(animation, PhaseEGenerators.CreateRandomRotation(StableId.New(), settings), GenerationSettingsSnapshot.ForRandomRotation(settings));
            }
        }

        private void DrawRandomSettings()
        {
            _seed = EditorGUILayout.IntField(FaceMotionUiText.Get("seed"), _seed);
            _duration = EditorGUILayout.FloatField(FaceMotionUiText.Get("duration"), _duration);
            _step = EditorGUILayout.FloatField(FaceMotionUiText.Get("step"), _step);
            _minimum = EditorGUILayout.FloatField(FaceMotionUiText.Get("minimum"), _minimum);
            _maximum = EditorGUILayout.FloatField(FaceMotionUiText.Get("maximum"), _maximum);
        }

        private void Apply(FaceMotionAnimationData animation, GeneratedMotion motion, GenerationSettingsSnapshot snapshot)
        {
            var result = GeneratedMotionUndoService.Apply(_session.ActiveProject, animation, motion, snapshot.GeneratorType, snapshot.SourcePresetId, snapshot.Hash(), snapshot.Serialize());
            _outcome = string.Format(FaceMotionUiText.Get("generationOutcome"), result.AddedKeys, result.ProtectedManualKeys);
            _session.RefreshAll();
        }

    }
}
