using System;
using System.Collections.Generic;
using System.Globalization;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using UnityEngine;
using GeneratedMotion = global::FaceMotion.Animation.GeneratedMotion;
using GeneratedTrackMotion = global::FaceMotion.Animation.GeneratedTrackMotion;
using GeneratedFloatKey = global::FaceMotion.Animation.GeneratedFloatKey;
using GeneratedVector3Key = global::FaceMotion.Animation.GeneratedVector3Key;
using MotionMergeRules = global::FaceMotion.Animation.MotionMergeRules;

namespace FaceMotion.Generation
{
    public sealed class FaceMotionBuiltinPreset
    {
        public FaceMotionBuiltinPreset(string id, string builtinKey, string displayName, params FaceMotionBuiltinPresetTarget[] targets)
        {
            Id = id;
            BuiltinKey = builtinKey;
            DisplayName = displayName;
            Targets = targets;
        }

        public string Id { get; }
        public string BuiltinKey { get; }
        public string DisplayName { get; }
        public IReadOnlyList<FaceMotionBuiltinPresetTarget> Targets { get; }
    }

    /// <summary>One logical target in a built-in pulse. Optional targets never block application.</summary>
    public sealed class FaceMotionBuiltinPresetTarget
    {
        public FaceMotionBuiltinPresetTarget(string logicalTargetId, bool optional = false, float stagger = 0f, float value = 100f)
        {
            LogicalTargetId = logicalTargetId;
            Optional = optional;
            Stagger = stagger;
            Value = value;
        }

        public string LogicalTargetId { get; }
        public bool Optional { get; }
        public float Stagger { get; }
        public float Value { get; }
    }

    /// <summary>Stable, avatar-neutral built-ins. Mapping resolves their logical IDs.</summary>
    public static class FaceMotionBuiltins
    {
        public static readonly IReadOnlyList<FaceMotionBuiltinPreset> Presets = new[]
        {
            new FaceMotionBuiltinPreset("c134e6e8df2a4bf1a08d7c95f3a6b201", "builtin.smile", "Smile", new FaceMotionBuiltinPresetTarget("mouth.smile"), new FaceMotionBuiltinPresetTarget("eye.smile.left", true, .04f), new FaceMotionBuiltinPresetTarget("eye.smile.right", true, .08f)),
            new FaceMotionBuiltinPreset("4a6c8e10b2d34f7591a3c5e7f9b0d412", "builtin.wink-left", "Wink Left", new FaceMotionBuiltinPresetTarget("eye.close.left")),
            new FaceMotionBuiltinPreset("7d9f1b23c4e54680a2d5f8b1c3e6a724", "builtin.wink-right", "Wink Right", new FaceMotionBuiltinPresetTarget("eye.close.right")),
            new FaceMotionBuiltinPreset("e2b4d6f8091a4c73b5e7f1a3d6c8b935", "builtin.blink", "Blink", new FaceMotionBuiltinPresetTarget("eye.close.left"), new FaceMotionBuiltinPresetTarget("eye.close.right", false, .04f)),
            new FaceMotionBuiltinPreset("19c3e5f7a8b24d60c1e4f6a9b2d7c846", "builtin.angry", "Angry", new FaceMotionBuiltinPresetTarget("brow.angry.left"), new FaceMotionBuiltinPresetTarget("brow.angry.right", false, .04f), new FaceMotionBuiltinPresetTarget("mouth.angry", true, .08f)),
            new FaceMotionBuiltinPreset("6f8a0c12d3e54b79a1c4e7f9b2d5a638", "builtin.sad", "Sad", new FaceMotionBuiltinPresetTarget("brow.sad.left"), new FaceMotionBuiltinPresetTarget("brow.sad.right", false, .04f), new FaceMotionBuiltinPresetTarget("mouth.sad", true, .08f)),
            new FaceMotionBuiltinPreset("b3d5f7a9012c4e68b1d4f6a8c2e9d750", "builtin.surprise", "Surprise", new FaceMotionBuiltinPresetTarget("eye.wide.left"), new FaceMotionBuiltinPresetTarget("eye.wide.right", false, .04f), new FaceMotionBuiltinPresetTarget("mouth.open", false, .08f)),
            new FaceMotionBuiltinPreset("8e1a3c5d7f904b26a8c1e4f6b9d2a573", "builtin.embarrassed", "Embarrassed", new FaceMotionBuiltinPresetTarget("cheek.blush.left", true), new FaceMotionBuiltinPresetTarget("cheek.blush.right", true, .04f), new FaceMotionBuiltinPresetTarget("mouth.smile", true, .08f), new FaceMotionBuiltinPresetTarget("eye.soft.left", true, .12f), new FaceMotionBuiltinPresetTarget("eye.soft.right", true, .16f))
        };

