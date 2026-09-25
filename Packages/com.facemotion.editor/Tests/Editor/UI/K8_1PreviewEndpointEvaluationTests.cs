using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Editor.Preview;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// K8-1: non-loop playback must publish the exact authored duration through the real
    /// session -> change -> gate -> pose path before playback stops, so the final authored
    /// value reaches the scene exactly once instead of an approximate sample near the end.
    /// </summary>
    public sealed class K8_1PreviewEndpointEvaluationTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private PreviewPlaybackController _playback;
        private PreviewEvaluationGate _gate;
        private SceneApplySession _sceneApply;
        private TempFaceMotionAsset _temp;
        private AvatarFixture _avatar;
        private FaceMotionAnimationData _animation;
        private readonly List<float> _changedAtDuration = new List<float>();
        private readonly List<bool> _playingDuringEndpoint = new List<bool>();
        private readonly List<float> _appliedTimes = new List<float>();

        [SetUp]
        public void SetUp()
        {
            _avatar = AvatarFixture.Create();
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _playback = new PreviewPlaybackController(_session);
            _sceneApply = new SceneApplySession();
            _temp = new TempFaceMotionAsset();
            _gate = new PreviewEvaluationGate((animation, time) =>
            {
                _appliedTimes.Add(time);
                _sceneApply.Apply(animation, time);
            });
            _changedAtDuration.Clear();
            _playingDuringEndpoint.Clear();
            _appliedTimes.Clear();

            var project = FaceMotionProject.CreateNew();
            string path = _temp.AssetPath("K8Endpoint");
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, path);
            Assert.That(_animations.Add(), Is.Not.Null);

            _animation = _session.GetSelectedAnimation();
            _animation.Timeline.Duration = 1f;
            _animation.Timeline.Loop = false;
            var track = FaceTrackData.CreateBlendShape(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f, InterpolationType.Linear));
            track.BlendShape.AddKey(FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear));
            _animation.Timeline.AddTrack(track);

            _sceneApply.Start(_avatar.Root);
            _session.Changed += OnSessionChanged;
        }

        [TearDown]
        public void TearDown()
        {
            _session.Changed -= OnSessionChanged;
            _sceneApply.Dispose();
            _temp?.Dispose();
            _avatar?.Dispose();
        }

        [Test]
        public void NonLoopOvershoot_PublishesExactEndpointOnceAndAppliesFinalPose()
        {
            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(11.5d);

            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(1f));
            Assert.That(_changedAtDuration, Has.Count.EqualTo(1), "The endpoint must be published exactly once.");
            Assert.That(_playingDuringEndpoint, Is.EqualTo(new[] { true }),
                "The endpoint is evaluated while playback is still active, then playback stops.");
            Assert.That(_appliedTimes, Has.Count.EqualTo(2), "Initial sample plus the endpoint sample.");
            Assert.That(_appliedTimes[_appliedTimes.Count - 1], Is.EqualTo(1f));
            Assert.That(Weight(), Is.EqualTo(100f), "The exact final authored value must be applied.");
        }

        [Test]
        public void NonLoopExactHit_StopsExactlyOnTheDurationSample()
        {
            _session.SetCurrentTime(0.75f);
            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.25d);

            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(1f));
            Assert.That(_changedAtDuration, Has.Count.EqualTo(1));
            Assert.That(Weight(), Is.EqualTo(100f));
        }

        [Test]
        public void PlayAlreadyAtDuration_RestartsThenStopsOnTheEndpointOnce()
        {
            _session.SetCurrentTime(1f);
            _changedAtDuration.Clear();
            _appliedTimes.Clear();

            _playback.Play();
            Assert.That(_session.ViewState.CurrentTime, Is.Zero);
            _playback.Tick(10d);
            _playback.Tick(12d);

            Assert.That(_playback.IsPlaying, Is.False);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(1f));
            Assert.That(_changedAtDuration, Has.Count.EqualTo(1));
            Assert.That(Weight(), Is.EqualTo(100f));
        }

        [Test]
        public void LoopPlayback_NeverPublishesTheEndpointAndKeepsPlaying()
        {
            _animation.Timeline.Loop = true;
            _session.SetCurrentTime(0.9f);
            _changedAtDuration.Clear();
            _appliedTimes.Clear();

            _playback.Play();
            _playback.Tick(10d);
            _playback.Tick(10.25d);

            Assert.That(_playback.IsPlaying, Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(_changedAtDuration, Is.Empty, "Looping playback wraps instead of stopping on the endpoint.");
            Assert.That(Weight(), Is.EqualTo(15f).Within(1e-2f));
        }

        private float Weight()
        {
            var renderer = AvatarFixture.FaceRendererPath;
            var found = _avatar.Root.transform.Find(renderer);
            Assert.That(found, Is.Not.Null, renderer);
            var smr = (SkinnedMeshRenderer)found.GetComponent(typeof(SkinnedMeshRenderer));
            Assert.That(smr, Is.Not.Null);
            return smr.GetBlendShapeWeight(smr.sharedMesh.GetBlendShapeIndex("Mouth_Smile"));
        }

        /// <summary>Mirrors FaceMotionWindow.SynchronizePreviewFromSession for non-scrub playback.</summary>
        private void OnSessionChanged()
        {
            float duration = _session.GetSelectedDuration();
            if (_session.ViewState.CurrentTime == duration)
            {
                _changedAtDuration.Add(duration);
                _playingDuringEndpoint.Add(_playback.IsPlaying);
            }

            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                return;
            }

            _gate.Scrubbing = false;
            _gate.Synchronize(animation, _session.ViewState.CurrentTime, _session.PreviewRevision);
        }
    }
}
