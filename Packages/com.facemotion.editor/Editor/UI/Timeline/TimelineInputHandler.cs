using System;
using FaceMotion.Editor.Diagnostics;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
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
        private float _keyDragAnchorPixelX;

        public TimelineInputHandler(
            FaceMotionEditorSession session,
            KeyframeController keys,
            TrackController tracks)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        }

        public bool HandleEvent(Event e, Rect plotRect, TimelineLayoutSnapshot layout)
        {
            if (e == null || layout == null)
            {
                return false;
            }

            switch (e.type)
            {
                case EventType.MouseMove:
                    UpdateHover(e.mousePosition, plotRect, layout);
                    return false;

                case EventType.ScrollWheel:
                    if (e.control || e.command)
                    {
                        ZoomAt(e.mousePosition.x, plotRect);
                        return true;
                    }

                    return false;

                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        OnLeftMouseDown(plotRect, layout, e.mousePosition, e.control || e.command);
                        return true;
                    }

                    if (e.button == 2)
                    {
                        if (plotRect.Contains(e.mousePosition))
                        {
                            _session.ViewState.DragMode = TimelineDragMode.Pan;
                            return true;
                        }
                    }

                    return false;

                case EventType.MouseDrag:
                    if (_session.ViewState.DragMode == TimelineDragMode.MoveKeys)
                    {
                        _keys.UpdateKeyDragAt(e.mousePosition.x, _keyDragAnchorPixelX, _session.ViewState.PixelsPerSecond);
                        return true;
                    }

                    if (_session.ViewState.DragMode == TimelineDragMode.Scrub)
                    {
                        ScrubTo(PixelToTime(e.mousePosition.x, plotRect));
                        return true;
                    }

                    if (_session.ViewState.DragMode == TimelineDragMode.Pan)
                    {
                        ScrollBy(e.delta.x / _session.ViewState.PixelsPerSecond, plotRect.width);
                        return true;
                    }

                    return false;

                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        if (_session.ViewState.DragMode == TimelineDragMode.MoveKeys)
                        {
                            _keys.EndKeyDrag();
                        }

                        _session.ViewState.DragMode = TimelineDragMode.None;
                        return true;
                    }

                    if (e.button == 2)
                    {
                        _session.ViewState.DragMode = TimelineDragMode.None;
                        return true;
                    }

                    return false;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                    {
                        _keys.DeleteSelectedKeys();
                        return true;
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
                            _keys.PasteAt(_session.ViewState.CurrentTime);
                            return true;
                        }
                    }

                    if (e.keyCode == KeyCode.Escape)
                    {
                        if (_session.ViewState.DragMode == TimelineDragMode.MoveKeys)
                        {
                            _keys.CancelKeyDrag();
                        }

                        _session.ViewState.DragMode = TimelineDragMode.None;
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
            FaceMotionPreviewTrace.Trace("A.ScrubTo", "requested={0}", time);
            _session.SetCurrentTime(_keys.SnapTime(time));
        }

        public void SelectRow(TimelineRow row)
        {
            if (row == null || row.Track == null)
            {
                return;
            }

            _tracks.Select(row.Track.TrackId);
        }

        public void SelectRowAt(float y, TimelineLayoutSnapshot layout)
        {
            SelectRow(TimelineHitTest.FindRowAt(layout, y));
        }

        public void ToggleKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return;
            }

            if (_session.Selection.Contains(keyId))
            {
                _session.Selection.Deselect(keyId);
            }
            else
            {
                _session.Selection.Select(keyId);
            }

            _session.NotifyChanged();
        }

        public void SelectKeySingle(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return;
            }

            _session.Selection.SetSingle(keyId);
            _session.NotifyChanged();
        }

        public void ZoomAt(float anchorPixelX, Rect plotRect)
        {
            float oldPps = _session.ViewState.PixelsPerSecond;
            float anchorTime = TimelineGeometry.PixelToTime(anchorPixelX, _session.ViewState.ScrollTime, oldPps, plotRect.x);
            float zoom = _session.ViewState.Zoom;
            float factor = ToolbarZoomFactor(1);
            float newZoom = TimelineViewState.ClampZoom(zoom * factor);
            _session.ViewState.Zoom = newZoom;
            _session.ViewState.ScrollTime = TimelineGeometry.ComputeZoomedScroll(newZoom, anchorPixelX, anchorTime, plotRect.x);
            ClampScroll(plotRect);
            _session.NotifyChanged();
        }

        /// <summary>Clamped zoom-step used by a single Wheel/Zoom request step.</summary>
        public static float ToolbarZoomFactor(int sign)
        {
            return sign >= 0 ? 1.2f : 1f / 1.2f;
        }

        public void ZoomStep(int sign, Rect plotRect)
        {
            float oldPps = _session.ViewState.PixelsPerSecond;
            float anchorTime = TimelineGeometry.PixelToTime(plotRect.center.x, _session.ViewState.ScrollTime, oldPps, plotRect.x);
            float zoom = TimelineViewState.ClampZoom(_session.ViewState.Zoom * ToolbarZoomFactor(sign));
            _session.ViewState.Zoom = zoom;
            _session.ViewState.ScrollTime = TimelineGeometry.ComputeZoomedScroll(zoom, plotRect.center.x, anchorTime, plotRect.x);
            ClampScroll(plotRect);
            _session.NotifyChanged();
        }

        public void ScrollBy(float deltaSeconds, float plotWidth)
        {
            _session.ViewState.ScrollTime = _session.ViewState.ScrollTime - deltaSeconds;
            ClampScrollTo(GetSelectedDuration(), plotWidth);
            _session.NotifyChanged();
        }

        public void FitToContent(float plotWidth)
        {
            float duration = GetSelectedDuration();
            _session.ViewState.Zoom = TimelineGeometry.FitZoom(duration, plotWidth);
            _session.ViewState.ScrollTime = 0f;
            _session.NotifyChanged();
        }

        // ----- Private helpers -----------------------------------------------------------

        private void OnLeftMouseDown(Rect plotRect, TimelineLayoutSnapshot layout, Vector2 p, bool add)
        {
            bool inLabel = p.x < plotRect.x;
            _session.ViewState.DragMode = TimelineDragMode.None;

            if (inLabel)
            {
                SelectRowAt(p.y, layout);
                return;
            }

            bool inRuler = p.y < plotRect.y + TimelineGeometry.RulerHeight;
            if (inRuler)
            {
                _session.ViewState.DragMode = TimelineDragMode.Scrub;
                ScrubTo(PixelToTime(p.x, plotRect));
                return;
            }

            if (TimelineHitTest.TryFindKeyAt(layout, plotRect.x, p.x, p.y, out var row, out string keyId, out _))
            {
                if (add)
                {
                    ToggleKey(keyId);
                    if (_keys.HasSelection)
                    {
                        BeginMoveDrag(p.x);
                    }
                }
                else
                {
                    SelectKeySingle(keyId);
                    BeginMoveDrag(p.x);
                }

                return;
            }

            SelectRowAt(p.y, layout);
            _session.ViewState.DragMode = TimelineDragMode.Scrub;
            ScrubTo(PixelToTime(p.x, plotRect));
        }

        private void BeginMoveDrag(float anchorPixelX)
        {
            if (!_keys.HasSelection)
            {
                return;
            }

            _session.ViewState.DragMode = TimelineDragMode.MoveKeys;
            _keyDragAnchorPixelX = anchorPixelX;
            _keys.BeginKeyDrag();
        }

        private void UpdateHover(Vector2 p, Rect plotRect, TimelineLayoutSnapshot layout)
        {
            string hovered = null;
            if (plotRect.Contains(p))
            {
                if (TimelineHitTest.TryFindKeyAt(layout, plotRect.x, p.x, p.y, out _, out string keyId, out _))
                {
                    hovered = keyId;
                }
            }

            if (!string.Equals(hovered, _session.ViewState.HoveredKeyId, StringComparison.Ordinal))
            {
                _session.ViewState.HoveredKeyId = hovered;
                _session.NotifyChanged();
            }
        }

        private float PixelToTime(float x, Rect plotRect)
        {
            return TimelineGeometry.PixelToTime(x, _session.ViewState.ScrollTime, _session.ViewState.PixelsPerSecond, plotRect.x);
        }

        private float GetSelectedDuration()
        {
            var animation = _session.GetSelectedAnimation();
            return animation == null || animation.Timeline == null ? 1f : animation.Timeline.Duration;
        }

        private void ClampScroll(Rect plotRect)
        {
            ClampScrollTo(GetSelectedDuration(), plotRect.width);
        }

        private void ClampScrollTo(float duration, float plotWidth)
        {
            _session.ViewState.ScrollTime = TimelineGeometry.ClampScrollTime(
                _session.ViewState.ScrollTime,
                duration,
                plotWidth,
                _session.ViewState.PixelsPerSecond);
        }
    }
}
