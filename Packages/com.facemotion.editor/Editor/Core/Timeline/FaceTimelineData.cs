using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Timeline
{
    /// <summary>An ordered timeline of tracks for one animation.</summary>
    [Serializable]
    public sealed class FaceTimelineData
    {
        [SerializeField] private float _duration = 1f;
        [SerializeField] private float _frameRate = 60f;
        [SerializeField] private bool _loop;
        [SerializeField] private List<FaceTrackData> _tracks = new List<FaceTrackData>();

        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        public float FrameRate
        {
            get => _frameRate;
            set => _frameRate = value;
        }

        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        public IReadOnlyList<FaceTrackData> Tracks => _tracks;

        /// <summary>Default timeline: 1s at 60 fps, no loop.</summary>
        public static FaceTimelineData CreateDefault()
        {
            return new FaceTimelineData();
        }

        public void AddTrack(FaceTrackData track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            _tracks.Add(track);
        }

        public bool RemoveTrack(string trackId)
        {
            int index = FindTrackIndex(trackId);
            if (index < 0)
            {
                return false;
            }

            _tracks.RemoveAt(index);
            return true;
        }

        public bool TryGetTrack(string trackId, out FaceTrackData track)
        {
            int index = FindTrackIndex(trackId);
            track = index >= 0 ? _tracks[index] : null;
            return index >= 0 && track != null;
        }

        /// <summary>Deep copy preserving track and key IDs.</summary>
        public FaceTimelineData Clone()
        {
            var clone = new FaceTimelineData
            {
                _duration = _duration,
                _frameRate = _frameRate,
                _loop = _loop,
                _tracks = new List<FaceTrackData>(_tracks.Count)
            };
            for (int i = 0; i < _tracks.Count; i++)
            {
                clone._tracks.Add(_tracks[i] == null ? null : _tracks[i].Clone());
            }

            return clone;
        }

        /// <summary>User-facing duplicate with new track and key IDs.</summary>
        public FaceTimelineData Duplicate()
        {
            return Duplicate(null);
        }

        /// <summary>
        /// Duplicates every track with provenance remapping. See
        /// <see cref="FloatKeyframeData.Duplicate"/> for the remap semantics.
        /// </summary>
        internal FaceTimelineData Duplicate(IReadOnlyDictionary<string, string> generationIdRemap)
        {
            var duplicate = new FaceTimelineData
            {
                _duration = _duration,
                _frameRate = _frameRate,
                _loop = _loop,
                _tracks = new List<FaceTrackData>(_tracks.Count)
            };
            for (int i = 0; i < _tracks.Count; i++)
            {
                duplicate._tracks.Add(_tracks[i] == null ? null : _tracks[i].Duplicate(generationIdRemap));
            }

            return duplicate;
        }

        internal void NormalizeStructure()
        {
            if (_tracks == null)
            {
                _tracks = new List<FaceTrackData>();
            }

            RemoveNullTracks(_tracks);

            for (int i = 0; i < _tracks.Count; i++)
            {
                _tracks[i].NormalizeStructure();
            }
        }

        private void RemoveNullTracks(List<FaceTrackData> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private int FindTrackIndex(string trackId)
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i] != null
                    && string.Equals(_tracks[i].TrackId, trackId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}