using System;
using UnityEngine;

namespace FaceMotion.Timeline
{
    /// <summary>
    /// Authoring source of a key. Values are serialized and stable; new kinds are additive.
    /// </summary>
    public enum OriginKind
    {
        Manual = 0,
        Preset = 1,
        Blink = 2,
        Random = 3,
        Imported = 4
    }

    /// <summary>
    /// Minimal per-key provenance. Full generated metadata lives in the project-level
    /// generation registry; a manual key carries an empty generation ID.
    /// </summary>
    [Serializable]
    public struct KeyOrigin : IEquatable<KeyOrigin>
    {
        [SerializeField] private OriginKind _kind;
        [SerializeField] private string _generationId;

        public KeyOrigin(OriginKind kind, string generationId)
        {
            _kind = kind;
            _generationId = generationId;
        }

        public static KeyOrigin Manual => new KeyOrigin(OriginKind.Manual, null);

        public OriginKind Kind => _kind;

        public string GenerationId => _generationId;

        public bool IsGenerated => _kind != OriginKind.Manual;

        /// <summary>True when generated but no generation ID is recorded.</summary>
        public bool IsOrphaned => IsGenerated && string.IsNullOrEmpty(_generationId);

        public bool Equals(KeyOrigin other)
        {
            return _kind == other._kind
                && string.Equals(_generationId, other._generationId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is KeyOrigin other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((int)_kind * 397);
                if (_generationId != null)
                {
                    hash ^= StringComparer.Ordinal.GetHashCode(_generationId);
                }

                return hash;
            }
        }

        public override string ToString()
        {
            return $"{_kind}:{(_generationId ?? string.Empty)}";
        }
    }
}