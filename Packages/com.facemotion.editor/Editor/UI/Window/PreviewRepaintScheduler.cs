namespace FaceMotion.Editor.UI.Window
{
    /// <summary>Coalesces scrub mutations into one repaint on the next editor callback.</summary>
    internal sealed class PreviewRepaintScheduler
    {
        public bool Pending { get; private set; }
        public int RequestCount { get; private set; }
        public int DispatchCount { get; private set; }

        /// <returns>True when the caller must schedule the deferred callback.</returns>
        public bool Request()
        {
            RequestCount++;
            if (Pending)
            {
                return false;
            }

            Pending = true;
            return true;
        }

        /// <returns>True when a pending repaint was consumed.</returns>
        public bool Dispatch()
        {
            if (!Pending)
            {
                return false;
            }

            Pending = false;
            DispatchCount++;
            return true;
        }

        public void Cancel()
        {
            Pending = false;
        }
    }
}
