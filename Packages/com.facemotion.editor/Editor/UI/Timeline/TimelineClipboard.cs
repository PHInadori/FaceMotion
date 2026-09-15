using System.Collections.Generic;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>
    /// Editor-only clipboard for timeline keys. Stores relative times so a group can be
    /// pasted anchored at the current time. Never persisted into a project.
    /// </summary>
    public sealed class TimelineClipboard
    {
        public sealed class ClipboardItem
        {
            public string TrackId;
            public TrackKind Kind;
            public float RelativeTime;
            public float FloatValue;
            public Vector3 VectorValue;
            public InterpolationType Interpolation;
        }

        private readonly List<ClipboardItem> _items = new List<ClipboardItem>();

        public IReadOnlyList<ClipboardItem> Items => _items;

        public bool HasItems => _items.Count > 0;

        public int Count => _items.Count;

        public void Set(IEnumerable<ClipboardItem> items)
        {
            _items.Clear();
            if (items == null)
            {
                return;
            }

            foreach (ClipboardItem item in items)
            {
                _items.Add(item);
            }
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}