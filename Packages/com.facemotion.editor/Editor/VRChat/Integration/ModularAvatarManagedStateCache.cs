using System;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>Window-session scoped cache; consumers never discover MA manifests directly.</summary>
    public sealed class ModularAvatarManagedStateCache
    {
        private IModularAvatarIntegrationBackend _backend;
        private VRCAvatarDescriptor _avatar;
        private ModularAvatarManagedStateSnapshot _snapshot;
        public int DiscoveryCount { get; private set; }

        public ModularAvatarManagedStateSnapshot Get(IModularAvatarIntegrationBackend backend, VRCAvatarDescriptor avatar)
        {
            if (backend == null || avatar == null) return null;
            if (!ReferenceEquals(_backend, backend) || !ReferenceEquals(_avatar, avatar) || _snapshot == null)
            {
                _backend = backend; _avatar = avatar;
                _snapshot = backend.InspectManagedState(avatar);
                DiscoveryCount++;
            }
            return _snapshot;
        }

        public void Invalidate(VRCAvatarDescriptor avatar = null)
        {
            if (avatar == null || ReferenceEquals(_avatar, avatar)) _snapshot = null;
        }
    }
}
