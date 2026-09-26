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
        internal const string EditableControlPrefix = "FaceMotion.AnimationList.";

        private const string RenameControlId = "rename";
        private const string DurationControlId = "duration";
        private const string FrameRateControlId = "frameRate";

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

            for (int i = 0; i < count; i++)
            {
                var anim = list[i];
                if (anim == null)
                {
                    continue;
                }

                bool isSelected = string.Equals(anim.AnimationId, _session.SelectedAnimationId, System.StringComparison.Ordinal);
                EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box", GUILayout.MinHeight(EditorGUIUtility.singleLineHeight + 4f));

                string label = !string.IsNullOrEmpty(anim.DisplayName) ? anim.DisplayName : FaceMotionUiText.Get("unnamed");
                string editingLabel = RowLabel(label, isSelected);
                bool selectClicked = GUILayout.Button(editingLabel, EditorStyles.miniButtonLeft);
                if (selectClicked)
                {
                    SelectAnimation(anim.AnimationId);
                    _renameAnimationId = null;
                }

                if (GUILayout.Button(FaceMotionUiText.Get("duplicate"), EditorStyles.miniButtonMid, GUILayout.Width(38)))
                {
                    SelectAnimation(anim.AnimationId);
                    _animation.Duplicate();
                }

                if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                {
                    if (EditorUtility.DisplayDialog(FaceMotionUiText.Get("deleteAnimation"), string.Format(FaceMotionUiText.Get("deleteAnimationConfirm"), label), FaceMotionUiText.Get("delete"), FaceMotionUiText.Get("cancel")))
                    {
                        SelectAnimation(anim.AnimationId);
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
            else if (_renameAnimationId != null || _settingsAnimationId != null)
            {
                // The selection was cleared while a field may still own the keyboard.
                // Drop the stale identity so the next selection reloads cleanly.
                _renameAnimationId = null;
                _settingsAnimationId = null;
                ReleaseTextFocus();
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

                // A deliberate animation switch must never retain a focused field
                // from the prior animation (its pending text would repaint into
                // this animation's field).
                ReleaseTextFocus();
            }

            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(EditableControlPrefix + RenameControlId);
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

        /// <summary>Row label for the current editing animation; presentational so tests can assert it without GUI.</summary>
        internal static string RowLabel(string displayName, bool isSelected)
        {
            string label = string.IsNullOrEmpty(displayName) ? FaceMotionUiText.Get("unnamed") : displayName;
            return isSelected ? label + "  [" + FaceMotionUiText.Get("editing") + "]" : label;
        }

        private void DrawTimelineSettings()
        {
            var selected = _session.GetSelectedAnimation();
            if (selected == null || selected.Timeline == null)
            {
                _settingsAnimationId = null;
                ReleaseFocusForMissingSettings(selected);
                return;
            }

            SynchronizeTimelineSettings();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("timelineSettings"), EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(EditableControlPrefix + DurationControlId);
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
            GUI.SetNextControlName(EditableControlPrefix + FrameRateControlId);
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

        /// <summary>Reloads pending settings only when their stable animation identity changes.</summary>
        internal void SynchronizeTimelineSettings()
        {
            var selected = _session.GetSelectedAnimation();
            if (selected == null || selected.Timeline == null)
            {
                _settingsAnimationId = null;
                ReleaseFocusForMissingSettings(selected);
                return;
            }

            if (!string.Equals(_settingsAnimationId, selected.AnimationId, System.StringComparison.Ordinal))
            {
                _settingsAnimationId = selected.AnimationId;
                _durationBuffer = selected.Timeline.Duration;
                _frameRateBuffer = selected.Timeline.FrameRate;
                _loopBuffer = selected.Timeline.Loop;

                // A deliberate animation switch must never retain a focused settings
                // field from the prior animation (its pending text would repaint into
                // this animation's field).
                ReleaseSettingsTextFocus();
            }
        }

        /// <summary>
        /// Releases keyboard focus after the settings fields stopped being drawn.
        /// With no selected animation every field of this panel is gone; while only
        /// the timeline data is missing (legacy animations) the rename field stays
        /// live, so only settings focus is released.
        /// </summary>
        private void ReleaseFocusForMissingSettings(FaceMotionAnimationData selected)
        {
            if (selected == null)
            {
                ReleaseTextFocus();
            }
            else
            {
                ReleaseSettingsTextFocus();
            }
        }

        /// <summary>True when the focused IMGUI control is a text field owned by this panel.</summary>
        internal static bool OwnsTextFocus(string focusedControlName)
        {
            return !string.IsNullOrEmpty(focusedControlName)
                && focusedControlName.StartsWith(EditableControlPrefix, System.StringComparison.Ordinal);
        }

        /// <summary>True when the focused IMGUI control is one of this panel's timeline-settings fields.</summary>
        internal static bool OwnsSettingsTextFocus(string focusedControlName)
        {
            return string.Equals(focusedControlName, EditableControlPrefix + DurationControlId, System.StringComparison.Ordinal)
                || string.Equals(focusedControlName, EditableControlPrefix + FrameRateControlId, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Releases keyboard focus owned by this panel so an in-progress edit from a
        /// previously selected animation can never repaint into another animation's
        /// field. Foreign focus is left untouched.
        /// </summary>
        internal void ReleaseTextFocus()
        {
            ReleaseTextFocus(GUI.GetNameOfFocusedControl());
        }

        /// <summary>Test seam: performs the release decision for a known focused-control name.</summary>
        internal void ReleaseTextFocus(string focusedControlName)
        {
            if (!OwnsTextFocus(focusedControlName))
            {
                return;
            }

            ClearOwnedTextFocus();
        }

        private void ReleaseSettingsTextFocus()
        {
            ReleaseSettingsTextFocus(GUI.GetNameOfFocusedControl());
        }

        private void ReleaseSettingsTextFocus(string focusedControlName)
        {
            if (!OwnsSettingsTextFocus(focusedControlName))
            {
                return;
            }

            ClearOwnedTextFocus();
        }

        private static void ClearOwnedTextFocus()
        {
            // GUI.FocusControl ends the native editing state on the next draw of the
            // old control; the direct assignments make the release effective for this
            // event already (stale repaint text and shortcut gating included).
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        private void SelectAnimation(string animationId)
        {
            if (!string.Equals(_session.SelectedAnimationId, animationId, System.StringComparison.Ordinal))
            {
                // Discard unapplied fields before the next animation is rendered.
                _settingsAnimationId = null;
            }

            _animation.Select(animationId);
        }
    }
}
