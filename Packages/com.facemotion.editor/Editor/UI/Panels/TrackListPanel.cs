using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
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
        internal const float DefaultCandidateListHeight = 220f;
        internal const float MinimumCandidateListHeight = 120f;
        internal const float MaximumCandidateListHeight = 420f;
        private readonly FaceMotionEditorSession _session;
        private readonly TrackController _tracks;
        private readonly PreviewOverrideState _previewOverride;
        private readonly Action _requestPreviewRepaint;
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
        private int _blendCategoryFilter;
        private int _blendFaceCategoryFilter;
        private bool _blendConflictsOnly;
        private bool _blendSafeOnly;
        private readonly ResizableVerticalSplitter _blendCandidateSplitter;
        private readonly ResizableVerticalSplitter _transformCandidateSplitter;

        private bool _blendListDirty = true;
        private readonly Dictionary<AvatarCandidateSnapshot.BlendShapeCandidate, string> _blendTooltipCache =
            new Dictionary<AvatarCandidateSnapshot.BlendShapeCandidate, string>();
        private FaceMotion.Data.FaceMotionAnimationData _addedBindingsAnimation;
        private HashSet<string> _addedBlendBindings;

        public TrackListPanel(
            FaceMotionEditorSession session,
            TrackController tracks,
            PreviewOverrideState previewOverride = null,
            Action requestPreviewRepaint = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            _previewOverride = previewOverride;
            _requestPreviewRepaint = requestPreviewRepaint;
            string prefix = "FaceMotion.Window.v2." + Hash128.Compute(Application.dataPath).ToString() + ".TrackPicker.";
            _blendCandidateSplitter = new ResizableVerticalSplitter(prefix + "BlendHeight", DefaultCandidateListHeight, MinimumCandidateListHeight, MaximumCandidateListHeight);
            _transformCandidateSplitter = new ResizableVerticalSplitter(prefix + "TransformHeight", DefaultCandidateListHeight, MinimumCandidateListHeight, MaximumCandidateListHeight);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(new GUIContent(FaceMotionUiText.Get("tracks"), FaceMotionUiText.Get("tooltipTrack")), EditorStyles.boldLabel);

            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                ClearHoverPreview();
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
                if (string.IsNullOrEmpty(_search))
                {
                    EditorGUILayout.HelpBox(FaceMotionUiText.Get("emptyTracks"), MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField(FaceMotionUiText.Get("noTracks"), EditorStyles.centeredGreyMiniLabel);
                }
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
                ClearHoverPreview();
                _transformKind = EditorGUILayout.Popup(FaceMotionUiText.Get("kind"), _transformKind, new[] { FaceMotionUiText.Get("position"), FaceMotionUiText.Get("rotation"), FaceMotionUiText.Get("scale") });
                DrawTransformPicker();
            }
            else
            {
                ClearHoverPreview();
            }
        }

        private void DrawBlendShapePicker()
        {
            if (!TryRefreshCandidateFilters())
            {
                _previewOverride?.Clear();
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAvatarForBlend"), MessageType.Info);
                DrawBlendShapeFallback();
                return;
            }

            string search = EditorGUILayout.TextField(FaceMotionUiText.Get("search"), _blendCandidateSearch);
            if (!string.Equals(search, _blendCandidateSearch, StringComparison.Ordinal))
            {
                _blendCandidateSearch = search;
                _blendListDirty = true;
            }

            DrawBlendShapeFilters();
            RefreshBlendView();
            _blendCandidateScroll = EditorGUILayout.BeginScrollView(_blendCandidateScroll, GUILayout.Height(_blendCandidateSplitter.Height));
            bool hoveringCandidate = false;
            DrawBlendShapeCandidates(_session.GetSelectedAnimation(), ref hoveringCandidate);

            EditorGUILayout.EndScrollView();
            Event current = Event.current;
            if (ShouldClearHoverPreview(current, hoveringCandidate))
            {
                ClearHoverPreview();
            }
            _blendCandidateSplitter.Draw(MinimumCandidateListHeight, MaximumCandidateListHeight);
            DrawBlendShapeFallback();
        }

        private void DrawBlendShapeFilters()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawCategoryButton(0, FaceMotionUiText.Get("browserAll"));
            DrawCategoryButton(1, FaceMotionUiText.Get("categoryFace"));
            DrawCategoryButton(2, FaceMotionUiText.Get("categoryHair"));
            DrawCategoryButton(3, FaceMotionUiText.Get("categoryBody"));
            DrawCategoryButton(4, FaceMotionUiText.Get("categoryClothes"));
            DrawCategoryButton(5, FaceMotionUiText.Get("categoryOther"));
            EditorGUILayout.EndHorizontal();

            if (_blendCategoryFilter == 1)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawFaceCategoryButton(0, FaceMotionUiText.Get("browserAll"));
                DrawFaceCategoryButton(1, FaceMotionUiText.Get("categoryEye"));
                DrawFaceCategoryButton(2, FaceMotionUiText.Get("categoryBlink"));
                DrawFaceCategoryButton(3, FaceMotionUiText.Get("categoryBrow"));
                DrawFaceCategoryButton(4, FaceMotionUiText.Get("categoryMouth"));
                DrawFaceCategoryButton(5, FaceMotionUiText.Get("categoryFaceOther"));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            bool conflicts = GUILayout.Toggle(_blendConflictsOnly, FaceMotionUiText.Get("browserConflicts"), EditorStyles.miniButtonLeft, GUILayout.Width(64f));
            bool safe = GUILayout.Toggle(_blendSafeOnly, FaceMotionUiText.Get("browserSafe"), EditorStyles.miniButtonRight, GUILayout.Width(54f));
            bool conflictsChanged = conflicts != _blendConflictsOnly;
            _blendConflictsOnly = conflicts;
            bool safeChanged = (safe && !conflicts) != _blendSafeOnly;
            _blendSafeOnly = safe && !conflicts;
            if (conflictsChanged || safeChanged)
            {
                _blendListDirty = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryButton(int category, string label)
        {
            bool selected = _blendCategoryFilter == category;
            if (GUILayout.Toggle(selected, label, EditorStyles.toolbarButton) && !selected)
            {
                _blendCategoryFilter = category;
            }
        }

        private void DrawFaceCategoryButton(int category, string label)
        {
            bool selected = _blendFaceCategoryFilter == category;
            if (GUILayout.Toggle(selected, label, EditorStyles.toolbarButton) && !selected)
            {
                _blendFaceCategoryFilter = category;
            }
        }

        private void DrawBlendShapeCandidates(FaceMotion.Data.FaceMotionAnimationData animation, ref bool hoveringCandidate)
        {
            if (!ReferenceEquals(_addedBindingsAnimation, animation))
            {
                _addedBlendBindings = BuildAddedBlendShapeBindings(animation);
                _addedBindingsAnimation = animation;
            }

            var added = _addedBlendBindings;
            for (int i = 0; i < _blendCandidates.Count; i++)
            {
                var candidate = _blendCandidates[i];
                if (!FilterAllowsCandidate(candidate) || !MatchesCategorySelection(candidate, _blendCategoryFilter, _blendFaceCategoryFilter))
                {
                    continue;
                }

                bool alreadyAdded = added != null
                    && added.Contains(BlendBindingKey(candidate.RendererPath, candidate.BlendShapeName));
                string label = candidate.BlendShapeName + "  (" + candidate.RendererPath + ")";
                if (alreadyAdded)
                {
                    label += " [" + FaceMotionUiText.Get("browserAdded") + "]";
                }
                else if (candidate.ConflictStatus == AvatarCandidateSnapshot.BlendShapeConflictStatus.Conflict)
                {
                    label += " [" + FaceMotionUiText.Get("browserConflict") + "]";
                }
                else if (candidate.ConflictStatus == AvatarCandidateSnapshot.BlendShapeConflictStatus.Warning)
                {
                    label += " [" + FaceMotionUiText.Get("browserCaution") + "]";
                }

                EditorGUI.BeginDisabledGroup(alreadyAdded);
                if (GUILayout.Button(new GUIContent(label, CachedTooltip(candidate)), EditorStyles.miniButton))
                {
                    _tracks.AddBlendShapeTrack(candidate);
                    _addMode = 0;
                    _previewOverride?.Clear();
                    _addedBindingsAnimation = null;
                    GUIUtility.ExitGUI();
                }

                EditorGUI.EndDisabledGroup();
                Event current = Event.current;
                if (_previewOverride != null && IsCandidateHoverEvent(current)
                    && GUILayoutUtility.GetLastRect().Contains(current.mousePosition))
                {
                    SetHoverPreview(candidate.ToBinding());
                    hoveringCandidate = true;
                }
            }
        }

        private bool FilterAllowsCandidate(AvatarCandidateSnapshot.BlendShapeCandidate candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (_blendConflictsOnly && candidate.ConflictStatus == AvatarCandidateSnapshot.BlendShapeConflictStatus.Safe)
            {
                return false;
            }

            if (_blendSafeOnly && candidate.ConflictStatus != AvatarCandidateSnapshot.BlendShapeConflictStatus.Safe)
            {
                return false;
            }

            return true;
        }

        internal static bool MatchesCategorySelection(
            AvatarCandidateSnapshot.BlendShapeCandidate candidate,
            int categoryFilter,
            int faceCategoryFilter)
        {
            if (candidate == null || categoryFilter == 0)
            {
                return candidate != null;
            }

            if (categoryFilter == 1)
            {
                return candidate.TopLevelCategory == "Face"
                    && (faceCategoryFilter == 0 || (int)candidate.Category == faceCategoryFilter - 1);
            }

            return (categoryFilter == 2 && candidate.TopLevelCategory == "Hair")
                || (categoryFilter == 3 && candidate.TopLevelCategory == "Body")
                || (categoryFilter == 4 && candidate.TopLevelCategory == "Clothes")
                || (categoryFilter == 5 && candidate.TopLevelCategory == "Other");
        }

        private static HashSet<string> BuildAddedBlendShapeBindings(FaceMotion.Data.FaceMotionAnimationData animation)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (animation == null || animation.Timeline == null)
            {
                return set;
            }

            for (int i = 0; i < animation.Timeline.Tracks.Count; i++)
            {
                var track = animation.Timeline.Tracks[i];
                if (track != null && track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                {
                    set.Add(BlendBindingKey(track.BlendShape.RendererPath, track.BlendShape.BlendShapeName));
                }
            }

            return set;
        }

        private static string BlendBindingKey(string rendererPath, string blendShapeName)
        {
            return rendererPath + "\n" + blendShapeName;
        }

        private string CachedTooltip(AvatarCandidateSnapshot.BlendShapeCandidate candidate)
        {
            if (_blendTooltipCache.TryGetValue(candidate, out string cached))
            {
                return cached;
            }

            string tooltip = CandidateTooltip(candidate);
            _blendTooltipCache[candidate] = tooltip;
            return tooltip;
        }

        private static string CandidateTooltip(AvatarCandidateSnapshot.BlendShapeCandidate candidate)
        {
            string text = FaceMotionUiText.Get("category") + ": " + candidate.TopLevelCategory + "/" + candidate.Category
                + "\n" + FaceMotionUiText.Get("classification") + ": " + candidate.ClassificationReason
                + "\n" + FaceMotionUiText.Get("bindings") + ": " + candidate.DisplayLabel;
            if (candidate.ConflictStatus != AvatarCandidateSnapshot.BlendShapeConflictStatus.Safe)
            {
                text += "\n" + FaceMotionUiText.Get("conflict") + ": " + candidate.ConflictReason
                    + "\n" + FaceMotionUiText.Get("source") + ": " + candidate.ConflictSource;
            }

            return text;
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
            _transformCandidateScroll = EditorGUILayout.BeginScrollView(_transformCandidateScroll, GUILayout.Height(_transformCandidateSplitter.Height));
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
            _transformCandidateSplitter.Draw(MinimumCandidateListHeight, MaximumCandidateListHeight);
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
                ClearHoverPreview();
                _candidateSource = candidates;
                _lastBlendCandidateSearch = null;
                _lastTransformCandidateSearch = null;
                _blendListDirty = true;
                _blendTooltipCache.Clear();
                _addedBindingsAnimation = null;
            }

            return true;
        }

        private void SetHoverPreview(BlendShapeBinding binding)
        {
            ApplyHoverPreviewChange(_previewOverride, binding, _requestPreviewRepaint);
        }

        private void ClearHoverPreview()
        {
            ApplyHoverPreviewChange(_previewOverride, null, _requestPreviewRepaint);
        }

        internal static bool ApplyHoverPreviewChange(
            PreviewOverrideState previewOverride,
            BlendShapeBinding? binding,
            Action requestPreviewRepaint)
        {
            if (previewOverride == null)
            {
                return false;
            }

            bool changed = binding.HasValue
                ? previewOverride.SetHover(binding.Value)
                : previewOverride.Clear();
            if (changed)
            {
                requestPreviewRepaint?.Invoke();
            }

            return changed;
        }

        internal static bool IsCandidateHoverEvent(Event current)
        {
            return current != null
                && (current.type == EventType.MouseEnterWindow
                    || current.type == EventType.MouseMove
                    || current.type == EventType.Repaint);
        }

        internal static bool ShouldClearHoverPreview(Event current, bool hoveringCandidate)
        {
            return current != null
                && (current.type == EventType.MouseLeaveWindow
                    || (!hoveringCandidate && IsCandidateHoverEvent(current)));
        }

        private void RefreshBlendView()
        {
            if (!_blendListDirty)
            {
                return;
            }

            _blendListDirty = false;
            _lastBlendCandidateSearch = _blendCandidateSearch;
            _blendCandidates = _candidateSource.FilterBlendShapes(_blendCandidateSearch);
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
                _addedBindingsAnimation = null;
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
