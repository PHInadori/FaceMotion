using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Diagnostics
{
    /// <summary>
    /// Optional, off-by-default diagnostic recorder for the preview scrub path. When enabled it
    /// writes one-line entries (category tag + message) to both the Console (Editor.log) and an
    /// in-memory ring buffer so tests and the user can correlate where a scrub update stops.
    /// Disabled by default; no cost, no log volume, unless a diagnostics session is active.
    /// </summary>
    public static class FaceMotionPreviewTrace
    {
        private const int Capacity = 4096;
        private static readonly List<string> Buffer = new List<string>(Capacity);
        private static bool _enabled;

        public static bool Enabled => _enabled;

        public static int Count => Buffer.Count;

        public static IReadOnlyList<string> Log => Buffer;

        /// <summary>Enables or disables recording for the current editor session only.</summary>
        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public static void Clear()
        {
            Buffer.Clear();
        }

        public static void ClearAndEnable()
        {
            Buffer.Clear();
            _enabled = true;
        }

        /// <summary>
        /// Appends one diagnostic entry. No-op when disabled so production runs never allocate
        /// or log anything from this call site.
        /// </summary>
        public static void Trace(string category, string format, params object[] args)
        {
            if (!_enabled)
            {
                return;
            }

            string line = "[FaceMotionPreviewTrace:" + category + "] "
                + string.Format(CultureInfo.InvariantCulture, format, args);
            Debug.Log(line);
            if (Buffer.Count >= Capacity)
            {
                Buffer.RemoveAt(0);
            }

            Buffer.Add(line);
        }

        // This is intentionally developer-only; it is not part of the normal FaceMotion workflow.
        [MenuItem("Tools/FaceMotion/Developer/Preview Trace: On")]
        public static void EnableFromMenu()
        {
            SetEnabled(true);
        }

        [MenuItem("Tools/FaceMotion/Developer/Preview Trace: Off")]
        public static void DisableFromMenu()
        {
            SetEnabled(false);
        }

        [MenuItem("Tools/FaceMotion/Developer/Preview Trace: Clear")]
        public static void ClearFromMenu()
        {
            Clear();
        }

        // Simulates the static-state reset performed by a domain reload without touching legacy preferences.
        internal static void ResetSessionForTests()
        {
            _enabled = false;
            Buffer.Clear();
        }
    }
}
