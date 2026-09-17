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
        private readonly string _blendBrowserFoldoutPrefix;

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
        private bool _blendConflictsOnly;
        private bool _blendSafeOnly;
        private readonly ResizableVerticalSplitter _blendCandidateSplitter;
        private readonly ResizableVerticalSplitter _transformCandidateSplitter;

        private const int LargeCategoryThreshold = 120;
        private bool _blendListDirty = true;
        private readonly int[] _blendCategoryCounts = new int[9];
        private readonly int[] _blendTopLevelCounts = new int[5];
        private readonly Dictionary<AvatarCandidateSnapshot.BlendShapeCandidate, string> _blendTooltipCache =
            new Dictionary<AvatarCandidateSnapshot.BlendShapeCandidate, string>();
        private FaceMotion.Data.FaceMotionAnimationData _addedBindingsAnimation;
        private HashSet<string> _addedBlendBindings;

        public TrackListPanel(FaceMotionEditorSession session, TrackController tracks, PreviewOverrideState previewOverride = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            _previewOverride = previewOverride;
            string prefix = "FaceMotion.Window.v2." + Hash128.Compute(Application.dataPath).ToString() + ".TrackPicker.";
            _blendBrowserFoldoutPrefix = prefix + "BlendBrowser.";
            _blendCandidateSplitter = new ResizableVerticalSplitter(prefix + "BlendHeight", DefaultCandidateListHeight, MinimumCandidateListHeight, MaximumCandidateListHeight);
            _transformCandidateSplitter = new ResizableVerticalSplitter(prefix + "TransformHeight", DefaultCandidateListHeight, MinimumCandidateListHeight, MaximumCandidateListHeight);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(new GUIContent(FaceMotionUiText.Get("tracks"), FaceMotionUiText.Get("tooltipTrack")), EditorStyles.boldLabel);

            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                _previewOverride?.Clear();
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
                _previewOverride?.Clear();
                _transformKind = EditorGUILayout.Popup(FaceMotionUiText.Get("kind"), _transformKind, new[] { FaceMotionUiText.Get("position"), FaceMotionUiText.Get("rotation"), FaceMotionUiText.Get("scale") });
                DrawTransformPicker();
            }
            else
            {
                _previewOverride?.Clear();
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
            DrawBlendShapeTree(_session.GetSelectedAnimation(), ref hoveringCandidate);

            EditorGUILayout.EndScrollView();
            if (!hoveringCandidate && Event.current != null && Event.current.type == EventType.Repaint)
            {
                _previewOverride?.Clear();
            }
            _blendCandidateSplitter.Draw(MinimumCandidateListHeight, MaximumCandidateListHeight);
            DrawBlendShapeFallback();
        }

        private void DrawBlendShapeFilters()
        {
            EditorGUILayout.BeginHorizontal();
            _blendCategoryFilter = EditorGUILayout.Popup(
                FaceMotionUiText.Get("filter"),
                _blendCategoryFilter,
                new[]
                {
                    FaceMotionUiText.Get("browserAll"),
                    FaceMotionUiText.Get("categoryFace"),
                    FaceMotionUiText.Get("categoryHair"),
                    FaceMotionUiText.Get("categoryBody"),
                    FaceMotionUiText.Get("categoryClothes"),
                    FaceMotionUiText.Get("categoryOther")
                });
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

        private void DrawBlendShapeTree(FaceMotion.Data.FaceMotionAnimationData animation, ref bool hoveringCandidate)
        {
            if (!ReferenceEquals(_addedBindingsAnimation, animation))
            {
                _addedBlendBindings = BuildAddedBlendShapeBindings(animation);
                _addedBindingsAnimation = animation;
            }

            var added = _addedBlendBindings;
            DrawTopLevelCategory("Face", new[]
            {
                AvatarCandidateSnapshot.BlendShapeCategory.Eye,
                AvatarCandidateSnapshot.BlendShapeCategory.Blink,
                AvatarCandidateSnapshot.BlendShapeCategory.Brow,
                AvatarCandidateSnapshot.BlendShapeCategory.Mouth,
                AvatarCandidateSnapshot.BlendShapeCategory.FaceOther
            }, animation, added, ref hoveringCandidate);
            DrawTopLevelCategory("Hair", new[] { AvatarCandidateSnapshot.BlendShapeCategory.Hair }, animation, added, ref hoveringCandidate);
            DrawTopLevelCategory("Body", new[] { AvatarCandidateSnapshot.BlendShapeCategory.Body }, animation, added, ref hoveringCandidate);
            DrawTopLevelCategory("Clothes", new[] { AvatarCandidateSnapshot.BlendShapeCategory.Clothes }, animation, added, ref hoveringCandidate);
            DrawTopLevelCategory("Other", new[] { AvatarCandidateSnapshot.BlendShapeCategory.Other }, animation, added, ref hoveringCandidate);
        }

        private void DrawTopLevelCategory(
            string topLevel,
            AvatarCandidateSnapshot.BlendShapeCategory[] categories,
            FaceMotion.Data.FaceMotionAnimationData animation,
            HashSet<string> addedBlendBindings,
            ref bool hoveringCandidate)
        {
            int total = CountVisible(categories);
            if (total == 0 || !MatchesTopLevelFilter(topLevel))
            {
                return;
            }

            bool open = DrawPersistentFoldout("Top." + topLevel, FaceMotionUiText.Get("category" + topLevel) + " (" + total + ")", total >= LargeCategoryThreshold);
            if (!open)
            {
                return;
            }

            EditorGUI.indentLevel++;
            if (string.Equals(topLevel, "Face", StringComparison.Ordinal))
            {
                for (int i = 0; i < categories.Length; i++)
                {
                    DrawFaceCategory(categories[i], animation, addedBlendBindings, ref hoveringCandidate);
                }
            }
            else
            {
                DrawCandidates(categories[0], animation, addedBlendBindings, ref hoveringCandidate);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFaceCategory(
            AvatarCandidateSnapshot.BlendShapeCategory category,
            FaceMotion.Data.FaceMotionAnimationData animation,
            HashSet<string> addedBlendBindings,
            ref bool hoveringCandidate)
        {
            int total = CountVisible(category);
            if (total == 0)
            {
                return;
            }

            bool open = DrawPersistentFoldout("Face." + category, FaceMotionUiText.Get("category" + category) + " (" + total + ")", total >= LargeCategoryThreshold);
            if (!open)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawCandidates(category, animation, addedBlendBindings, ref hoveringCandidate);
            EditorGUI.indentLevel--;
        }

        private void DrawCandidates(
            AvatarCandidateSnapshot.BlendShapeCategory category,
            FaceMotion.Data.FaceMotionAnimationData animation,
            HashSet<string> addedBlendBindings,
            ref bool hoveringCandidate)
        {
            for (int i = 0; i < _blendCandidates.Count; i++)
            {
                var candidate = _blendCandidates[i];
                if (!FilterAllowsCandidate(candidate) || candidate.Category != category)
                {
                    continue;
                }

                bool alreadyAdded = addedBlendBindings != null
                    && addedBlendBindings.Contains(BlendBindingKey(candidate.RendererPath, candidate.BlendShapeName));
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
                if (_previewOverride != null && Event.current != null && Event.current.type == EventType.Repaint
                    && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    _previewOverride.SetHover(candidate.ToBinding());
                    hoveringCandidate = true;
                }
            }
        }

        private bool DrawPersistentFoldout(string suffix, string label, bool defaultClosed)
        {
            string key = _blendBrowserFoldoutPrefix + suffix;
            bool saved = EditorPrefs.GetBool(key, !defaultClosed);
            bool forceOpen = !string.IsNullOrEmpty(_blendCandidateSearch);
            bool visible = forceOpen ? true : saved;
            bool changed = EditorGUILayout.Foldout(visible, label, true);
            if (!forceOpen && changed != saved)
            {
                EditorPrefs.SetBool(key, changed);
            }

            return forceOpen || changed;
        }

        private int CountVisible(AvatarCandidateSnapshot.BlendShapeCategory[] categories)
        {
            int count = 0;
            for (int i = 0; i < categories.Length; i++)
            {
                count += CountVisible(categories[i]);
            }

            return count;
        }

        private int CountVisible(AvatarCandidateSnapshot.BlendShapeCategory category)
        {
            return _blendCategoryCounts[(int)category];
        }

        private bool FilterAllowsCandidate(AvatarCandidateSnapshot.BlendShapeCandidate candidate)
        {
            if (candidate == null || candidate.IsSeparator)
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

        private bool MatchesTopLevelFilter(string topLevel)
        {
            return _blendCategoryFilter == 0
                || (_blendCategoryFilter == 1 && topLevel == "Face")
                || (_blendCategoryFilter == 2 && topLevel == "Hair")
                || (_blendCategoryFilter == 3 && topLevel == "Body")
                || (_blendCategoryFilter == 4 && topLevel == "Clothes")
                || (_blendCategoryFilter == 5 && topLevel == "Other");
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
                _candidateSource = candidates;
                _lastBlendCandidateSearch = null;
                _lastTransformCandidateSearch = null;
                _blendListDirty = true;
                _blendTooltipCache.Clear();
                _addedBindingsAnimation = null;
            }

            return true;
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
            Array.Clear(_blendCategoryCounts, 0, _blendCategoryCounts.Length);
            Array.Clear(_blendTopLevelCounts, 0, _blendTopLevelCounts.Length);
            for (int i = 0; i < _blendCandidates.Count; i++)
            {
                var candidate = _blendCandidates[i];
                if (!FilterAllowsCandidate(candidate))
                {
                    continue;
                }

                _blendCategoryCounts[(int)candidate.Category]++;
                _blendTopLevelCounts[TopLevelIndex(candidate.TopLevelCategory)]++;
            }
        }

        private static int TopLevelIndex(string topLevel)
        {
            return topLevel == "Hair" ? 1
                : topLevel == "Body" ? 2
                : topLevel == "Clothes" ? 3
                : topLevel == "Other" ? 4
                : 0;
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
