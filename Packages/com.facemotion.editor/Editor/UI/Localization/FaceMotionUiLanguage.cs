using UnityEngine;

namespace FaceMotion.Editor.UI.Localization
{
    /// <summary>Resolves the product UI language from Unity's editor language.</summary>
    public static class FaceMotionUiLanguage
    {
        public static FaceMotionDiagnosticLanguage Resolve()
        {
            return Application.systemLanguage == SystemLanguage.Japanese
                ? FaceMotionDiagnosticLanguage.Japanese
                : FaceMotionDiagnosticLanguage.English;
        }
    }
}