using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    /// <summary>
    /// Central registry of per-code localized user explanations, Japanese and English.
    /// Lookup is O(1) through prebuilt Dictionaries. Entries are registered per category in
    /// separate partial files so the 135-entry surface stays readable.
    /// </summary>
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        private static readonly Dictionary<string, DiagnosticLocalizedText> Japanese =
            new Dictionary<string, DiagnosticLocalizedText>(System.StringComparer.Ordinal);

        private static readonly Dictionary<string, DiagnosticLocalizedText> English =
            new Dictionary<string, DiagnosticLocalizedText>(System.StringComparer.Ordinal);

        static FaceMotionDiagnosticLocalizationCatalog()
        {
            RegisterDataTexts(Japanese, English);
            RegisterMigrationTexts(Japanese, English);
            RegisterAvatarTexts(Japanese, English);
            RegisterMappingTexts(Japanese, English);
            RegisterCommandUiExportTexts(Japanese, English);
            RegisterIntegrationDirectTexts(Japanese, English);
            RegisterModularAvatarTexts(Japanese, English);
        }

        public static IReadOnlyDictionary<string, DiagnosticLocalizedText> JapaneseEntries => Japanese;

        public static IReadOnlyDictionary<string, DiagnosticLocalizedText> EnglishEntries => English;

        public static bool TryGet(
            string code,
            FaceMotionDiagnosticLanguage language,
            out DiagnosticLocalizedText text)
        {
            if (code == null)
            {
                text = null;
                return false;
            }

            var table = language == FaceMotionDiagnosticLanguage.Japanese ? Japanese : English;
            return table.TryGetValue(code, out text);
        }

        /// <summary>The other product language, used as the per-field fallback.</summary>
        public static FaceMotionDiagnosticLanguage Other(FaceMotionDiagnosticLanguage language)
        {
            return language == FaceMotionDiagnosticLanguage.Japanese
                ? FaceMotionDiagnosticLanguage.English
                : FaceMotionDiagnosticLanguage.Japanese;
        }

        private static void Add(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en,
            string code,
            DiagnosticLocalizedFields fieldsJa,
            DiagnosticLocalizedFields fieldsEn)
        {
            ja[code] = new DiagnosticLocalizedText(
                fieldsJa.Title, fieldsJa.Summary, fieldsJa.Cause,
                fieldsJa.Impact, fieldsJa.Resolution, fieldsJa.Caution);
            en[code] = new DiagnosticLocalizedText(
                fieldsEn.Title, fieldsEn.Summary, fieldsEn.Cause,
                fieldsEn.Impact, fieldsEn.Resolution, fieldsEn.Caution);
        }

        static partial void RegisterDataTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);

        static partial void RegisterMigrationTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);

        static partial void RegisterAvatarTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);

        static partial void RegisterMappingTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);

        static partial void RegisterCommandUiExportTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);

        static partial void RegisterIntegrationDirectTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);

        static partial void RegisterModularAvatarTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en);
    }
}