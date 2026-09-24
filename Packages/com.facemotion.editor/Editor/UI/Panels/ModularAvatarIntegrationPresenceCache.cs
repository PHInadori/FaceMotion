using System;
using System.Collections.Generic;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.VRChat.Integration;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>
    /// Keeps Modular Avatar integration presence queries off the per-repaint path. The
    /// backend scan runs at most once per session version (and once per avatar), so rapid
    /// IMGUI repaints reuse the last result instead of calling AssetDatabase every frame.
    /// Per-parameter presence is cached per parameter per version.
    /// </summary>
    internal sealed class ModularAvatarIntegrationPresenceCache
    {
        private readonly IModularAvatarIntegrationBackend _backend;
        private readonly FaceMotionEditorSession _session;
        private readonly Dictionary<string, bool> _parameterHits = new Dictionary<string, bool>(StringComparer.Ordinal);
        private int _version = -1;
        private VRCAvatarDescriptor _cachedAvatar;
        private bool _hasIntegration;
        private bool _hasIntegrationDirty = true;

        public ModularAvatarIntegrationPresenceCache(IModularAvatarIntegrationBackend backend, FaceMotionEditorSession session)
        {
            _backend = backend;
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool HasExistingIntegration(VRCAvatarDescriptor avatar)
        {
            if (_backend == null || avatar == null)
            {
                return false;
            }

            if (IsStale(avatar))
            {
                _cachedAvatar = avatar;
                _version = _session.Version;
                _parameterHits.Clear();
                _hasIntegrationDirty = true;
            }

            if (_hasIntegrationDirty)
            {
                _hasIntegration = _backend.HasExistingIntegration(avatar);
                _hasIntegrationDirty = false;
            }

            return _hasIntegration;
        }

        public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter)
        {
            if (_backend == null || avatar == null || string.IsNullOrEmpty(parameter))
            {
                return false;
            }

            if (IsStale(avatar))
            {
                _cachedAvatar = avatar;
                _version = _session.Version;
                _parameterHits.Clear();
                _hasIntegrationDirty = true;
            }

            if (_parameterHits.TryGetValue(parameter, out bool hit))
            {
                return hit;
            }

            bool value = _backend.HasExistingIntegration(avatar, parameter);
            _parameterHits[parameter] = value;
            return value;
        }

        private bool IsStale(VRCAvatarDescriptor avatar)
        {
            return !ReferenceEquals(_cachedAvatar, avatar) || _version != _session.Version;
        }
    }
}