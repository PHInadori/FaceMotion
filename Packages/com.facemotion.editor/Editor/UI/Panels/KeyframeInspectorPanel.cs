using System;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Timeline;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    public sealed class KeyframeInspectorPanel
    {
        internal const string EditableControlPrefix = "FaceMotion.KeyInspector.";
        internal const float DefaultBlendShapeAuthoringValue = 100f;
        internal const float MinimumQuickKeyValueWidth = 72f;
        internal const float QuickKeyControlSpacing = 6f;

        private readonly FaceMotionEditorSession _session;
        private readonly KeyframeController _keys;

        private string _inspectedTrackId;
        private string _inspectedKeyId;
        private float _time = 0f;
        private float _floatValue = DefaultBlendShapeAuthoringValue;
        private Vector3 _vectorValue = Vector3.zero;
        private int _interpolationIndex = (int)InterpolationType.Linear;
        private int _loadedSessionVersion = -1;

        public KeyframeInspectorPanel(
            FaceMotionEditorSession session,
            KeyframeController keys)
        {
            _session = session
                ?? throw new ArgumentNullException(nameof(session));

            _keys = keys
                ?? throw new ArgumentNullException(nameof(keys));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(
                FaceMotionUiText.Get("keyInspector"),
                EditorStyles.boldLabel);

            Synchronize();

            int selectionCount = _session.Selection.Count;

            // K5:
            // Multiple selected keys are intentionally read-only in the inspector.
            // Bulk value/time/interpolation editing is not supported.
            if (selectionCount >= 2)
            {
                GUI.FocusControl(null);

                EditorGUILayout.HelpBox(
                    string.Format(
                        FaceMotionUiText.Get("multipleKeysSelected"),
                        selectionCount),
                    MessageType.Info);

                return;
            }

            string inspected = GetOnlySelectedKeyId();
            var selectedTrack = FindTrackForKey(inspected);

            if (inspected == null || selectedTrack == null)
            {
                _time = DrawFloatField(
                    "time",
                    FaceMotionUiText.Get("time"),
                    _time);

                bool hasTrackSelected =
                    !string.IsNullOrEmpty(
                        _session.SelectedTrackId);

                EditorGUILayout.HelpBox(
                    FaceMotionUiText.Get(
                        hasTrackSelected
                            ? "emptyKeys"
                            : "noKeySelected"),
                    MessageType.Info);
            }
            else
            {
                _time = DrawFloatField(
                    "time",
                    FaceMotionUiText.Get("time"),
                    _time);

                var track = selectedTrack;

                if (track != null)
                {
                    if (track.Kind == TrackKind.BlendShape)
                    {
                        _floatValue = DrawFloatField(
                            "blendShape",
                            FaceMotionUiText.Get("blendShape"),
                            _floatValue);
                    }
                    else if (TrackKinds.IsTransform(track.Kind))
                    {
                        EditorGUILayout.LabelField(
                            FaceMotionUiText.Get("value"));

                        EditorGUI.indentLevel++;

                        _vectorValue.x = DrawFloatField(
                            "x",
                            FaceMotionUiText.Get("xLocal"),
                            _vectorValue.x);

                        _vectorValue.y = DrawFloatField(
                            "y",
                            FaceMotionUiText.Get("yLocal"),
                            _vectorValue.y);

                        _vectorValue.z = DrawFloatField(
                            "z",
                            FaceMotionUiText.Get("zLocal"),
                            _vectorValue.z);

                        EditorGUI.indentLevel--;
                    }

                    _interpolationIndex =
                        EditorGUILayout.Popup(
                            FaceMotionUiText.Get("interpolation"),
                            _interpolationIndex,
                            InterpolationNames);
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    new GUIContent(
                        FaceMotionUiText.Get("apply"),
                        FaceMotionUiText.Get("apply")),
                    EditorStyles.miniButtonLeft,
                    GUILayout.Height(22f)))
            {
                ApplyChanges(inspected);
            }

            if (GUILayout.Button(
                    new GUIContent(
                        FaceMotionUiText.Get("revert"),
                        FaceMotionUiText.Get("revert")),
                    EditorStyles.miniButtonRight,
                    GUILayout.Height(22f)))
            {
                LoadFields(inspected);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    new GUIContent(
                        FaceMotionUiText.Get("addKey"),
                        FaceMotionUiText.Get("addKey")),
                    EditorStyles.miniButtonLeft,
                    GUILayout.Height(22f)))
            {
                var created =
                    _keys.AddKeyAtCurrentTime(
                        _floatValue,
                        _vectorValue,
                        (InterpolationType)_interpolationIndex);

                if (created != null)
                {
                    _inspectedKeyId = created;
                    LoadFields(created);
                }
            }

            if (GUILayout.Button(
                    new GUIContent(
                        FaceMotionUiText.Get("selectAll"),
                        FaceMotionUiText.Get("selectAll")),
                    EditorStyles.miniButtonMid,
                    GUILayout.Height(22f)))
            {
                _keys.SelectAllKeys();
            }

            if (GUILayout.Button(
                    new GUIContent(
                        FaceMotionUiText.Get("delete"),
                        FaceMotionUiText.Get("delete")),
                    EditorStyles.miniButtonRight,
                    GUILayout.Height(22f)))
            {
                _keys.DeleteSelectedKeys();
                _inspectedKeyId = null;
            }

            EditorGUILayout.EndHorizontal();

            DrawQuickKeyControls();
        }

        /// <summary>
        /// Reloads the edit buffer after selection, drag, undo,
        /// or other session changes.
        /// </summary>
        internal void Synchronize()
        {
            string inspected =
                GetOnlySelectedKeyId();

            var track =
                FindTrackForKey(inspected) ??
                _session.GetSelectedTrack();

            string trackId =
                track == null
                    ? null
                    : track.TrackId;

            bool selectionChanged =
                !string.Equals(
                    trackId,
                    _inspectedTrackId,
                    StringComparison.Ordinal)
                ||
                !string.Equals(
                    inspected,
                    _inspectedKeyId,
                    StringComparison.Ordinal);

            if (selectionChanged)
            {
                // A deliberate key switch must never retain a focused field
                // or edit buffer from the prior key.
                GUI.FocusControl(null);

                LoadFields(
                    inspected,
                    track);
            }
            else if (
                _loadedSessionVersion != _session.Version &&
                !EditorGUIUtility.editingTextField)
            {
                LoadFields(
                    inspected,
                    track);
            }
        }

        internal static bool OwnsKeyboardFocus()
        {
            return OwnsKeyboardFocus(GUI.GetNameOfFocusedControl());
        }

        internal static bool OwnsKeyboardFocus(string focusedControlName)
        {
            return !string.IsNullOrEmpty(focusedControlName)
                && focusedControlName.StartsWith(
                    EditableControlPrefix,
                    StringComparison.Ordinal);
        }

        /// <summary>Shared Quick Key action used by the inspector button and Shortcut Manager.</summary>
        internal string ApplyQuickKey()
        {
            string keyId = _keys.ApplyQuickKey(QuickKeyPreferences.Value);
            if (keyId != null)
            {
                _inspectedKeyId = keyId;
                LoadFields(keyId);
            }

            return keyId;
        }

        private static float DrawFloatField(
            string controlId,
            string label,
            float value)
        {
            GUI.SetNextControlName(
                EditableControlPrefix + controlId);

            return EditorGUILayout.FloatField(
                label,
                value);
        }

        private static readonly string[] InterpolationNames =
        {
            FaceMotionUiText.Get("hold"),
            FaceMotionUiText.Get("linear"),
            FaceMotionUiText.Get("easeIn"),
            FaceMotionUiText.Get("easeOut"),
            FaceMotionUiText.Get("easeInOut"),
            FaceMotionUiText.Get("smooth")
        };

        private void DrawQuickKeyControls()
        {
            float configuredValue = QuickKeyPreferences.Value;
            bool canApply = _keys.CanApplyQuickKey();
            var valueContent = new GUIContent(
                FaceMotionUiText.Get("quickKeyValue"),
                FaceMotionUiText.Get("tooltipQuickKeyValue"));
            var quickKeyContent = new GUIContent(
                FaceMotionUiText.Get("quickKey"),
                canApply
                    ? FaceMotionUiText.Get("tooltipQuickKey")
                    : FaceMotionUiText.Get("tooltipQuickKeyBlendShapeOnly"));
            float buttonWidth = Mathf.Max(
                76f,
                EditorStyles.miniButton.CalcSize(quickKeyContent).x);
            float availableWidth = Mathf.Max(0f, EditorGUIUtility.currentViewWidth - 28f);

            if (ShouldStackQuickKeyControls(
                    availableWidth,
                    EditorStyles.label.CalcSize(valueContent).x,
                    buttonWidth))
            {
                DrawQuickKeyValue(valueContent, configuredValue);
                DrawQuickKeyButton(quickKeyContent, canApply, -1f);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawQuickKeyValue(valueContent, configuredValue);
            DrawQuickKeyButton(quickKeyContent, canApply, buttonWidth);
            EditorGUILayout.EndHorizontal();
        }

        internal static bool ShouldStackQuickKeyControls(
            float availableWidth,
            float valueLabelWidth,
            float buttonWidth)
        {
            return availableWidth < valueLabelWidth
                + MinimumQuickKeyValueWidth
                + buttonWidth
                + QuickKeyControlSpacing;
        }

        private static void DrawQuickKeyValue(GUIContent content, float configuredValue)
        {
            GUI.SetNextControlName(EditableControlPrefix + "quickKeyValue");
            float enteredValue = EditorGUILayout.FloatField(content, configuredValue);
            if (!Mathf.Approximately(enteredValue, configuredValue))
            {
                QuickKeyPreferences.Value = enteredValue;
            }
        }

        private void DrawQuickKeyButton(
            GUIContent content,
            bool canApply,
            float width)
        {
            using (new EditorGUI.DisabledScope(!canApply))
            {
                GUILayoutOption[] options = width > 0f
                    ? new[] { GUILayout.Width(width), GUILayout.Height(22f) }
                    : new[] { GUILayout.ExpandWidth(true), GUILayout.Height(22f) };
                if (GUILayout.Button(content, EditorStyles.miniButton, options))
                {
                    ApplyQuickKey();
                }
            }
        }

        private string GetOnlySelectedKeyId()
        {
            if (_session.Selection.Count != 1)
            {
                return null;
            }

            return _session.Selection.KeyIds[0];
        }

        private void LoadFields(
            string keyId)
        {
            LoadFields(
                keyId,
                FindTrackForKey(keyId));
        }

        private void LoadFields(
            string keyId,
            FaceTrackData track)
        {
            _inspectedTrackId =
                track == null
                    ? null
                    : track.TrackId;

            _inspectedKeyId =
                keyId;

            _loadedSessionVersion =
                _session.Version;

            if (keyId == null)
            {
                _time =
                    _session.ViewState.CurrentTime;
                _floatValue = DefaultBlendShapeAuthoringValue;
                _vectorValue = Vector3.zero;
                _interpolationIndex = (int)InterpolationType.Linear;

                return;
            }

            // A different key must never inherit an edit buffer
            // from the prior selection.
            _floatValue = DefaultBlendShapeAuthoringValue;
            _vectorValue = Vector3.zero;
            _interpolationIndex =
                (int)InterpolationType.Linear;

            if (track == null)
            {
                _time =
                    _session.ViewState.CurrentTime;

                return;
            }

            bool found = false;

            if (track.Kind == TrackKind.BlendShape &&
                track.BlendShape != null)
            {
                foreach (var key in track.BlendShape.Keys)
                {
                    if (key != null &&
                        string.Equals(
                            key.KeyId,
                            keyId,
                            StringComparison.Ordinal))
                    {
                        _time = key.Time;
                        _floatValue = key.Value;
                        _interpolationIndex =
                            (int)key.Interpolation;

                        found = true;
                        break;
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) &&
                     track.Transform != null)
            {
                foreach (var key in track.Transform.Keys)
                {
                    if (key != null &&
                        string.Equals(
                            key.KeyId,
                            keyId,
                            StringComparison.Ordinal))
                    {
                        _time = key.Time;
                        _vectorValue = key.Value;
                        _interpolationIndex =
                            (int)key.Interpolation;

                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                _time =
                    _session.ViewState.CurrentTime;
            }
        }

        private FaceTrackData FindTrackForKey(
            string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return null;
            }

            var animation =
                _session.GetSelectedAnimation();

            if (animation == null ||
                animation.Timeline == null)
            {
                return null;
            }

            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape &&
                    track.BlendShape != null)
                {
                    foreach (var key in track.BlendShape.Keys)
                    {
                        if (key != null &&
                            string.Equals(
                                key.KeyId,
                                keyId,
                                StringComparison.Ordinal))
                        {
                            return track;
                        }
                    }
                }
                else if (TrackKinds.IsTransform(track.Kind) &&
                         track.Transform != null)
                {
                    foreach (var key in track.Transform.Keys)
                    {
                        if (key != null &&
                            string.Equals(
                                key.KeyId,
                                keyId,
                                StringComparison.Ordinal))
                        {
                            return track;
                        }
                    }
                }
            }

            return null;
        }

        private void ApplyChanges(
            string keyId)
        {
            if (keyId == null)
            {
                return;
            }

            if (_keys.ApplyKeyEdits(
                    keyId,
                    _time,
                    _floatValue,
                    _vectorValue,
                    (InterpolationType)_interpolationIndex))
            {
                LoadFields(keyId);
            }
        }
    }
}
