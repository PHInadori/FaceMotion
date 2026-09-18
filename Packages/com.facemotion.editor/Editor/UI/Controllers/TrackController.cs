using System;
using FaceMotion.Avatar;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Timeline;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>Track list operations. Duplicate bindings are rejected on the safe side.</summary>
    public sealed class TrackController
    {
        private readonly FaceMotionEditorSession _session;

        public TrackController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public FaceTrackData GetSelectedTrack()
        {
            return _session.GetSelectedTrack();
        }

        public bool AddBlendShapeTrack(string rendererPath, string blendShapeName)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            string binding = rendererPath + " / " + blendShapeName;
            if (TrackDuplicateFinder.HasDuplicate(animation, TrackKind.BlendShape, rendererPath, blendShapeName, null))
            {
                SetOutcome(ControllerDiagnostics.DuplicateTrack(binding, animation.AnimationId));
                return false;
            }

            var result = UICommandRunner.Run(
                _session,
                new AddBlendShapeTrackCommand(animation.AnimationId, rendererPath, blendShapeName));
            if (result.Succeeded)
            {
                SelectLastTrack(animation);
            }

            return result.Succeeded;
        }

        /// <summary>Adds the exact binding represented by an avatar candidate snapshot.</summary>
        public bool AddBlendShapeTrack(AvatarCandidateSnapshot.BlendShapeCandidate candidate)
        {
            if (candidate == null) return false;
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }
            if (TrackDuplicateFinder.HasDuplicate(animation, TrackKind.BlendShape, candidate.RendererPath, candidate.BlendShapeName, null))
            {
                SetOutcome(ControllerDiagnostics.DuplicateTrack(candidate.RendererPath + " / " + candidate.BlendShapeName, animation.AnimationId));
                return false;
            }

            var binding = candidate.ToBinding();
            UnityAvatarObjectCache cache = _session.ActiveObjectCache;
            if (cache == null || !cache.TryGetRenderer(binding.RendererPath, out var renderer)
                || !cache.TryGetBlendShapeIndex(binding, out int index)
                || renderer == null || renderer.sharedMesh == null || index < 0 || index >= renderer.sharedMesh.blendShapeCount
                || !string.Equals(renderer.sharedMesh.GetBlendShapeName(index), binding.BlendShapeName, StringComparison.Ordinal))
            {
                SetOutcome(ControllerDiagnostics.InvalidValue("The selected blend shape is no longer available on the active avatar."));
                return false;
            }

            float value = renderer.GetBlendShapeWeight(index);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                SetOutcome(ControllerDiagnostics.InvalidValue("The active avatar returned an invalid blend shape weight."));
                return false;
            }

            var command = new AddBlendShapeTrackWithInitialKeyCommand(animation.AnimationId, binding.RendererPath, binding.BlendShapeName, value);
            var result = UICommandRunner.Run(_session, command);
            if (result.Succeeded && !string.IsNullOrEmpty(command.CreatedTrackId)) Select(command.CreatedTrackId);
            return result.Succeeded;
        }

        public bool AddTransformTrack(TrackKind kind, string transformPath)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            if (!TrackKinds.IsTransform(kind))
            {
                return false;
            }

            if (TrackDuplicateFinder.HasDuplicate(animation, kind, null, null, transformPath))
            {
                SetOutcome(ControllerDiagnostics.DuplicateTrack(transformPath + " (" + kind + ")", animation.AnimationId));
                return false;
            }

            var result = UICommandRunner.Run(
                _session,
                new AddTransformTrackCommand(animation.AnimationId, kind, transformPath));
            if (result.Succeeded)
            {
                SelectLastTrack(animation);
            }

            return result.Succeeded;
        }

        /// <summary>Adds the exact transform path represented by an avatar candidate snapshot.</summary>
        public bool AddTransformTrack(TrackKind kind, AvatarCandidateSnapshot.TransformCandidate candidate)
        {
            return candidate != null && AddTransformTrack(kind, candidate.RelativePath);
        }

        public bool RemoveTrack(string trackId)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            var result = UICommandRunner.Run(_session, new RemoveTrackCommand(animation.AnimationId, trackId));
            return result.Succeeded;
        }

        public string Select(string trackId)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(trackId, out _))
            {
                return null;
            }

            _session.SelectedTrackId = trackId;
            _session.Selection.Clear();
            _session.NotifyChanged();
            return trackId;
        }

        public void ClearSelection()
        {
            _session.SelectedTrackId = null;
            _session.Selection.Clear();
            _session.NotifyChanged();
        }

        private void SelectLastTrack(FaceMotion.Data.FaceMotionAnimationData animation)
        {
            if (animation.Timeline != null && animation.Timeline.Tracks.Count > 0)
            {
                _session.SelectedTrackId = animation.Timeline.Tracks[animation.Timeline.Tracks.Count - 1].TrackId;
                _session.Selection.Clear();
                _session.NotifyChanged();
            }
        }

        private void SetOutcome(FaceMotion.Diagnostics.FaceMotionDiagnostic diagnostic)
        {
            _session.SetLastOperationDiagnostic(diagnostic);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }
    }
}
