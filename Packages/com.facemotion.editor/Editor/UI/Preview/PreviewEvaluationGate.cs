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
        private int _pendingRevision;
        private bool _pending;
        private FaceMotionAnimationData _lastFlushedAnimation;
        private float _lastFlushedTime;
        private int _lastFlushedRevision;
        private bool _hasLastFlushed;

        public PreviewEvaluationGate(Action<FaceMotionAnimationData, float> flush)
        {
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        }

        /// <summary>True while the timeline is in a scrub drag gesture.</summary>
        public bool Scrubbing { get; set; }

        public bool Pending => _pending;
        /// <summary>Number of coalesced flushes that actually evaluated.</summary>
        public int FlushCount { get; private set; }

        public void Synchronize(FaceMotionAnimationData animation, float time, int revision)
        {
            if (Scrubbing)
            {
                _pending = true;
                _pendingAnimation = animation;
                _pendingTime = time;
                _pendingRevision = revision;
                return;
            }

            EvaluateIfNeeded(animation, time, revision, false);
        }

        /// <summary>Pushes the latest pending sample once, if any. Returns true when it evaluated.</summary>
        public bool FlushPending(int currentRevision)
        {
            if (!_pending)
            {
                return false;
            }

            return Flush(currentRevision);
        }

        /// <summary>End-of-gesture flush: the exact final snapped time is evaluated once.</summary>
        public void EndScrub(int currentRevision)
        {
            if (_pending)
            {
                Flush(currentRevision);
            }
        }

        private bool Flush(int currentRevision)
        {
            _pending = false;
            var animation = _pendingAnimation;
            float time = _pendingTime;
            int revision = _pendingRevision;
            _pendingAnimation = null;
            _pendingTime = 0f;
            _pendingRevision = 0;
            if (revision != currentRevision) return false;
            return EvaluateIfNeeded(animation, time, revision, true);
        }

        private bool EvaluateIfNeeded(FaceMotionAnimationData animation, float time, int revision, bool coalesced)
        {
            // Repeated out-of-bounds MouseDrag events all clamp to the same endpoint. They
            // must not re-evaluate the full avatar pose while the pointer remains outside.
            if (_hasLastFlushed && ReferenceEquals(animation, _lastFlushedAnimation) &&
                Math.Abs(time - _lastFlushedTime) < 0.0001f && revision == _lastFlushedRevision)
            {
                return false;
            }
            _hasLastFlushed = true;
            _lastFlushedAnimation = animation;
            _lastFlushedTime = time;
            _lastFlushedRevision = revision;
            if (coalesced) FlushCount++;
            _flush(animation, time);
            return true;
        }
    }
}
