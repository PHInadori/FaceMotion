namespace FaceMotion.Timeline
{
    /// <summary>
    /// Kind of a composition track. Values are serialized and stable.
    /// </summary>
    public enum TrackKind
    {
        BlendShape = 0,
        TransformPosition = 1,
        TransformRotation = 2,
        TransformScale = 3
    }

    public static class TrackKinds
    {
        /// <summary>True for the three transform track kinds.</summary>
        public static bool IsTransform(TrackKind kind)
        {
            return kind >= TrackKind.TransformPosition && kind <= TrackKind.TransformScale;
        }
    }
}