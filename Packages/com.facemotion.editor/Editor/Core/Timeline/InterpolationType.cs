namespace FaceMotion.Timeline
{
    /// <summary>
    /// Interpolation applied to the segment leaving a key. Values are serialized and
    /// stable; the numeric values below are a release contract and must never be
    /// reordered, renumbered, or reused once a schema has been released.
    /// </summary>
    public enum InterpolationType
    {
        /// <summary>Holds the left value for the whole segment; switches at the exact right key time.</summary>
        Hold = 0,

        /// <summary>Linear interpolation across the segment.</summary>
        Linear = 1,

        /// <summary>Quadratic ease-in (slow start, fast finish).</summary>
        EaseIn = 2,

        /// <summary>Quadratic ease-out (fast start, slow finish).</summary>
        EaseOut = 3,

        /// <summary>Quadratic ease-in-out (slow start and finish, fast middle).</summary>
        EaseInOut = 4,

        /// <summary>Smoothstep (symmetric S-curve, identical first derivative at both ends).</summary>
        Smooth = 5
    }
}