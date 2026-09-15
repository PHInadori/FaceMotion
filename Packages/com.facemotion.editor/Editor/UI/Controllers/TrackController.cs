using System;
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
            return candidate != null && AddBlendShapeTrack(candidate.RendererPath, candidate.BlendShapeName);
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
