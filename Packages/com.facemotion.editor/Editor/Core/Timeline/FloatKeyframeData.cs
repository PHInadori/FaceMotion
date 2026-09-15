using System;
using System.Collections.Generic;
using FaceMotion.Data;
using UnityEngine;

namespace FaceMotion.Timeline
{
    /// <summary>
    /// One blend shape key. Interpolation applies to the segment leaving this key toward
    /// the next key; the final key's interpolation is not used by evaluation.
    /// </summary>
    [Serializable]
    public sealed class FloatKeyframeData
    {
        [SerializeField] private string _keyId;
        [SerializeField] private float _time;
        [SerializeField] private float _value;
        [SerializeField] private InterpolationType _interpolation;
        [SerializeField] private KeyOrigin _origin;

        public string KeyId => _keyId;

        public float Time
        {
            get => _time;
            set => _time = value;
        }

        public float Value
        {
            get => _value;
            set => _value = value;
        }

        public InterpolationType Interpolation
        {
            get => _interpolation;
            set => _interpolation = value;
        }

        public KeyOrigin Origin => _origin;

        /// <summary>Creates a manual key with a new ID.</summary>
        public static FloatKeyframeData Create(
            float time,
            float value,
            InterpolationType interpolation = InterpolationType.Linear)
        {
            return Create(time, value, interpolation, KeyOrigin.Manual);
        }

        /// <summary>Creates a key with an explicit origin and a new ID.</summary>
        public static FloatKeyframeData Create(
            float time,
            float value,
            InterpolationType interpolation,
            KeyOrigin origin)
        {
            var key = new FloatKeyframeData
            {
                _keyId = StableId.New(),
                _time = time,
                _value = value,
                _interpolation = interpolation,
                _origin = origin
            };
            return key;
        }

        /// <summary>Deep copy preserving the key ID and origin.</summary>
        public FloatKeyframeData Clone()
        {
            var clone = new FloatKeyframeData
            {
                _keyId = _keyId,
                _time = _time,
                _value = _value,
                _interpolation = _interpolation,
                _origin = _origin
            };
            return clone;
        }

        /// <summary>
        /// User-facing single-key duplicate: new key ID, same authored values, Manual
        /// origin. A duplicate is not fresh generator output.
        /// </summary>
        public FloatKeyframeData Duplicate()
        {
            return Duplicate(null);
        }

        internal void SetKeyIdForMigration(string id)
        {
            _keyId = id ?? string.Empty;
        }

        /// <summary>
        /// Duplicates with provenance remapping. When an animation is duplicated, generated
        /// keys keep their generated origin and are re-bound to a fresh generation id via
        /// <paramref name="generationIdRemap"/>; manual keys stay manual. Without a remap
        /// (single track duplicate), generated origins become Manual so the copy never
        /// shares a generation id with the original.
        /// </summary>
        internal FloatKeyframeData Duplicate(IReadOnlyDictionary<string, string> generationIdRemap)
        {
            var origin = KeyOrigin.Manual;
            if (_origin.IsGenerated
                && generationIdRemap != null
                && generationIdRemap.TryGetValue(_origin.GenerationId, out var remappedId))
            {
                origin = new KeyOrigin(_origin.Kind, remappedId);
            }

            var duplicate = new FloatKeyframeData
            {
                _keyId = StableId.New(),
                _time = _time,
                _value = _value,
                _interpolation = _interpolation,
                _origin = origin
            };
            return duplicate;
        }
    }
}