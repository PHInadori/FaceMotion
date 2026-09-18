using System;
using System.Collections.Generic;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.VRChat.Integration;
using UnityEditor;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>Session bridge for K2. Checkbox IDs are snapshotted before any Unity mutation.</summary>
    public sealed class BatchIntegrationController
    {
        private readonly FaceMotionEditorSession _session;
        public BatchIntegrationController(FaceMotionEditorSession session) { _session = session ?? throw new ArgumentNullException(nameof(session)); }
        public BatchIntegrationResult LastResult { get; private set; }

        public BatchIntegrationResult Execute(Action<BatchIntegrationStage, int, int> progress)
        {
            var ids = new List<string>(_session.BatchAnimationIds);
            var avatar = _session.ActiveAvatarRoot == null ? null : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            LastResult = BatchIntegrationService.Execute(new BatchIntegrationRequest(avatar, _session.ActiveProject, ids, null), progress);
            if (LastResult.Diagnostics.Count > 0) _session.SetLastOperationDiagnostic(LastResult.Diagnostics[LastResult.Diagnostics.Count - 1]);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
            if (LastResult.Succeeded && LastResult.GeneratedObject != null) { Selection.activeObject = LastResult.GeneratedObject; EditorGUIUtility.PingObject(LastResult.GeneratedObject); }
            return LastResult;
        }
    }
}
