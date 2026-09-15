using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.Diagnostics;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.VRChat;
using FaceMotion.Timeline;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Session
{
    /// <summary>
    /// Editor-side session state for FaceMotionWindow. Holds the active project, active
    /// avatar, view state, selection, clipboard, and diagnostics cache in memory. Nothing
    /// here is serialized into a FaceMotion project; only a few scalar fields are mirrored
    /// to EditorPrefs by FaceMotionSessionStateStore for domain reload continuity.
    /// </summary>
    public sealed class FaceMotionEditorSession
    {
        private readonly List<FaceMotionDiagnostic> _diagnostics = new List<FaceMotionDiagnostic>();
        private readonly Dictionary<string, TrackBindingValidation> _trackBindings =
            new Dictionary<string, TrackBindingValidation>(StringComparer.Ordinal);
        private FaceMotionDiagnostic _lastOperationDiagnostic;

        public ValidationReport LastProjectValidation { get; set; }

        public FaceMotionProject ActiveProject { get; private set; }

        public string ActiveProjectAssetPath { get; private set; }

        public VRCAvatarDescriptor ActiveDescriptor { get; private set; }

        public GameObject ActiveAvatarRoot { get; private set; }

        public UnityAvatarObjectCache ActiveObjectCache { get; private set; }

        public AvatarIndex ActiveAvatarIndex => ActiveObjectCache == null ? null : ActiveObjectCache.Index;

        public DescriptorValidation LastDescriptorValidation { get; private set; }

        public AvatarScanReport LastAvatarReport { get; private set; }

        public bool AvatarIndexDirty { get; set; }

        public string SelectedAnimationId { get; set; }

        public string SelectedTrackId { get; set; }

        public TimelineViewState ViewState { get; } = new TimelineViewState();

        public TimelineSelection Selection { get; } = new TimelineSelection();

        public TimelineClipboard Clipboard { get; } = new TimelineClipboard();

        public AvatarCandidateSnapshot Candidates { get; private set; }

        /// <summary>Optional persisted profile used by logical Phase E built-ins for this editor session.</summary>
        public AvatarMappingProfile ActiveMappingProfile { get; private set; }

        public int Version { get; private set; }

        public event Action Changed;

        /// <summary>Updates the shared playhead used by timeline, inspector, and preview.</summary>
        public bool SetCurrentTime(float time)
        {
            float old = ViewState.CurrentTime;
            float clamped = Mathf.Clamp(time, 0f, GetSelectedDuration());
            FaceMotionPreviewTrace.Trace("B.SetCurrentTime", "old={0} requested={1} clamped={2}", old, time, clamped);
            if (Mathf.Approximately(old, clamped))
            {
                return false;
            }

            ViewState.CurrentTime = clamped;
            NotifyChanged();
            return true;
        }

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics => _diagnostics;

        public IReadOnlyList<TrackBindingValidation> TrackBindings
        {
            get
            {
                var list = new List<TrackBindingValidation>(_trackBindings.Count);
                foreach (var value in _trackBindings.Values)
                {
                    list.Add(value);
                }

                return list;
            }
        }

        /// <summary>Last controller-level outcome diagnostic (successes clear this).</summary>
        public FaceMotionDiagnostic LastOperationDiagnostic => _lastOperationDiagnostic;

        public void NotifyChanged()
        {
            Version++;
            Changed?.Invoke();
        }

        public void SetActiveProject(FaceMotionProject project, string assetPath)
        {
            ActiveProject = project;
            ActiveProjectAssetPath = assetPath;
            SelectedAnimationId = null;
            SelectedTrackId = null;
            Selection.Clear();
            ViewState.CurrentTime = 0f;
            _lastOperationDiagnostic = null;
            LastProjectValidation = null;
            RecomputeBindingDiagnostics();
            RecomputeDiagnostics();
            NotifyChanged();
        }

        public void ClearProject()
        {
            SetActiveProject(null, null);
        }

        public void SetAvatar(
            VRCAvatarDescriptor descriptor,
            GameObject avatarRoot,
            DescriptorValidation descriptorValidation,
            AvatarScanReport avatarReport,
            UnityAvatarObjectCache cache,
            AvatarCandidateSnapshot candidates)
        {
            ActiveDescriptor = descriptor;
            ActiveAvatarRoot = avatarRoot;
            LastDescriptorValidation = descriptorValidation;
            LastAvatarReport = avatarReport;
            ActiveObjectCache = cache;
            Candidates = candidates;
            AvatarIndexDirty = false;
            RecomputeBindingDiagnostics();
            RecomputeDiagnostics();
            NotifyChanged();
        }

        public void ClearAvatar()
        {
            ActiveDescriptor = null;
            ActiveAvatarRoot = null;
            LastDescriptorValidation = null;
            LastAvatarReport = null;
            ActiveObjectCache = null;
            Candidates = null;
            AvatarIndexDirty = false;
            RecomputeBindingDiagnostics();
            RecomputeDiagnostics();
            NotifyChanged();
        }

        public void SetMappingProfile(AvatarMappingProfile profile)
        {
            ActiveMappingProfile = profile;
            NotifyChanged();
        }

        public void SetLastOperationDiagnostic(FaceMotionDiagnostic diagnostic)
        {
            _lastOperationDiagnostic = diagnostic;
        }

        /// <summary>Revalidates selection after data/undo changes and prunes dead IDs.</summary>
        public void ValidateSelections()
        {
            if (ActiveProject == null)
            {
                SelectedAnimationId = null;
                SelectedTrackId = null;
                Selection.Clear();
                return;
            }

            FaceMotionAnimationData animation = null;
            if (!string.IsNullOrEmpty(SelectedAnimationId)
                && ActiveProject.TryGetAnimation(SelectedAnimationId, out animation))
            {
                if (animation.Timeline == null)
                {
                    animation = null;
                }
            }

            if (animation == null)
            {
                SelectedAnimationId = null;
                SelectedTrackId = null;
                Selection.Clear();
                return;
            }

            if (string.IsNullOrEmpty(SelectedTrackId)
                || !animation.Timeline.TryGetTrack(SelectedTrackId, out var track))
            {
                SelectedTrackId = null;
                Selection.Clear();
                return;
            }

            if (track == null)
            {
                SelectedTrackId = null;
                Selection.Clear();
                return;
            }

            Selection.Prune(ExistsInAnimation(animation));
        }

        /// <summary>Rebuilds the track binding cache and merged diagnostics. Use after Undo.</summary>
        public void RefreshAfterUndo()
        {
            RecomputeBindingDiagnostics();
            RecomputeDiagnostics();
            ValidateSelections();
            NotifyChanged();
        }

        public void RecomputeBindingDiagnostics()
        {
            _trackBindings.Clear();
            if (ActiveProject != null && ActiveAvatarIndex != null)
            {
                IReadOnlyList<TrackBindingValidation> results =
                    AvatarTrackBindingValidator.ValidateTimeline(ActiveProject, ActiveAvatarIndex);
                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i] != null)
                    {
                        _trackBindings[results[i].TrackId] = results[i];
                    }
                }
            }
        }

        public void RecomputeDiagnostics()
        {
            _diagnostics.Clear();
            if (LastProjectValidation != null && LastProjectValidation.Diagnostics != null)
            {
                _diagnostics.AddRange(LastProjectValidation.Diagnostics);
            }

            if (LastDescriptorValidation != null && LastDescriptorValidation.Diagnostics != null)
            {
                _diagnostics.AddRange(LastDescriptorValidation.Diagnostics);
            }

            if (LastAvatarReport != null && LastAvatarReport.Diagnostics != null)
            {
                _diagnostics.AddRange(LastAvatarReport.Diagnostics);
            }

            foreach (var value in _trackBindings.Values)
            {
                if (value != null && value.Diagnostic != null)
                {
                    _diagnostics.Add(value.Diagnostic);
                }
            }

            if (_lastOperationDiagnostic != null)
            {
                _diagnostics.Add(_lastOperationDiagnostic);
            }
        }

        public void RefreshAll()
        {
            RecomputeBindingDiagnostics();
            RecomputeDiagnostics();
            NotifyChanged();
        }

        public bool TryGetTrackBinding(string trackId, out TrackBindingValidation validation)
        {
            return _trackBindings.TryGetValue(trackId, out validation);
        }

        public FaceMotionAnimationData GetSelectedAnimation()
        {
            if (ActiveProject == null || string.IsNullOrEmpty(SelectedAnimationId))
            {
                return null;
            }

            ActiveProject.TryGetAnimation(SelectedAnimationId, out var animation);
            return animation;
        }

        public FaceTrackData GetSelectedTrack()
        {
            var animation = GetSelectedAnimation();
            if (animation == null || animation.Timeline == null || string.IsNullOrEmpty(SelectedTrackId))
            {
                return null;
            }

            animation.Timeline.TryGetTrack(SelectedTrackId, out var track);
            return track;
        }

        public float GetSelectedDuration()
        {
            var animation = GetSelectedAnimation();
            return animation == null || animation.Timeline == null ? 1f : animation.Timeline.Duration;
        }

        public float GetSelectedFrameRate()
        {
            var animation = GetSelectedAnimation();
            return animation == null || animation.Timeline == null ? 60f : animation.Timeline.FrameRate;
        }

        private static Func<string, bool> ExistsInAnimation(FaceMotionAnimationData animation)
        {
            return keyId =>
            {
                if (animation.Timeline == null)
                {
                    return false;
                }

                foreach (var track in animation.Timeline.Tracks)
                {
                    if (track == null)
                    {
                        continue;
                    }

                    if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                    {
                        foreach (var key in track.BlendShape.Keys)
                        {
                            if (key != null && string.Equals(key.KeyId, keyId, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }
                    else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                    {
                        foreach (var key in track.Transform.Keys)
                        {
                            if (key != null && string.Equals(key.KeyId, keyId, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            };
        }
    }
}
