namespace FaceMotion.Diagnostics
{
    /// <summary>
    /// Stable diagnostic codes used across data, migration, validation, and commands.
    /// Codes follow the FM-&lt;AREA&gt;-&lt;NUMBER&gt; pattern and are stable once released.
    /// </summary>
    public static class FaceMotionDiagnosticCodes
    {
        public const string NullProject = "FM-DATA-0001";
        public const string InvalidProjectId = "FM-DATA-0002";

        public const string InvalidAnimationId = "FM-ANIM-0001";
        public const string DuplicateAnimationId = "FM-ANIM-0002";
        public const string NullAnimation = "FM-ANIM-0003";
        public const string NullTimeline = "FM-ANIM-0004";

        public const string InvalidDuration = "FM-TIM-0001";
        public const string InvalidFrameRate = "FM-TIM-0002";

        public const string InvalidTrackId = "FM-TRK-0001";
        public const string DuplicateTrackId = "FM-TRK-0002";
        public const string NullTrack = "FM-TRK-0003";
        public const string PayloadMismatch = "FM-TRK-0004";
        public const string UnsupportedRotationMode = "FM-TRK-0005";
        public const string NullPayload = "FM-TRK-0006";
        public const string DuplicateTrackBinding = "FM-TRK-0007";

        public const string InvalidKeyId = "FM-KEY-0001";
        public const string DuplicateKeyId = "FM-KEY-0002";
        public const string NullKey = "FM-KEY-0003";
        public const string DuplicateKeyTime = "FM-KEY-0004";
        public const string UnsortedKeys = "FM-KEY-0005";
        public const string NegativeKeyTime = "FM-KEY-0006";
        public const string KeyBeyondDuration = "FM-KEY-0007";
        public const string NonFiniteKeyValue = "FM-KEY-0008";
        public const string NonFiniteKeyTime = "FM-KEY-0009";
        public const string ManualKeyWithGenerationId = "FM-KEY-0010";

        public const string OrphanedGenerationKey = "FM-GEN-0001";
        public const string NullGenerationRecord = "FM-GEN-0002";
        public const string DuplicateGenerationId = "FM-GEN-0003";
        public const string ManualKeyProtected = "FM-GEN-0004";

        public const string FutureSchema = "FM-MIG-0001";
        public const string UninitializedSchema = "FM-MIG-0002";
        public const string MissingMigrationStep = "FM-MIG-0003";
        public const string MigrationFailed = "FM-MIG-0004";
        public const string MigrationValidationFailed = "FM-MIG-0005";

        // Phase I.2: schema migration / compatibility codes. Released once used.
        public const string UpgradedSchema = "FM-MIG-UPGRADED";
        public const string FutureSchemaBlocked = "FM-MIG-FUTURE-VERSION";
        public const string MalformedData = "FM-MIG-MALFORMED";
        public const string IdRepaired = "FM-MIG-ID-REPAIRED";
        public const string DuplicateId = "FM-MIG-DUPLICATE-ID";
        public const string PartialRecovery = "FM-MIG-PARTIAL";
        public const string IntegrationAmbiguous = "FM-MIG-INTEGRATION-AMBIGUOUS";

        public const string CommandInvalidProject = "FM-CMD-0001";
        public const string CommandTargetNotFound = "FM-CMD-0002";
        public const string CommandInvalidArgument = "FM-CMD-0003";
        public const string CommandKindMismatch = "FM-CMD-0004";

        public const string UIAssetCreateFailed = "FM-UI-0001";
        public const string UIProjectLoadFailed = "FM-UI-0002";
        public const string UISelectionInvalid = "FM-UI-0003";
        public const string UIPasteTrackMissing = "FM-UI-0004";
        public const string UIPasteCollision = "FM-UI-0005";

