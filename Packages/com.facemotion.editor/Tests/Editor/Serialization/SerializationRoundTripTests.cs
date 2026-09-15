using FaceMotion.Data;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class SerializationRoundTripTests
    {
        [Test]
        public void RoundTrip_PreservesFullProject()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("Blink");
                animation.Timeline.Duration = 2.5f;
                animation.Timeline.FrameRate = 30f;
                animation.Timeline.Loop = true;

                var blend = FaceTrackData.CreateBlendShape("body/face", "jawOpen");
                var generatedId = StableId.New();
                blend.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f, InterpolationType.Linear));
                blend.BlendShape.AddKey(
                    FloatKeyframeData.Create(0.5f, 40f, InterpolationType.Linear,
                        new KeyOrigin(OriginKind.Blink, generatedId)));
                blend.BlendShape.AddKey(FloatKeyframeData.Create(1f, 80f, InterpolationType.EaseIn));
                blend.BlendShape.AddKey(FloatKeyframeData.Create(2f, 100f, InterpolationType.EaseOut));
                animation.Timeline.AddTrack(blend);

                var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Jaw", true, MotionRotationMode.ShortestQuaternion);
                rotation.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero, InterpolationType.Linear));
                rotation.Transform.AddKey(Vector3KeyframeData.Create(2f, new Vector3(0f, 45f, 0f), InterpolationType.Smooth));
                animation.Timeline.AddTrack(rotation);

                project.AddAnimation(animation);
                project.AddGenerationRecord(GenerationRecord.Create(animation.AnimationId, GeneratorType.Blink, "preset-01", 1, "hash-01"));

                string assetPath = temp.AssetPath("Project");
                EditorExtensions.SetDirtyAndSave(project, assetPath);

                var expectedProjectId = project.ProjectId;
                var expectedAnimationId = animation.AnimationId;
                var expectedTrackId = blend.TrackId;
                var expectedGeneratorId = project.Generations[0].GenerationId;
                var expectedGenerationId = generatedId;
                var expectedBlendKeyIds = new[]
                {
                    blend.BlendShape.Keys[0].KeyId,
                    blend.BlendShape.Keys[1].KeyId,
                    blend.BlendShape.Keys[2].KeyId,
                    blend.BlendShape.Keys[3].KeyId
                };

                string copyPath = temp.AssetPath("ProjectCopy");
                var loaded = EditorExtensions.ReloadCopy(assetPath, copyPath);
                Assert.That(loaded, Is.Not.SameAs(project), "Copy reload must produce a freshly deserialized instance.");
                Assert.That(loaded, Is.Not.Null, "Round-tripped project asset must load.");

                Assert.That(loaded.ProjectId, Is.EqualTo(expectedProjectId));
                Assert.That(loaded.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion));
                Assert.That(loaded.Animations.Count, Is.EqualTo(1));

                var loadedAnimation = loaded.Animations[0];
                Assert.That(loadedAnimation.AnimationId, Is.EqualTo(expectedAnimationId));
                Assert.That(loadedAnimation.DisplayName, Is.EqualTo("Blink"));
                Assert.That(loadedAnimation.Timeline.Duration, Is.EqualTo(2.5f));
                Assert.That(loadedAnimation.Timeline.FrameRate, Is.EqualTo(30f));
                Assert.That(loadedAnimation.Timeline.Loop, Is.True);
                Assert.That(loadedAnimation.Timeline.Tracks.Count, Is.EqualTo(2));

                var loadedBlend = loadedAnimation.Timeline.Tracks[0];
                Assert.That(loadedBlend.Kind, Is.EqualTo(TrackKind.BlendShape));
                Assert.That(loadedBlend.TrackId, Is.EqualTo(expectedTrackId));
                Assert.That(loadedBlend.BlendShape, Is.Not.Null);
                Assert.That(loadedBlend.BlendShape.RendererPath, Is.EqualTo("body/face"));
                Assert.That(loadedBlend.BlendShape.BlendShapeName, Is.EqualTo("jawOpen"));
                Assert.That(loadedBlend.BlendShape.Keys.Count, Is.EqualTo(4));
                for (int i = 0; i < expectedBlendKeyIds.Length; i++)
                {
                    Assert.That(loadedBlend.BlendShape.Keys[i].KeyId, Is.EqualTo(expectedBlendKeyIds[i]));
                }

                Assert.That(loadedBlend.BlendShape.Keys[1].Origin.Kind, Is.EqualTo(OriginKind.Blink));
                Assert.That(loadedBlend.BlendShape.Keys[1].Origin.GenerationId, Is.EqualTo(expectedGenerationId));
                Assert.That(loadedBlend.BlendShape.Keys[2].Interpolation, Is.EqualTo(InterpolationType.EaseIn));
                Assert.That(loadedBlend.BlendShape.Keys[3].Value, Is.EqualTo(100f));
                Assert.That(loadedBlend.BlendShape.Keys[3].Interpolation, Is.EqualTo(InterpolationType.EaseOut));

                var loadedRotation = loadedAnimation.Timeline.Tracks[1];
                Assert.That(loadedRotation.Kind, Is.EqualTo(TrackKind.TransformRotation));
                Assert.That(loadedRotation.Transform, Is.Not.Null);
                Assert.That(loadedRotation.Transform.TransformPath, Is.EqualTo("Jaw"));
                Assert.That(loadedRotation.Transform.RotationMode, Is.EqualTo(MotionRotationMode.ShortestQuaternion));
                Assert.That(loadedRotation.Transform.Keys.Count, Is.EqualTo(2));
                Assert.That(loadedRotation.Transform.Keys[1].Value, Is.EqualTo(new Vector3(0f, 45f, 0f)));
                Assert.That(loadedRotation.Transform.Keys[1].Interpolation, Is.EqualTo(InterpolationType.Smooth));

                Assert.That(loaded.Generations.Count, Is.EqualTo(1));
                var loadedGeneration = loaded.Generations[0];
                Assert.That(loadedGeneration.GenerationId, Is.EqualTo(expectedGeneratorId));
                Assert.That(loadedGeneration.AnimationId, Is.EqualTo(expectedAnimationId));
                Assert.That(loadedGeneration.GeneratorType, Is.EqualTo(GeneratorType.Blink));
                Assert.That(loadedGeneration.SourcePresetId, Is.EqualTo("preset-01"));
                Assert.That(loadedGeneration.AlgorithmVersion, Is.EqualTo(1));
                Assert.That(loadedGeneration.SettingsHash, Is.EqualTo("hash-01"));
            }
        }

        [Test]
        public void RoundTrip_MultipleAnimations_KeepsOrderAndIdentity()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var first = FaceMotionAnimationData.Create("A");
                var second = FaceMotionAnimationData.Create("B");
                project.AddAnimation(first);
                project.AddAnimation(second);

                string assetPath = temp.AssetPath("Stable");
                EditorExtensions.SetDirtyAndSave(project, assetPath);

                var expectedFirstId = first.AnimationId;
                var expectedSecondId = second.AnimationId;
                string copyPath = temp.AssetPath("StableCopy");
                var loaded = EditorExtensions.ReloadCopy(assetPath, copyPath);
                Assert.That(loaded, Is.Not.SameAs(project), "Copy reload must produce a freshly deserialized instance.");
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Animations.Count, Is.EqualTo(2));
                Assert.That(loaded.Animations[0].DisplayName, Is.EqualTo("A"));
                Assert.That(loaded.Animations[0].AnimationId, Is.EqualTo(expectedFirstId));
                Assert.That(loaded.Animations[1].DisplayName, Is.EqualTo("B"));
                Assert.That(loaded.Animations[1].AnimationId, Is.EqualTo(expectedSecondId));
                Assert.That(loaded.Animations[0].AnimationId, Is.Not.EqualTo(loaded.Animations[1].AnimationId));
            }
        }

        [Test]
        public void RoundTrip_TransformScaleTrack_PreservesPayload()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("Scale");
                var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
                scale.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.one, InterpolationType.Linear));
                scale.Transform.AddKey(Vector3KeyframeData.Create(1f, new Vector3(2f, 3f, 4f), InterpolationType.Hold));
                animation.Timeline.AddTrack(scale);
                project.AddAnimation(animation);

                string assetPath = temp.AssetPath("ScaleProject");
                EditorExtensions.SetDirtyAndSave(project, assetPath);

                var expectedTrackId = scale.TrackId;
                var expectedKeyIds = new[] { scale.Transform.Keys[0].KeyId, scale.Transform.Keys[1].KeyId };

                string copyPath = temp.AssetPath("ScaleProjectCopy");
                var loaded = EditorExtensions.ReloadCopy(assetPath, copyPath);
                Assert.That(loaded, Is.Not.SameAs(project));
                Assert.That(loaded, Is.Not.Null);

                var loadedAnimation = loaded.Animations[0];
                Assert.That(loadedAnimation.Timeline.Tracks.Count, Is.EqualTo(1));
                var loadedScale = loadedAnimation.Timeline.Tracks[0];
                Assert.That(loadedScale.Kind, Is.EqualTo(TrackKind.TransformScale));
                Assert.That(loadedScale.TrackId, Is.EqualTo(expectedTrackId));
                // Unity materializes the inactive serialized payload on reload, so the
                // blend shape field may be non-null; TrackKind is the source of truth.
                Assert.That(loadedScale.Transform, Is.Not.Null);
                Assert.That(ProjectValidator.Validate(loaded).HasBlocking, Is.False);
                Assert.That(loadedScale.Transform.TransformPath, Is.EqualTo("Head"));
                Assert.That(loadedScale.Transform.Keys.Count, Is.EqualTo(2));
                Assert.That(loadedScale.Transform.Keys[0].KeyId, Is.EqualTo(expectedKeyIds[0]));
                Assert.That(loadedScale.Transform.Keys[1].KeyId, Is.EqualTo(expectedKeyIds[1]));
                Assert.That(loadedScale.Transform.Keys[0].Value, Is.EqualTo(Vector3.one));
                Assert.That(loadedScale.Transform.Keys[1].Value, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(loadedScale.Transform.Keys[1].Interpolation, Is.EqualTo(InterpolationType.Hold));
            }
        }
    }
}