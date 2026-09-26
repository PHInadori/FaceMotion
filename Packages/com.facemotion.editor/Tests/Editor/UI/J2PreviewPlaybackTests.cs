using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Window;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        [Test]
        public void PreviewControl_PlayPauseResumeAndStop_KeepExplicitReview()
        {
            SetupAnimation(false);
            var panel = new PreviewPanel(_session, null, null, _playback);
            Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
            Assert.That(panel.TogglePlayback(), Is.True);
            Assert.That(_playback.IsPlaying, Is.True);
            Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);

            _playback.Tick(10d);
            _playback.Tick(10.25d);
            Assert.That(panel.TogglePlayback(), Is.True);
            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);

            Assert.That(panel.TogglePlayback(), Is.True);
            Assert.That(_playback.IsPlaying, Is.True);
            _playback.Stop();
            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.Zero);
            Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
        }

        [Test]
        public void PreviewControl_WithoutAnimation_DoesNotAcknowledgeReview()
        {
            var panel = new PreviewPanel(_session, null, null, _playback);
            Assert.That(panel.TogglePlayback(), Is.False);
            Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
        }

        [Test]
        public void Space_WhenPreviewActive_UsesTheSamePlayPauseControl()
        {
            SetupAnimation(false);
            using (var fixture = AvatarFixture.Create())
            using (var preview = new PreviewSession())
            {
                preview.EnsureAvatar(fixture.Root);
                var panel = new PreviewPanel(_session, preview, null, _playback);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.False);
                var play = Key(KeyCode.Space);
                Assert.That(Space(play, true, false, string.Empty, preview.IsActive, false, panel), Is.True);
                Assert.That(play.type, Is.EqualTo(EventType.Used));
                Assert.That(_playback.IsPlaying, Is.True);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);

                var pause = Key(KeyCode.Space);
                Assert.That(Space(pause, true, false, string.Empty, preview.IsActive, false, panel), Is.True);
                Assert.That(pause.type, Is.EqualTo(EventType.Used));
                Assert.That(_playback.IsPlaying, Is.False);
                Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);

                var resume = Key(KeyCode.Space);
                Assert.That(Space(resume, true, false, string.Empty, preview.IsActive, false, panel), Is.True);
                Assert.That(_playback.IsPlaying, Is.True);
            }
        }

        [TestCase("FaceMotion.AnimationList.rename")]
        [TestCase("FaceMotion.AnimationList.duration")]
        [TestCase("FaceMotion.AnimationList.frameRate")]
        [TestCase("FaceMotion.KeyInspector.time")]
        [TestCase("FaceMotion.KeyInspector.quickKeyValue")]
        [TestCase("FaceMotion.KeyInspector.blendShape")]
        public void Space_NamedTextAndNumericEditors_DoNotToggle(string focusedControl)
        {
            SetupAnimation(false);
            var panel = new PreviewPanel(_session, null, null, _playback);
            var e = Key(KeyCode.Space);
            Assert.That(Space(e, true, false, focusedControl, true, false, panel), Is.False);
            Assert.That(e.type, Is.EqualTo(EventType.KeyDown));
            Assert.That(_playback.IsPlaying, Is.False);
        }

        [Test]
        public void Space_SearchAndUnnamedTextEditing_DoNotToggle()
        {
            SetupAnimation(false);
            var panel = new PreviewPanel(_session, null, null, _playback);
            var e = Key(KeyCode.Space);
            Assert.That(Space(e, true, true, string.Empty, true, false, panel), Is.False);
            Assert.That(e.type, Is.EqualTo(EventType.KeyDown));
            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(Space(Key(KeyCode.Space), true, false, string.Empty, true, false, panel), Is.True,
                "The first Space after leaving a numeric field should work.");
        }

        [Test]
        public void Space_OutsideWindowOrPreview_WhileDraggingOrModified_IsNotConsumed()
        {
            SetupAnimation(false);
            var panel = new PreviewPanel(_session, null, null, _playback);
            Assert.That(Space(Key(KeyCode.Space), false, false, string.Empty, true, false, panel), Is.False);
            Assert.That(Space(Key(KeyCode.Space), true, false, string.Empty, false, false, panel), Is.False);
            Assert.That(Space(Key(KeyCode.Space), true, false, string.Empty, true, true, panel), Is.False);
            var modified = Key(KeyCode.Space);
            modified.control = true;
            Assert.That(Space(modified, true, false, string.Empty, true, false, panel), Is.False);
            Assert.That(_playback.IsPlaying, Is.False);
        }

        [Test]
        public void Space_UnrelatedKeysAndKeyUp_RemainUntouched()
        {
            SetupAnimation(false);
            var panel = new PreviewPanel(_session, null, null, _playback);
            var unrelated = Key(KeyCode.F);
            Assert.That(Space(unrelated, true, false, string.Empty, true, false, panel), Is.False);
            Assert.That(unrelated.type, Is.EqualTo(EventType.KeyDown));
            var keyUp = new Event { type = EventType.KeyUp, keyCode = KeyCode.Space };
            Assert.That(Space(keyUp, true, false, string.Empty, true, false, panel), Is.False);
            Assert.That(_playback.IsPlaying, Is.False);
        }

        [Test]
        public void PreviewControl_AtEndpoint_RestartsAndLoopWrapsWithoutChangingReview()
        {
            SetupAnimation(false);
            var panel = new PreviewPanel(_session, null, null, _playback);
            Assert.That(panel.TogglePlayback(), Is.True);
            _playback.Tick(10d);
            _playback.Tick(11d);
            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(1f));
            Assert.That(_session.HasReviewedCurrentAnimation(), Is.True);
            Assert.That(panel.TogglePlayback(), Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.Zero);
            _session.GetSelectedAnimation().Timeline.Loop = true;
            _playback.Tick(20d);
            _playback.Tick(21.25d);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void PreviewControl_NarrowLayout_ContainsAllButtonsAfterToggle()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 180f, 400f));
            PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect);
            foreach (Rect rect in new[] { controls.Start, controls.StopPreview, controls.SceneApply,
                         controls.Play, controls.PlaybackStop, controls.Fit, controls.Reset })
            {
                Assert.That(rect.width, Is.GreaterThan(0f));
                Assert.That(rect.x, Is.GreaterThanOrEqualTo(layout.ControlsRect.x));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(layout.ControlsRect.xMax));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.yMax));
            }
            Assert.That(controls.Play.width, Is.GreaterThan(controls.PlaybackStop.width));
        }

        private static Event Key(KeyCode code) => new Event { type = EventType.KeyDown, keyCode = code };

        private static bool Space(Event e, bool focused, bool editing, string name,
            bool previewActive, bool gestureActive, PreviewPanel panel)
        {
            return FaceMotionWindow.HandlePreviewSpace(e, focused, editing, name, previewActive, gestureActive, panel);
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
