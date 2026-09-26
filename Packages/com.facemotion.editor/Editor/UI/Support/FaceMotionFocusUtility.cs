using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>Shared IMGUI focus release for an unclaimed FaceMotion window background click.</summary>
    internal static class FaceMotionFocusUtility
    {
        internal static bool ShouldReleaseOnBackgroundMouseDown(
            EventType eventType,
            int button,
            int hotControl,
            bool editingTextField)
        {
            return eventType == EventType.MouseDown
                && button == 0
                && hotControl == 0
                && editingTextField;
        }

        internal static void ClearTextFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }
    }
}