        public static FaceMotionBuiltinPreset Find(string id)
        {
            string normalizedId = NormalizeId(id);
            foreach (var preset in Presets) if (preset.Id == normalizedId) return preset;
            return null;
        }

        /// <summary>Maps persisted legacy builtin keys to their permanent preset GUIDs.</summary>
        public static string NormalizeId(string id)
        {
            foreach (var preset in Presets) if (preset.BuiltinKey == id) return preset.Id;
            return id;
        }
    }

    public static class LogicalMappingSuggestions
    {
        private static readonly string[] Targets = { "mouth.smile", "eye.smile.left", "eye.smile.right", "eye.close.left", "eye.close.right", "brow.angry.left", "brow.angry.right", "mouth.angry", "brow.sad.left", "brow.sad.right", "mouth.sad", "eye.wide.left", "eye.wide.right", "mouth.open", "cheek.blush.left", "cheek.blush.right", "eye.soft.left", "eye.soft.right" };

        /// <summary>Suggests only unique, ordinal bindings; no binding is guessed on ambiguity.</summary>
        public static IReadOnlyList<BindingMappingEntry> Suggest(AvatarIndex index)
        {
            var result = new List<BindingMappingEntry>();
            if (index == null) return result;
            foreach (string target in Targets)
            {
                string token = NormalizeToken(target.Substring(target.IndexOf('.') + 1));
                BlendShapeIndexEntry match = null;
                int count = 0;
                for (int i = 0; i < index.BlendShapes.Count; i++)
                {
                    var candidate = index.BlendShapes[i];
                    if (NormalizeToken(candidate.BlendShapeName).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        match = candidate;
                        count++;
                    }
                }
                if (count == 1)
                {
                    result.Add(BindingMappingEntry.CreateBlendShape(target, match.RendererPath, match.BlendShapeName));
                }
            }
            return result;
        }

        private static string NormalizeToken(string value)
        {
            var result = new System.Text.StringBuilder();
            foreach (char character in value) if (char.IsLetterOrDigit(character)) result.Append(character);
            return result.ToString();
        }
    }

    /// <summary>Suggestions are candidates only; confirmation is the sole path into a mapping profile.</summary>
    public static class LogicalMappingConfirmation
    {
        public static bool Confirm(AvatarMappingProfile profile, AvatarIndex index, BindingMappingEntry candidate)
        {
            if (profile == null || index == null || candidate == null || !candidate.BlendShape.HasValue) return false;
            bool suggested = false;
            foreach (var entry in LogicalMappingSuggestions.Suggest(index))
                if (entry.LogicalTargetId == candidate.LogicalTargetId && entry.BlendShape.Value.Equals(candidate.BlendShape.Value)) suggested = true;
            if (!suggested) return false;
            foreach (var entry in profile.Mappings) if (entry != null && entry.LogicalTargetId == candidate.LogicalTargetId) return false;
            profile.SetAvatarFingerprint(index.Fingerprint);
            profile.AddMapping(candidate.Clone());
            return true;
        }

