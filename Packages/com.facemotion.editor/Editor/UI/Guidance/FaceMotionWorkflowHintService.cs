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
        public const string HintSelectAvatar = "guidanceHintAvatar";
        public const string HintCreateAnimation = "guidanceHintAnimation";
        public const string HintAddTrack = "guidanceHintTrack";
        public const string HintAddKey = "guidanceHintKey";
        public const string HintPreviewAndIntegrate = "guidanceHintReady";

        /// <summary>Derives the guidance model from live session state.</summary>
        public static FaceMotionGuidanceModel Evaluate(FaceMotionEditorSession session)
        {
            if (session == null)
            {
                return Evaluate(hasAvatar: false, hasAnimation: false, hasTrack: false, hasKey: false);
            }

            bool hasAvatar = session.ActiveDescriptor != null;
            FaceMotionAnimationData animation = session.GetSelectedAnimation();
            bool hasAnimation = animation != null && animation.Timeline != null;
            bool hasTrack = hasAnimation && CountTracks(animation) > 0;
            bool hasKey = hasTrack && HasAnyKey(animation);
            return Evaluate(hasAvatar, hasAnimation, hasTrack, hasKey);
        }

        /// <summary>Pure, side-effect-free state resolution, kept separate so it is trivial to test.</summary>
        public static FaceMotionGuidanceModel Evaluate(bool hasAvatar, bool hasAnimation, bool hasTrack, bool hasKey)
        {
            if (!hasAvatar)
            {
                return new FaceMotionGuidanceModel(FaceMotionUxState.SelectAvatar, 1, HintSelectAvatar);
            }

            if (!hasAnimation)
            {
                return new FaceMotionGuidanceModel(FaceMotionUxState.CreateAnimation, 2, HintCreateAnimation);
            }

            if (!hasTrack)
            {
                return new FaceMotionGuidanceModel(FaceMotionUxState.AddTrack, 3, HintAddTrack);
            }

            if (!hasKey)
            {
                return new FaceMotionGuidanceModel(FaceMotionUxState.AddKey, 4, HintAddKey);
            }

            return new FaceMotionGuidanceModel(FaceMotionUxState.PreviewAndIntegrate, 5, HintPreviewAndIntegrate);
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

        private static bool HasAnyKey(FaceMotionAnimationData animation)
        {
            if (animation == null || animation.Timeline == null || animation.Timeline.Tracks == null)
            {
                return false;
            }

            for (int i = 0; i < animation.Timeline.Tracks.Count; i++)
            {
                FaceTrackData track = animation.Timeline.Tracks[i];
                if (track == null)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape && track.BlendShape != null && track.BlendShape.Keys.Count > 0)
                {
                    return true;
                }

                if (TrackKinds.IsTransform(track.Kind) && track.Transform != null && track.Transform.Keys.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
