using FaceMotion.Editor.UI.Localization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>
    /// Compact, always-available shortcut reference. It is collapsed by default and persists its
    /// state, so it never adds permanent visual weight to the window.
    /// </summary>
    public sealed class ShortcutHelpPanel
    {
        internal const string FoldoutKey = "FaceMotion.Window.v2.ShortcutHelpOpen";

        internal static readonly string[] ShortcutKeys =
        {
            "shortcutTimelineZoom",
            "shortcutDeleteKey",
            "shortcutPreviewCamera",
            "shortcutFocus",
            "shortcutSelectKey",
            "shortcutToggleMultiSelect",
            "shortcutRangeSelect",
            "shortcutClearSelection",
            "shortcutMoveKeys",
            "shortcutCopyKeys",
            "shortcutPasteKeys",
            "shortcutDuplicateKeys",
            "shortcutNudgeKeys",
            "shortcutMultiKeyInspector"
        };

        public void OnGUI()
        {
            bool open = EditorPrefs.GetBool(FoldoutKey, false);
            bool next = EditorGUILayout.Foldout(open, FaceMotionUiText.Get("shortcutHelpTitle"), true);
            if (next != open)
            {
                EditorPrefs.SetBool(FoldoutKey, next);
            }

            if (!next)
            {
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < ShortcutKeys.Length; i++)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get(ShortcutKeys[i]), EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUI.indentLevel--;
        }
    }
}
