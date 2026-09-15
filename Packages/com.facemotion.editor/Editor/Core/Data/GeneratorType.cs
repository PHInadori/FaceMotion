namespace FaceMotion.Data
{
    /// <summary>
    /// Identifies which generator produced a generation record. Values are serialized;
    /// numeric values are stable and must not be reordered or reused.
    /// </summary>
    public enum GeneratorType
    {
        Preset = 0,
        Blink = 1,
        Random = 2,
        Imported = 3
    }
}