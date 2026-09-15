using System;
using FaceMotion.Generation;
using UnityEngine;

namespace FaceMotion.Data
{
    /// <summary>
    /// One entry of the project-level generation registry. Full provenance metadata is
    /// stored once here instead of on every key.
    /// </summary>
    [Serializable]
    public sealed class GenerationRecord
    {
        [SerializeField] private string _generationId;
        [SerializeField] private string _animationId;
        [SerializeField] private GeneratorType _generatorType;
        [SerializeField] private string _sourcePresetId;
        [SerializeField] private int _algorithmVersion;
        [SerializeField] private string _settingsHash;
        [SerializeField] private string _settingsSnapshot;

        public string GenerationId => _generationId;

        public string AnimationId => _animationId;

        public GeneratorType GeneratorType => _generatorType;

        public string SourcePresetId => _sourcePresetId;

        public int AlgorithmVersion => _algorithmVersion;

        public string SettingsHash => _settingsHash;

        /// <summary>Canonical serialized settings sufficient to reproduce this generated pass.</summary>
        public string SettingsSnapshot => _settingsSnapshot;

        /// <summary>Creates a record with a fresh generation ID.</summary>
        public static GenerationRecord Create(
            string animationId,
            GeneratorType generatorType,
            string sourcePresetId,
            int algorithmVersion,
            string settingsHash,
            string generationId = null,
            string settingsSnapshot = null)
        {
            var record = new GenerationRecord
            {
                _generationId = generationId ?? StableId.New(),
                _animationId = animationId ?? string.Empty,
                _generatorType = generatorType,
                _sourcePresetId = sourcePresetId ?? string.Empty,
                _algorithmVersion = algorithmVersion,
                _settingsHash = settingsHash ?? string.Empty,
                _settingsSnapshot = settingsSnapshot ?? string.Empty
            };
            return record;
        }

        /// <summary>Deep copy preserving every field, including the generation ID.</summary>
        public GenerationRecord Clone()
        {
            return new GenerationRecord
            {
                _generationId = _generationId,
                _animationId = _animationId,
                _generatorType = _generatorType,
                _sourcePresetId = _sourcePresetId,
                _algorithmVersion = _algorithmVersion,
                _settingsHash = _settingsHash,
                _settingsSnapshot = _settingsSnapshot
            };
        }

        /// <summary>
        /// Creates an independent record for a duplicated animation: a fresh generation ID
        /// while every authored field (generator type, preset, algorithm version, settings
        /// hash and snapshot) is preserved. The animation id is attached afterwards, once
        /// the duplicate animation id is known; see SetAnimationIdForDuplicate.
        /// </summary>
        public static GenerationRecord CreateDuplicate(GenerationRecord source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new GenerationRecord
            {
                _generationId = StableId.New(),
                _animationId = string.Empty,
                _generatorType = source._generatorType,
                _sourcePresetId = source._sourcePresetId,
                _algorithmVersion = source._algorithmVersion,
                _settingsHash = source._settingsHash,
                _settingsSnapshot = source._settingsSnapshot
            };
        }

        internal void SetAnimationIdForDuplicate(string animationId)
        {
            _animationId = animationId ?? string.Empty;
        }

        internal void SetGenerationIdForMigration(string id)
        {
            _generationId = id ?? string.Empty;
        }

        internal void NormalizeBuiltinPresetIds()
        {
            _sourcePresetId = FaceMotionBuiltins.NormalizeId(_sourcePresetId);
            _settingsSnapshot = GenerationSettingsSnapshot.NormalizeLegacyBuiltinPresetId(_settingsSnapshot);
        }
    }
}