        public static bool TryResolveConfirmed(AvatarMappingProfile profile, AvatarIndex index, string logicalTargetId, out BlendShapeBinding binding)
        {
            binding = default(BlendShapeBinding);
            if (profile == null || index == null) return false;
            foreach (var entry in profile.Mappings)
            {
                if (entry != null && entry.LogicalTargetId == logicalTargetId && entry.BlendShape.HasValue && index.ResolveBlendShape(entry.BlendShape.Value).Status == NameResolutionStatus.Unique)
                {
                    binding = entry.BlendShape.Value;
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class BlinkGenerationSettings
    {
        public int Seed = 1;
        public float StartTime;
        public float Duration = 3f;
        public float MinInterval = 1.5f;
        public float MaxInterval = 3f;
        public float ClosedValue = 100f;
        public string LogicalTargetId = "eye.blink";
    }

    public sealed class RandomBlendShapeGenerationSettings
    {
        public int Seed = 1;
        public float StartTime;
        public float Duration = 2f;
        public float Step = 0.25f;
        public float MinValue;
        public float MaxValue = 100f;
        public string LogicalTargetId = "mouth.smile";
    }

    public sealed class RandomRotationGenerationSettings
    {
        public int Seed = 1;
        public float StartTime;
        public float Duration = 2f;
        public float Step = 0.25f;
        public Vector3 MinEuler = new Vector3(-5f, -5f, -5f);
        public Vector3 MaxEuler = new Vector3(5f, 5f, 5f);
        public string TransformPath = "";
    }

    public static class PhaseEGenerators
    {
        public const int AlgorithmVersion = 1;

        /// <summary>Builds fixed 0 -> value -> 0 preset pulses; target stagger is part of the preset contract.</summary>
        public static GeneratedMotion CreatePreset(string generationId, FaceMotionBuiltinPreset preset, IReadOnlyDictionary<string, BlendShapeBinding> bindings)
        {
            if (preset == null || bindings == null) throw new ArgumentNullException();
            var motion = new GeneratedMotion(generationId, AlgorithmVersion);
            foreach (var target in preset.Targets)
            {
                if (!bindings.TryGetValue(target.LogicalTargetId, out var binding))
                {
                    if (!target.Optional) throw new ArgumentException("A required preset target is not mapped: " + target.LogicalTargetId, nameof(bindings));
                    continue;
                }
                var track = GeneratedTrackMotion.CreateBlendShape(binding);
                motion.Tracks.Add(track);
                track.FloatKeys.Add(new GeneratedFloatKey(target.Stagger, 0f));
                track.FloatKeys.Add(new GeneratedFloatKey(target.Stagger + .10f, target.Value, InterpolationType.EaseIn));
                track.FloatKeys.Add(new GeneratedFloatKey(target.Stagger + .25f, 0f, InterpolationType.EaseOut));
            }
            return motion;
        }

        public static GeneratedMotion CreateBlink(string generationId, BlinkGenerationSettings settings, BlendShapeBinding binding)
        {
            var motion = new GeneratedMotion(generationId, AlgorithmVersion);
            var track = GeneratedTrackMotion.CreateBlendShape(binding);
            motion.Tracks.Add(track);
            var random = new System.Random(settings.Seed);
            float end = settings.StartTime + Mathf.Max(0f, settings.Duration);
            track.FloatKeys.Add(new GeneratedFloatKey(settings.StartTime, 0f));
            for (float t = settings.StartTime + Next(random, settings.MinInterval, settings.MaxInterval); t < end; t += Next(random, settings.MinInterval, settings.MaxInterval))
            {
                track.FloatKeys.Add(new GeneratedFloatKey(t, 0f, InterpolationType.EaseIn));
                track.FloatKeys.Add(new GeneratedFloatKey(Mathf.Min(t + 0.06f, end), settings.ClosedValue, InterpolationType.EaseOut));
                track.FloatKeys.Add(new GeneratedFloatKey(Mathf.Min(t + 0.14f, end), 0f));
            }
            return motion;
        }

        public static GeneratedMotion CreateRandomBlendShape(string generationId, RandomBlendShapeGenerationSettings settings, BlendShapeBinding binding)
        {
            var motion = new GeneratedMotion(generationId, AlgorithmVersion);
            var track = GeneratedTrackMotion.CreateBlendShape(binding);
            motion.Tracks.Add(track);
            var random = new System.Random(settings.Seed);
            AddRandomFloatKeys(track, random, settings.StartTime, settings.Duration, settings.Step, settings.MinValue, settings.MaxValue);
            return motion;
        }

        public static GeneratedMotion CreateRandomRotation(string generationId, RandomRotationGenerationSettings settings)
        {
            var motion = new GeneratedMotion(generationId, AlgorithmVersion);
            var track = GeneratedTrackMotion.CreateTransform(new TransformBinding(settings.TransformPath), TrackKind.TransformRotation);
            motion.Tracks.Add(track);
            var random = new System.Random(settings.Seed);
            float end = settings.StartTime + Mathf.Max(0f, settings.Duration);
            float step = Mathf.Max(0.001f, settings.Step);
            for (float t = settings.StartTime; t <= end + 0.0001f; t += step)
            {
                track.Vector3Keys.Add(new GeneratedVector3Key(t, new Vector3(Next(random, settings.MinEuler.x, settings.MaxEuler.x), Next(random, settings.MinEuler.y, settings.MaxEuler.y), Next(random, settings.MinEuler.z, settings.MaxEuler.z)), InterpolationType.Smooth));
            }
            return motion;
        }

        private static void AddRandomFloatKeys(GeneratedTrackMotion track, System.Random random, float start, float duration, float step, float min, float max)
        {
            float end = start + Mathf.Max(0f, duration);
            step = Mathf.Max(0.001f, step);
            for (float t = start; t <= end + 0.0001f; t += step) track.FloatKeys.Add(new GeneratedFloatKey(t, Next(random, min, max), InterpolationType.Smooth));
        }

        private static float Next(System.Random random, float min, float max) => min + ((float)random.NextDouble() * (max - min));
    }

    public sealed class GeneratedMotionApplyResult
    {
        public int AddedKeys { get; internal set; }
        public int ProtectedManualKeys { get; internal set; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics => _diagnostics;
        private readonly List<FaceMotionDiagnostic> _diagnostics = new List<FaceMotionDiagnostic>();
        internal void AddDiagnostic(FaceMotionDiagnostic diagnostic) => _diagnostics.Add(diagnostic);
    }

    /// <summary>Converts a pure generated pass into timeline keys under the conservative collision contract.</summary>
    public static class GeneratedMotionApplier
    {
        public static GeneratedMotionApplyResult Apply(FaceMotionProject project, FaceMotionAnimationData animation, GeneratedMotion motion, GeneratorType type, string sourcePresetId, string settingsHash, string settingsSnapshot = null)
        {
            var result = new GeneratedMotionApplyResult();
            if (project == null || animation == null || motion == null) throw new ArgumentNullException();
            if (!project.HasGeneration(motion.GenerationId)) project.AddGenerationRecord(GenerationRecord.Create(animation.AnimationId, type, sourcePresetId, motion.AlgorithmVersion, settingsHash, motion.GenerationId, settingsSnapshot));
            foreach (var generated in motion.Tracks)
            {
                var target = FindOrCreate(animation.Timeline, generated);
                if (generated.Kind == TrackKind.BlendShape)
                {
                    foreach (var key in generated.FloatKeys) Add(target.BlendShape, key, new KeyOrigin(type == GeneratorType.Blink ? OriginKind.Blink : type == GeneratorType.Random ? OriginKind.Random : OriginKind.Preset, motion.GenerationId), result);
                }
                else foreach (var key in generated.Vector3Keys) Add(target.Transform, key, new KeyOrigin(type == GeneratorType.Random ? OriginKind.Random : OriginKind.Preset, motion.GenerationId), result);
            }
            return result;
        }

        private static FaceTrackData FindOrCreate(FaceTimelineData timeline, GeneratedTrackMotion generated)
        {
            foreach (var track in timeline.Tracks)
            {
                if (track.Kind != generated.Kind) continue;
                if (generated.Kind == TrackKind.BlendShape && track.BlendShape.RendererPath == generated.BlendShape.Value.RendererPath && track.BlendShape.BlendShapeName == generated.BlendShape.Value.BlendShapeName) return track;
                if (generated.Kind != TrackKind.BlendShape && track.Transform.TransformPath == generated.Transform.Value.TransformPath) return track;
            }
            FaceTrackData created = generated.Kind == TrackKind.BlendShape ? FaceTrackData.CreateBlendShape(generated.BlendShape.Value.RendererPath, generated.BlendShape.Value.BlendShapeName) : FaceTrackData.CreateTransform(generated.Kind, generated.Transform.Value.TransformPath);
            timeline.AddTrack(created);
            return created;
        }

        private static void Add(BlendShapeTrackPayload payload, GeneratedFloatKey incoming, KeyOrigin origin, GeneratedMotionApplyResult result)
        {
            foreach (var existing in payload.Keys) if (Mathf.Approximately(existing.Time, incoming.Time)) { Protect(existing.Origin, result); return; }
            payload.AddKey(FloatKeyframeData.Create(incoming.Time, incoming.Value, incoming.Interpolation, origin));
            result.AddedKeys++;
        }

        private static void Add(TransformTrackPayload payload, GeneratedVector3Key incoming, KeyOrigin origin, GeneratedMotionApplyResult result)
        {
            foreach (var existing in payload.Keys) if (Mathf.Approximately(existing.Time, incoming.Time)) { Protect(existing.Origin, result); return; }
            payload.AddKey(Vector3KeyframeData.Create(incoming.Time, incoming.Value, incoming.Interpolation, origin));
            result.AddedKeys++;
        }

        private static void Protect(KeyOrigin origin, GeneratedMotionApplyResult result)
        {
            if (!MotionMergeRules.ManualKeyIsProtected(origin)) return;
            result.ProtectedManualKeys++;
            result.AddDiagnostic(new FaceMotionDiagnostic("FM-GEN-0004", FaceMotionDiagnosticSeverity.Warning, "Generated key skipped because a manual key occupies the same time.", string.Empty, false, "Move or remove the manual key to allow generation."));
        }
    }
}
