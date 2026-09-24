using System;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.VRChat;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>
    /// Avatar selection, index build/rebuild, hierarchy-dirty flag, and binding diagnostics.
    /// The window coalesces hierarchy notifications and calls RebuildIndex after mutations settle.
    /// </summary>
    public sealed class AvatarController
    {
        private readonly FaceMotionEditorSession _session;
        private readonly Action<VRCAvatarDescriptor> _buildObserver;

        public AvatarController(FaceMotionEditorSession session)
            : this(session, null)
        {
        }

        internal AvatarController(
            FaceMotionEditorSession session,
            Action<VRCAvatarDescriptor> buildObserver)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _buildObserver = buildObserver;
        }

        public bool HasAvatar => _session.ActiveDescriptor != null;

        public void SetDescriptor(VRCAvatarDescriptor descriptor)
        {
            if (ReferenceEquals(descriptor, _session.ActiveDescriptor))
            {
                if (descriptor != null)
                {
                    return;
                }

                if (_session.ActiveDescriptor == null)
                {
                    return;
                }
            }

            if (descriptor == null)
            {
                _session.ClearAvatar();
                return;
            }

            BuildAvatar(descriptor);
        }

        public void RebuildIndex()
        {
            VRCAvatarDescriptor descriptor = _session.ActiveDescriptor;
            if (descriptor == null)
            {
                return;
            }

            BuildAvatar(descriptor);
        }

        /// <summary>Ensures preview consumers never use a missing or hierarchy-stale index.</summary>
        public bool EnsureAvatarIndexCurrent()
        {
            if (_session.ActiveDescriptor != null &&
                (_session.ActiveAvatarIndex == null || _session.AvatarIndexDirty))
            {
                BuildAvatar(_session.ActiveDescriptor);
                return true;
            }
            return false;
        }

        /// <summary>Refreshes the active index after a FaceMotion integration operation settles.</summary>
        public bool RefreshIndexAfterIntegration(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || !ReferenceEquals(descriptor, _session.ActiveDescriptor)) return false;
            MarkAvatarDirtyFromHierarchy();
            return EnsureAvatarIndexCurrent();
        }

        public void RebuildBindings()
        {
            _session.RecomputeBindingDiagnostics();
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        /// <summary>
        /// Called on EditorApplication.hierarchyChanged. The notification is global, so compare
        /// the selected avatar before dirtying it; preview clone changes are unrelated.
        /// </summary>
        public void MarkAvatarDirtyFromHierarchy()
        {
            if (_session.ActiveAvatarRoot != null
                && !_session.AvatarIndexDirty
                && (_session.ActiveObjectCache == null
                    || !_session.ActiveObjectCache.MatchesCurrentHierarchy(_session.ActiveAvatarRoot)))
            {
                _session.AvatarIndexDirty = true;
                _session.NotifyChanged();
            }
        }

        private void BuildAvatar(VRCAvatarDescriptor descriptor)
        {
            _buildObserver?.Invoke(descriptor);
            var validation = VRCAvatarDescriptorAdapter.Validate(descriptor);
            var cache = new UnityAvatarObjectCache();
            var report = cache.Rebuild(descriptor.gameObject);
            var candidates = AvatarCandidateSnapshot.Build(report.Index, descriptor);
            _session.SetAvatar(descriptor, descriptor.gameObject, validation, report, cache, candidates);
        }
    }
}
