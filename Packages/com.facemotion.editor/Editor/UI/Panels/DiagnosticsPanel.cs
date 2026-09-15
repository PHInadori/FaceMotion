using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    public sealed class DiagnosticsPanel
    {
        private readonly FaceMotionEditorSession _session;

        public DiagnosticsPanel(FaceMotionEditorSession session)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("diagnostics"), EditorStyles.boldLabel);

            IReadOnlyList<FaceMotionDiagnostic> diagnostics = _session.Diagnostics;
            if (diagnostics == null || diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("noIssues"), MessageType.Info);
            }
            else
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    var d = diagnostics[i];
                    if (d == null)
                    {
                        continue;
                    }

                    EditorGUILayout.HelpBox(FaceMotionUiText.FormatDiagnostic(d.Code, d.Message, d.SuggestedFix), ToMessageType(d));
                }
            }

            if (GUILayout.Button(FaceMotionUiText.Get("refresh"), EditorStyles.miniButton))
            {
                _session.RefreshAll();
            }
        }

        private static MessageType ToMessageType(FaceMotionDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                return MessageType.None;
            }

            if (diagnostic.Severity == FaceMotionDiagnosticSeverity.Error)
            {
                return MessageType.Error;
            }

            return diagnostic.Severity == FaceMotionDiagnosticSeverity.Warning
                ? MessageType.Warning
                : MessageType.Info;
        }
    }
}
