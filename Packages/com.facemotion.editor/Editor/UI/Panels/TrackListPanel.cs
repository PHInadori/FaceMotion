using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Timeline;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    public sealed class TrackListPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly TrackController _tracks;

        private int _addMode;
        private bool _advancedBlend;
        private bool _advancedTransform;
        private string _rendererPath;
        private string _blendShapeName;
        private string _transformPath;
        private int _transformKind;
        private string _search = string.Empty;
        private string _blendCandidateSearch = string.Empty;
        private string _transformCandidateSearch = string.Empty;
        private string _lastBlendCandidateSearch;
        private string _lastTransformCandidateSearch;
        private AvatarCandidateSnapshot _candidateSource;
        private IReadOnlyList<AvatarCandidateSnapshot.BlendShapeCandidate> _blendCandidates;
        private IReadOnlyList<AvatarCandidateSnapshot.TransformCandidate> _transformCandidates;
        private Vector2 _blendCandidateScroll;
        private Vector2 _transformCandidateScroll;

        public TrackListPanel(FaceMotionEditorSession session, TrackController tracks)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("tracks"), EditorStyles.boldLabel);

            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAnimation"), MessageType.Info);
                return;
            }

            _search = EditorGUILayout.TextField(FaceMotionUiText.Get("search"), _search);
            DrawAddSection();
            EditorGUILayout.Space();

            Dictionary<string, TrackBindingValidation> bindings = BuildBindingMap();
            string selectedId = _session.SelectedTrackId;
            int index = 0;
            foreach (var track in SnapshotTracks(animation.Timeline.Tracks))
            {
                if (track == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_search) && !MatchesFilter(track, _search))
                {
                    continue;
                }

                bool isSelected = string.Equals(track.TrackId, selectedId, StringComparison.Ordinal);
                DrawTrackRow(track, isSelected, bindings);
                index++;
            }

            if (index == 0)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("noTracks"), EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawAddSection()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceMotionUiText.Get("addBlendShape"), EditorStyles.miniButtonLeft))
            {
                _addMode = _addMode == 1 ? 0 : 1;
            }

            if (GUILayout.Button(FaceMotionUiText.Get("addTransform"), EditorStyles.miniButtonRight))
            {
                _addMode = _addMode == 2 ? 0 : 2;
            }

            EditorGUILayout.EndHorizontal();

            if (_addMode == 1)
            {
                DrawBlendShapePicker();
            }
            else if (_addMode == 2)
            {
                _transformKind = EditorGUILayout.Popup(FaceMotionUiText.Get("kind"), _transformKind, new[] { FaceMotionUiText.Get("position"), FaceMotionUiText.Get("rotation"), FaceMotionUiText.Get("scale") });
                DrawTransformPicker();
            }
        }

        private void DrawBlendShapePicker()
        {
            if (!TryRefreshCandidateFilters())
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAvatarForBlend"), MessageType.Info);
                DrawBlendShapeFallback();
                return;
            }

            _blendCandidateSearch = EditorGUILayout.TextField(FaceMotionUiText.Get("search"), _blendCandidateSearch);
            RefreshBlendCandidatesIfNeeded();
            _blendCandidateScroll = EditorGUILayout.BeginScrollView(_blendCandidateScroll, GUILayout.Height(110f));
            for (int i = 0; i < _blendCandidates.Count; i++)
            {
                var candidate = _blendCandidates[i];
                if (candidate != null && GUILayout.Button(candidate.DisplayLabel, EditorStyles.miniButton))
                {
                    _tracks.AddBlendShapeTrack(candidate);
                    _addMode = 0;
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndScrollView();
            DrawBlendShapeFallback();
        }

        private void DrawTransformPicker()
        {
            if (!TryRefreshCandidateFilters())
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAvatarForTransform"), MessageType.Info);
                DrawTransformFallback();
                return;
            }

            _transformCandidateSearch = EditorGUILayout.TextField(FaceMotionUiText.Get("search"), _transformCandidateSearch);
            RefreshTransformCandidatesIfNeeded();
            _transformCandidateScroll = EditorGUILayout.BeginScrollView(_transformCandidateScroll, GUILayout.Height(110f));
            for (int i = 0; i < _transformCandidates.Count; i++)
            {
                var candidate = _transformCandidates[i];
                bool isRoot = candidate == null || string.IsNullOrEmpty(candidate.RelativePath);
                EditorGUI.BeginDisabledGroup(isRoot);
                if (candidate != null && GUILayout.Button(candidate.DisplayLabel, EditorStyles.miniButton))
                {
                    _tracks.AddTransformTrack(KindForIndex(_transformKind), candidate);
                    _addMode = 0;
                    GUIUtility.ExitGUI();
                }

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndScrollView();
            DrawTransformFallback();
        }

        private bool TryRefreshCandidateFilters()
        {
            AvatarCandidateSnapshot candidates = _session.Candidates;
            if (candidates == null)
            {
                _candidateSource = null;
                _blendCandidates = null;
                _transformCandidates = null;
                return false;
            }

            if (!ReferenceEquals(_candidateSource, candidates))
            {
                _candidateSource = candidates;
                _lastBlendCandidateSearch = null;
                _lastTransformCandidateSearch = null;
            }

            return true;
        }

        private void RefreshBlendCandidatesIfNeeded()
        {
            if (_blendCandidates == null || !string.Equals(_lastBlendCandidateSearch, _blendCandidateSearch, StringComparison.Ordinal))
            {
                _blendCandidates = _candidateSource.FilterBlendShapes(_blendCandidateSearch);
                _lastBlendCandidateSearch = _blendCandidateSearch;
            }
        }

        private void RefreshTransformCandidatesIfNeeded()
        {
            if (_transformCandidates == null || !string.Equals(_lastTransformCandidateSearch, _transformCandidateSearch, StringComparison.Ordinal))
            {
                _transformCandidates = _candidateSource.FilterTransforms(_transformCandidateSearch);
                _lastTransformCandidateSearch = _transformCandidateSearch;
            }
        }

        private void DrawBlendShapeFallback()
        {
            _advancedBlend = EditorGUILayout.Foldout(_advancedBlend, FaceMotionUiText.Get("advancedManualBinding"));
            if (_advancedBlend)
            {
                _rendererPath = EditorGUILayout.TextField(FaceMotionUiText.Get("rendererPath"), _rendererPath);
                _blendShapeName = EditorGUILayout.TextField(FaceMotionUiText.Get("blendShapeName"), _blendShapeName);
                if (GUILayout.Button(FaceMotionUiText.Get("addManualBlendShape")))
                {
                    _tracks.AddBlendShapeTrack(_rendererPath, _blendShapeName);
                }
            }

            DrawAddCancel();
        }

        private void DrawTransformFallback()
        {
            _advancedTransform = EditorGUILayout.Foldout(_advancedTransform, FaceMotionUiText.Get("advancedManualBinding"));
            if (_advancedTransform)
            {
                _transformPath = EditorGUILayout.TextField(FaceMotionUiText.Get("transformPath"), _transformPath);
                if (GUILayout.Button(FaceMotionUiText.Get("addManualTransform")))
                {
                    _tracks.AddTransformTrack(KindForIndex(_transformKind), _transformPath);
                }
            }

            DrawAddCancel();
        }

        private void DrawAddCancel()
        {
            if (GUILayout.Button(FaceMotionUiText.Get("cancel"), GUILayout.Width(60f)))
            {
                _addMode = 0;
            }
        }

        private void DrawTrackRow(FaceTrackData track, bool isSelected, Dictionary<string, TrackBindingValidation> bindings)
        {
            EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box");

            string label = GetTrackLabel(track);
            if (bindings != null && track.TrackId != null && bindings.TryGetValue(track.TrackId, out var binding))
            {
                if (binding.Status != MappingResolutionStatus.Resolved)
                {
                    label += " !";
                }
            }

            if (GUILayout.Button(label, EditorStyles.miniLabel))
            {
                _tracks.Select(track.TrackId);
            }

            if (GUILayout.Button(FaceMotionUiText.Get("deleteShort"), EditorStyles.miniButtonRight, GUILayout.Width(34)))
            {
                _tracks.RemoveTrack(track.TrackId);
            }

            EditorGUILayout.EndHorizontal();
        }

        private Dictionary<string, TrackBindingValidation> BuildBindingMap()
        {
            IReadOnlyList<TrackBindingValidation> list = _session.TrackBindings;
            if (list == null || list.Count == 0)
            {
                return null;
            }

            var map = new Dictionary<string, TrackBindingValidation>(list.Count, StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].TrackId != null)
                {
                    map[list[i].TrackId] = list[i];
                }
            }

            return map;
        }

        internal static FaceTrackData[] SnapshotTracks(IReadOnlyList<FaceTrackData> tracks)
        {
            var snapshot = new FaceTrackData[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
            {
                snapshot[i] = tracks[i];
            }

            return snapshot;
        }

        private static bool MatchesFilter(FaceTrackData track, string search)
        {
            if (track == null)
            {
                return false;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                return Contains(track.BlendShape.RendererPath, search)
                    || Contains(track.BlendShape.BlendShapeName, search);
            }

            if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                return Contains(track.Transform.TransformPath, search);
            }

            return false;
        }

        private static bool Contains(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            {
                return false;
            }

            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTrackLabel(FaceTrackData track)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                return "B " + track.BlendShape.BlendShapeName + " (" + Shorten(track.BlendShape.RendererPath) + ")";
            }

            if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                string tag = track.Kind == TrackKind.TransformPosition ? "P" : track.Kind == TrackKind.TransformRotation ? "R" : "S";
                return tag + " " + Shorten(track.Transform.TransformPath);
            }

            return track.TrackId;
        }

        private static string Shorten(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        private static TrackKind KindForIndex(int index)
        {
            if (index == 1) return TrackKind.TransformRotation;
            if (index == 2) return TrackKind.TransformScale;
            return TrackKind.TransformPosition;
        }
    }
}
