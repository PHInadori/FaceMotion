using System;
using System.Collections.Generic;
using FaceMotion.Data;
using UnityEngine;

namespace FaceMotion.Timeline
{
    /// <summary>
    /// Composition envelope for one timeline track. Holds common metadata plus explicit
    /// blend shape and transform payload fields; exactly one payload is active per kind.
    /// </summary>
    [Serializable]
    public sealed class FaceTrackData
    {
        [SerializeField] private string _trackId;
        [SerializeField] private TrackKind _kind;
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _displayOrder;
        [SerializeField] private BlendShapeTrackPayload _blendShape;
        [SerializeField] private TransformTrackPayload _transform;

        public string TrackId => _trackId;

        public TrackKind Kind => _kind;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public int DisplayOrder
        {
            get => _displayOrder;
            set => _displayOrder = value;
        }

        public BlendShapeTrackPayload BlendShape => _blendShape;

        public TransformTrackPayload Transform => _transform;

        public static FaceTrackData CreateBlendShape(string rendererPath, string blendShapeName, bool enabled = true)
        {
            var track = new FaceTrackData
            {
                _trackId = StableId.New(),
                _kind = TrackKind.BlendShape,
                _enabled = enabled,
                _blendShape = BlendShapeTrackPayload.Create(rendererPath, blendShapeName)
            };
            return track;
        }

        public static FaceTrackData CreateTransform(
            TrackKind kind,
            string transformPath,
            bool enabled = true,
            MotionRotationMode rotationMode = MotionRotationMode.ShortestQuaternion)
        {
            if (!TrackKinds.IsTransform(kind))
            {
                throw new ArgumentException("A transform track kind is required.", nameof(kind));
            }

            var track = new FaceTrackData
            {
                _trackId = StableId.New(),
                _kind = kind,
                _enabled = enabled,
                _transform = TransformTrackPayload.Create(transformPath, rotationMode)
            };
            return track;
        }

        /// <summary>Deep copy preserving this track and key IDs.</summary>
        public FaceTrackData Clone()
        {
            var clone = new FaceTrackData
            {
                _trackId = _trackId,
                _kind = _kind,
                _enabled = _enabled,
                _displayOrder = _displayOrder,
                _blendShape = _blendShape == null ? null : _blendShape.Clone(),
                _transform = _transform == null ? null : _transform.Clone()
            };
            return clone;
        }

        /// <summary>User-facing duplicate with a new track ID and duplicated keys.</summary>
        public FaceTrackData Duplicate()
        {
            return Duplicate(null);
        }

        /// <summary>
        /// Duplicates the track with provenance remapping. See
        /// <see cref="FloatKeyframeData.Duplicate"/> for the remap semantics: a single
        /// track duplicate (null remap) converts generated origins to Manual; an animation
        /// duplicate passes the generation-id remap created by the project.
        /// </summary>
        internal FaceTrackData Duplicate(IReadOnlyDictionary<string, string> generationIdRemap)
        {
            var duplicate = new FaceTrackData
            {
                _trackId = StableId.New(),
                _kind = _kind,
                _enabled = _enabled,
                _displayOrder = _displayOrder,
                _blendShape = _blendShape == null ? null : _blendShape.Duplicate(generationIdRemap),
                _transform = _transform == null ? null : _transform.Duplicate(generationIdRemap)
            };
            return duplicate;
        }

        internal void SetTrackIdForMigration(string id)
        {
            _trackId = id ?? string.Empty;
        }

        internal void NormalizeStructure()
        {
            if (_kind == TrackKind.BlendShape)
            {
                if (_blendShape == null)
                {
                    _blendShape = BlendShapeTrackPayload.Create(string.Empty, string.Empty);
                }

                _blendShape.NormalizeStructure();
            }
            else if (TrackKinds.IsTransform(_kind))
            {
                if (_transform == null)
                {
                    _transform = TransformTrackPayload.Create(string.Empty, MotionRotationMode.ShortestQuaternion);
                }

                _transform.NormalizeStructure();
            }
        }
    }
}