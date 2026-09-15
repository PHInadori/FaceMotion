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
    /// The index is only rebuilt on explicit user action; hierarchy changes only set a flag.
    /// </summary>
    public sealed class AvatarController
    {
        private readonly FaceMotionEditorSession _session;

        public AvatarController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
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

        public void RebuildBindings()
        {
            _session.RecomputeBindingDiagnostics();
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        /// <summary>Called on EditorApplication.hierarchyChanged; only sets the dirty flag.</summary>
        public void MarkAvatarDirtyFromHierarchy()
        {
            if (_session.ActiveAvatarRoot != null && !_session.AvatarIndexDirty)
            {
                _session.AvatarIndexDirty = true;
                _session.NotifyChanged();
            }
        }

        private void BuildAvatar(VRCAvatarDescriptor descriptor)
        {
            var validation = VRCAvatarDescriptorAdapter.Validate(descriptor);
            var cache = new UnityAvatarObjectCache();
            var report = cache.Rebuild(descriptor.gameObject);
            var candidates = AvatarCandidateSnapshot.Build(report.Index);
            _session.SetAvatar(descriptor, descriptor.gameObject, validation, report, cache, candidates);
        }
    }
}