using System.Collections.Generic;
using FaceMotion.Data;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    public enum VrchatIntegrationStatus { NotApplied, Applied }

    public static class VrchatIntegrationStatusService
    {
        public static Dictionary<string, VrchatIntegrationStatus> Resolve(FaceMotionProject project, VRCAvatarDescriptor avatar, ModularAvatarManagedStateSnapshot snapshot)
        {
            var result = new Dictionary<string, VrchatIntegrationStatus>();
            if (project == null) return result;
            for (var i = 0; i < project.Animations.Count; i++)
            {
                var animation = project.Animations[i];
                if (animation == null) continue;
                var parameter = OneClickIntegrationService.MaParameterName(new OneClickIntegrationRequest(avatar, animation, project, null));
                var status = VrchatIntegrationStatus.NotApplied;
                if (snapshot != null) for (var j = 0; j < snapshot.Items.Count; j++) if (FaceMotionIntegrationIdentityMatcher.Matches(animation, parameter, snapshot.Items[j])) { status = VrchatIntegrationStatus.Applied; break; }
                result[animation.AnimationId] = status;
            }
            return result;
        }
    }
}