        public const string AvatarRootMissing = "FM-AVT-0001";
        public const string DescriptorMissing = "FM-AVT-0002";
        public const string InactiveAvatarRoot = "FM-AVT-0003";
        public const string MissingAnimator = "FM-AVT-0004";
        public const string AnimatorAvatarNull = "FM-AVT-0005";
        public const string NonHumanoidAvatar = "FM-AVT-0006";
        public const string DuplicateTransformPath = "FM-AVT-0007";
        public const string RendererSharedMeshMissing = "FM-AVT-0008";
        public const string DuplicateBlendShapeNameInMesh = "FM-AVT-0009";
        public const string FingerprintFailure = "FM-AVT-0010";

        public const string InvalidProfileId = "FM-MAP-0001";
        public const string NullMappingProfile = "FM-MAP-0002";
        public const string FutureProfileSchema = "FM-MAP-0003";
        public const string UninitializedProfileSchema = "FM-MAP-0004";
        public const string InvalidEntryId = "FM-MAP-0005";
        public const string DuplicateEntryId = "FM-MAP-0006";
        public const string MissingLogicalTargetId = "FM-MAP-0007";
        public const string InvalidLogicalTargetKind = "FM-MAP-0008";
        public const string MissingBinding = "FM-MAP-0009";
        public const string InvalidFingerprint = "FM-MAP-0010";
        public const string DuplicateLogicalTarget = "FM-MAP-0011";
        public const string MissingRenderer = "FM-MAP-0012";
        public const string MissingBlendShape = "FM-MAP-0013";
        public const string MissingTransform = "FM-MAP-0014";
        public const string AmbiguousBinding = "FM-MAP-0015";
        public const string InvalidBinding = "FM-MAP-0016";
        public const string ProfileFingerprintMismatch = "FM-MAP-0017";
        public const string StaleMappingButValid = "FM-MAP-0018";

        public const string ForeignContentInFolder = "FM-OWNERSHIP-FOREIGN-CONTENT";
        public const string TamperedOwnedPaths = "FM-OWNERSHIP-TAMPERED";

        // FM-EXPORT (AnimationClip exporter). Inline literals centralized here; the string
        // values are unchanged.
        public const string ExportCreateParentFailed = "FM-EXPORT-CREATE-PARENT-FAILED";
        public const string ExportPathOccupied = "FM-EXPORT-PATH-OCCUPIED";
        public const string ExportSucceeded = "FM-EXPORT-SUCCEEDED";
        public const string ExportWriteFailed = "FM-EXPORT-WRITE-FAILED";
        public const string ExportNoTimeline = "FM-EXPORT-NO-TIMELINE";
        public const string ExportInvalidDuration = "FM-EXPORT-INVALID-DURATION";
        public const string ExportInvalidFrameRate = "FM-EXPORT-INVALID-FRAMERATE";
        public const string ExportInvalidPath = "FM-EXPORT-INVALID-PATH";
        public const string ExportNullTrack = "FM-EXPORT-NULL-TRACK";
        public const string ExportInvalidBlendShape = "FM-EXPORT-INVALID-BLENDSHAPE";
        public const string ExportInvalidTransform = "FM-EXPORT-INVALID-TRANSFORM";
        public const string ExportUnsupportedRotation = "FM-EXPORT-UNSUPPORTED-ROTATION";
        public const string ExportInvalidKey = "FM-EXPORT-INVALID-KEY";

