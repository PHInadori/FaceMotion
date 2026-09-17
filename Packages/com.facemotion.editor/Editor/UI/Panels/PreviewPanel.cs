using FaceMotion.Editor.Preview;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
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
        private readonly FaceMotionEditorSession _session;
        private readonly PreviewSession _preview;
        private readonly SceneApplySession _sceneApply;
        private readonly PreviewPlaybackController _playback;
        private int _lastOverrideChangeCount;

        public PreviewPanel(FaceMotionEditorSession session, PreviewSession preview, SceneApplySession sceneApply, PreviewPlaybackController playback)
        {
            _session = session;
            _preview = preview;
            _sceneApply = sceneApply;
            _playback = playback;
        }

        public void OnGUI(Rect assignedRect, bool textControlOwnsKeyboard = false)
        {
            var layout = CalculateLayout(assignedRect);
            GUI.Label(layout.HeaderRect, FaceMotionUiText.Get("preview"), EditorStyles.boldLabel);
            GUI.Label(new Rect(layout.HeaderRect.x + 65f, layout.HeaderRect.y, layout.HeaderRect.width - 65f, HeaderHeight), FaceMotionUiText.Get("previewIsolation"), EditorStyles.miniLabel);
            if (_session.ActiveAvatarRoot == null)
            {
                EditorGUI.HelpBox(layout.RenderRect, FaceMotionUiText.Get("selectAvatarToPreview"), MessageType.Info);
                return;
            }

            if (GUI.Button(new Rect(layout.ControlsRect.x, layout.ControlsRect.y, 110f, 20f), _preview.IsActive ? FaceMotionUiText.Get("rebuildPreview") : FaceMotionUiText.Get("startPreview")))
            {
                _preview.RebuildAvatar(_session.ActiveAvatarRoot);
                _preview.Evaluate(_session.GetSelectedAnimation(), _session.ViewState.CurrentTime);
            }

            if (GUI.Button(new Rect(layout.ControlsRect.x + 114f, layout.ControlsRect.y, 90f, 20f), FaceMotionUiText.Get("stopPreview")))
            {
                _preview.Dispose();
            }

            if (GUI.Button(new Rect(layout.ControlsRect.x + 208f, layout.ControlsRect.y, 110f, 20f), _sceneApply.IsActive ? FaceMotionUiText.Get("stopSceneApply") : FaceMotionUiText.Get("applyToScene")))
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
                float buttonX = layout.ControlsRect.x;
                float buttonY = layout.ControlsRect.y + 24f;
                EditorGUI.BeginDisabledGroup(!_playback.CanPlay);
                if (GUI.Button(new Rect(buttonX, buttonY, 52f, 20f), FaceMotionUiText.Get("previewPlay")))
                {
                    _playback.Play();
                }

                buttonX += 54f;
                if (GUI.Button(new Rect(buttonX, buttonY, 52f, 20f), FaceMotionUiText.Get("previewPause")))
                {
                    _playback.Pause();
                }

                buttonX += 54f;
                if (GUI.Button(new Rect(buttonX, buttonY, 52f, 20f), FaceMotionUiText.Get("previewStop")))
                {
                    _playback.Stop();
                }

                EditorGUI.EndDisabledGroup();
                buttonX += 54f;
                if (GUI.Button(new Rect(buttonX, buttonY, 60f, 20f), FaceMotionUiText.Get("fitAvatar")))
                {
                    fit = true;
                }

                buttonX += 64f;
                if (GUI.Button(new Rect(buttonX, buttonY, 60f, 20f), FaceMotionUiText.Get("resetView")))
                {
                    reset = true;
                }

                buttonX += 64f;
                GUI.Label(new Rect(buttonX, buttonY, Mathf.Max(0f, layout.ControlsRect.xMax - buttonX), 20f), FaceMotionUiText.Get("previewControls"), EditorStyles.miniLabel);
                Rect previewRect = layout.RenderRect;
                if (fit)
                {
                    _preview.FitCamera(previewRect);
                }

                if (reset)
                {
                    _preview.ResetCamera();
                }

                if (_lastOverrideChangeCount != _preview.Override.ChangeCount)
                {
                    _lastOverrideChangeCount = _preview.Override.ChangeCount;
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

        public static PreviewPanelLayout CalculateLayout(Rect assignedRect)
        {
            float width = Mathf.Max(0f, assignedRect.width - Padding * 2f);
            var header = new Rect(assignedRect.x + Padding, assignedRect.y + Padding, width, HeaderHeight);
            var controls = new Rect(header.x, header.yMax + Padding, width, ControlsHeight);
            var render = new Rect(controls.x, controls.yMax + Padding, width, Mathf.Max(0f, assignedRect.yMax - (controls.yMax + Padding)));
            return new PreviewPanelLayout(header, controls, render);
        }
    }

    public readonly struct PreviewPanelLayout
    {
        public PreviewPanelLayout(Rect headerRect, Rect controlsRect, Rect renderRect) { HeaderRect = headerRect; ControlsRect = controlsRect; RenderRect = renderRect; }
        public Rect HeaderRect { get; }
        public Rect ControlsRect { get; }
        public Rect RenderRect { get; }
    }
}
