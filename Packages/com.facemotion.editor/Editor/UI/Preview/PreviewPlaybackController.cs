using System;
using FaceMotion.Editor.UI.Session;
using UnityEngine;

namespace FaceMotion.Editor.UI.Preview
{
    /// <summary>
    /// Advances the shared session playhead while preview playback is active. Time flows
    /// from an explicit realtime source passed to <see cref="Tick"/> so tests can drive
    /// exact deltas; the window passes EditorApplication.timeSinceStartup.
    /// </summary>
    public sealed class PreviewPlaybackController
    {
        private readonly FaceMotionEditorSession _session;
        private bool _initialized;
        private double _lastRealtime;

        public PreviewPlaybackController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool IsPlaying => _session.ViewState.IsPlaying;

        public bool CanPlay
        {
            get
            {
                var animation = _session.GetSelectedAnimation();
                return animation != null && animation.Timeline != null && animation.Timeline.Duration > 0f;
            }
        }

        public void Toggle()
        {
            if (IsPlaying)
            {
                Pause();
                return;
            }

            Play();
        }

        public void Play()
        {
            if (!CanPlay)
            {
                return;
            }

            var animation = _session.GetSelectedAnimation();
            float duration = animation.Timeline.Duration;
            if (duration > 0f && _session.ViewState.CurrentTime >= duration)
            {
                _session.SetCurrentTime(0f);
            }

            _session.ViewState.IsPlaying = true;
            _initialized = false;
            _session.NotifyChanged();
        }

        public void Pause()
        {
            _initialized = false;
            if (!IsPlaying)
            {
                return;
            }

            _session.ViewState.IsPlaying = false;
            _session.NotifyChanged();
        }

        public void Stop()
        {
            _initialized = false;
            bool wasPlaying = IsPlaying;
            _session.ViewState.IsPlaying = false;
            _session.SetCurrentTime(0f);
            if (wasPlaying)
            {
                _session.NotifyChanged();
            }
        }

        public void Tick(double realtimeNow)
        {
            if (!IsPlaying)
            {
                return;
            }

            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                _session.ViewState.IsPlaying = false;
                return;
            }

            if (!_initialized)
            {
                _lastRealtime = realtimeNow;
                _initialized = true;
                return;
            }

            float dt = Mathf.Max(0f, (float)(realtimeNow - _lastRealtime));
            _lastRealtime = realtimeNow;
            if (dt <= 0f)
            {
                return;
            }

            float duration = animation.Timeline.Duration;
            float t = _session.ViewState.CurrentTime + dt;
            if (duration > 0f)
            {
                if (animation.Timeline.Loop)
                {
                    t = t % duration;
                }
                else
                {
                    if (t >= duration)
                    {
                        // Publish the exact endpoint while playback is still active so the
                        // session change evaluates the final authored pose exactly once.
                        if (_session.ViewState.CurrentTime != duration)
                        {
                            _session.SetCurrentTime(duration);
                        }
                        _session.ViewState.IsPlaying = false;
                        return;
                    }
                }
            }

            if (_session.ViewState.CurrentTime != t)
            {
                _session.SetCurrentTime(t);
            }
        }
    }
}
