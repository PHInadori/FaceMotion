namespace FaceMotion.Editor.UI.Localization
{
    /// <summary>
    /// One language's localized user explanation for a single diagnostic code. Displayed
    /// fields may be empty; the presentation layer falls back per field to the other
    /// language and finally to the original diagnostic message.
    /// </summary>
    public sealed class DiagnosticLocalizedText
    {
        public DiagnosticLocalizedText(
            string title,
            string summary,
            string cause,
            string impact,
            string resolution,
            string caution)
        {
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            Cause = cause ?? string.Empty;
            Impact = impact ?? string.Empty;
            Resolution = resolution ?? string.Empty;
            Caution = caution ?? string.Empty;
        }

        public string Title { get; }

        public string Summary { get; }

        public string Cause { get; }

        public string Impact { get; }

        public string Resolution { get; }

        public string Caution { get; }
    }

    /// <summary>Convenience immutable field bundle used by the catalog registration.</summary>
    public struct DiagnosticLocalizedFields
    {
        public string Title;
        public string Summary;
        public string Cause;
        public string Impact;
        public string Resolution;
        public string Caution;
    }
}