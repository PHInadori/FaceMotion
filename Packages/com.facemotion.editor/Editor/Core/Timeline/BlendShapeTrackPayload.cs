using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Timeline
{
    /// <summary>
    /// Blend shape track payload: renderer path and blend shape name bindings plus float
    /// keys. The blendshape index is never persistent identity.
    /// </summary>
    [Serializable]
    public sealed class BlendShapeTrackPayload
    {
        [SerializeField] private string _rendererPath;
        [SerializeField] private string _blendShapeName;
        [SerializeField] private List<FloatKeyframeData> _keys = new List<FloatKeyframeData>();

        public string RendererPath
        {
            get => _rendererPath;
            set => _rendererPath = value;
        }

        public string BlendShapeName
        {
            get => _blendShapeName;
            set => _blendShapeName = value;
        }

        public IReadOnlyList<FloatKeyframeData> Keys => _keys;

        public static BlendShapeTrackPayload Create(string rendererPath, string blendShapeName)
        {
            var payload = new BlendShapeTrackPayload
            {
                _rendererPath = rendererPath ?? string.Empty,
                _blendShapeName = blendShapeName ?? string.Empty,
                _keys = new List<FloatKeyframeData>()
            };
            return payload;
        }

        public void AddKey(FloatKeyframeData key)
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
        public BlendShapeTrackPayload Clone()
        {
            var clone = new BlendShapeTrackPayload
            {
                _rendererPath = _rendererPath,
                _blendShapeName = _blendShapeName,
                _keys = new List<FloatKeyframeData>(_keys.Count)
            };
            for (int i = 0; i < _keys.Count; i++)
            {
                clone._keys.Add(_keys[i] == null ? null : _keys[i].Clone());
            }

            return clone;
        }

        /// <summary>User-facing duplicate with new key IDs; generated origins become Manual.</summary>
        public BlendShapeTrackPayload Duplicate()
        {
            return Duplicate(null);
        }

        /// <summary>
        /// Duplicates every key with provenance remapping. See
        /// <see cref="FloatKeyframeData.Duplicate"/> for the remap semantics.
        /// </summary>
        internal BlendShapeTrackPayload Duplicate(IReadOnlyDictionary<string, string> generationIdRemap)
        {
            var duplicate = new BlendShapeTrackPayload
            {
                _rendererPath = _rendererPath,
                _blendShapeName = _blendShapeName,
                _keys = new List<FloatKeyframeData>(_keys.Count)
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
                _keys = new List<FloatKeyframeData>();
            }

            RemoveNulls(_keys);
            StableSortByTime(_keys);
        }

        internal void SortKeys()
        {
            if (_keys == null)
            {
                _keys = new List<FloatKeyframeData>();
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

        internal static void RemoveNulls(List<FloatKeyframeData> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                }
            }
        }

        internal static void StableSortByTime(List<FloatKeyframeData> keys)
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