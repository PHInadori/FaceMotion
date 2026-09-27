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

        /// <summary>
        /// Legacy two-row reference height. Kept for existing test contracts; the layout no
        /// longer enforces it as a minimum — controls height comes from the visible rows.
        /// </summary>
        public const float ControlsHeight = 48f;
        public const float Padding = 4f;
        internal const float ControlHeight = 20f;
        internal const float ControlSpacing = 4f;
        internal const float HeaderHelpButtonWidth = 24f;
        internal const float HeaderSpacing = 4f;

        /// <summary>Minimum residual width on the playback row before the hint label is shown.</summary>
        internal const float PreviewHintMinimumWidth = 80f;

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
            var layout = CalculateLayout(assignedRect, _helpOpen,
                previewActive: _preview.IsActive,
                hasAvatar: _session.ActiveAvatarRoot != null);
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

            PreviewControlsLayout controls = CalculateControlsLayout(layout.ControlsRect, _preview.IsActive);
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

        /// <summary>
        /// Pure panel layout. Controls height is derived from the rows of the actually
        /// visible controls, driven by two independent explicit state flags:
        /// <paramref name="hasAvatar"/> false means no control at all is drawn (the empty-state
        /// help box takes the render area), so the reserved height is zero;
        /// with an avatar, <paramref name="previewActive"/> false reserves only the action row
        /// (Start/Rebuild, Stop Preview, Scene Apply) while true also accounts the playback row
        /// (Play/Pause, Playback Stop, Fit, Reset, Hint).
        /// Defaults preserve the existing contract (selected avatar, active preview);
        /// production passes the live state explicitly.
        /// </summary>
        public static PreviewPanelLayout CalculateLayout(Rect assignedRect, bool showHelp = false,
            SystemLanguage language = SystemLanguage.Japanese, bool previewActive = true,
            bool hasAvatar = true)
        {
            float width = Mathf.Max(0f, assignedRect.width - Padding * 2f);
            var header = new Rect(assignedRect.x + Padding, assignedRect.y + Padding, width, HeaderHeight);
            float controlsY = header.yMax + Padding + (showHelp ? HelpHeight(width, language) + Padding : 0f);
            float controlsHeight = 0f;
            if (hasAvatar)
            {
                PreviewControlsLayout controlLayout = CalculateControlsLayout(
                    new Rect(header.x, controlsY, width, 0f), previewActive, language);
                controlsHeight = controlLayout.Height;
            }

            var controls = new Rect(header.x, controlsY, width, controlsHeight);
            var render = new Rect(controls.x, controls.yMax + Padding, width, Mathf.Max(0f, assignedRect.yMax - (controls.yMax + Padding)));
            return new PreviewPanelLayout(header, controls, render);
        }

        internal static float HelpHeight(float width, SystemLanguage language = SystemLanguage.Japanese)
        {
            string text = FaceMotionUiText.Get("contextHelpPreview", language);
            return Mathf.Ceil(Mathf.Max(1f, text.Length) / Mathf.Max(1f, (width - 32f) / 16f)) * 16f + 16f;
        }

        /// <summary>
        /// Flow layout for the visible controls. With <paramref name="includePlayback"/> false
        /// only the always-visible action row (Start/Rebuild, Stop Preview, Scene Apply) is
        /// placed, so the resulting <see cref="PreviewControlsLayout.Height"/> accounts a
        /// single row at normal width instead of the full active grid.
        /// </summary>
        internal static PreviewControlsLayout CalculateControlsLayout(Rect controlsRect,
            bool includePlayback = true, SystemLanguage language = SystemLanguage.Japanese)
        {
            var layout = new PreviewControlsLayout(controlsRect, includePlayback);
            float startWidth = Mathf.Max(ButtonWidth("startPreview", language), ButtonWidth("rebuildPreview", language));
            float sceneWidth = Mathf.Max(ButtonWidth("applyToScene", language), ButtonWidth("stopSceneApply", language));
            layout.AddAction(startWidth);
            layout.AddAction(ButtonWidth("stopPreview", language));
            layout.AddAction(sceneWidth);
            if (includePlayback)
            {
                layout.BeginPlaybackRow();
                layout.AddPlayback(Mathf.Max(ButtonWidth("previewPlay", language), ButtonWidth("previewPause", language)));
                layout.AddPlayback(ButtonWidth("previewStop", language));
                layout.AddPlayback(ButtonWidth("fitAvatar", language));
                layout.AddPlayback(ButtonWidth("resetView", language));
            }

            layout.Finish();
            return layout;
        }

        // Internal (not private) so layout tests can assert the Play/Pause max-reservation contract.
        internal static float ButtonWidth(string localizationKey, SystemLanguage language = SystemLanguage.Japanese)
        {
            string label = FaceMotionUiText.Get(localizationKey, language);
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
        private readonly bool _includePlayback;
        private float _x;
        private float _y;
        private bool _hasRow;

        public PreviewControlsLayout(Rect bounds, bool includePlayback = true)
        {
            _bounds = bounds;
            _includePlayback = includePlayback;
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
            if (_includePlayback)
            {
                float hintWidth = _bounds.xMax - _x;
                if (hintWidth >= PreviewPanel.PreviewHintMinimumWidth)
                {
                    Hint = new Rect(_x, _y, hintWidth, PreviewPanel.ControlHeight);
                }
            }

            // Required height: bottom-most placed control yMax relative to the bounds top,
            // plus one spacing of bottom padding. No fixed two-row assumption.
            Height = _hasRow
                ? _y + PreviewPanel.ControlHeight - _bounds.y + PreviewPanel.ControlSpacing
                : 0f;
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
