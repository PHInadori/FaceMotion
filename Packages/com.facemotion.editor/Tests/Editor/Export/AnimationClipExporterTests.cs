using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Editor.Export;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class AnimationClipExporterTests
    {
        private const string Folder = "Assets/__FaceMotionTests_Export";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_Export");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Export_SamplesCanonicalValuesAndWritesStandardBindings()
        {
            var animation = FaceMotionAnimationData.Create("Sampled");
            animation.Timeline.Duration = .5f;
            animation.Timeline.FrameRate = 4f;
            animation.Timeline.Loop = true;
            var blend = FaceTrackData.CreateBlendShape("Face", "Smile");
            blend.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f, InterpolationType.EaseIn));
            blend.BlendShape.AddKey(FloatKeyframeData.Create(.5f, 100f));
            animation.Timeline.AddTrack(blend);
            var position = FaceTrackData.CreateTransform(TrackKind.TransformPosition, "Jaw");
            position.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero));
            position.Transform.AddKey(Vector3KeyframeData.Create(.5f, new Vector3(2f, 4f, 6f)));
            animation.Timeline.AddTrack(position);
            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Jaw");
            rotation.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero));
            rotation.Transform.AddKey(Vector3KeyframeData.Create(.5f, new Vector3(0f, 90f, 0f)));
            animation.Timeline.AddTrack(rotation);

            var result = AnimationClipExporter.Export(animation, Folder + "/sample.anim");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Clip.frameRate, Is.EqualTo(4f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(result.Clip).loopTime, Is.True);
            AssertCurve(result.Clip, "Face", typeof(SkinnedMeshRenderer), "blendShape.Smile", .25f, 25f);
            AssertCurve(result.Clip, "Jaw", typeof(Transform), "m_LocalPosition.y", .25f, 2f);
            Assert.That(Evaluate(result.Clip, "Jaw", "m_LocalRotation.y", .25f), Is.EqualTo(Mathf.Sin(Mathf.PI / 8f)).Within(1e-4f));
        }

        [Test]
        public void Export_OverwritePreservesGuidAndReplacesCurves()
        {
            string path = Folder + "/overwrite.anim";
            var first = CreateBlendAnimation("A", 25f);
            Assert.That(AnimationClipExporter.Export(first, path).Succeeded, Is.True);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var second = CreateBlendAnimation("B", 75f);
            var result = AnimationClipExporter.Export(second, path);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(result.Clip.name, Is.EqualTo("overwrite"));
            Assert.That(Evaluate(result.Clip, "Face", "blendShape.Smile", 0f), Is.EqualTo(75f));
        }

        [Test]
        public void Export_CreatesOneMissingParentFolder()
        {
            var result = AnimationClipExporter.Export(CreateBlendAnimation("Nested", 25f), Folder + "/Created/sample.anim");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Created"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Created/sample.anim"), Is.SameAs(result.Clip));
        }

        [Test]
        public void Export_CreatesMissingNestedParentFolders()
        {
            var result = AnimationClipExporter.Export(CreateBlendAnimation("Nested", 25f), Folder + "/Created/Deep/sample.anim");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Created"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Created/Deep"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Created/Deep/sample.anim"), Is.SameAs(result.Clip));
        }

        [Test]
        public void Export_RejectsPackagesAndOccupiedNonClipDestinations()
        {
            var packages = AnimationClipExporter.Export(CreateBlendAnimation("Unsafe", 1f), "Packages/com.example/unsafe.anim");
            var marker = ScriptableObject.CreateInstance<ExportMarker>(); AssetDatabase.CreateAsset(marker, Folder + "/occupied.anim");
            var occupied = AnimationClipExporter.Export(CreateBlendAnimation("Occupied", 1f), Folder + "/occupied.anim");

            Assert.That(packages.Succeeded, Is.False);
            Assert.That(HasDiagnostic(packages, "FM-EXPORT-INVALID-PATH"), Is.True);
            Assert.That(occupied.Succeeded, Is.False);
            Assert.That(HasDiagnostic(occupied, "FM-EXPORT-PATH-OCCUPIED"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ExportMarker>(Folder + "/occupied.anim"), Is.SameAs(marker));
        }

        [Test]
        public void Export_InvalidInputDoesNotCreateMissingParentFolders()
        {
            var invalid = CreateBlendAnimation("Invalid", 1f); invalid.Timeline.Duration = 0f;

            var result = AnimationClipExporter.Export(invalid, Folder + "/Created/Deep/sample.anim");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Created"), Is.False);
        }

        [TestCase(InterpolationType.Hold)]
        [TestCase(InterpolationType.Linear)]
        [TestCase(InterpolationType.EaseIn)]
        [TestCase(InterpolationType.EaseOut)]
        [TestCase(InterpolationType.EaseInOut)]
        [TestCase(InterpolationType.Smooth)]
        public void Export_EachInterpolationMatchesCanonicalEvaluatorAtEveryEmittedFrame(InterpolationType interpolation)
        {
            var animation = FaceMotionAnimationData.Create("Interpolation");
            animation.Timeline.Duration = .51f;
            animation.Timeline.FrameRate = 4f;
            var blend = FaceTrackData.CreateBlendShape("Face", "Smile");
            blend.BlendShape.AddKey(FloatKeyframeData.Create(0f, 10f, interpolation));
            blend.BlendShape.AddKey(FloatKeyframeData.Create(.51f, 90f));
            var position = FaceTrackData.CreateTransform(TrackKind.TransformPosition, "Jaw");
            position.Transform.AddKey(Vector3KeyframeData.Create(0f, new Vector3(1f, 2f, 3f), interpolation));
            position.Transform.AddKey(Vector3KeyframeData.Create(.51f, new Vector3(9f, 8f, 7f)));
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Jaw");
            scale.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.one, interpolation));
            scale.Transform.AddKey(Vector3KeyframeData.Create(.51f, Vector3.one * 2f));
            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Jaw");
            rotation.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero, interpolation));
            rotation.Transform.AddKey(Vector3KeyframeData.Create(.51f, new Vector3(30f, 90f, 20f)));
            animation.Timeline.AddTrack(blend);
            animation.Timeline.AddTrack(position);
            animation.Timeline.AddTrack(scale);
            animation.Timeline.AddTrack(rotation);

            var result = AnimationClipExporter.Export(animation, Folder + "/" + interpolation + ".anim");

            Assert.That(result.Succeeded, Is.True);
            float[] times = MotionSampler.CreateSampleTimes(animation.Timeline.Duration, animation.Timeline.FrameRate);
            Assert.That(CurveTimes(result.Clip, "Face", typeof(SkinnedMeshRenderer), "blendShape.Smile"), Is.EqualTo(times));
            for (int i = 0; i < times.Length; i++)
            {
                float time = times[i];
                CanonicalMotionEvaluator.Instance.TryEvaluateFloat(blend.BlendShape.Keys, time, out float expectedBlend);
                CanonicalMotionEvaluator.Instance.TryEvaluatePosition(position.Transform.Keys, time, out Vector3 expectedPosition);
                CanonicalMotionEvaluator.Instance.TryEvaluateScale(scale.Transform.Keys, time, out Vector3 expectedScale);
                CanonicalMotionEvaluator.Instance.TryEvaluateRotation(rotation.Transform.Keys, time, out Quaternion expectedRotation);
                Assert.That(Evaluate(result.Clip, "Face", "blendShape.Smile", time), Is.EqualTo(expectedBlend).Within(1e-4f));
                AssertVector3(result.Clip, "Jaw", "m_LocalPosition", time, expectedPosition);
                AssertVector3(result.Clip, "Jaw", "m_LocalScale", time, expectedScale);
                Quaternion actualRotation = new Quaternion(Evaluate(result.Clip, "Jaw", "m_LocalRotation.x", time), Evaluate(result.Clip, "Jaw", "m_LocalRotation.y", time), Evaluate(result.Clip, "Jaw", "m_LocalRotation.z", time), Evaluate(result.Clip, "Jaw", "m_LocalRotation.w", time));
                Assert.That(Mathf.Abs(Quaternion.Dot(actualRotation, expectedRotation)), Is.EqualTo(1f).Within(1e-4f));
            }
        }

        [Test]
        public void Export_NonAlignedDurationIncludesExactTerminalKey()
        {
            var animation = CreateBlendAnimation("Duration", 12f);
            animation.Timeline.Duration = .51f;
            animation.Timeline.FrameRate = 4f;

            var result = AnimationClipExporter.Export(animation, Folder + "/duration.anim");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(CurveTimes(result.Clip, "Face", typeof(SkinnedMeshRenderer), "blendShape.Smile"), Is.EqualTo(new[] { 0f, .25f, .5f, .51f }));
        }

        [Test]
        public void Validate_RejectsInvalidBindingsAndNonFiniteAuthoringData()
        {
            var invalidBinding = FaceMotionAnimationData.Create("InvalidBinding");
            var missingName = FaceTrackData.CreateBlendShape("Face", string.Empty);
            missingName.BlendShape.AddKey(FloatKeyframeData.Create(0f, 1f));
            invalidBinding.Timeline.AddTrack(missingName);
            var nonFinite = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Jaw");
            nonFinite.Transform.AddKey(Vector3KeyframeData.Create(0f, new Vector3(float.NaN, 1f, 1f)));
            invalidBinding.Timeline.AddTrack(nonFinite);

            var validation = AnimationClipExporter.Validate(invalidBinding, Folder + "/invalid.anim");

            Assert.That(validation.IsValid, Is.False);
            Assert.That(HasDiagnostic(validation, "FM-EXPORT-INVALID-BLENDSHAPE"), Is.True);
            Assert.That(HasDiagnostic(validation, "FM-EXPORT-INVALID-KEY"), Is.True);
            Assert.That(HasFix(validation), Is.True, "Export diagnostics are shown directly in the UI and must provide remediation.");
        }

        [Test]
        public void Validate_RejectsInvalidTimelineAndPathTraversalWithoutChangingAssets()
        {
            var animation = CreateBlendAnimation("Unsafe", 1f);
            animation.Timeline.Duration = float.PositiveInfinity;
            animation.Timeline.FrameRate = float.NaN;

            var result = AnimationClipExporter.Export(animation, "Assets/__FaceMotionTests_Export/../escaped.anim");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(HasDiagnostic(result, "FM-EXPORT-INVALID-DURATION"), Is.True);
            Assert.That(HasDiagnostic(result, "FM-EXPORT-INVALID-FRAMERATE"), Is.True);
            Assert.That(HasDiagnostic(result, "FM-EXPORT-INVALID-PATH"), Is.True);
            Assert.That(AssetDatabase.LoadMainAssetAtPath("Assets/escaped.anim"), Is.Null);
        }

        [Test]
        public void Export_DoesNotMutateSourceTimeline()
        {
            var animation = CreateBlendAnimation("Immutable", 25f);
            animation.Timeline.Duration = .51f;
            animation.Timeline.FrameRate = 4f;
            FaceMotionAnimationData before = animation.Clone();

            Assert.That(AnimationClipExporter.Export(animation, Folder + "/immutable.anim").Succeeded, Is.True);

            Assert.That(animation.Timeline.Duration, Is.EqualTo(before.Timeline.Duration));
            Assert.That(animation.Timeline.FrameRate, Is.EqualTo(before.Timeline.FrameRate));
            Assert.That(animation.Timeline.Tracks[0].BlendShape.Keys[0].Time, Is.EqualTo(before.Timeline.Tracks[0].BlendShape.Keys[0].Time));
            Assert.That(animation.Timeline.Tracks[0].BlendShape.Keys[0].Value, Is.EqualTo(before.Timeline.Tracks[0].BlendShape.Keys[0].Value));
            Assert.That(animation.Timeline.Tracks[0].BlendShape.Keys[0].Interpolation, Is.EqualTo(before.Timeline.Tracks[0].BlendShape.Keys[0].Interpolation));
        }

        [Test]
        public void Validate_RejectsOutsideAssetsAndDoesNotCreateFile()
        {
            var result = AnimationClipExporter.Export(CreateBlendAnimation("Unsafe", 1f), "../unsafe.anim");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("FM-EXPORT-INVALID-PATH"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath("../unsafe.anim"), Is.Null);
        }

        private static FaceMotionAnimationData CreateBlendAnimation(string name, float value)
        {
            var animation = FaceMotionAnimationData.Create(name);
            var track = FaceTrackData.CreateBlendShape("Face", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, value));
            animation.Timeline.AddTrack(track);
            return animation;
        }

        private sealed class ExportMarker : ScriptableObject { }

        private static void AssertCurve(AnimationClip clip, string path, System.Type type, string property, float time, float expected)
        {
            var binding = EditorCurveBinding.FloatCurve(path, type, property);
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding), Is.Not.Null);
            Assert.That(Evaluate(clip, path, property, time), Is.EqualTo(expected).Within(1e-4f));
        }

        private static float Evaluate(AnimationClip clip, string path, string property, float time)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path == path && binding.propertyName == property) return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
            }

            Assert.Fail("Missing curve " + path + "/" + property);
            return 0f;
        }

        private static float[] CurveTimes(AnimationClip clip, string path, System.Type type, string property)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property));
            Assert.That(curve, Is.Not.Null);
            var times = new float[curve.keys.Length];
            for (int i = 0; i < times.Length; i++) times[i] = curve.keys[i].time;
            return times;
        }

        private static void AssertVector3(AnimationClip clip, string path, string property, float time, Vector3 expected)
        {
            Assert.That(Evaluate(clip, path, property + ".x", time), Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(Evaluate(clip, path, property + ".y", time), Is.EqualTo(expected.y).Within(1e-4f));
            Assert.That(Evaluate(clip, path, property + ".z", time), Is.EqualTo(expected.z).Within(1e-4f));
        }

        private static bool HasDiagnostic(AnimationClipExportValidation validation, string code)
        {
            for (int i = 0; i < validation.Diagnostics.Count; i++) if (validation.Diagnostics[i].Code == code) return true;
            return false;
        }

        private static bool HasFix(AnimationClipExportValidation validation)
        {
            for (int i = 0; i < validation.Diagnostics.Count; i++) if (!string.IsNullOrEmpty(validation.Diagnostics[i].SuggestedFix)) return true;
            return false;
        }
    }
}
