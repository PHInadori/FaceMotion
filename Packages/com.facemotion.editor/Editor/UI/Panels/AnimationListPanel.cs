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
        private string _renameBuffer;
        private string _renameAnimationId;
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

            if (count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(FaceMotionUiText.Get("batchSelectAll"), EditorStyles.miniButtonLeft)) _session.SelectAllBatchAnimations();
                if (GUILayout.Button(FaceMotionUiText.Get("batchClearAll"), EditorStyles.miniButtonRight)) _session.ClearBatchSelection();
                EditorGUILayout.EndHorizontal();
            }

            for (int i = 0; i < count; i++)
            {
                var anim = list[i];
                if (anim == null)
                {
                    continue;
                }

                bool isSelected = string.Equals(anim.AnimationId, _session.SelectedAnimationId, System.StringComparison.Ordinal);
                EditorGUILayout.BeginHorizontal();

                bool batchSelected = EditorGUILayout.Toggle(_session.IsBatchSelected(anim.AnimationId), GUILayout.Width(18f));
                if (batchSelected != _session.IsBatchSelected(anim.AnimationId)) _session.SetBatchSelected(anim.AnimationId, batchSelected);

                string label = !string.IsNullOrEmpty(anim.DisplayName) ? anim.DisplayName : FaceMotionUiText.Get("unnamed");
                if (GUILayout.Button(label, isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft))
                {
                    _animation.Select(anim.AnimationId);
                    _renameAnimationId = null;
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

            if (count == 0)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("emptyAnimations"), MessageType.Info);
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
                DrawAnimationName();
                DrawTimelineSettings();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(FaceMotionUiText.Get("delete"), EditorStyles.miniButton))
                {
                    _animation.Delete();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawAnimationName()
        {
            var selected = _session.GetSelectedAnimation();
            if (selected == null) return;
            if (!string.Equals(_renameAnimationId, selected.AnimationId, System.StringComparison.Ordinal))
            {
                _renameAnimationId = selected.AnimationId;
                _renameBuffer = selected.DisplayName ?? string.Empty;
            }

            EditorGUILayout.BeginHorizontal();
            _renameBuffer = EditorGUILayout.TextField(new GUIContent(FaceMotionUiText.Get("animationName"), FaceMotionUiText.Get("tooltipAnimationName")), _renameBuffer);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_renameBuffer) || string.Equals(_renameBuffer.Trim(), selected.DisplayName, System.StringComparison.Ordinal)))
            {
                if (GUILayout.Button(FaceMotionUiText.Get("apply"), GUILayout.Width(48f)))
                {
                    if (_animation.Rename(_renameBuffer)) _renameBuffer = _session.GetSelectedAnimation()?.DisplayName ?? _renameBuffer;
                }
            }
            EditorGUILayout.EndHorizontal();
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
            _durationBuffer = EditorGUILayout.FloatField(new GUIContent(FaceMotionUiText.Get("duration"), FaceMotionUiText.Get("tooltipDuration")), _durationBuffer);
            if (GUILayout.Button(FaceMotionUiText.Get("apply"), GUILayout.Width(48f)))
            {
                if (_animation.SetDuration(_durationBuffer))
                {
                    _durationBuffer = _session.GetSelectedDuration();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _frameRateBuffer = EditorGUILayout.FloatField(new GUIContent(FaceMotionUiText.Get("frameRate"), FaceMotionUiText.Get("tooltipFrameRate")), _frameRateBuffer);
            if (GUILayout.Button(FaceMotionUiText.Get("apply"), GUILayout.Width(48f)))
            {
                if (_animation.SetFrameRate(_frameRateBuffer))
                {
                    _frameRateBuffer = _session.GetSelectedFrameRate();
                }
            }

            EditorGUILayout.EndHorizontal();

            bool loop = EditorGUILayout.Toggle(new GUIContent(FaceMotionUiText.Get("loop"), FaceMotionUiText.Get("tooltipLoop")), _loopBuffer);
            if (loop != _loopBuffer)
            {
                _loopBuffer = loop;
                _animation.SetLoop(loop);
            }
        }
    }
}
