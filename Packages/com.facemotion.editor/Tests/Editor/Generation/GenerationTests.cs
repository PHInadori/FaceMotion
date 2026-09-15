using System.Linq;
using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class GenerationTests
    {
        [Test]
        public void GeneratedMotion_CarriesGenerationMetadata()
        {
            var motion = new GeneratedMotion("gen-1234", 1);
            Assert.That(motion.GenerationId, Is.EqualTo("gen-1234"));
            Assert.That(motion.AlgorithmVersion, Is.EqualTo(1));
            Assert.That(motion.Tracks, Is.Not.Null);
            Assert.That(motion.Tracks, Is.Empty);
        }

        [Test]
        public void GeneratedFloatKey_DefaultsToLinear()
        {
            var key = new GeneratedFloatKey(0f, 42f);
            Assert.That(key.Time, Is.EqualTo(0f));
            Assert.That(key.Value, Is.EqualTo(42f));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.Linear));
        }

        [Test]
        public void GeneratedVector3Key_HoldsTypedValue()
        {
            var key = new GeneratedVector3Key(0.5f, new Vector3(1f, 2f, 3f), InterpolationType.EaseIn);
            Assert.That(key.Time, Is.EqualTo(0.5f));
            Assert.That(key.Value, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.EaseIn));
        }

        [Test]
        public void GeneratedTrack_BlendShape_ProducesMatchingPayload()
        {
            var track = GeneratedTrackMotion.CreateBlendShape(new BlendShapeBinding("Body/Renderer", "Smile"));
            Assert.That(track.Kind, Is.EqualTo(TrackKind.BlendShape));
            Assert.That(track.BlendShape.HasValue, Is.True);
            Assert.That(track.BlendShape.Value.RendererPath, Is.EqualTo("Body/Renderer"));
            Assert.That(track.Transform.HasValue, Is.False);

            Assert.That(track.TryCreateTrackPayload(out var blendShape, out var transform), Is.True);
            Assert.That(blendShape, Is.Not.Null);
            Assert.That(transform, Is.Null);
            Assert.That(blendShape.RendererPath, Is.EqualTo("Body/Renderer"));
            Assert.That(blendShape.BlendShapeName, Is.EqualTo("Smile"));
        }

        [Test]
        public void GeneratedTrack_Transform_ProducesMatchingPayload()
        {
            var track = GeneratedTrackMotion.CreateTransform(new TransformBinding("Jaw"), TrackKind.TransformRotation);
            Assert.That(track.Kind, Is.EqualTo(TrackKind.TransformRotation));
            Assert.That(track.Transform.HasValue, Is.True);
            Assert.That(track.Transform.Value.TransformPath, Is.EqualTo("Jaw"));

            Assert.That(track.TryCreateTrackPayload(out var blendShape, out var transform), Is.True);
            Assert.That(blendShape, Is.Null);
            Assert.That(transform, Is.Not.Null);
            Assert.That(transform.TransformPath, Is.EqualTo("Jaw"));
            Assert.That(transform.RotationMode, Is.EqualTo(MotionRotationMode.ShortestQuaternion));
        }

        [Test]
        public void GeneratedTrack_TransformScale_ProducesMatchingPayload()
        {
            var track = GeneratedTrackMotion.CreateTransform(new TransformBinding("Head"), TrackKind.TransformScale);
            Assert.That(track.Kind, Is.EqualTo(TrackKind.TransformScale));

            Assert.That(track.TryCreateTrackPayload(out var blendShape, out var transform), Is.True);
            Assert.That(blendShape, Is.Null);
            Assert.That(transform, Is.Not.Null);
            Assert.That(transform.TransformPath, Is.EqualTo("Head"));
        }

        [Test]
        public void GeneratedTrack_CreateTransform_RequiresTransformKind()
        {
            Assert.That(
                () => GeneratedTrackMotion.CreateTransform(new TransformBinding("Head"), TrackKind.BlendShape),
                Throws.ArgumentException);
        }

        [Test]
        public void GeneratedTrack_BlendShape_UsesFloatKeysOnly()
        {
            var track = GeneratedTrackMotion.CreateBlendShape(new BlendShapeBinding("Body/Renderer", "Smile"));
            track.FloatKeys.Add(new GeneratedFloatKey(0f, 0f));
            track.FloatKeys.Add(new GeneratedFloatKey(0.5f, 90f, InterpolationType.EaseIn));

            Assert.That(track.FloatKeys.Count, Is.EqualTo(2));
            Assert.That(track.Vector3Keys, Is.Empty);
            Assert.That(track.FloatKeys[1].Time, Is.EqualTo(0.5f));
            Assert.That(track.FloatKeys[1].Value, Is.EqualTo(90f));
            Assert.That(track.FloatKeys[1].Interpolation, Is.EqualTo(InterpolationType.EaseIn));
        }

        [Test]
        public void GeneratedTrack_Transform_UsesVector3KeysOnly()
        {
            var track = GeneratedTrackMotion.CreateTransform(new TransformBinding("Head"), TrackKind.TransformScale);
            track.Vector3Keys.Add(new GeneratedVector3Key(0f, Vector3.one));
            track.Vector3Keys.Add(new GeneratedVector3Key(1f, new Vector3(2f, 2f, 2f), InterpolationType.Smooth));

            Assert.That(track.Vector3Keys.Count, Is.EqualTo(2));
            Assert.That(track.FloatKeys, Is.Empty);
            Assert.That(track.Vector3Keys[1].Time, Is.EqualTo(1f));
            Assert.That(track.Vector3Keys[1].Value, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(track.Vector3Keys[1].Interpolation, Is.EqualTo(InterpolationType.Smooth));
        }

        [Test]
        public void GeneratedMotion_IsPureIntermediate_DoesNotReferenceTimelineData()
        {
            var modelTypes = new[]
            {
                typeof(GeneratedMotion),
                typeof(GeneratedTrackMotion),
                typeof(GeneratedFloatKey),
                typeof(GeneratedVector3Key)
            };
            var timelineTypes = new[] { typeof(FaceTrackData), typeof(FaceTimelineData), typeof(TransformTrackPayload), typeof(BlendShapeTrackPayload) };

            foreach (var modelType in modelTypes)
            {
                foreach (var member in modelType.GetProperties())
                {
                    Assert.That(
                        timelineTypes.Contains(member.PropertyType),
                        Is.False,
                        $"{modelType.Name}.{member.Name} must not expose timeline data.");
                }

                foreach (var member in modelType.GetFields())
                {
                    Assert.That(
                        timelineTypes.Contains(member.FieldType),
                        Is.False,
                        $"{modelType.Name}.{member.Name} must not expose timeline data.");
                }
            }
        }
    }
}