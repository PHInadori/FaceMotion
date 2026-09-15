using System;
using FaceMotion.Data;
using FaceMotion.Editor.Diagnostics;
using FaceMotion.Editor.Preview;
using FaceMotion.Editor.UI.Localization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Preview
{
    /// <summary>Owns the preview renderer and its isolated avatar clone for one editor window.</summary>
    public sealed class PreviewSession : IDisposable
    {
        private PreviewAvatarClone _clone;
        private PreviewObjectCache _cache;
        private PreviewBaseline _baseline;
        private PreviewRenderUtility _renderer;
        private GameObject _source;
        private Bounds _bounds;
        private bool _hasBounds;
        private readonly PreviewCameraState _camera = new PreviewCameraState();

        public bool IsActive => _clone != null && _clone.Root != null;
        public string Diagnostic { get; private set; }
        internal int CloneCreationCount { get; private set; }
        internal int EvaluateCallCount { get; private set; }
        internal int RenderCallCount { get; private set; }

        public void EnsureAvatar(GameObject source)
        {
            if (ReferenceEquals(source, _source) && IsActive)
            {
                return;
            }

            Dispose();
            if (source == null)
            {
                return;
            }

            _source = source;
            _clone = new PreviewAvatarClone(source);
            CloneCreationCount++;
            _cache = new PreviewObjectCache(_clone.Root);
            _baseline = new PreviewBaseline();
            _renderer = new PreviewRenderUtility();
            _renderer.AddSingleGO(_clone.Root);
            _renderer.camera.clearFlags = CameraClearFlags.Color;
            _renderer.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            _hasBounds = PreviewCameraState.TryCalculateBounds(_clone.Root, out _bounds);
            _camera.Reset(_bounds);
            _camera.ApplyTo(_renderer.camera);
            Diagnostic = _hasBounds ? null : FaceMotionUiText.Get("previewNoRenderers");
        }

        public void RebuildAvatar(GameObject source)
        {
            Dispose();
            EnsureAvatar(source);
        }

        public void Evaluate(FaceMotionAnimationData animation, float time)
        {
            EvaluateCallCount++;
            FaceMotionPreviewTrace.Trace(
                "E.Evaluate",
                "callCount={0} animationId={1} time={2} active={3} event={4}",
                EvaluateCallCount,
                animation == null ? "<null>" : animation.AnimationId,
                time,
                IsActive,
                Event.current == null ? "<none>" : Event.current.type.ToString());
            if (IsActive)
            {
                PreviewMotionApplier.Apply(animation, _cache, _baseline, time);
            }
        }

        public void Draw(Rect rect)
        {
            if (!IsActive || rect.width < 1f || rect.height < 1f)
            {
                return;
            }

            RenderCallCount++;
            string eventType = Event.current == null ? "<none>" : Event.current.type.ToString();
            Texture texture = null;
            _renderer.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            _renderer.BeginPreview(rect, GUIStyle.none);
            try
            {
                FaceMotionPreviewTrace.Trace("G.Render", "renderCall={0} event={1}", RenderCallCount, eventType);
                _renderer.camera.Render();
            }
            finally
            {
                texture = _renderer.EndPreview();
            }

            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            FaceMotionPreviewTrace.Trace(
                "G.Draw",
                "renderCall={0} event={1} textureId={2}",
                RenderCallCount,
                eventType,
                texture == null ? 0 : texture.GetInstanceID());
        }

        public void FitCamera(Rect rect)
        {
            if (IsActive)
            {
                _hasBounds = PreviewCameraState.TryCalculateBounds(_clone.Root, out _bounds);
                _camera.Fit(_bounds, _renderer.camera.fieldOfView, rect.width / Mathf.Max(1f, rect.height));
                _camera.ApplyTo(_renderer.camera);
            }
        }

        public void ResetCamera()
        {
            if (IsActive)
            {
                _hasBounds = PreviewCameraState.TryCalculateBounds(_clone.Root, out _bounds);
                _camera.Reset(_bounds);
                _camera.ApplyTo(_renderer.camera);
            }
        }

        public void HandleCameraInput(Rect rect)
        {
            if (!IsActive || !rect.Contains(Event.current.mousePosition))
            {
                return;
            }

            Event current = Event.current;
            if (current.type == EventType.ScrollWheel)
            {
                _camera.Zoom(current.delta.y);
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && !current.alt)
            {
                _camera.Orbit(current.delta);
            }
            else if (current.type == EventType.MouseDrag && (current.button == 2 || (current.button == 0 && current.alt)))
            {
                _camera.Pan(current.delta, rect.height);
            }
            else
            {
                return;
            }

            _camera.ApplyTo(_renderer.camera);
            current.Use();
        }

        public void Dispose()
        {
            _baseline?.Restore();
            _renderer?.Cleanup();
            _renderer = null;
            _cache = null;
            _baseline = null;
            _clone?.Dispose();
            _clone = null;
            _source = null;
            _hasBounds = false;
            Diagnostic = null;
        }
    }
}
