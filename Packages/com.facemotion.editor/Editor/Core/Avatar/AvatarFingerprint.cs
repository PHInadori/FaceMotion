using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FaceMotion.Avatar
{
    /// <summary>
    /// Serialized fingerprint of an avatar structure. It is independent of instance IDs,
    /// scene paths, and the avatar display name, so the same avatar structure produces the
    /// same fingerprint across editor restarts and between copies of a hierarchy.
    /// </summary>
    [Serializable]
    public sealed class AvatarFingerprint
    {
        public const int HashLength = 64;

        [SerializeField] private int _algorithmVersion;
        [SerializeField] private int _formatVersion;
        [SerializeField] private string _hash;

        internal AvatarFingerprint()
        {
        }

        public int AlgorithmVersion => _algorithmVersion;

        public int FormatVersion => _formatVersion;

        public string Hash => _hash;

        public static AvatarFingerprint Create(int algorithmVersion, int formatVersion, string hash)
        {
            if (hash == null || hash.Length != HashLength || !IsLowerHex(hash))
            {
                throw new ArgumentException("The fingerprint hash must be 64 lowercase hex characters.", nameof(hash));
            }

            return new AvatarFingerprint
            {
                _algorithmVersion = algorithmVersion,
                _formatVersion = formatVersion,
                _hash = hash
            };
        }

        public bool IsValid => _hash != null && _hash.Length == HashLength && IsLowerHex(_hash);

        public bool EqualsValue(AvatarFingerprint other)
        {
            return other != null
                && _algorithmVersion == other._algorithmVersion
                && _formatVersion == other._formatVersion
                && string.Equals(_hash, other._hash, StringComparison.Ordinal);
        }

        public AvatarFingerprint Clone()
        {
            return new AvatarFingerprint
            {
                _algorithmVersion = _algorithmVersion,
                _formatVersion = _formatVersion,
                _hash = _hash
            };
        }

        public override string ToString()
        {
            return $"v{_algorithmVersion}.{_formatVersion}:{_hash}";
        }

        private static bool IsLowerHex(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isLowerHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isLowerHex)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Deterministic fingerprint builder. The canonical input is a newline-joined, ordinally
    /// sorted list of UTF-8 lines of the shapes <c>T|&lt;transformPath&gt;</c>,
    /// <c>R|&lt;rendererPath&gt;</c>, and <c>B|&lt;rendererPath&gt;|&lt;blendShapeName&gt;</c>.
    /// The result is the lowercase hex SHA-256 of those bytes. Weights, materials, instance
    /// IDs, and root names never participate.
    /// </summary>
    public static class AvatarFingerprintBuilder
    {
        public const int AlgorithmVersion = 1;
        public const int FormatVersion = 1;

        public static AvatarFingerprint Compute(
            IReadOnlyList<TransformIndexEntry> transforms,
            IReadOnlyList<RendererIndexEntry> renderers,
            IReadOnlyList<BlendShapeIndexEntry> blendShapes)
        {
            return AvatarFingerprint.Create(
                AlgorithmVersion,
                FormatVersion,
                ComputeHash(transforms, renderers, blendShapes));
        }

        public static AvatarFingerprint Compute(AvatarIndex index)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            return Compute(index.Transforms, index.Renderers, index.BlendShapes);
        }

        public static string ComputeHash(
            IReadOnlyList<TransformIndexEntry> transforms,
            IReadOnlyList<RendererIndexEntry> renderers,
            IReadOnlyList<BlendShapeIndexEntry> blendShapes)
        {
            var lines = new List<string>();
            if (transforms != null)
            {
                for (int i = 0; i < transforms.Count; i++)
                {
                    if (transforms[i] != null)
                    {
                        lines.Add("T|" + transforms[i].RelativePath);
                    }
                }
            }

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] != null)
                    {
                        lines.Add("R|" + renderers[i].RelativePath);
                    }
                }
            }

            if (blendShapes != null)
            {
                for (int i = 0; i < blendShapes.Count; i++)
                {
                    if (blendShapes[i] != null)
                    {
                        lines.Add("B|" + blendShapes[i].RendererPath + "|" + blendShapes[i].BlendShapeName);
                    }
                }
            }

            lines.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder(lines.Count * 48);
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(lines[i]);
            }

            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return ToLowerHex(bytes);
            }
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string hex = "0123456789abcdef";
            var chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 0x0F];
            }

            return new string(chars);
        }
    }
}