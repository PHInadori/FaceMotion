using FaceMotion.Data;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>Shared stable-ID then safe-legacy identity rules for MA state consumers.</summary>
    public static class FaceMotionIntegrationIdentityMatcher
    {
        public static bool Matches(FaceMotionAnimationData animation, string canonicalParameter, ModularAvatarManagedState state)
        {
            if (animation == null || state == null) return false;
            return state.HasAnimationId
                ? state.AnimationId == animation.AnimationId
                : state.ParameterName == canonicalParameter;
        }
    }
}
