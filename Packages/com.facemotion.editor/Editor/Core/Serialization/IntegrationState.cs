namespace FaceMotion.Serialization
{
    /// <summary>
    /// Serialized runtime-verified state of a persisted integration. Attached means the
    /// avatar currently references the generated integration assets; Detached means the
    /// generated assets and manifest are retained but the avatar no longer references them.
    /// Unknown is the legacy default and is resolved lazily by the migration services.
    /// </summary>
    public enum IntegrationState
    {
        Unknown = 0,
        Attached = 1,
        Detached = 2
    }
}