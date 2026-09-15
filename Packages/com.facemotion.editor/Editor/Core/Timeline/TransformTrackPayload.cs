using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Timeline
{
    /// <summary>
    /// Transform track payload: relative transform path plus vector3 keys. Position and
    /// scale keys store world/local components; rotation keys store Euler angles as-is.
    /// Euler angles are never normalized to (0..360), and 360-degree spin is unsupported.
    /// </summary>
    [Serializable]
    public sealed class TransformTrackPayload
    {
        [SerializeField] private string _transformPath;
        [SerializeField] private MotionRotationMode _rotationMode = MotionRotationMode.ShortestQuaternion;
        [SerializeField] private List<Vector3KeyframeData> _keys = new List<Vector3KeyframeData>();

        public string TransformPath
        {
            get => _transformPath;
            set => _transformPath = value;
        }

        public MotionRotationMode RotationMode
        {
            get => _rotationMode;
            set => _rotationMode = value;
        }

        public IReadOnlyList<Vector3KeyframeData> Keys => _keys;

        public static TransformTrackPayload Create(string transformPath, MotionRotationMode rotationMode = MotionRotationMode.ShortestQuaternion)
        {
            var payload = new TransformTrackPayload
            {
                _transformPath = transformPath ?? string.Empty,
                _rotationMode = rotationMode,
                _keys = new List<Vector3KeyframeData>()
            };
            return payload;
        }

        public void AddKey(Vector3KeyframeData key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _keys.Add(key);
        }

        public bool RemoveKey(string keyId)
        {
            int index = FindKeyIndex(keyId);
            if (index < 0)
            {
                return false;
            }

            _keys.RemoveAt(index);
            return true;
        }

        /// <summary>Deep copy preserving every key ID.</summary>
        public TransformTrackPayload Clone()
        {
            var clone = new TransformTrackPayload
            {
                _transformPath = _transformPath,
                _rotationMode = _rotationMode,
                _keys = new List<Vector3KeyframeData>(_keys.Count)
            };
            for (int i = 0; i < _keys.Count; i++)
            {
                clone._keys.Add(_keys[i] == null ? null : _keys[i].Clone());
            }

            return clone;
        }

        /// <summary>User-facing duplicate with new key IDs; generated origins become Manual.</summary>
        public TransformTrackPayload Duplicate()
        {
            return Duplicate(null);
        }

        /// <summary>
        /// Duplicates every key with provenance remapping. See
        /// <see cref="Vector3KeyframeData.Duplicate"/> for the remap semantics.
        /// </summary>
        internal TransformTrackPayload Duplicate(IReadOnlyDictionary<string, string> generationIdRemap)
        {
            var duplicate = new TransformTrackPayload
            {
                _transformPath = _transformPath,
                _rotationMode = _rotationMode,
                _keys = new List<Vector3KeyframeData>(_keys.Count)
            };
            for (int i = 0; i < _keys.Count; i++)
            {
                duplicate._keys.Add(_keys[i] == null ? null : _keys[i].Duplicate(generationIdRemap));
            }

            return duplicate;
        }

        internal void NormalizeStructure()
        {
            if (_keys == null)
            {
                _keys = new List<Vector3KeyframeData>();
            }

            RemoveNulls(_keys);
            StableSortByTime(_keys);
        }

        internal void SortKeys()
        {
            if (_keys == null)
            {
                _keys = new List<Vector3KeyframeData>();
            }

            StableSortByTime(_keys);
        }

        private int FindKeyIndex(string keyId)
        {
            for (int i = 0; i < _keys.Count; i++)
            {
                if (_keys[i] != null
                    && string.Equals(_keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        internal static void RemoveNulls(List<Vector3KeyframeData> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                }
            }
        }

        internal static void StableSortByTime(List<Vector3KeyframeData> keys)
        {
            if (keys.Count < 2)
            {
                return;
            }

            for (int i = 1; i < keys.Count; i++)
            {
                var key = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j].Time > key.Time)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }

                keys[j + 1] = key;
            }
        }
    }
}