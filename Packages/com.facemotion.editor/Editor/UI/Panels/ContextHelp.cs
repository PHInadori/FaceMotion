using FaceMotion.Editor.UI.Localization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>Transient, read-only section help; no project or editor preferences are changed.</summary>
    internal static class ContextHelp
    {
        internal static bool Toggle(bool open) => !open;

        internal static string Text(string key) => FaceMotionUiText.Get(key);

        internal static void DrawHeader(string titleKey, string helpKey, ref bool open)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Text(titleKey), EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("?", Text("contextHelpTooltip")), EditorStyles.miniButton,
                    GUILayout.Width(24f), GUILayout.Height(20f)))
            {
                open = Toggle(open);
            }

            EditorGUILayout.EndHorizontal();
            if (open)
            {
                EditorGUILayout.HelpBox(Text(helpKey), MessageType.Info);
            }
        }
    }
}
