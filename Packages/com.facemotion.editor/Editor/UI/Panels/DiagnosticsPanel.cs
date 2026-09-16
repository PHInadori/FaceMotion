using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Session;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>
    /// Diagnostics list with a drill-down detail view. Rows show severity icon, title,
    /// summary, action level, and code. The detail view keeps user-facing guidance separate
    /// from the raw generator message, which is available in a collapsed technical foldout.
    /// The "Select" button never mutates anything: it only highlights the resolved object in
    /// the Hierarchy.
    /// </summary>
    public sealed class DiagnosticsPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly List<FaceMotionDiagnosticPresentation> _presentations =
            new List<FaceMotionDiagnosticPresentation>();
        private readonly HashSet<int> _expandedTechnicalDetails = new HashSet<int>();

        private FaceMotionDiagnosticLanguage _language;
        private int _selectedIndex = -1;
        private string _copiedCode;
        private double _copyFlashUntil;

        public DiagnosticsPanel(FaceMotionEditorSession session)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
        }

        public void OnGUI()
        {
            _language = FaceMotionUiLanguage.Resolve();
            RebuildPresentations();

            EditorGUILayout.LabelField(Text("diagnostics"), EditorStyles.boldLabel);

            if (_presentations.Count == 0)
            {
                EditorGUILayout.HelpBox(Text("noIssues"), MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _presentations.Count; i++)
                {
                    DrawItem(i, _presentations[i]);
                }
            }

            if (GUILayout.Button(Text("refresh"), EditorStyles.miniButton))
            {
                _session.RefreshAll();
            }
        }

        private void RebuildPresentations()
        {
            _presentations.Clear();
            IReadOnlyList<FaceMotionDiagnostic> diagnostics = _session.Diagnostics;
            if (diagnostics == null)
            {
                _selectedIndex = -1;
                return;
            }

            for (int i = 0; i < diagnostics.Count; i++)
            {
                FaceMotionDiagnostic diagnostic = diagnostics[i];
                if (diagnostic == null)
                {
                    continue;
                }

                _presentations.Add(
                    new FaceMotionDiagnosticPresentation(diagnostic, _language));
            }

            if (_selectedIndex >= _presentations.Count)
            {
                _selectedIndex = -1;
            }

            _expandedTechnicalDetails.RemoveWhere(index => index >= _presentations.Count);
        }

        private void DrawItem(int index, FaceMotionDiagnosticPresentation p)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool isSelected = _selectedIndex == index;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(SeverityIcon(p.Severity), GUILayout.Width(20), GUILayout.Height(20));
            isSelected = EditorGUILayout.Foldout(isSelected, p.Title, true);
            GUILayout.FlexibleSpace();
            GUILayout.Label(p.ActionLevelText, EditorStyles.miniLabel);

            if (GUILayout.Button(p.Code, EditorStyles.miniLabel))
            {
                Copy(p);
            }

            if (GUILayout.Button(CopyLabel(p), EditorStyles.miniButton))
            {
                Copy(p);
            }

            if (FaceMotionDiagnosticSelectionResolver.CanResolve(p, AvatarRoot, ObjectCache)
                && GUILayout.Button(Text("select"), EditorStyles.miniButton))
            {
                Select(p);
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label(p.Summary, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndVertical();

            if (isSelected)
            {
                _selectedIndex = index;
                DrawDetail(index, p);
            }
        }

        private void DrawDetail(int index, FaceMotionDiagnosticPresentation p)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(p.Title, EditorStyles.boldLabel);

            DrawField(Text("diagnosticCode"), p.Code);
            DrawField(Text("severity"), p.SeverityText);
            DrawField(Text("actionLevel"), p.ActionLevelText);
            DrawField(Text("context"), p.ContextId);
            DrawField(Text("cause"), p.Cause);
            DrawField(Text("impact"), p.Impact);
            DrawField(Text("resolution"), p.Resolution);
            DrawField(Text("caution"), p.Caution);

            bool expanded = _expandedTechnicalDetails.Contains(index);
            expanded = EditorGUILayout.Foldout(expanded, Text("technicalDetails"), true);
            if (expanded)
            {
                _expandedTechnicalDetails.Add(index);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawField(Text("rawDiagnosticMessage"), p.Detail);
                DrawField(Text("contextId"), p.ContextId);
                DrawField(Text("diagnosticCode"), p.Code);
                DrawField(Text("category"), p.Category);
                EditorGUILayout.EndVertical();
            }
            else
            {
                _expandedTechnicalDetails.Remove(index);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawField(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EditorGUILayout.LabelField(label, value, EditorStyles.wordWrappedLabel);
        }

        private GameObject AvatarRoot => _session.ActiveAvatarRoot;

        private FaceMotion.Editor.Avatar.UnityAvatarObjectCache ObjectCache => _session.ActiveObjectCache;

        private void Select(FaceMotionDiagnosticPresentation p)
        {
            Transform target = FaceMotionDiagnosticSelectionResolver.Resolve(
                p,
                _session.ActiveAvatarRoot,
                _session.ActiveObjectCache);
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
        }

        private void Copy(FaceMotionDiagnosticPresentation p)
        {
            EditorGUIUtility.systemCopyBuffer = p.Code;
            _copiedCode = p.Code;
            _copyFlashUntil = EditorApplication.timeSinceStartup + 1.5d;
        }

        private string CopyLabel(FaceMotionDiagnosticPresentation p)
        {
            if (_copiedCode == p.Code
                && EditorApplication.timeSinceStartup < _copyFlashUntil)
            {
                return Text("copied");
            }

            return Text("copyCode");
        }

        private string Text(string key)
        {
            return FaceMotionUiText.Get(
                key,
                _language == FaceMotionDiagnosticLanguage.Japanese
                    ? SystemLanguage.Japanese
                    : SystemLanguage.English);
        }

        private static GUIContent SeverityIcon(FaceMotionDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case FaceMotionDiagnosticSeverity.Error:
                    return EditorGUIUtility.IconContent("console.erroricon");
                case FaceMotionDiagnosticSeverity.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon");
                default:
                    return EditorGUIUtility.IconContent("console.infoicon");
            }
        }
    }
}
