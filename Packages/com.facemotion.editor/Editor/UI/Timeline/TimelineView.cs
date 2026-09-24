using System;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using UnityEngine;
using UnityEditor;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>
    /// One IMGUI widget hosting the timeline interaction: builds the lane layout from the
    /// current selection, routes events through TimelineInputHandler, and draws with
    /// TimelineRenderer. The window keeps this thin — stateless, no Undo calls here.
    /// </summary>
    public sealed class TimelineView
    {
        private readonly FaceMotionEditorSession _session;
        private readonly KeyframeController _keys;
        private readonly TrackController _tracks;
        private readonly TimelineInputHandler _input;

        public float LabelWidth = TimelineGeometry.DefaultLabelWidth;

        public TimelineView(FaceMotionEditorSession session, KeyframeController keys, TrackController tracks)
            : this(session, keys, tracks, null)
        {
        }

        public TimelineView(FaceMotionEditorSession session, KeyframeController keys, TrackController tracks, Action scrubEnded)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            _input = new TimelineInputHandler(_session, _keys, _tracks, scrubEnded);
        }

        public TimelineInputHandler Input => _input;

        /// <summary>Changes editor-only snapping state; this never mutates a project asset.</summary>
        public void SetSnapEnabled(bool enabled)
        {
            if (_session.ViewState.SnapEnabled != enabled)
            {
                _session.ViewState.SnapEnabled = enabled;
                _session.NotifyChanged();
            }
        }

        public void OnGUI(Rect rect)
        {
            OnGUI(rect, false);
        }

        public void OnGUI(Rect rect, bool textControlOwnsKeyboard)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                DrawEmpty(rect, FaceMotionUiText.Get("selectAnimation"));
                return;
            }

            float duration = animation.Timeline.Duration;
            float plotWidth = Mathf.Max(1f, rect.width - LabelWidth);
            float pps = _session.ViewState.PixelsPerSecond;
            _session.ViewState.ScrollTime = TimelineGeometry.ClampScrollTime(
                _session.ViewState.ScrollTime,
                duration,
                plotWidth,
                pps);

            float cursorMax = duration;
            if (_session.ViewState.CurrentTime > cursorMax)
            {
                _session.SetCurrentTime(cursorMax);
            }

            float rowsTop = rect.y + TimelineGeometry.RulerHeight;
            TimelineLayoutSnapshot layout = TimelineLayoutBuilder.Build(
                animation,
                _session.TrackBindings,
                LabelWidth,
                rowsTop,
                _session.ViewState.ScrollTime,
                _session.ViewState.Zoom,
                duration);

            Rect plotRect = new Rect(LabelWidth, rect.y, plotWidth, rect.height);

            DrawTimelineHeader(rect, animation.Timeline.FrameRate);

            Event current = Event.current;
            if (current != null && _input.HandleEvent(current, plotRect, layout, textControlOwnsKeyboard))
            {
                current.Use();
            }

            TimelineRenderer.Draw(rect, layout, _session.ViewState, _session.Selection);
        }

        private void DrawTimelineHeader(Rect rect, float frameRate)
        {
            Rect header = new Rect(rect.x, rect.y, LabelWidth, TimelineGeometry.RulerHeight);
            EditorGUI.DrawRect(header, new Color(0.16f, 0.16f, 0.17f, 1f));

            string label = FaceMotionUiText.Get("snap") + " " + frameRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " " + FaceMotionUiText.Get("fps");
            bool enabled = GUI.Toggle(
                new Rect(header.x + 5f, header.y + 3f, header.width - 10f, header.height - 6f),
                _session.ViewState.SnapEnabled,
                label,
                EditorStyles.toolbarButton);
            SetSnapEnabled(enabled);
        }

        private static void DrawEmpty(Rect rect, string message)
        {
            var bg = new GUIContent();
            GUI.Box(rect, bg, EditorStyles.helpBox);
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f, 1f) }
            };
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.4f, rect.width, 30f), message, style);
        }
    }
}
