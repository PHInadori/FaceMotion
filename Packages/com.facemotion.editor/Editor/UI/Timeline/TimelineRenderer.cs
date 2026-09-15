using FaceMotion.Avatar;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Timeline;
using UnityEngine;
using UnityEditor;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>
    /// IMGUI drawing for the timeline: ruler, grid, lanes, labels, key markers, selection,
    /// and the playhead. Pure drawing — all state lives in the session/selection, and input
    /// is handled separately by TimelineInputHandler.
    /// </summary>
    public static class TimelineRenderer
    {
        private static readonly Color PlotBackground = new Color(0.09f, 0.09f, 0.10f, 1f);
        private static readonly Color RulerBackground = new Color(0.16f, 0.16f, 0.17f, 1f);
        private static readonly Color RowBackground = new Color(0.12f, 0.12f, 0.13f, 1f);
        private static readonly Color RowBackgroundAlt = new Color(0.14f, 0.14f, 0.15f, 1f);
        private static readonly Color GridLine = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color MajorGridLine = new Color(1f, 1f, 1f, 0.09f);
        private static readonly Color LabelBackground = new Color(0.11f, 0.11f, 0.12f, 1f);
        private static readonly Color CursorColor = new Color(1f, 0.55f, 0.2f, 1f);
        private static readonly Color KeyColor = new Color(0.75f, 0.75f, 0.78f, 1f);
        private static readonly Color KeySelected = new Color(0.36f, 0.78f, 1f, 1f);
        private static readonly Color KeyHovered = new Color(1f, 0.9f, 0.4f, 1f);
        private static readonly Color MissingColor = new Color(1f, 0.45f, 0.35f, 1f);

        private static GUIStyle _rulerText;
        private static GUIStyle _rowLabel;
        private static GUIStyle _hintLabel;
        private static GUIContent _content = new GUIContent();

        public static void Draw(
            Rect rect,
            TimelineLayoutSnapshot layout,
            TimelineViewState view,
            TimelineSelection selection)
        {
            EnsureStyles();

            Rect plot = new Rect(layout.PlotLeft, rect.y, Mathf.Max(1f, rect.width - layout.PlotLeft), rect.height);
            EditorGUI.DrawRect(plot, PlotBackground);

            float pps = layout.PixelsPerSecond;
            float scroll = layout.ScrollTime;
            float rulerHeight = TimelineGeometry.RulerHeight;
            float rowsTop = plot.y + rulerHeight;

            DrawRuler(new Rect(plot.x, plot.y, plot.width, rulerHeight), scroll, pps, plot.x, view.CurrentTime);
            DrawGrid(new Rect(plot.x, rowsTop, plot.width, Mathf.Max(0f, plot.height - rulerHeight)), scroll, pps, plot.x);
            DrawRows(new Rect(rect.x, rowsTop, rect.width, Mathf.Max(1f, plot.height - rulerHeight)), layout, selection, view.HoveredKeyId);
            DrawCursor(new Rect(plot.x, rowsTop, plot.width, Mathf.Max(0f, plot.height - rulerHeight)), view.CurrentTime, scroll, pps, plot.x);

            if (layout.RowCount == 0)
            {
                GUI.Label(new Rect(plot.x + 10f, rowsTop + 8f, plot.width - 20f, 24f), FaceMotionUiText.Get("noTracks"), _hintLabel);
            }
        }

        private static void DrawRuler(Rect ruler, float scroll, float pps, float plotLeft, float currentTime)
        {
            EditorGUI.DrawRect(ruler, RulerBackground);

            float major = TimelineGeometry.GetMajorStep(pps);
            float minor = TimelineGeometry.GetMinorStep(pps);
            float firstMajor = Mathf.Max(0f, scroll) % major;
            float minorStep = minor;

            int maxTicks = 1000;
            float y = ruler.y;
            float h = ruler.height;

            for (float t = scroll + firstMajor, i = 0f; t <= scroll + TimelineGeometry.VisibleDuration(ruler.width, pps) && i < maxTicks; t += minorStep, i++)
            {
                float x = TimelineGeometry.TimeToPixel(t, scroll, pps, plotLeft);
                if (x < plotLeft - 1f)
                {
                    continue;
                }

                bool isMajor = Mathf.Abs(t / major - Mathf.Round(t / major)) < 0.001f;
                EditorGUI.DrawRect(
                    new Rect(x, isMajor ? y : y + h * 0.5f, 1f, isMajor ? h : h * 0.5f),
                    isMajor ? MajorGridLine : GridLine);

                if (isMajor)
                {
                    SetContent(FormatTime(t, major));
                    _rulerText.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                    GUI.Label(new Rect(x + 4f, y + 2f, 90f, h - 4f), _content, _rulerText);
                }
            }

            float cursorX = TimelineGeometry.TimeToPixel(currentTime, scroll, pps, plotLeft);
            if (cursorX >= plotLeft - 1f && cursorX <= ruler.xMax)
            {
                SetContent(FormatTime(currentTime, Mathf.Min(major, 0.1f)));
                _rulerText.normal.textColor = CursorColor;
                GUI.Label(new Rect(Mathf.Max(plotLeft, cursorX - 30f), y - 4f, 70f, h), _content, _rulerText);
            }
        }

        private static void DrawGrid(Rect grid, float scroll, float pps, float plotLeft)
        {
            float major = TimelineGeometry.GetMajorStep(pps);
            float minor = TimelineGeometry.GetMinorStep(pps);

            int maxTicks = 1000;
            for (float t = scroll, i = 0f; t <= scroll + TimelineGeometry.VisibleDuration(grid.width, pps) && i < maxTicks; t += minor, i++)
            {
                if (t < 0f)
                {
                    continue;
                }

                float x = TimelineGeometry.TimeToPixel(t, scroll, pps, plotLeft);
                if (x < plotLeft - 1f)
                {
                    continue;
                }

                bool isMajor = Mathf.Abs(t / major - Mathf.Round(t / major)) < 0.001f;
                EditorGUI.DrawRect(new Rect(x, grid.y, 1f, grid.height), isMajor ? MajorGridLine : GridLine);
            }
        }

        private static void DrawRows(
            Rect area,
            TimelineLayoutSnapshot layout,
            TimelineSelection selection,
            string hoveredKeyId)
        {
            EditorGUI.DrawRect(new Rect(area.x, area.y, layout.LabelWidth, area.height), LabelBackground);

            for (int i = 0; i < layout.Rows.Count; i++)
            {
                var row = layout.Rows[i];
                var rowRect = new Rect(area.x, row.Y, area.width, row.Height);
                EditorGUI.DrawRect(rowRect, (i % 2) == 0 ? RowBackground : RowBackgroundAlt);
                EditorGUI.DrawRect(new Rect(area.x, row.Y + row.Height - 1f, area.width, 1f), GridLine);

                DrawRowLabel(new Rect(area.x, row.Y, layout.LabelWidth, row.Height), row);

                var track = row.Track;
                if (track == null)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                {
                    foreach (var key in track.BlendShape.Keys)
                    {
                        if (key == null)
                        {
                            continue;
                        }

                        DrawKey(row, layout, key.Time, selection != null && selection.Contains(key.KeyId), hoveredKeyId == key.KeyId);
                    }
                }
                else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                {
                    foreach (var key in track.Transform.Keys)
                    {
                        if (key == null)
                        {
                            continue;
                        }

                        DrawKey(row, layout, key.Time, selection != null && selection.Contains(key.KeyId), hoveredKeyId == key.KeyId);
                    }
                }
            }

            EditorGUI.DrawRect(new Rect(area.x + layout.LabelWidth - 1f, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.1f));
        }

        private static void DrawRowLabel(Rect cell, TimelineRow row)
        {
            var track = row.Track;
            if (track == null)
            {
                return;
            }

            string display;
            bool missing = false;
            if (row.Binding != null && row.Binding.Status != MappingResolutionStatus.Resolved)
            {
                missing = true;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                display = ToRowText(missing, "B", track.BlendShape.BlendShapeName, track.BlendShape.RendererPath);
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                display = ToRowText(missing, LayerTag(track.Kind), LastSegment(track.Transform.TransformPath), track.Transform.TransformPath);
            }
            else
            {
                return;
            }

            SetContent(display);
            _rowLabel.normal.textColor = missing ? MissingColor : new Color(0.9f, 0.9f, 0.9f, 1f);
            GUI.Label(cell, _content, _rowLabel);
        }

        private static void DrawKey(TimelineRow row, TimelineLayoutSnapshot layout, float time, bool selected, bool hovered)
        {
            float x = TimelineGeometry.TimeToPixel(time, layout.ScrollTime, layout.PixelsPerSecond, layout.PlotLeft);
            float y = row.Y + row.Height * 0.5f;
            float size = selected ? 9f : 7f;

            Color color = selected ? KeySelected : (hovered ? KeyHovered : KeyColor);
            EditorGUI.DrawRect(new Rect(x - 0.5f, y - size * 0.5f, 1f, size), color);

            Rect marker = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);
            EditorGUI.DrawRect(marker, color);
            EditorGUIUtility.AddCursorRect(marker, MouseCursor.Link);
            GUI.Label(marker, new GUIContent(string.Empty, string.Format(FaceMotionUiText.Get("keyTooltip"), time)), GUIStyle.none);
        }

        private static void DrawCursor(Rect area, float currentTime, float scroll, float pps, float plotLeft)
        {
            if (area.height <= 0f)
            {
                return;
            }

            float x = TimelineGeometry.TimeToPixel(currentTime, scroll, pps, plotLeft);
            if (x < plotLeft - 1f || x > area.xMax + 1f)
            {
                return;
            }

            EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), CursorColor);
        }

        private static string LayerTag(TrackKind kind)
        {
            if (kind == TrackKind.TransformPosition)
            {
                return "P";
            }

            if (kind == TrackKind.TransformRotation)
            {
                return "R";
            }

            if (kind == TrackKind.TransformScale)
            {
                return "S";
            }

            return "T";
        }

        private static string ToRowText(bool missing, string tag, string name, string path)
        {
            if (string.IsNullOrEmpty(name))
            {
                return missing ? tag + " " + FaceMotionUiText.Get("missing") : tag + " " + FaceMotionUiText.Get("empty");
            }

            string baseText = tag + " " + name;
            if (missing)
            {
                return baseText + " ?";
            }

            string shortPath = LastSegment(path);
            if (!string.IsNullOrEmpty(shortPath))
            {
                return baseText + "  (" + shortPath + ")";
            }

            return baseText;
        }

        private static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int idx = path.LastIndexOf('/');
            if (idx < 0 || idx == path.Length - 1)
            {
                return path;
            }

            return path.Substring(idx + 1);
        }

        private static string FormatTime(float t, float step)
        {
            if (t < 0f)
            {
                return "0.000";
            }

            if (step < 0.1f)
            {
                return t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (step < 1f)
            {
                return t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            }

            return t.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void SetContent(string text)
        {
            _content.text = text;
            _content.image = null;
            _content.tooltip = null;
        }

        private static void EnsureStyles()
        {
            if (_rulerText == null)
            {
                _rulerText = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 10,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_rowLabel == null)
            {
                _rowLabel = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 2, 0, 0)
                };
            }

            if (_hintLabel == null)
            {
                _hintLabel = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    normal = { textColor = new Color(0.65f, 0.65f, 0.65f, 1f) }
                };
            }
        }
    }
}
