using FaceMotion.Editor.Preview;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using System;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>Timeline preview controls with a deliberately separate, reversible scene mode.</summary>
    public sealed class PreviewPanel
    {
        public const float HeaderHeight = 20f;
        public const float ControlsHeight = 48f;
        public const float Padding = 4f;
        internal const float ControlHeight = 20f;
        internal const float ControlSpacing = 4f;
        internal const float HeaderHelpButtonWidth = 24f;
        internal const float HeaderSpacing = 4f;

        /// <summary>Deterministic per-character metrics used when editor styles are unavailable.</summary>
        internal const float HeaderLatinGlyphWidth = 7f;
        internal const float HeaderWideGlyphWidth = 12f;
        private readonly FaceMotionEditorSession _session;
        private readonly PreviewSession _preview;
        private readonly SceneApplySession _sceneApply;
        private readonly PreviewPlaybackController _playback;
        private readonly Action _ensureAvatarIndex;
        private readonly Action _previewRebuilt;
        private int _lastOverrideChangeCount;
        private bool _helpOpen;

        public PreviewPanel(FaceMotionEditorSession session, PreviewSession preview, SceneApplySession sceneApply, PreviewPlaybackController playback, Action ensureAvatarIndex = null, Action previewRebuilt = null)
        {
            _session = session;
            _preview = preview;
            _sceneApply = sceneApply;
            _playback = playback;
            _ensureAvatarIndex = ensureAvatarIndex;
            _previewRebuilt = previewRebuilt;
        }

        public void OnGUI(Rect assignedRect, bool textControlOwnsKeyboard = false)
        {
            var layout = CalculateLayout(assignedRect, _helpOpen);
            PreviewHeaderLayout header = CalculateHeaderLayout(layout.HeaderRect);
            GUI.Label(header.TitleRect, FaceMotionUiText.Get("preview"), EditorStyles.boldLabel);
            GUI.Label(header.IsolationRect, FaceMotionUiText.Get("previewIsolation"), EditorStyles.miniLabel);
            Rect helpButton = header.HelpRect;
            if (GUI.Button(helpButton, new GUIContent("?", FaceMotionUiText.Get("contextHelpTooltip")), EditorStyles.miniButton))
            {
                _helpOpen = ContextHelp.Toggle(_helpOpen);
            }

            if (_helpOpen)
            {
                EditorGUI.HelpBox(new Rect(layout.HeaderRect.x, layout.HeaderRect.yMax + Padding,
                    layout.HeaderRect.width, HelpHeight(layout.HeaderRect.width)), ContextHelp.Text("contextHelpPreview"), MessageType.Info);
            }

            if (_session.ActiveAvatarRoot == null)
            {
                EditorGUI.HelpBox(layout.RenderRect, FaceMotionUiText.Get("selectAvatarToPreview"), MessageType.Info);
                return;
            }

            PreviewControlsLayout controls = CalculateControlsLayout(layout.ControlsRect);
            if (GUI.Button(controls.Start, _preview.IsActive ? FaceMotionUiText.Get("rebuildPreview") : FaceMotionUiText.Get("startPreview"), EditorStyles.miniButton))
            {
                _ensureAvatarIndex?.Invoke();
                _preview.RebuildAvatar(_session.ActiveAvatarRoot);
                _session.NotifyPoseChanged();
                _previewRebuilt?.Invoke();
            }

            if (GUI.Button(controls.StopPreview, FaceMotionUiText.Get("stopPreview"), EditorStyles.miniButton))
            {
                _preview.Dispose();
            }

            if (GUI.Button(controls.SceneApply, _sceneApply.IsActive ? FaceMotionUiText.Get("stopSceneApply") : FaceMotionUiText.Get("applyToScene"), EditorStyles.miniButton))
            {
                if (_sceneApply.IsActive)
                {
                    _sceneApply.Dispose();
                }
                else
                {
                    _sceneApply.Start(_session.ActiveAvatarRoot);
                    _sceneApply.Apply(_session.GetSelectedAnimation(), _session.ViewState.CurrentTime);
                }
            }

            if (_preview.IsActive)
            {
                bool fit = false;
                bool reset = false;
                EditorGUI.BeginDisabledGroup(!_playback.CanPlay);
                string playbackLabel = FaceMotionUiText.Get(_playback.IsPlaying ? "previewPause" : "previewPlay");
                if (GUI.Button(controls.Play, new GUIContent(playbackLabel, playbackLabel), EditorStyles.miniButton))
                {
                    TogglePlayback();
                }

                if (GUI.Button(controls.PlaybackStop, FaceMotionUiText.Get("previewStop"), EditorStyles.miniButton))
                {
                    _playback.Stop();
                }

                EditorGUI.EndDisabledGroup();
                if (GUI.Button(controls.Fit, FaceMotionUiText.Get("fitAvatar"), EditorStyles.miniButton))
                {
                    fit = true;
                }

                if (GUI.Button(controls.Reset, FaceMotionUiText.Get("resetView"), EditorStyles.miniButton))
                {
                    reset = true;
                }

                if (controls.Hint.width > 0f)
                {
                    GUI.Label(controls.Hint, FaceMotionUiText.Get("previewControls"), EditorStyles.miniLabel);
                }
                Rect previewRect = layout.RenderRect;
                if (fit)
                {
                    _preview.FitCamera(previewRect);
                }

                if (reset)
                {
                    _preview.ResetCamera();
                }

                if (ConsumeOverrideChange(ref _lastOverrideChangeCount, _preview.Override))
                {
                    _preview.Evaluate(_session.GetSelectedAnimation(), _session.ViewState.CurrentTime);
                }

                _preview.HandleCameraInput(previewRect, textControlOwnsKeyboard);
                _preview.Draw(previewRect);

                if (!string.IsNullOrEmpty(_preview.Diagnostic))
                {
                    EditorGUI.HelpBox(layout.RenderRect, _preview.Diagnostic, MessageType.Warning);
                }
            }
        }

        /// <summary>
        /// Narrow consumer for preview-override (hover) change notifications: each
        /// ChangeCount bump is consumed at most once and reported as needing exactly one
        /// preview re-evaluation.
        /// </summary>
        internal static bool ConsumeOverrideChange(ref int lastChangeCount, PreviewOverrideState overrideState)
        {
            if (overrideState == null || lastChangeCount == overrideState.ChangeCount)
            {
                return false;
            }

            lastChangeCount = overrideState.ChangeCount;
            return true;
        }

        /// <summary>Only the visible Preview Play control acknowledges review of current content.</summary>
        internal bool StartPlayback()
        {
            _playback.Play();
            if (!_playback.IsPlaying)
            {
                return false;
            }

            _session.MarkCurrentAnimationReviewed();
            return true;
        }

        /// <summary>The visible playback control and the window's Space shortcut share this path.</summary>
        internal bool TogglePlayback()
        {
            if (_playback.IsPlaying)
            {
                _playback.Pause();
                return true;
            }

            return StartPlayback();
        }

        /// <summary>
        /// Header geometry from measured title width: the isolation note starts after the
        /// title plus spacing, ends before the fixed-width help button, and clamps to zero
        /// width instead of producing a negative rect. Pure helper: no session/preview state.
        /// </summary>
        internal static PreviewHeaderLayout CalculateHeaderLayout(Rect headerRect,
            SystemLanguage language = SystemLanguage.Japanese)
        {
            string title = FaceMotionUiText.Get("preview", language);
            GUIStyle titleStyle = Event.current != null ? EditorStyles.boldLabel : null;
            float titleWidth = MeasureHeaderTextWidth(title, titleStyle);
            var titleRect = new Rect(headerRect.x, headerRect.y, titleWidth, headerRect.height);

            var helpRect = new Rect(
                headerRect.xMax - HeaderHelpButtonWidth,
                headerRect.y,
                HeaderHelpButtonWidth,
                headerRect.height);

            float isolationX = titleRect.xMax + HeaderSpacing;
            float isolationMax = helpRect.x - HeaderSpacing;
            float isolationWidth = Mathf.Max(0f, isolationMax - isolationX);
            var isolationRect = new Rect(isolationX, headerRect.y, isolationWidth, headerRect.height);
            return new PreviewHeaderLayout(titleRect, isolationRect, helpRect);
        }

        /// <summary>
        /// Measures text with the given style inside OnGUI; falls back to a deterministic
        /// character-metric estimate whenever styles (or finite metrics) are unavailable.
        /// </summary>
        internal static float MeasureHeaderTextWidth(string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            if (style != null && Event.current != null)
            {
                float measured = style.CalcSize(new GUIContent(text)).x;
                if (measured > 0f && !float.IsNaN(measured) && !float.IsInfinity(measured))
                {
                    return measured;
                }
            }

            return EstimateHeaderTextWidth(text);
        }

        /// <summary>String-length dependent estimate: Latin glyphs 7px, wide glyphs 12px.</summary>
        internal static float EstimateHeaderTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            float width = 0f;
            foreach (char c in text)
            {
                width += c < 128 ? HeaderLatinGlyphWidth : HeaderWideGlyphWidth;
            }

            return width;
        }

        public static PreviewPanelLayout CalculateLayout(Rect assignedRect, bool showHelp = false,
            SystemLanguage language = SystemLanguage.Japanese)
        {
            float width = Mathf.Max(0f, assignedRect.width - Padding * 2f);
            var header = new Rect(assignedRect.x + Padding, assignedRect.y + Padding, width, HeaderHeight);
            float controlsY = header.yMax + Padding + (showHelp ? HelpHeight(width, language) + Padding : 0f);
            PreviewControlsLayout controlLayout = CalculateControlsLayout(new Rect(header.x, controlsY, width, 0f));
            var controls = new Rect(header.x, controlsY, width, Mathf.Max(ControlsHeight, controlLayout.Height));
            var render = new Rect(controls.x, controls.yMax + Padding, width, Mathf.Max(0f, assignedRect.yMax - (controls.yMax + Padding)));
            return new PreviewPanelLayout(header, controls, render);
        }

        internal static float HelpHeight(float width, SystemLanguage language = SystemLanguage.Japanese)
        {
            string text = FaceMotionUiText.Get("contextHelpPreview", language);
            return Mathf.Ceil(Mathf.Max(1f, text.Length) / Mathf.Max(1f, (width - 32f) / 16f)) * 16f + 16f;
        }

        internal static PreviewControlsLayout CalculateControlsLayout(Rect controlsRect)
        {
            var layout = new PreviewControlsLayout(controlsRect);
            float startWidth = Mathf.Max(ButtonWidth("startPreview"), ButtonWidth("rebuildPreview"));
            float sceneWidth = Mathf.Max(ButtonWidth("applyToScene"), ButtonWidth("stopSceneApply"));
            layout.AddAction(startWidth);
            layout.AddAction(ButtonWidth("stopPreview"));
            layout.AddAction(sceneWidth);
            layout.BeginPlaybackRow();
            layout.AddPlayback(Mathf.Max(ButtonWidth("previewPlay"), ButtonWidth("previewPause")));
            layout.AddPlayback(ButtonWidth("previewStop"));
            layout.AddPlayback(ButtonWidth("fitAvatar"));
            layout.AddPlayback(ButtonWidth("resetView"));
            layout.Finish();
            return layout;
        }

        private static float ButtonWidth(string localizationKey)
        {
            string label = FaceMotionUiText.Get(localizationKey);
            if (Event.current == null)
            {
                // Layout tests and non-GUI callers do not have an editor skin; reserve a conservative text width.
                return label.Length * 8f + 16f + ControlSpacing;
            }

            return Mathf.Ceil(EditorStyles.miniButton.CalcSize(new GUIContent(label)).x) + ControlSpacing;
        }
    }

    public readonly struct PreviewPanelLayout
    {
        public PreviewPanelLayout(Rect headerRect, Rect controlsRect, Rect renderRect) { HeaderRect = headerRect; ControlsRect = controlsRect; RenderRect = renderRect; }
        public Rect HeaderRect { get; }
        public Rect ControlsRect { get; }
        public Rect RenderRect { get; }
    }

    /// <summary>Measured header geometry: title, isolation note, and fixed help button without overlap.</summary>
    internal readonly struct PreviewHeaderLayout
    {
        public PreviewHeaderLayout(Rect titleRect, Rect isolationRect, Rect helpRect)
        {
            TitleRect = titleRect;
            IsolationRect = isolationRect;
            HelpRect = helpRect;
        }

        public Rect TitleRect { get; }
        public Rect IsolationRect { get; }
        public Rect HelpRect { get; }
    }

    internal struct PreviewControlsLayout
    {
        private readonly Rect _bounds;
        private float _x;
        private float _y;
        private bool _hasRow;

        public PreviewControlsLayout(Rect bounds)
        {
            _bounds = bounds;
            _x = bounds.x;
            _y = bounds.y;
            _hasRow = false;
            Start = default;
            StopPreview = default;
            SceneApply = default;
            Play = default;
            PlaybackStop = default;
            Fit = default;
            Reset = default;
            Hint = default;
            Height = 0f;
        }

        public Rect Start { get; private set; }
        public Rect StopPreview { get; private set; }
        public Rect SceneApply { get; private set; }
        public Rect Play { get; private set; }
        public Rect PlaybackStop { get; private set; }
        public Rect Fit { get; private set; }
        public Rect Reset { get; private set; }
        public Rect Hint { get; private set; }
        public float Height { get; private set; }

        public void AddAction(float width)
        {
            Rect rect = Add(width);
            if (Start.width == 0f) Start = rect;
            else if (StopPreview.width == 0f) StopPreview = rect;
            else SceneApply = rect;
        }

        public void BeginPlaybackRow()
        {
            if (_hasRow)
            {
                NextRow();
            }
        }

        public void AddPlayback(float width)
        {
            Rect rect = Add(width);
            if (Play.width == 0f) Play = rect;
            else if (PlaybackStop.width == 0f) PlaybackStop = rect;
            else if (Fit.width == 0f) Fit = rect;
            else Reset = rect;
        }

        public void Finish()
        {
            float hintWidth = _bounds.xMax - _x;
            if (hintWidth >= 80f)
            {
                Hint = new Rect(_x, _y, hintWidth, PreviewPanel.ControlHeight);
            }

            Height = _hasRow ? _y + PreviewPanel.ControlHeight - _bounds.y : 0f;
        }

        private Rect Add(float requestedWidth)
        {
            float width = Mathf.Min(requestedWidth, _bounds.width);
            if (_hasRow && _x + width > _bounds.xMax)
            {
                NextRow();
            }

            var rect = new Rect(_x, _y, width, PreviewPanel.ControlHeight);
            _x += width + PreviewPanel.ControlSpacing;
            _hasRow = true;
            return rect;
        }

        private void NextRow()
        {
            _x = _bounds.x;
            _y += PreviewPanel.ControlHeight + PreviewPanel.ControlSpacing;
        }
    }
}
