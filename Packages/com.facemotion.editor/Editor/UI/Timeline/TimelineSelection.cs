using System;
using System.Collections.Generic;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>
    /// Timeline key selection. Identity is always a stable key ID; list indices are never
    /// selection identity. A selection is animation-scoped and is pruned by the session.
    /// </summary>
    public sealed class TimelineSelection
    {
        private readonly HashSet<string> _keyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        private string _primaryKeyId;

        public int Count => _keyIds.Count;

        /// <summary>
        /// Stable anchor key for range gestures. It identifies which key was the range start
        /// (the first key of a shift gesture). It is state only: the shift-range computation
        /// lives in the input/controller layer and the result is passed to SetSelection.
        /// </summary>
        public string PrimaryKeyId => _primaryKeyId;

        /// <summary>Sets the range anchor. The anchor must already be part of the selection;
        /// an unknown id leaves the anchor unchanged.</summary>
        public void SetPrimaryKeyId(string keyId)
        {
            if (keyId != null && _keyIds.Contains(keyId))
            {
                _primaryKeyId = keyId;
            }
        }

        public IReadOnlyList<string> KeyIds => _order;

        public bool Contains(string keyId)
        {
            return keyId != null && _keyIds.Contains(keyId);
        }

        public void Clear()
        {
            _keyIds.Clear();
            _order.Clear();
            _primaryKeyId = null;
        }

        public void Select(string keyId)
        {
            if (string.IsNullOrEmpty(keyId) || !_keyIds.Add(keyId))
            {
                return;
            }

            _order.Add(keyId);
        }

        public void Toggle(string keyId)
        {
            if (_keyIds.Contains(keyId))
            {
                Deselect(keyId);
            }
            else
            {
                Select(keyId);
            }
        }

        public void Deselect(string keyId)
        {
            if (_keyIds.Remove(keyId))
            {
                _order.Remove(keyId);
            }
        }

        public void SetSelection(IEnumerable<string> keyIds)
        {
            Clear();
            if (keyIds == null)
            {
                return;
            }

            foreach (string keyId in keyIds)
            {
                Select(keyId);
            }
        }

        public void SetSingle(string keyId)
        {
            Clear();
            Select(keyId);
        }

        /// <summary>Removes ids that no longer exist; returns true when anything was removed.</summary>
        public bool Prune(Func<string, bool> exists)
        {
            bool changed = false;
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                if (!exists(_order[i]))
                {
                    _keyIds.Remove(_order[i]);
                    _order.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }
    }
}