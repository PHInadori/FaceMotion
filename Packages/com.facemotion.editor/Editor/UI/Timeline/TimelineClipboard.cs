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

        public string SourceAnimationId { get; private set; }

        public IReadOnlyList<ClipboardItem> Items => _items;

        public bool HasItems => _items.Count > 0;

        public int Count => _items.Count;

        public void Set(string sourceAnimationId, IEnumerable<ClipboardItem> items)
        {
            _items.Clear();
            SourceAnimationId = sourceAnimationId;
            if (items == null)
            {
                return;
            }

            foreach (ClipboardItem item in items)
            {
                if (item != null)
                {
                    _items.Add(new ClipboardItem
                    {
                        TrackId = item.TrackId,
                        Kind = item.Kind,
                        RelativeTime = item.RelativeTime,
                        FloatValue = item.FloatValue,
                        VectorValue = item.VectorValue,
                        Interpolation = item.Interpolation
                    });
                }
            }
        }

        public void Clear()
        {
            _items.Clear();
            SourceAnimationId = null;
        }
    }
}
