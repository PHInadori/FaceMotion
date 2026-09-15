using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FaceMotion.Animation;
using FaceMotion.Data;
using UnityEngine;

namespace FaceMotion.Generation
{
    /// <summary>Serializable, Unity-object-free input used to reproduce one generated pass.</summary>
    [Serializable]
    public sealed class GenerationSettingsSnapshot
    {
        public int FormatVersion = 1;
        public GeneratorType GeneratorType;
        public string SourcePresetId = string.Empty;
        public string RendererPath = string.Empty;
        public string BlendShapeName = string.Empty;
        public string TransformPath = string.Empty;
        public int Seed;
        public float StartTime;
        public float Duration;
        public float Step;
        public float MinValue;
        public float MaxValue;
        public Vector3 MinEuler;
        public Vector3 MaxEuler;
        public float MinInterval;
        public float MaxInterval;
        public float ClosedValue;
        public List<PresetBindingSnapshot> PresetBindings = new List<PresetBindingSnapshot>();

        public string Serialize() => JsonUtility.ToJson(this);

        public string Hash() => ComputeHash(Serialize());

        public static string ComputeHash(string value)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var result = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        public static GenerationSettingsSnapshot ForPreset(FaceMotionBuiltinPreset preset, IReadOnlyDictionary<string, BlendShapeBinding> bindings)
        {
            var snapshot = new GenerationSettingsSnapshot { GeneratorType = GeneratorType.Preset, SourcePresetId = preset.Id };
            foreach (var pair in bindings) snapshot.PresetBindings.Add(new PresetBindingSnapshot { LogicalTargetId = pair.Key, RendererPath = pair.Value.RendererPath, BlendShapeName = pair.Value.BlendShapeName });
            return snapshot;
        }

        public static GenerationSettingsSnapshot ForBlink(BlinkGenerationSettings value, BlendShapeBinding binding)
        {
            return new GenerationSettingsSnapshot { GeneratorType = GeneratorType.Blink, SourcePresetId = FaceMotionBuiltins.Find("builtin.blink").Id, RendererPath = binding.RendererPath, BlendShapeName = binding.BlendShapeName, Seed = value.Seed, StartTime = value.StartTime, Duration = value.Duration, MinInterval = value.MinInterval, MaxInterval = value.MaxInterval, ClosedValue = value.ClosedValue };
        }

        public static GenerationSettingsSnapshot ForRandomBlendShape(RandomBlendShapeGenerationSettings value, BlendShapeBinding binding)
        {
            return new GenerationSettingsSnapshot { GeneratorType = GeneratorType.Random, RendererPath = binding.RendererPath, BlendShapeName = binding.BlendShapeName, Seed = value.Seed, StartTime = value.StartTime, Duration = value.Duration, Step = value.Step, MinValue = value.MinValue, MaxValue = value.MaxValue };
        }

        public static GenerationSettingsSnapshot ForRandomRotation(RandomRotationGenerationSettings value)
        {
            return new GenerationSettingsSnapshot { GeneratorType = GeneratorType.Random, TransformPath = value.TransformPath, Seed = value.Seed, StartTime = value.StartTime, Duration = value.Duration, Step = value.Step, MinEuler = value.MinEuler, MaxEuler = value.MaxEuler };
        }

        public static bool TryRegenerate(string serialized, string generationId, out GeneratedMotion motion)
        {
            motion = null;
            if (string.IsNullOrEmpty(serialized)) return false;
            var snapshot = JsonUtility.FromJson<GenerationSettingsSnapshot>(serialized);
            if (snapshot == null || snapshot.FormatVersion != 1) return false;
            snapshot.SourcePresetId = FaceMotionBuiltins.NormalizeId(snapshot.SourcePresetId);
            if (snapshot.GeneratorType == GeneratorType.Preset)
            {
                var preset = FaceMotionBuiltins.Find(snapshot.SourcePresetId);
                if (preset == null) return false;
                var bindings = new Dictionary<string, BlendShapeBinding>();
                foreach (var binding in snapshot.PresetBindings) bindings[binding.LogicalTargetId] = new BlendShapeBinding(binding.RendererPath, binding.BlendShapeName);
                try { motion = PhaseEGenerators.CreatePreset(generationId, preset, bindings); return true; }
                catch (ArgumentException) { return false; }
            }
            if (!string.IsNullOrEmpty(snapshot.BlendShapeName))
            {
                var binding = new BlendShapeBinding(snapshot.RendererPath, snapshot.BlendShapeName);
                if (snapshot.GeneratorType == GeneratorType.Blink)
                    motion = PhaseEGenerators.CreateBlink(generationId, new BlinkGenerationSettings { Seed = snapshot.Seed, StartTime = snapshot.StartTime, Duration = snapshot.Duration, MinInterval = snapshot.MinInterval, MaxInterval = snapshot.MaxInterval, ClosedValue = snapshot.ClosedValue }, binding);
                else
                    motion = PhaseEGenerators.CreateRandomBlendShape(generationId, new RandomBlendShapeGenerationSettings { Seed = snapshot.Seed, StartTime = snapshot.StartTime, Duration = snapshot.Duration, Step = snapshot.Step, MinValue = snapshot.MinValue, MaxValue = snapshot.MaxValue }, binding);
                return true;
            }
            if (!string.IsNullOrEmpty(snapshot.TransformPath))
            {
                motion = PhaseEGenerators.CreateRandomRotation(generationId, new RandomRotationGenerationSettings { Seed = snapshot.Seed, StartTime = snapshot.StartTime, Duration = snapshot.Duration, Step = snapshot.Step, MinEuler = snapshot.MinEuler, MaxEuler = snapshot.MaxEuler, TransformPath = snapshot.TransformPath });
                return true;
            }
            return false;
        }

        internal static string NormalizeLegacyBuiltinPresetId(string serialized)
        {
            if (string.IsNullOrEmpty(serialized)) return serialized;
            var snapshot = JsonUtility.FromJson<GenerationSettingsSnapshot>(serialized);
            if (snapshot == null) return serialized;
            string normalizedId = FaceMotionBuiltins.NormalizeId(snapshot.SourcePresetId);
            if (normalizedId == snapshot.SourcePresetId) return serialized;
            snapshot.SourcePresetId = normalizedId;
            return snapshot.Serialize();
        }
    }

    [Serializable]
    public sealed class PresetBindingSnapshot
    {
        public string LogicalTargetId;
        public string RendererPath;
        public string BlendShapeName;
    }

    /// <summary>Recreates pure motion from persisted provenance without touching a project or scene.</summary>
    public static class GeneratedMotionRegenerator
    {
        public static bool TryCreate(GenerationRecord record, string generationId, out GeneratedMotion motion)
        {
            motion = null;
            return record != null && GenerationSettingsSnapshot.TryRegenerate(record.SettingsSnapshot, generationId, out motion);
        }
    }
}
