using System;
using FaceMotion.Data;

namespace FaceMotion.Editor.UI.Preview
{
    /// <summary>
    /// Coalesces preview/scene evaluation during interactive scrub drags so intermediate
    /// samples never trigger work per event. Latest request wins; EndScrub and FlushPending
    /// push the final value exactly once. Outside a scrub gesture every request evaluates
    /// synchronously, preserving one-to-one semantics used by direct scrub calls and tests.
    /// </summary>
    public sealed class PreviewEvaluationGate
    {
        private readonly Action<FaceMotionAnimationData, float> _flush;
        private FaceMotionAnimationData _pendingAnimation;
        private float _pendingTime;
        private bool _pending;

        public PreviewEvaluationGate(Action<FaceMotionAnimationData, float> flush)
        {
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        }

        /// <summary>True while the timeline is in a scrub drag gesture.</summary>
        public bool Scrubbing { get; set; }

        public bool Pending => _pending;

        /// <summary>Number of coalesced flushes that actually evaluated.</summary>
        public int FlushCount { get; private set; }

        public void Synchronize(FaceMotionAnimationData animation, float time)
        {
            if (Scrubbing)
            {
                _pending = true;
                _pendingAnimation = animation;
                _pendingTime = time;
                return;
            }

            _flush(animation, time);
        }

        /// <summary>Pushes the latest pending sample once, if any. Returns true when it evaluated.</summary>
        public bool FlushPending()
        {
            if (!_pending)
            {
                return false;
            }

            Flush();
            return true;
        }

        /// <summary>End-of-gesture flush: the exact final snapped time is evaluated once.</summary>
        public void EndScrub()
        {
            if (_pending)
            {
                Flush();
            }
        }

        private void Flush()
        {
            _pending = false;
            FlushCount++;
            var animation = _pendingAnimation;
            float time = _pendingTime;
            _pendingAnimation = null;
            _pendingTime = 0f;
            _flush(animation, time);
        }
    }
}