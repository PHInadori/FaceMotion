using System;

namespace FaceMotion.Data
{
    /// <summary>
    /// SDK-neutral blend shape binding DTO. The renderer index is never persistent
    /// identity; only the relative renderer path and the blend shape name bind. The hint
    /// fields are non-authoritative assistance for diagnostics and for the future
    /// keyword-based resolver; they never participate in identity or equality.
    /// </summary>
    public readonly struct BlendShapeBinding : IEquatable<BlendShapeBinding>
    {
        public readonly string RendererPath;

        public readonly string BlendShapeName;

        public readonly string RendererNameHint;

        public readonly string MeshNameHint;

        public BlendShapeBinding(string rendererPath, string blendShapeName)
            : this(rendererPath, blendShapeName, null, null)
        {
        }

        public BlendShapeBinding(string rendererPath, string blendShapeName, string rendererNameHint, string meshNameHint)
        {
            RendererPath = rendererPath ?? string.Empty;
            BlendShapeName = blendShapeName ?? string.Empty;
            RendererNameHint = rendererNameHint ?? string.Empty;
            MeshNameHint = meshNameHint ?? string.Empty;
        }

        public bool IsValid => RendererPath.Length > 0 && BlendShapeName.Length > 0;

        public BlendShapeBinding WithHints(string rendererNameHint, string meshNameHint)
        {
            return new BlendShapeBinding(RendererPath, BlendShapeName, rendererNameHint, meshNameHint);
        }

        public bool Equals(BlendShapeBinding other)
        {
            return string.Equals(RendererPath, other.RendererPath, StringComparison.Ordinal)
                && string.Equals(BlendShapeName, other.BlendShapeName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BlendShapeBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(RendererPath) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(BlendShapeName);
            }
        }

        public override string ToString()
        {
            return $"{RendererPath}:{BlendShapeName}";
        }
    }

    /// <summary>SDK-neutral transform binding DTO (relative path only).</summary>
    public readonly struct TransformBinding : IEquatable<TransformBinding>
    {
        public readonly string TransformPath;

        public readonly string TransformNameHint;

        public TransformBinding(string transformPath)
            : this(transformPath, null)
        {
        }

        public TransformBinding(string transformPath, string transformNameHint)
        {
            TransformPath = transformPath ?? string.Empty;
            TransformNameHint = transformNameHint ?? string.Empty;
        }

        public bool IsValid => TransformPath.Length > 0;

        public TransformBinding WithHint(string transformNameHint)
        {
            return new TransformBinding(TransformPath, transformNameHint);
        }

        public bool Equals(TransformBinding other)
        {
            return string.Equals(TransformPath, other.TransformPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TransformBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(TransformPath);
        }

        public override string ToString()
        {
            return TransformPath;
        }
    }
}