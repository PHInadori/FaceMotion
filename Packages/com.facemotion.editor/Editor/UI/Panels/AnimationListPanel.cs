using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    public sealed class AnimationListPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly AnimationController _animation;
        private bool _renameActive;
        private string _renameBuffer;
        private string _settingsAnimationId;
        private float _durationBuffer;
        private float _frameRateBuffer;
        private bool _loopBuffer;

        public AnimationListPanel(FaceMotionEditorSession session, AnimationController animation)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
            _animation = animation ?? throw new System.ArgumentNullException(nameof(animation));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("animations"), EditorStyles.boldLabel);

            // Unity can refresh a serialized project while this IMGUI event is drawing. Render a
            // stable snapshot so a list shrink cannot invalidate an already observed count.
            IReadOnlyList<FaceMotionAnimationData> list = Snapshot(_session.ActiveProject?.Animations);
            int count = list.Count;

            for (int i = 0; i < count; i++)
            {
                var anim = list[i];
                if (anim == null)
                {
                    continue;
                }

                bool isSelected = string.Equals(anim.AnimationId, _session.SelectedAnimationId, System.StringComparison.Ordinal);
                EditorGUILayout.BeginHorizontal();

                string label = !string.IsNullOrEmpty(anim.DisplayName) ? anim.DisplayName : FaceMotionUiText.Get("unnamed");
                if (GUILayout.Button(label, isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft))
                {
                    _animation.Select(anim.AnimationId);
                    _renameActive = false;
                }

                if (GUILayout.Button(FaceMotionUiText.Get("duplicate"), EditorStyles.miniButtonMid, GUILayout.Width(38)))
                {
                    _animation.Select(anim.AnimationId);
                    _animation.Duplicate();
                }

                if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                {
                    if (EditorUtility.DisplayDialog(FaceMotionUiText.Get("deleteAnimation"), string.Format(FaceMotionUiText.Get("deleteAnimationConfirm"), label), FaceMotionUiText.Get("delete"), FaceMotionUiText.Get("cancel")))
                    {
                        _animation.Select(anim.AnimationId);
                        _animation.Delete();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceMotionUiText.Get("newAnimation"), GUILayout.Width(140)))
            {
                _animation.Add();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("selected"), _session.SelectedAnimationId ?? FaceMotionUiText.Get("none"));

            if (_session.SelectedAnimationId != null)
            {
                DrawTimelineSettings();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(FaceMotionUiText.Get("rename"), EditorStyles.miniButton))
                {
                    _renameActive = true;
                    _renameBuffer = _session.GetSelectedAnimation()?.DisplayName ?? string.Empty;
                }

                if (GUILayout.Button(FaceMotionUiText.Get("delete"), EditorStyles.miniButton))
                {
                    _animation.Delete();
                }

                EditorGUILayout.EndHorizontal();

                if (_renameActive)
                {
                    EditorGUILayout.BeginHorizontal();
                    _renameBuffer = EditorGUILayout.TextField(_renameBuffer);
                    if (GUILayout.Button(FaceMotionUiText.Get("ok"), GUILayout.Width(36)))
                    {
                        _animation.Rename(_renameBuffer);
                        _renameActive = false;
                    }

                    if (GUILayout.Button(FaceMotionUiText.Get("cancel"), GUILayout.Width(56)))
                    {
                        _renameActive = false;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        internal static IReadOnlyList<FaceMotionAnimationData> Snapshot(IReadOnlyList<FaceMotionAnimationData> source)
        {
            return source == null
                ? new List<FaceMotionAnimationData>()
                : new List<FaceMotionAnimationData>(source);
        }

        private void DrawTimelineSettings()
        {
            var selected = _session.GetSelectedAnimation();
            if (selected == null || selected.Timeline == null)
            {
                return;
            }

            if (!string.Equals(_settingsAnimationId, selected.AnimationId, System.StringComparison.Ordinal))
            {
                _settingsAnimationId = selected.AnimationId;
                _durationBuffer = selected.Timeline.Duration;
                _frameRateBuffer = selected.Timeline.FrameRate;
                _loopBuffer = selected.Timeline.Loop;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("timelineSettings"), EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _durationBuffer = EditorGUILayout.FloatField(FaceMotionUiText.Get("duration"), _durationBuffer);
            if (GUILayout.Button(FaceMotionUiText.Get("apply"), GUILayout.Width(48f)))
            {
                if (_animation.SetDuration(_durationBuffer))
                {
                    _durationBuffer = _session.GetSelectedDuration();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _frameRateBuffer = EditorGUILayout.FloatField(FaceMotionUiText.Get("frameRate"), _frameRateBuffer);
            if (GUILayout.Button(FaceMotionUiText.Get("apply"), GUILayout.Width(48f)))
            {
                if (_animation.SetFrameRate(_frameRateBuffer))
                {
                    _frameRateBuffer = _session.GetSelectedFrameRate();
                }
            }

            EditorGUILayout.EndHorizontal();

            bool loop = EditorGUILayout.Toggle(FaceMotionUiText.Get("loop"), _loopBuffer);
            if (loop != _loopBuffer)
            {
                _loopBuffer = loop;
                _animation.SetLoop(loop);
            }
        }
    }
}
