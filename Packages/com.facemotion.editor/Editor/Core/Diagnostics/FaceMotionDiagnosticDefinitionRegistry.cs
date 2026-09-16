using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    /// <summary>
    /// Central registry of non-UI diagnostic metadata, keyed by diagnostic code. Lookup is
    /// O(1) through a prebuilt Dictionary. Definitions are registered per category in
    /// separate partial files so the 137-entry surface stays readable.
    /// </summary>
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        private static readonly Dictionary<string, DiagnosticDefinition> Definitions =
            new Dictionary<string, DiagnosticDefinition>(System.StringComparer.Ordinal);

        static FaceMotionDiagnosticDefinitionRegistry()
        {
            RegisterDataCodes(Definitions);
            RegisterMigrationCodes(Definitions);
            RegisterAvatarCodes(Definitions);
            RegisterMappingCodes(Definitions);
            RegisterCommandUiExportCodes(Definitions);
            RegisterIntegrationDirectCodes(Definitions);
            RegisterModularAvatarCodes(Definitions);
        }

        /// <summary>Returns the definition for a code, or null when the code is unknown.</summary>
        public static DiagnosticDefinition Get(string code)
        {
            if (code == null)
            {
                return null;
            }

            Definitions.TryGetValue(code, out DiagnosticDefinition definition);
            return definition;
        }

        public static IReadOnlyDictionary<string, DiagnosticDefinition> All => Definitions;

        private static void Add(
            Dictionary<string, DiagnosticDefinition> defs,
            string code,
            FaceMotionDiagnosticActionLevel nonBlockingActionLevel,
            string category,
            FaceMotionDiagnosticSelectionKind selectionKind = FaceMotionDiagnosticSelectionKind.None,
            bool canAutoFix = false)
        {
            defs[code] = new DiagnosticDefinition(
                code,
                nonBlockingActionLevel,
                category,
                canAutoFix,
                selectionKind);
        }

        static partial void RegisterDataCodes(Dictionary<string, DiagnosticDefinition> defs);
        static partial void RegisterMigrationCodes(Dictionary<string, DiagnosticDefinition> defs);
        static partial void RegisterAvatarCodes(Dictionary<string, DiagnosticDefinition> defs);
        static partial void RegisterMappingCodes(Dictionary<string, DiagnosticDefinition> defs);
        static partial void RegisterCommandUiExportCodes(Dictionary<string, DiagnosticDefinition> defs);
        static partial void RegisterIntegrationDirectCodes(Dictionary<string, DiagnosticDefinition> defs);
        static partial void RegisterModularAvatarCodes(Dictionary<string, DiagnosticDefinition> defs);
    }
}
