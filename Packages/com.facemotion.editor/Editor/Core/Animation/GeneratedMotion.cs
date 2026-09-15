using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// Generator-independent intermediate motion model for one generated pass. This is a
    /// pure data container: it has no reference to a timeline, a project, scenes, or any
    /// Unity object, so it can be previewed, inspected, or applied through a merge policy
    /// without the generator knowing how timelines work.
    /// </summary>
    public sealed class GeneratedMotion
    {
        public GeneratedMotion(string generationId, int algorithmVersion)
        {
            GenerationId = generationId;
            AlgorithmVersion = algorithmVersion;
            Tracks = new List<GeneratedTrackMotion>();
        }

        public string GenerationId { get; }

        public int AlgorithmVersion { get; }

        public List<GeneratedTrackMotion> Tracks { get; }
    }

    /// <summary>One generated key on a blend shape track motion.</summary>
    public sealed class GeneratedFloatKey
    {
        public GeneratedFloatKey(float time, float value, InterpolationType interpolation = InterpolationType.Linear)
        {
            Time = time;
            Value = value;
            Interpolation = interpolation;
        }

        public float Time { get; set; }

        public float Value { get; set; }

        public InterpolationType Interpolation { get; set; }
    }

    /// <summary>One generated key on a position, rotation, or scale track motion.</summary>
    public sealed class GeneratedVector3Key
    {
        public GeneratedVector3Key(float time, Vector3 value, InterpolationType interpolation = InterpolationType.Linear)
        {
            Time = time;
            Value = value;
            Interpolation = interpolation;
        }

        public float Time { get; set; }

        public Vector3 Value { get; set; }

        public InterpolationType Interpolation { get; set; }
    }

    /// <summary>
    /// One generated track target. The value kind is enforced by the track kind: a blend
    /// shape track carries float keys only, a transform track carries vector3 keys only.
    /// Exactly one binding is set to match the track kind.
    /// </summary>
    public sealed class GeneratedTrackMotion
    {
        private readonly BlendShapeBinding? _blendShape;
        private readonly TransformBinding? _transform;

        private GeneratedTrackMotion(TrackKind kind, BlendShapeBinding? blendShape, TransformBinding? transform)
        {
            Kind = kind;
            _blendShape = blendShape;
            _transform = transform;
            FloatKeys = new List<GeneratedFloatKey>();
            Vector3Keys = new List<GeneratedVector3Key>();
        }

        public TrackKind Kind { get; }

        public BlendShapeBinding? BlendShape => _blendShape;

        public TransformBinding? Transform => _transform;

        /// <summary>Keys for blend shape tracks. Empty for transform tracks.</summary>
        public List<GeneratedFloatKey> FloatKeys { get; }

        /// <summary>Keys for position, rotation, and scale tracks. Empty for blend shape tracks.</summary>
        public List<GeneratedVector3Key> Vector3Keys { get; }

        public static GeneratedTrackMotion CreateBlendShape(BlendShapeBinding binding)
        {
            return new GeneratedTrackMotion(TrackKind.BlendShape, binding, null);
        }

        public static GeneratedTrackMotion CreateTransform(TransformBinding binding, TrackKind kind)
        {
            if (!TrackKinds.IsTransform(kind))
            {
                throw new ArgumentException("A transform track kind is required.", nameof(kind));
            }

            return new GeneratedTrackMotion(kind, null, binding);
        }

        /// <summary>Constructs the matching project track payload for this generated target.</summary>
        public bool TryCreateTrackPayload(out BlendShapeTrackPayload blendShape, out TransformTrackPayload transform)
        {
            blendShape = null;
            transform = null;
            if (Kind == TrackKind.BlendShape)
            {
                blendShape = BlendShapeTrackPayload.Create(_blendShape.Value.RendererPath, _blendShape.Value.BlendShapeName);
                return true;
            }

            if (TrackKinds.IsTransform(Kind))
            {
                transform = TransformTrackPayload.Create(_transform.Value.TransformPath, MotionRotationMode.ShortestQuaternion);
                return true;
            }

            return false;
        }
    }
}