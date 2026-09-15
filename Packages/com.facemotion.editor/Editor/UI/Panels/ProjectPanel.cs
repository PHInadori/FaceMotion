using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    public sealed class ProjectPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly ProjectController _project;

        public ProjectPanel(FaceMotionEditorSession session, ProjectController project)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
            _project = project ?? throw new System.ArgumentNullException(nameof(project));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("project"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(FaceMotionUiText.Get("openProject"), EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(true)))
            {
                string absolute = EditorUtility.OpenFilePanel(FaceMotionUiText.Get("openProjectDialog"), "Assets", "asset");
                if (!string.IsNullOrEmpty(absolute))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(ToProjectRelativePath(absolute));
                    _project.LoadProject(asset);
                }
            }

            if (GUILayout.Button(FaceMotionUiText.Get("newProject"), EditorStyles.miniButtonRight, GUILayout.ExpandWidth(true)))
            {
                _project.CreateProject();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(FaceMotionUiText.Get("saveProject"), EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
            {
                _project.SaveProject();
            }

            string label = _session.ActiveProjectAssetPath ?? FaceMotionUiText.Get("noProject");
            EditorGUILayout.LabelField(FaceMotionUiText.Get("asset"), label);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("animations"), (_session.ActiveProject?.Animations?.Count ?? 0).ToString());
        }

        /// <summary>Converts an OS path under the project to an Assets-relative path.</summary>
        internal static string ToProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }

            string dataPath = Application.dataPath;
            if (absolutePath.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
            {
                string rel = absolutePath.Substring(dataPath.Length).Replace('\\', '/');
                return "Assets" + rel;
            }

            return absolutePath;
        }
    }
}
