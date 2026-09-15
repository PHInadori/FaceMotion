using System.Collections.Generic;
using System.Diagnostics;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class NormalizeValidationTests
    {
        private static FaceMotionProject BuildValidProject()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Valid");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(1f, 100f, InterpolationType.EaseOut));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);
            return project;
        }

        // ---- Normalize is mechanical only ----

        [Test]
        public void Normalize_RepairsNullCollections()
        {
            var project = BuildValidProject();
            var animation = project.Animations[0];

            ReflectionUtil.SetField(animation.Timeline, "_tracks", null);
            ReflectionUtil.SetField(project, "_generations", null);

            ProjectNormalizer.Normalize(project);

            Assert.That(project.Animations, Is.Not.Null);
            Assert.That(project.Animations.Count, Is.EqualTo(1));
            Assert.That(project.Generations, Is.Not.Null);
            Assert.That(project.Generations.Count, Is.Zero);
            Assert.That(project.Animations[0].Timeline, Is.Not.Null);
            Assert.That(project.Animations[0].Timeline.Tracks, Is.Not.Null);
            Assert.That(project.Animations[0].Timeline.Tracks.Count, Is.Zero);
        }

        [Test]
        public void Normalize_StableSortsUnsortedKeys()
        {
            var animation = FaceMotionAnimationData.Create("Sorted");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.6f, 60f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.2f, 20f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.4f, 40f));
            animation.Timeline.AddTrack(track);
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);

            ProjectNormalizer.Normalize(project);

            var keys = animation.Timeline.Tracks[0].BlendShape.Keys;
            Assert.That(keys.Count, Is.EqualTo(3));
            Assert.That(keys[0].Time, Is.EqualTo(0.2f));
            Assert.That(keys[1].Time, Is.EqualTo(0.4f));
            Assert.That(keys[2].Time, Is.EqualTo(0.6f));
            Assert.That(keys[2].Value, Is.EqualTo(60f));
        }

        [Test]
        public void Normalize_RemovesNullEntries()
        {
            var project = BuildValidProject();
            var animation = project.Animations[0];
            var timeline = animation.Timeline;

            ((List<FaceTrackData>)ReflectionUtil.GetFieldValue(timeline, "_tracks")).Add(null);
            ((List<FloatKeyframeData>)ReflectionUtil.GetFieldValue(timeline.Tracks[0].BlendShape, "_keys")).Add(null);

            ProjectNormalizer.Normalize(project);

            Assert.That(timeline.Tracks.Count, Is.EqualTo(1));
            Assert.That(timeline.Tracks[0].BlendShape.Keys.Count, Is.EqualTo(2));
        }

        [Test]
        public void Normalize_DoesNotFabricateUserValues()
        {
            var project = BuildValidProject();
            var animation = project.Animations[0];
            animation.Timeline.Duration = 0f;

            ProjectNormalizer.Normalize(project);

            Assert.That(animation.Timeline.Duration, Is.EqualTo(0f));
            var report = ProjectValidator.Validate(project);
            Assert.That(report.HasBlocking, Is.True);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidDuration), Is.Not.Null);
        }

        // ---- Validation: the clean baseline ----

        [Test]
        public void Validate_ValidProject_HasNoDiagnostics()
        {
            var report = ProjectValidator.Validate(BuildValidProject());
            Assert.That(report.HasBlocking, Is.False);
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Diagnostics, Is.Empty);
        }

        // ---- Blocking structural rules ----

        [Test]
        public void Validate_InvalidProjectId_IsBlocking()
        {
            var project = BuildValidProject();
            ReflectionUtil.SetField(project, "_projectId", "not-a-valid-id");
            var report = ProjectValidator.Validate(project);
            Assert.That(report.HasBlocking, Is.True);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidProjectId), Is.Not.Null);
        }

        [Test]
        public void Validate_InvalidAnimationId_IsBlocking()
        {
            var project = BuildValidProject();
            ReflectionUtil.SetField(project.Animations[0], "_animationId", "bad");
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidAnimationId), Is.Not.Null);
        }

        [Test]
        public void Validate_DuplicateAnimationIds_IsBlocking()
        {
            var project = FaceMotionProject.CreateNew();
            var first = FaceMotionAnimationData.Create("A");
            var second = FaceMotionAnimationData.Create("B");
            ReflectionUtil.SetField(second, "_animationId", first.AnimationId);
            project.AddAnimation(first);
            project.AddAnimation(second);

            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.DuplicateAnimationId), Is.Not.Null);
        }

        [Test]
        public void Validate_InvalidTrackId_IsBlocking()
        {
            var project = BuildValidProject();
            ReflectionUtil.SetField(project.Animations[0].Timeline.Tracks[0], "_trackId", "bad");
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidTrackId), Is.Not.Null);
        }

        [Test]
        public void Validate_InvalidKeyId_IsBlocking()
        {
            var project = BuildValidProject();
            var key = project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0];
            ReflectionUtil.SetField(key, "_keyId", "bad");
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidKeyId), Is.Not.Null);
        }

        [Test]
        public void Validate_DuplicateKeyIds_IsBlocking()
        {
            var project = BuildValidProject();
            var track = project.Animations[0].Timeline.Tracks[0];
            var keyA = FloatKeyframeData.Create(0f, 0f);
            var keyB = FloatKeyframeData.Create(0.5f, 50f);
            ReflectionUtil.SetField(keyB, "_keyId", keyA.KeyId);
            track.BlendShape.AddKey(keyA);
            track.BlendShape.AddKey(keyB);

            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.DuplicateKeyId), Is.Not.Null);
        }

        [Test]
        public void Validate_PayloadMismatch_IsBlocking()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Mismatch");
            var blend = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            blend.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            ReflectionUtil.SetField(blend, "_blendShape", null);
            animation.Timeline.AddTrack(blend);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.PayloadMismatch), Is.Not.Null);
        }

        [Test]
        public void Validate_TimelineDomainValues_AreBlocked()
        {
            var project = BuildValidProject();
            var animation = project.Animations[0];
            animation.Timeline.Duration = 0f;
            animation.Timeline.FrameRate = 0f;

            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidDuration), Is.Not.Null);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.InvalidFrameRate), Is.Not.Null);
        }

        [Test]
        public void Validate_NegativeKeyTime_IsBlocking()
        {
            var project = BuildValidProject();
            project.Animations[0].Timeline.Tracks[0].BlendShape.AddKey(FloatKeyframeData.Create(-0.5f, 10f));
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.NegativeKeyTime), Is.Not.Null);
        }

        [Test]
        public void Validate_NaNKeyTime_IsBlocking()
        {
            var project = BuildValidProject();
            project.Animations[0].Timeline.Tracks[0].BlendShape.AddKey(FloatKeyframeData.Create(float.NaN, 1f));
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.NonFiniteKeyTime), Is.Not.Null);
        }

        [Test]
        public void Validate_NaNKeyValue_IsBlocking()
        {
            var project = BuildValidProject();
            project.Animations[0].Timeline.Tracks[0].BlendShape.AddKey(FloatKeyframeData.Create(0f, float.NaN));
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.NonFiniteKeyValue), Is.Not.Null);
        }

        [Test]
        public void Validate_InfinityKeyValue_IsBlocking()
        {
            var project = BuildValidProject();
            project.Animations[0].Timeline.Tracks[0].BlendShape.AddKey(FloatKeyframeData.Create(0f, float.PositiveInfinity));
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.NonFiniteKeyValue), Is.Not.Null);
        }

        [Test]
        public void Validate_UnsupportedRotationMode_IsBlocking()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Rot");
            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Head");
            rotation.Transform.RotationMode = MotionRotationMode.EulerContinuous;
            rotation.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero));
            animation.Timeline.AddTrack(rotation);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.UnsupportedRotationMode), Is.Not.Null);
        }

        [Test]
        public void Validate_TransformScaleTrack_IsValid()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Scale");
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
            scale.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.one));
            scale.Transform.AddKey(Vector3KeyframeData.Create(1f, new Vector3(2f, 2f, 2f), InterpolationType.Hold));
            animation.Timeline.AddTrack(scale);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            Assert.That(report.HasBlocking, Is.False);
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Diagnostics, Is.Empty);
        }

        [Test]
        public void Validate_TransformScaleTrack_WithNonFiniteKey_IsBlocking()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Scale");
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
            scale.Transform.AddKey(Vector3KeyframeData.Create(0f, new Vector3(float.NaN, 1f, 1f)));
            animation.Timeline.AddTrack(scale);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.NonFiniteKeyValue), Is.Not.Null);
        }

        [Test]
        public void Normalize_TransformScaleTrack_KeepsSortAndKeys()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Scale");
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
            scale.Transform.AddKey(Vector3KeyframeData.Create(0.6f, new Vector3(6f, 6f, 6f)));
            scale.Transform.AddKey(Vector3KeyframeData.Create(0.2f, new Vector3(2f, 2f, 2f)));
            animation.Timeline.AddTrack(scale);
            project.AddAnimation(animation);

            ProjectNormalizer.Normalize(project);

            var keys = animation.Timeline.Tracks[0].Transform.Keys;
            Assert.That(keys.Count, Is.EqualTo(2));
            Assert.That(keys[0].Time, Is.EqualTo(0.2f));
            Assert.That(keys[1].Time, Is.EqualTo(0.6f));
            Assert.That(keys[1].Value, Is.EqualTo(new Vector3(6f, 6f, 6f)));
        }

        // ---- Non-blocking warnings ----

        [Test]
        public void Validate_KeyBeyondDuration_IsWarning()
        {
            var project = BuildValidProject();
            project.Animations[0].Timeline.Tracks[0].BlendShape.AddKey(FloatKeyframeData.Create(5f, 50f));
            var report = ProjectValidator.Validate(project);

            var diagnostic = TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.KeyBeyondDuration);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic.Blocking, Is.False);
            Assert.That(report.HasBlocking, Is.False);
        }

        [Test]
        public void Validate_UnsortedKeys_IsBlocking()
        {
            var project = BuildValidProject();
            var track = project.Animations[0].Timeline.Tracks[0];
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.5f, 50f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.1f, 10f));
            var report = ProjectValidator.Validate(project);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.UnsortedKeys), Is.Not.Null);
        }

        [Test]
        public void Validate_DuplicateKeyTime_IsWarning()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Duplicate");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.5f, 50f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(0.5f, 51f));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);

            var diagnostic = TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.DuplicateKeyTime);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic.Blocking, Is.False);
        }

        // ---- Medium-size performance smoke ----

        [Test]
        public void Performance_MediumProject_ValidatesWithinBudget()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Perf");
            for (int trackIndex = 0; trackIndex < 100; trackIndex++)
            {
                var track = FaceTrackData.CreateBlendShape("Renderer" + trackIndex, "Shape" + trackIndex);
                for (int keyIndex = 0; keyIndex < 100; keyIndex++)
                {
                    track.BlendShape.AddKey(FloatKeyframeData.Create(keyIndex / 100f, keyIndex, InterpolationType.Smooth));
                }

                animation.Timeline.AddTrack(track);
            }

            project.AddAnimation(animation);

            var stopwatch = Stopwatch.StartNew();
            var report = ProjectValidator.Validate(project);
            stopwatch.Stop();

            Assert.That(report.HasBlocking, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(20000));
        }
    }
}