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
        public const string HintStartPreview = "guidanceHintPreview";
        public const string HintPreviewAndIntegrate = "guidanceHintReady";

        /// <summary>Derives the guidance model from live session state; preview is only ready while it plays.</summary>
        public static FaceMotionGuidanceModel Evaluate(FaceMotionEditorSession session, bool hasPreview)
        {
            if (session == null) return Evaluate(false, false, false, false, hasPreview);
            FaceMotionAnimationData animation = session.GetSelectedAnimation();
            bool hasAnimation = animation != null && animation.Timeline != null;
            return Evaluate(session.ActiveProject != null, session.ActiveDescriptor != null, hasAnimation, hasAnimation && CountTracks(animation) > 0, hasPreview);
        }

        /// <summary>
        /// The six beginner steps, in workflow order. Each step is evaluated before the next, so
        /// removing any prerequisite moves the hint backwards instead of skipping ahead.
        /// </summary>
        public static FaceMotionGuidanceModel Evaluate(bool hasProject, bool hasAvatar, bool hasAnimation, bool hasTrack, bool hasPreview)
        {
            if (!hasProject) return new FaceMotionGuidanceModel(FaceMotionUxState.CreateProject, 1, HintCreateProject);
            if (!hasAvatar) return new FaceMotionGuidanceModel(FaceMotionUxState.SelectAvatar, 2, HintSelectAvatar);
            if (!hasAnimation) return new FaceMotionGuidanceModel(FaceMotionUxState.CreateAnimation, 3, HintCreateAnimation);
            if (!hasPreview) return new FaceMotionGuidanceModel(FaceMotionUxState.StartPreview, 4, HintStartPreview);
            if (!hasTrack) return new FaceMotionGuidanceModel(FaceMotionUxState.AddTrack, 5, HintAddTrack);
            return new FaceMotionGuidanceModel(FaceMotionUxState.PreviewAndIntegrate, 6, HintPreviewAndIntegrate);
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
