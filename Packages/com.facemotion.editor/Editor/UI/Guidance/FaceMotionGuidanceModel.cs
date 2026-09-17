namespace FaceMotion.Editor.UI.Guidance
{
    /// <summary>
    /// Immutable presentation model for the single "next action" guidance strip. It carries
    /// only a state, a step number, and a localization key; the window owns all rendering.
    /// </summary>
    public sealed class FaceMotionGuidanceModel
    {
        public FaceMotionGuidanceModel(FaceMotionUxState state, int stepNumber, string hintKey)
        {
            State = state;
            StepNumber = stepNumber;
            HintKey = hintKey;
        }

        public FaceMotionUxState State { get; }

        public int StepNumber { get; }

        public string HintKey { get; }

        /// <summary>True when the user has reached the final preview/integration step.</summary>
        public bool IsComplete => State == FaceMotionUxState.PreviewAndIntegrate;
    }
}
