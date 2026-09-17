using System;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.VRChat.Integration;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>
    /// Thin session-aware wrapper for the one-click integration flow. It owns no workflow
    /// logic: it resolves the current selection, delegates to OneClickIntegrationService,
    /// updates session diagnostics, and highlights the generated object on success.
    /// </summary>
    public sealed class OneClickIntegrationController
    {
        private readonly FaceMotionEditorSession _session;
        private OneClickPreflight _preflight;
        private int _preflightVersion = -1;
        private OneClickIntegrationResult _lastResult;

        public OneClickIntegrationController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public OneClickIntegrationResult LastResult => _lastResult;

        /// <summary>Recomputes the write-free preflight only when the session changed.</summary>
        public OneClickPreflight GetPreflight()
        {
            if (_preflightVersion == _session.Version && _preflight != null)
            {
                return _preflight;
            }

            _preflight = OneClickIntegrationService.Preflight(BuildRequest());
            _preflightVersion = _session.Version;
            return _preflight;
        }

        public OneClickIntegrationResult Execute(Action<OneClickStage> progress)
        {
            var result = OneClickIntegrationService.Execute(BuildRequest(), progress);
            _lastResult = result;
            if (result.Diagnostics.Count > 0)
            {
                _session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
            }

            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
            if (result.Succeeded && result.Manifest != null)
            {
                Selection.activeObject = result.Manifest;
                EditorGUIUtility.PingObject(result.Manifest);
            }

            return result;
        }

        private OneClickIntegrationRequest BuildRequest()
        {
            var avatar = _session.ActiveAvatarRoot == null
                ? null
                : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            return new OneClickIntegrationRequest(
                avatar,
                _session.GetSelectedAnimation(),
                _session.ActiveProject,
                null);
        }
    }
}