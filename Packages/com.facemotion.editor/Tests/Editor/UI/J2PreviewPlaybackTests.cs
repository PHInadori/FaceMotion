using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using NUnit.Framework;
using UnityEditor;

namespace FaceMotion.Editor.Tests
{
    public sealed class J2PreviewPlaybackTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private PreviewPlaybackController _playback;
        private TempFaceMotionAsset _temp;

        [SetUp]
        public void SetUp()
        {
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _playback = new PreviewPlaybackController(_session);
            _temp = new TempFaceMotionAsset();
        }

        [TearDown]
        public void TearDown()
        {
            _temp?.Dispose();
        }

        [Test]
        public void Play_SetsPlayingAndFirstTickOnlyInitializesClock()
        {
            SetupAnimation(false);
            _session.SetCurrentTime(0.4f);

            _playback.Play();
            _playback.Tick(10d);

            Assert.That(_playback.IsPlaying, Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void Tick_AdvancesPlayheadByElapsedRealtime()
        {
            SetupAnimation(false);

            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.25d);

            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void LoopPlayback_WrapsAtDuration()
        {
            SetupAnimation(true);
            _session.SetCurrentTime(0.9f);

            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.25d);

            Assert.That(_playback.IsPlaying, Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.15f).Within(1e-5f));
        }

        [Test]
        public void NonLoopPlayback_StopsAtDuration()
        {
            SetupAnimation(false);
            _session.SetCurrentTime(0.9f);

            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.25d);

            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Pause_KeepsCurrentPlayhead()
        {
            SetupAnimation(false);
            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.5d);

            _playback.Pause();

            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Stop_StopsAndReturnsPlayheadToZero()
        {
            SetupAnimation(false);
            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.5d);

            _playback.Stop();

            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.Zero);
        }

        [Test]
        public void Play_AtDurationEnd_RestartsFromZero()
        {
            SetupAnimation(false);
            _session.SetCurrentTime(1f);

            _playback.Play();

            Assert.That(_playback.IsPlaying, Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.Zero);
        }

        [Test]
        public void Play_WithoutAnimation_StaysStopped()
        {
            _playback.Play();

            Assert.That(_playback.CanPlay, Is.False);
            Assert.That(_playback.IsPlaying, Is.False);
        }

        [Test]
        public void Tick_AfterPause_DoesNotAdvance()
        {
            SetupAnimation(false);
            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.25d);
            _playback.Pause();

            _playback.Tick(11d);

            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void Toggle_SwitchesBetweenPlayAndPause()
        {
            SetupAnimation(false);

            _playback.Toggle();
            Assert.That(_playback.IsPlaying, Is.True);

            _playback.Toggle();
            Assert.That(_playback.IsPlaying, Is.False);
        }

        private void SetupAnimation(bool loop)
        {
            var project = FaceMotionProject.CreateNew();
            string path = _temp.AssetPath("J2Playback");
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, path);
            Assert.That(_animations.Add(), Is.Not.Null);
            _session.GetSelectedAnimation().Timeline.Duration = 1f;
            _session.GetSelectedAnimation().Timeline.Loop = loop;
        }
    }
}
