using System;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>
    /// Routes IMGUI events for the timeline into selection/drag/scrub/pan/zoom behavior.
    /// All project mutations go through the controllers (single Undo group per gesture),
    /// and all geometry comes from TimelineGeometry so this stays testable without a window.
    /// </summary>
    public sealed class TimelineInputHandler
    {
        private readonly FaceMotionEditorSession _session;
        private readonly KeyframeController _keys;
        private readonly TrackController _tracks;
        private readonly Action _scrubEnded;
        private float _keyDragAnchorPixelX;

        public TimelineInputHandler(
            FaceMotionEditorSession session,
            KeyframeController keys,
            TrackController tracks,
            Action scrubEnded = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            _scrubEnded = scrubEnded;
        }

        public bool HandleEvent(
            Event e,
            Rect plotRect,
            TimelineLayoutSnapshot layout)
        {
            return HandleEvent(
                e,
                plotRect,
                layout,
                false);
        }

        public bool HandleEvent(
            Event e,
            Rect plotRect,
            TimelineLayoutSnapshot layout,
            bool textControlOwnsKeyboard)
        {
            return HandleEvent(e, e == null ? EventType.Ignore : e.type, plotRect, layout, textControlOwnsKeyboard);
        }

        /// <summary>
        /// Accepts the IMGUI event type selected by the owning control. TimelineView uses the
        /// raw type while it owns hotControl, so an outside drag remains a timeline gesture.
        /// </summary>
        internal bool HandleEvent(
            Event e,
            EventType eventType,
            Rect plotRect,
            TimelineLayoutSnapshot layout,
            bool textControlOwnsKeyboard)
        {
            return HandleEventCore(e, eventType, plotRect, layout, textControlOwnsKeyboard);
        }

        private bool HandleEventCore(
            Event e,
            EventType eventType,
            Rect plotRect,
            TimelineLayoutSnapshot layout,
            bool textControlOwnsKeyboard)
        {
            if (e == null || layout == null)
            {
                return false;
            }

            switch (eventType)
            {
                case EventType.MouseMove:
                    UpdateHover(
                        e.mousePosition,
                        plotRect,
                        layout);

                    return false;

                case EventType.ScrollWheel:
                    if ((e.control || e.command) &&
                        plotRect.Contains(e.mousePosition))
                    {
                        int sign =
                            e.delta.y < 0f
                                ? 1
                                : e.delta.y > 0f
                                    ? -1
                                    : 0;

                        if (sign != 0)
                        {
                            ZoomAt(
                                e.mousePosition.x,
                                plotRect,
                                sign);

                            return true;
                        }
                    }

                    return false;

                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        OnLeftMouseDown(
                            plotRect,
                            layout,
                            e.mousePosition,
                            e.control || e.command,
                            e.shift);

                        return true;
                    }

                    if (e.button == 2)
                    {
                        if (plotRect.Contains(
                                e.mousePosition))
                        {
                            _session.ViewState.DragMode =
                                TimelineDragMode.Pan;

                            return true;
                        }
                    }

                    return false;

                case EventType.MouseDrag:
                    if (_session.ViewState.DragMode ==
                        TimelineDragMode.MoveKeys)
                    {
                        _keys.UpdateKeyDragAt(
                            e.mousePosition.x,
                            _keyDragAnchorPixelX,
                            _session.ViewState.PixelsPerSecond);

                        return true;
                    }

                    if (_session.ViewState.DragMode ==
                        TimelineDragMode.Scrub)
                    {
                        ScrubTo(
                            PixelToTime(
                                e.mousePosition.x,
                                plotRect));

                        return true;
                    }

                    if (_session.ViewState.DragMode ==
                        TimelineDragMode.Pan)
                    {
                        ScrollBy(
                            e.delta.x /
                            _session.ViewState.PixelsPerSecond,
                            plotRect.width);

                        return true;
                    }

                    return false;

                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        EndPointerGesture(false);
                        return true;
                    }

                    if (e.button == 2)
                    {
                        if (_session.ViewState.DragMode == TimelineDragMode.Pan)
                        {
                            EndPointerGesture(false);
                        }

                        return true;
                    }

                    return false;

                case EventType.KeyDown:
                    // Let IMGUI text controls own keyboard shortcuts
                    // while they are editing.
                    if (!CanHandleKeyboardShortcut(
                            EditorGUIUtility.editingTextField,
                            textControlOwnsKeyboard))
                    {
                        return false;
                    }

                    if (e.keyCode == KeyCode.Delete ||
                        e.keyCode == KeyCode.Backspace)
                    {
                        return _keys.DeleteSelectedKeys();
                    }

                    if (e.control || e.command)
                    {
                        if (e.keyCode == KeyCode.A)
                        {
                            _keys.SelectAllKeys();
                            return true;
                        }

                        if (e.keyCode == KeyCode.C)
                        {
                            _keys.CopySelection();
                            return true;
                        }

                        if (e.keyCode == KeyCode.V)
                        {
                            // Paste is all-or-nothing: occupied destinations are updated
                            // in place, so a failed paste never consumed the key event.
                            return _keys
                                .PasteAt(
                                    _session.ViewState.CurrentTime)
                                .Succeeded;
                        }

                        if (e.keyCode == KeyCode.D)
                        {
                            return _keys.DuplicateSelection();
                        }
                    }

                    if (e.keyCode == KeyCode.Escape)
                    {
                        EndPointerGesture(true);
                        return true;
                    }

                    if (e.keyCode == KeyCode.Home)
                    {
                        ScrubTo(0f);
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        // ----- Testable interactions -----------------------------------------------------

        public void ScrubTo(float time)
        {
            _session.SetCurrentTime(
                _keys.SnapTime(time));
        }

        public void SelectRow(
            TimelineRow row)
        {
            if (row == null ||
                row.Track == null)
            {
                return;
            }

            _tracks.Select(
                row.Track.TrackId);
        }

        public void SelectRowAt(
            float y,
            TimelineLayoutSnapshot layout)
        {
            SelectRow(
                TimelineHitTest.FindRowAt(
                    layout,
                    y));
        }

        public void ToggleKey(
            string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return;
            }

            if (_session.Selection.Contains(
                    keyId))
            {
                _session.Selection.Deselect(
                    keyId);
            }
            else
            {
                _session.Selection.Select(
                    keyId);
            }

            _session.NotifyChanged();
        }

        public void SelectKeySingle(
            string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return;
            }

            _session.Selection.SetSingle(
                keyId);

            _session.NotifyChanged();
        }

        public void ZoomAt(
            float anchorPixelX,
            Rect plotRect)
        {
            ZoomAt(
                anchorPixelX,
                plotRect,
                1);
        }

        public void ZoomAt(
            float anchorPixelX,
            Rect plotRect,
            int sign)
        {
            if (sign == 0)
            {
                return;
            }

            float oldPps =
                _session.ViewState.PixelsPerSecond;

            float anchorTime =
                TimelineGeometry.PixelToTime(
                    anchorPixelX,
                    _session.ViewState.ScrollTime,
                    oldPps,
                    plotRect.x);

            float zoom =
                _session.ViewState.Zoom;

            float factor =
                ToolbarZoomFactor(sign);

            float newZoom =
                TimelineViewState.ClampZoom(
                    zoom * factor);

            _session.ViewState.Zoom =
                newZoom;

            _session.ViewState.MarkManualViewport();

            _session.ViewState.ScrollTime =
                TimelineGeometry.ComputeZoomedScroll(
                    newZoom,
                    anchorPixelX,
                    anchorTime,
                    plotRect.x);

            ClampScroll(plotRect);
            _session.NotifyChanged();
        }

        /// <summary>
        /// Clamped zoom-step used by a single Wheel/Zoom request step.
        /// </summary>
        public static float ToolbarZoomFactor(
            int sign)
        {
            return sign >= 0
                ? 1.2f
                : 1f / 1.2f;
        }

        internal static bool CanHandleKeyboardShortcut(
            bool editingTextField,
            bool textControlOwnsKeyboard = false)
        {
            return !editingTextField &&
                   !textControlOwnsKeyboard;
        }

        public void ZoomStep(
            int sign,
            Rect plotRect)
        {
            float oldPps =
                _session.ViewState.PixelsPerSecond;

            float anchorTime =
                TimelineGeometry.PixelToTime(
                    plotRect.center.x,
                    _session.ViewState.ScrollTime,
                    oldPps,
                    plotRect.x);

            float zoom =
                TimelineViewState.ClampZoom(
                    _session.ViewState.Zoom *
                    ToolbarZoomFactor(sign));

            _session.ViewState.Zoom =
                zoom;

            _session.ViewState.MarkManualViewport();

            _session.ViewState.ScrollTime =
                TimelineGeometry.ComputeZoomedScroll(
                    zoom,
                    plotRect.center.x,
                    anchorTime,
                    plotRect.x);

            ClampScroll(plotRect);
            _session.NotifyChanged();
        }

        public void ScrollBy(
            float deltaSeconds,
            float plotWidth)
        {
            _session.ViewState.ScrollTime =
                _session.ViewState.ScrollTime -
                deltaSeconds;

            _session.ViewState.MarkManualViewport();

            ClampScrollTo(
                GetSelectedDuration(),
                plotWidth);

            _session.NotifyChanged();
        }

        public void FitToContent(
            float plotWidth)
        {
            float duration =
                GetSelectedDuration();

            _session.ViewState.Zoom =
                TimelineGeometry.FitZoom(
                    duration,
                    plotWidth);

            _session.ViewState.ScrollTime =
                0f;

            _session.ViewState.MarkAutoFit(duration);

            _session.NotifyChanged();
        }

        /// <summary>
        /// Ends a pointer gesture exactly once. Cancellation rolls back key moves but keeps
        /// the last scrubbed playhead, which is already the authoritative session time.
        /// </summary>
        internal bool EndPointerGesture(bool cancelled)
        {
            TimelineDragMode mode = _session.ViewState.DragMode;
            if (mode == TimelineDragMode.None)
            {
                return false;
            }

            _session.ViewState.DragMode = TimelineDragMode.None;
            if (mode == TimelineDragMode.MoveKeys)
            {
                if (cancelled)
                {
                    _keys.CancelKeyDrag();
                }
                else
                {
                    _keys.EndKeyDrag();
                }
            }
            else if (mode == TimelineDragMode.Scrub)
            {
                _scrubEnded?.Invoke();
            }

            return true;
        }

        // ----- Private helpers -----------------------------------------------------------

        private void OnLeftMouseDown(
            Rect plotRect,
            TimelineLayoutSnapshot layout,
            Vector2 p,
            bool add,
            bool shift)
        {
            bool inLabel =
                p.x < plotRect.x;

            // Recover from a pointer-up that Unity delivered after our hot control was
            // lost. A stale scrub must not block authoring until another click.
            EndPointerGesture(true);

            if (inLabel)
            {
                SelectRowAt(
                    p.y,
                    layout);

                return;
            }

            bool inRuler =
                p.y <
                plotRect.y +
                TimelineGeometry.RulerHeight;

            if (inRuler)
            {
                _session.ViewState.DragMode =
                    TimelineDragMode.Scrub;

                ScrubTo(
                    PixelToTime(
                        p.x,
                        plotRect));

                return;
            }

            if (TimelineHitTest.TryFindKeyAt(
                    layout,
                    plotRect.x,
                    p.x,
                    p.y,
                    out var row,
                    out string keyId,
                    out _))
            {
                // Shift takes precedence over Ctrl/Cmd.
                // K5 does not implement additive Shift-range.
                if (shift)
                {
                    string anchor =
                        _session.Selection.PrimaryKeyId;

                    _keys.SelectRange(
                        anchor,
                        keyId);

                    if (_keys.HasSelection)
                    {
                        BeginMoveDrag(
                            p.x);
                    }
                }
                else if (add)
                {
                    ToggleKey(
                        keyId);

                    if (_keys.HasSelection)
                    {
                        BeginMoveDrag(
                            p.x);
                    }
                }
                else
                {
                    // Grabbing an already-selected key keeps the whole blue working
                    // group: the selection is only replaced when the pressed key is
                    // not part of it, so MouseDown never collapses a live group and
                    // a plain click without drag leaves the group intact.
                    if (!_session.Selection.Contains(
                            keyId))
                    {
                        SelectKeySingle(
                            keyId);
                    }

                    BeginMoveDrag(
                        p.x);
                }

                return;
            }

            // Empty plot-area click clears the key selection.
            // Label/ruler/key clicks intentionally keep their existing behavior.
            if (_session.Selection.Count > 0)
            {
                _session.Selection.Clear();
                _session.NotifyChanged();
            }

            SelectRowAt(
                p.y,
                layout);

            _session.ViewState.DragMode =
                TimelineDragMode.Scrub;

            ScrubTo(
                PixelToTime(
                    p.x,
                    plotRect));
        }

        private void BeginMoveDrag(
            float anchorPixelX)
        {
            if (!_keys.HasSelection)
            {
                return;
            }

            _session.ViewState.DragMode =
                TimelineDragMode.MoveKeys;

            _keyDragAnchorPixelX =
                anchorPixelX;

            _keys.BeginKeyDrag();
        }

        private void UpdateHover(
            Vector2 p,
            Rect plotRect,
            TimelineLayoutSnapshot layout)
        {
            string hovered = null;

            if (plotRect.Contains(p))
            {
                if (TimelineHitTest.TryFindKeyAt(
                        layout,
                        plotRect.x,
                        p.x,
                        p.y,
                        out _,
                        out string keyId,
                        out _))
                {
                    hovered =
                        keyId;
                }
            }

            if (!string.Equals(
                    hovered,
                    _session.ViewState.HoveredKeyId,
                    StringComparison.Ordinal))
            {
                _session.ViewState.HoveredKeyId =
                    hovered;

                _session.NotifyChanged();
            }
        }

        private float PixelToTime(
            float x,
            Rect plotRect)
        {
            return TimelineGeometry.PixelToTime(
                x,
                _session.ViewState.ScrollTime,
                _session.ViewState.PixelsPerSecond,
                plotRect.x);
        }

        private float GetSelectedDuration()
        {
            var animation =
                _session.GetSelectedAnimation();

            return animation == null ||
                   animation.Timeline == null
                ? 1f
                : animation.Timeline.Duration;
        }

        private void ClampScroll(
            Rect plotRect)
        {
            ClampScrollTo(
                GetSelectedDuration(),
                plotRect.width);
        }

        private void ClampScrollTo(
            float duration,
            float plotWidth)
        {
            _session.ViewState.ScrollTime =
                TimelineGeometry.ClampScrollTime(
                    _session.ViewState.ScrollTime,
                    duration,
                    plotWidth,
                    _session.ViewState.PixelsPerSecond);
        }
    }
}
