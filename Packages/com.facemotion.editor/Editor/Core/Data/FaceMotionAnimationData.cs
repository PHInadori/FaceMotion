using System;
using System.Collections.Generic;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Data
{
    /// <summary>One authored animation inside a project.</summary>
    [Serializable]
    public sealed class FaceMotionAnimationData
    {
        [SerializeField] private string _animationId;
        [SerializeField] private string _displayName;
        [SerializeField] private FaceTimelineData _timeline;

        public string AnimationId => _animationId;

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public FaceTimelineData Timeline => _timeline;

        /// <summary>Creates an animation with a fresh ID and a default timeline.</summary>
        public static FaceMotionAnimationData Create(string displayName)
        {
            var animation = new FaceMotionAnimationData
            {
                _animationId = StableId.New(),
                _displayName = displayName ?? string.Empty,
                _timeline = FaceTimelineData.CreateDefault()
            };
            return animation;
        }

        /// <summary>Deep copy preserving animation, track, and key IDs.</summary>
        public FaceMotionAnimationData Clone()
        {
            var clone = new FaceMotionAnimationData
            {
                _animationId = _animationId,
                _displayName = _displayName,
                _timeline = _timeline == null ? null : _timeline.Clone()
            };
            return clone;
        }

        /// <summary>
        /// User-facing duplicate with fresh animation, track, and key IDs. Generated keys
        /// stay generated and are re-bound to the fresh generation records produced by
        /// <see cref="FaceMotionProject.DuplicateAnimation"/>; a key whose generation has no
        /// mapping (defensive fallback) is converted to Manual so the copies never share a
        /// generation id with the source animation.
        /// </summary>
        internal FaceMotionAnimationData Duplicate(IReadOnlyDictionary<string, string> generationIdRemap)
        {
            var duplicate = new FaceMotionAnimationData
            {
                _animationId = StableId.New(),
                _displayName = _displayName,
                _timeline = _timeline == null ? null : _timeline.Duplicate(generationIdRemap)
            };
            return duplicate;
        }

        internal void SetAnimationIdForMigration(string id)
        {
            _animationId = id ?? string.Empty;
        }

        internal void NormalizeStructure()
        {
            if (_timeline == null)
            {
                _timeline = FaceTimelineData.CreateDefault();
            }

            _timeline.NormalizeStructure();
        }
    }
}