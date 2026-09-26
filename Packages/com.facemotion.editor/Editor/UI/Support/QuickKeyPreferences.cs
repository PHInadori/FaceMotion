using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>Per-user authoring preference for the BlendShape Quick Key action.</summary>
    internal static class QuickKeyPreferences
    {
        internal const string ValueKey = "FaceMotion.Window.v4.QuickKeyValue";
        internal const float DefaultValue = 100f;

        internal static float Value
        {
            get
            {
                return Clamp(EditorPrefs.GetFloat(ValueKey, DefaultValue));
            }
            set
            {
                EditorPrefs.SetFloat(ValueKey, Clamp(value));
            }
        }

        internal static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultValue;
            }

            return Mathf.Clamp(value, 0f, 100f);
        }
    }
}
