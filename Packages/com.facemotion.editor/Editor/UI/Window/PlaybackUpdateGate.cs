namespace FaceMotion.Editor.UI.Window
{
    /// <summary>Keeps the editor update callback subscribed only while playback needs ticks.</summary>
    internal sealed class PlaybackUpdateGate
    {
        public bool IsRegistered { get; private set; }

        /// <returns>True when the caller must change its EditorApplication subscription.</returns>
        public bool Synchronize(bool shouldBeRegistered)
        {
            if (IsRegistered == shouldBeRegistered)
            {
                return false;
            }

            IsRegistered = shouldBeRegistered;
            return true;
        }

        public bool Reset()
        {
            return Synchronize(false);
        }
    }
}
