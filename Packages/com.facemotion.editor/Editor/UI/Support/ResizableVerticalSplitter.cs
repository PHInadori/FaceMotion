using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>Small IMGUI splitter for independently scrollable list panes.</summary>
    internal sealed class ResizableVerticalSplitter
    {
        public const float HandleHeight = 5f;

        private readonly string _preferenceKey;
        private float _dragStartY;
        private float _dragStartHeight;
        private int _controlId;

        public ResizableVerticalSplitter(string preferenceKey, float defaultHeight, float minimum, float maximum)
        {
            _preferenceKey = preferenceKey;
            Height = Clamp(EditorPrefs.GetFloat(preferenceKey, defaultHeight), minimum, maximum);
        }

        public float Height { get; private set; }

        public void Draw(float minimum, float maximum)
        {
            Rect handle = GUILayoutUtility.GetRect(0f, HandleHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(handle, new Color(1f, 1f, 1f, 0.12f));
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeVertical);

            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            _controlId = GUIUtility.GetControlID(FocusType.Passive, handle);
            if (current.type == EventType.MouseDown && current.button == 0 && handle.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = _controlId;
                _dragStartY = current.mousePosition.y;
                _dragStartHeight = Height;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && GUIUtility.hotControl == _controlId)
            {
                Height = Clamp(_dragStartHeight + current.mousePosition.y - _dragStartY, minimum, maximum);
                GUI.changed = true;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp && GUIUtility.hotControl == _controlId)
            {
                GUIUtility.hotControl = 0;
                EditorPrefs.SetFloat(_preferenceKey, Height);
                current.Use();
            }
        }

        internal static float Clamp(float height, float minimum, float maximum)
        {
            return Mathf.Clamp(height, minimum, Mathf.Max(minimum, maximum));
        }
    }
}
