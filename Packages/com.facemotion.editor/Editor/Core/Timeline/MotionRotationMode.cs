namespace FaceMotion.Timeline
{
    /// <summary>
    /// Rotation evaluation mode for transform-rotation tracks. Values are serialized and
    /// stable. EulerContinuous is a reserved future mode; the first evaluator implements
    /// ShortestQuaternion only and 360-degree spin is not supported.
    /// </summary>
    public enum MotionRotationMode
    {
        ShortestQuaternion = 0,
        EulerContinuous = 1
    }
}