        // FM-G (Direct VRChat integration / generation step). Inline literals centralized.
        public const string GenerationAvatar = "FM-G-AVATAR";
        public const string GenerationPrefabAsset = "FM-G-PREFAB-ASSET";
        public const string GenerationClip = "FM-G-CLIP";
        public const string GenerationPath = "FM-G-PATH";
        public const string GenerationParameterName = "FM-G-PARAMETER-NAME";
        public const string GenerationOutputConflict = "FM-G-OUTPUT-CONFLICT";
        public const string GenerationPlan = "FM-G-PLAN";
        public const string GenerationApplied = "FM-G-APPLIED";
        public const string GenerationApply = "FM-G-APPLY";
        public const string GenerationRollback = "FM-G-ROLLBACK";
        public const string GenerationOwnership = "FM-G-OWNERSHIP";
        public const string GenerationRolledBack = "FM-G-ROLLED-BACK";
        public const string GenerationParameterConflict = "FM-G-PARAMETER-CONFLICT";
        public const string GenerationBudget = "FM-G-BUDGET";
        public const string GenerationMenuCapacity = "FM-G-MENU-CAPACITY";
        public const string GenerationFx = "FM-G-FX";
        public const string GenerationAnimatorParameterConflict = "FM-G-ANIMATOR-PARAMETER-CONFLICT";
        public const string GenerationLayerConflict = "FM-G-LAYER-CONFLICT";
        public const string GenerationWriteDefaults = "FM-G-WRITE-DEFAULTS";
        public const string GenerationBindingConflict = "FM-G-BINDING-CONFLICT";
        public const string GenerationManifest = "FM-G-MANIFEST";
        public const string GenerationPlanUnexpected = "FM-G-PLAN-UNEXPECTED";

        // FM-H-MA (optional Modular Avatar backend). Inline literals centralized.
        public const string ModularAvatarMetadataUnavailable = "FM-H-MA-METADATA-UNAVAILABLE";
        public const string ModularAvatarNotInstalled = "FM-H-MA-NOT-INSTALLED";
        public const string ModularAvatarVersionUnavailable = "FM-H-MA-VERSION-UNAVAILABLE";
        public const string ModularAvatarNotImplemented = "FM-H-MA-NOT-IMPLEMENTED";
        public const string ModularAvatarManifest = "FM-H-MA-MANIFEST";
        public const string ModularAvatarAvatar = "FM-H-MA-AVATAR";
        public const string ModularAvatarPrefabAsset = "FM-H-MA-PREFAB-ASSET";
        public const string ModularAvatarClip = "FM-H-MA-CLIP";
        public const string ModularAvatarPath = "FM-H-MA-PATH";
        public const string ModularAvatarParameterName = "FM-H-MA-PARAMETER-NAME";
        public const string ModularAvatarManifestConflict = "FM-H-MA-MANIFEST-CONFLICT";
        public const string ModularAvatarPlan = "FM-H-MA-PLAN";
        public const string ModularAvatarDetached = "FM-H-MA-DETACHED";
        public const string ModularAvatarApplied = "FM-H-MA-APPLIED";
        public const string ModularAvatarApply = "FM-H-MA-APPLY";
        public const string ModularAvatarOwnership = "FM-H-MA-OWNERSHIP";
        public const string ModularAvatarRemoved = "FM-H-MA-REMOVED";
        public const string ModularAvatarParameterConflict = "FM-H-MA-PARAMETER-CONFLICT";
        public const string ModularAvatarBindingConflict = "FM-H-MA-BINDING-CONFLICT";
        public const string ModularAvatarCrossBindingConflict = "FM-H-MA-CROSS-BINDING-CONFLICT";
        public const string ModularAvatarPlanUnexpected = "FM-H-MA-PLAN-UNEXPECTED";

        // FM-UI-INFO (controller status). Inline literal centralized.
        public const string UiInfo = "FM-UI-INFO";

        // FM-J4 (one-click VRChat integration). Inline literals centralized.
        public const string OneClickNoCurrentAnimation = "FM-J4-NO-ANIMATION";
        public const string OneClickNoAvatar = "FM-J4-NO-AVATAR";
        public const string OneClickForeignClip = "FM-J4-FOREIGN-CLIP";
        public const string OneClickBackendUnavailable = "FM-J4-BACKEND-UNAVAILABLE";
        public const string OneClickCrossBackend = "FM-J4-CROSS-BACKEND";
        public const string OneClickExported = "FM-J4-EXPORTED";
        public const string OneClickReapplied = "FM-J4-REAPPLIED";
        public const string OneClickSucceeded = "FM-J4-SUCCEEDED";
        public const string OneClickApplyStopped = "FM-J4-APPLY-STOPPED";
        public const string OneClickNoPartialState = "FM-J4-NO-PARTIAL-STATE";
    }
}
