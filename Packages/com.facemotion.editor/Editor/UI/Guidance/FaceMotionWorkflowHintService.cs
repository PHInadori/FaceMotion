using FaceMotion.Data;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Timeline;

namespace FaceMotion.Editor.UI.Guidance
{
    /// <summary>
    /// Presentation-layer helper that turns session data into a single "next action" hint for
    /// the onboarding strip. It only reads state and never mutates anything, so advanced users
    /// can ignore the hint without losing access to any control.
    /// </summary>
    public static class FaceMotionWorkflowHintService
    {
        public const string HintCreateProject = "guidanceHintProject";
        public const string HintSelectAvatar = "guidanceHintAvatar";
        public const string HintCreateAnimation = "guidanceHintAnimation";
        public const string HintAddTrack = "guidanceHintTrack";
        public const string HintAddKey = "guidanceHintKey";
        public const string HintStartPreview = "guidanceHintPreview";
        public const string HintSelectForVrchat = "guidanceHintSelectVrchat";
        public const string HintPreviewAndIntegrate = "guidanceHintReady";

        /// <summary>Derives a read-only next action from the currently available authoring state.</summary>
        public static FaceMotionGuidanceModel Evaluate(FaceMotionEditorSession session, bool hasPreview)
        {
            if (session == null)
            {
                return Model(FaceMotionUxState.CreateProject, HintCreateProject);
            }

            FaceMotionAnimationData animation = session.GetSelectedAnimation();
            bool hasAnimation = HasAnyAnimation(session.ActiveProject)
                && animation != null
                && animation.Timeline != null;
            bool hasTrack = hasAnimation && CountTracks(animation) > 0;
            bool hasAuthoredKey = hasTrack && HasAuthoredKey(animation);
            return Evaluate(
                session.ActiveProject != null,
                session.ActiveDescriptor != null,
                hasAnimation,
                hasTrack,
                hasAuthoredKey,
                session.HasReviewedCurrentAnimation(),
                session.BatchAnimationIds.Count > 0);
        }

        /// <summary>Pure authoring-state evaluator used by the UI and regression tests.</summary>
        public static FaceMotionGuidanceModel Evaluate(
            bool hasProject,
            bool hasAvatar,
            bool hasAnimation,
            bool hasTrack,
            bool hasAuthoredKey,
            bool hasPreview,
            bool hasVrchatSelection)
        {
            if (!hasProject) return Model(FaceMotionUxState.CreateProject, HintCreateProject);
            if (!hasAvatar) return Model(FaceMotionUxState.SelectAvatar, HintSelectAvatar);
            if (!hasAnimation) return Model(FaceMotionUxState.CreateAnimation, HintCreateAnimation);
            if (!hasTrack) return Model(FaceMotionUxState.AddTrack, HintAddTrack);
            if (!hasAuthoredKey) return Model(FaceMotionUxState.AddKey, HintAddKey);
            if (!hasPreview) return Model(FaceMotionUxState.StartPreview, HintStartPreview);
            if (!hasVrchatSelection) return Model(FaceMotionUxState.SelectForVrchat, HintSelectForVrchat);
            return Model(FaceMotionUxState.PreviewAndIntegrate, HintPreviewAndIntegrate);
        }

        /// <summary>Compatibility seam for older state-only callers.</summary>
        public static FaceMotionGuidanceModel Evaluate(bool hasProject, bool hasAvatar, bool hasAnimation, bool hasTrack, bool hasPreview)
        {
            return Evaluate(hasProject, hasAvatar, hasAnimation, hasTrack, hasTrack, hasPreview, hasPreview);
        }

        private static FaceMotionGuidanceModel Model(FaceMotionUxState state, string hintKey)
        {
            return new FaceMotionGuidanceModel(state, (int)state, hintKey);
        }

        private static bool HasAnyAnimation(FaceMotionProject project)
        {
            if (project == null || project.Animations == null)
            {
                return false;
            }

            for (int i = 0; i < project.Animations.Count; i++)
            {
                if (project.Animations[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAuthoredKey(FaceMotionAnimationData animation)
        {
            for (int i = 0; i < animation.Timeline.Tracks.Count; i++)
            {
                FaceTrackData track = animation.Timeline.Tracks[i];
                if (track == null)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape &&
                    track.BlendShape != null &&
                    HasAuthoredFloatKey(track.BlendShape.Keys))
                {
                    return true;
                }

                if (TrackKinds.IsTransform(track.Kind) &&
                    track.Transform != null &&
                    HasAuthoredVectorKey(track.Transform.Keys))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAuthoredFloatKey(System.Collections.Generic.IReadOnlyList<FloatKeyframeData> keys)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && !keys[i].Origin.IsBaseline)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAuthoredVectorKey(System.Collections.Generic.IReadOnlyList<Vector3KeyframeData> keys)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && !keys[i].Origin.IsBaseline)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountTracks(FaceMotionAnimationData animation)
        {
            if (animation == null || animation.Timeline == null || animation.Timeline.Tracks == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < animation.Timeline.Tracks.Count; i++)
            {
                if (animation.Timeline.Tracks[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

    }
}
