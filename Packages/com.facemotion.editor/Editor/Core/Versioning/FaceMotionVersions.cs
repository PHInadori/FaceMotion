namespace FaceMotion.Versioning
{
    public static class FaceMotionVersions
    {
        public const string ToolVersion = "0.1.0";
        public const int ProjectSchemaVersion = 1;
        public const int PresetSchemaVersion = 1;
        public const int IntegrationManifestVersion = 1;
        public const int GeneratorAlgorithmVersion = 1;
        public const int IntegrationBackendVersion = 1;
        public const int MappingProfileSchemaVersion = 1;
        public const int AvatarFingerprintAlgorithmVersion = 1;
        public const int AvatarFingerprintFormatVersion = 1;

        /// <summary>Version 0 is the legacy equivalent of "no version field was present".</summary>
        public const int LegacySchemaVersion = 0;

        /// <summary>Algorithm version 0 means the pass predates algorithm versions; it must never be lifted.</summary>
        public const int LegacyGeneratorAlgorithmVersion = 0;
    }
}
