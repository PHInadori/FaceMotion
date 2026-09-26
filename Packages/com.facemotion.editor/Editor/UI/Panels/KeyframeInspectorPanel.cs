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
        private float _loadedTime = 0f;
        private float _floatValue = DefaultBlendShapeAuthoringValue;
        private Vector3 _vectorValue = Vector3.zero;
        private int _interpolationIndex = (int)InterpolationType.Linear;
        private int _loadedSessionVersion = -1;
        private string _autoApplyControlName;
        private string _autoApplyKeyId;
        private int _autoApplyUndoGroup = -1;

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
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("keyInspector"), EditorStyles.boldLabel);
            if (GUILayout.Button(FaceMotionUiText.Get("keyActions"), EditorStyles.miniButton, GUILayout.Width(72f)))
            {
                ShowKeyActionsMenu();
            }

            EditorGUILayout.EndHorizontal();

            Synchronize();

            if (!OwnsKeyboardFocus())
            {
                ResetAutoApplyUndoSession();
            }

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

            DrawCurrentTrackAndAddKey();

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
                EditorGUILayout.BeginHorizontal();
                _time = DrawFloatField(
                    "time",
                    FaceMotionUiText.Get("time"),
                    _time);
                using (new EditorGUI.DisabledScope(Mathf.Approximately(_time, _loadedTime)))
                {
                    if (GUILayout.Button(FaceMotionUiText.Get("moveKey"), GUILayout.Width(64f)))
                    {
                        ApplySelectedTime(inspected);
                    }
                }

                EditorGUILayout.EndHorizontal();

                var track = selectedTrack;

                if (track != null)
                {
                    if (track.Kind == TrackKind.BlendShape)
                    {
                        EditorGUI.BeginChangeCheck();
                        float value = DrawFloatField(
                            "blendShape",
                            FaceMotionUiText.Get("blendShape"),
                            _floatValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _floatValue = Mathf.Clamp(value, 0f, 100f);
                            AutoApplySelectedValue(inspected, track);
                        }
                    }
                    else if (TrackKinds.IsTransform(track.Kind))
                    {
                        EditorGUILayout.LabelField(
                            FaceMotionUiText.Get("value"));

                        EditorGUI.indentLevel++;

                        EditorGUI.BeginChangeCheck();
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

                        if (EditorGUI.EndChangeCheck())
                        {
                            AutoApplySelectedValue(inspected, track);
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    int interpolation = EditorGUILayout.Popup(
                            FaceMotionUiText.Get("interpolation"),
                            _interpolationIndex,
                            InterpolationNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _interpolationIndex = interpolation;
                        AutoApplySelectedInterpolation(inspected, track);
                    }
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    new GUIContent(
                        FaceMotionUiText.Get("selectAll"),
                        FaceMotionUiText.Get("selectAll")),
                    EditorStyles.miniButtonLeft,
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

        private void ShowKeyActionsMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("copyKeys")), false, _keys.CopySelection);
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("pasteKeys")), false, () => _keys.PasteAt(_session.ViewState.CurrentTime));
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("duplicateKeys")), false, () => _keys.DuplicateSelection());
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("nudgeKeysEarlier")), false, () => _keys.NudgeSelectedKeys(-1));
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("nudgeKeysLater")), false, () => _keys.NudgeSelectedKeys(1));
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("nudgeKeysEarlierFive")), false, () => _keys.NudgeSelectedKeys(-5));
            menu.AddItem(new GUIContent(FaceMotionUiText.Get("nudgeKeysLaterFive")), false, () => _keys.NudgeSelectedKeys(5));
            menu.ShowAsContext();
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

        private void DrawCurrentTrackAndAddKey()
        {
            FaceTrackData track = _session.GetSelectedTrack();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("selectedTrack"), EditorStyles.miniLabel);

            if (track == null)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("noTrackSelected"), EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("+ " + FaceMotionUiText.Get("addKey"), GUILayout.Height(24f));
                }
            }
            else
            {
                EditorGUILayout.LabelField(GetTrackDisplayName(track), EditorStyles.boldLabel);
                if (GUILayout.Button("+ " + FaceMotionUiText.Get("addKey"), GUILayout.Height(24f)))
                {
                    AddKeyFromInspector();
                }
            }

            EditorGUILayout.EndVertical();
        }

        internal bool AddKeyFromInspector()
        {
            if (_session.GetSelectedTrack() == null)
            {
                return false;
            }

            string created = _keys.AddKeyAtCurrentTime(
                _floatValue,
                _vectorValue,
                (InterpolationType)_interpolationIndex);
            if (created == null)
            {
                return false;
            }

            _inspectedKeyId = created;
            LoadFields(created);
            return true;
        }

        internal static string GetTrackDisplayName(FaceTrackData track)
        {
            if (track == null)
            {
                return FaceMotionUiText.Get("noTrackSelected");
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                return FaceMotionUiText.Get("trackBlendShape") + ": " + track.BlendShape.BlendShapeName;
            }

            if (track.Transform != null)
            {
                string channel = track.Kind == TrackKind.TransformPosition
                    ? FaceMotionUiText.Get("trackPosition")
                    : track.Kind == TrackKind.TransformRotation
                        ? FaceMotionUiText.Get("trackRotation")
                        : FaceMotionUiText.Get("trackScale");
                return channel + ": " + LastSegment(track.Transform.TransformPath);
            }

            return FaceMotionUiText.Get("noTrackSelected");
        }

        internal bool AutoApplySelectedValue(string keyId, FaceTrackData track)
        {
            return AutoApplySelectedValue(keyId, track, GUI.GetNameOfFocusedControl());
        }

        internal bool AutoApplySelectedValue(string keyId, FaceTrackData track, string focusedControlName)
        {
            if (keyId == null || track == null ||
                !IsFinite(_floatValue) || !IsFinite(_vectorValue))
            {
                return false;
            }

            if (track.Kind == TrackKind.BlendShape)
            {
                _floatValue = Mathf.Clamp(_floatValue, 0f, 100f);
            }

            bool applied = _keys.SetKeyValue(keyId, _floatValue, _vectorValue);
            CompleteAutoApply(keyId, applied, focusedControlName);
            return applied;
        }

        internal bool AutoApplySelectedInterpolation(string keyId, FaceTrackData track)
        {
            if (keyId == null || track == null)
            {
                return false;
            }

            bool applied = _keys.SetKeyInterpolation(keyId, (InterpolationType)_interpolationIndex);
            CompleteAutoApply(keyId, applied, GUI.GetNameOfFocusedControl());
            return applied;
        }

        private void CompleteAutoApply(string keyId, bool applied, string focused)
        {
            if (!applied)
            {
                return;
            }

            if (!OwnsKeyboardFocus(focused))
            {
                ResetAutoApplyUndoSession();
                return;
            }

            int currentGroup = Undo.GetCurrentGroup();
            if (string.Equals(_autoApplyControlName, focused, StringComparison.Ordinal) &&
                string.Equals(_autoApplyKeyId, keyId, StringComparison.Ordinal) &&
                _autoApplyUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_autoApplyUndoGroup);
            }
            else
            {
                _autoApplyControlName = focused;
                _autoApplyKeyId = keyId;
                _autoApplyUndoGroup = currentGroup;
            }
        }

        private void ResetAutoApplyUndoSession()
        {
            _autoApplyControlName = null;
            _autoApplyKeyId = null;
            _autoApplyUndoGroup = -1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
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
                _loadedTime = _time;

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

            _loadedTime = _time;
        }

        internal bool ApplySelectedTime(string keyId)
        {
            if (keyId == null || !IsFinite(_time) || Mathf.Approximately(_time, _loadedTime))
            {
                return false;
            }

            bool moved = _keys.SetKeyTime(keyId, _time);
            LoadFields(keyId);
            return moved;
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

    }
}
