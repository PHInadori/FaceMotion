using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class DomainTests
    {
        // ---- Stable ID contract ----

        [Test]
        public void StableId_New_ProducesValidUniqueIds()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < 256; i++)
            {
                string id = StableId.New();
                Assert.That(StableId.IsValid(id), Is.True);
                Assert.That(ids.Add(id), Is.True, "Generated stable IDs must be unique.");
            }
        }

        [Test]
        public void StableId_IsValid_EnforcesLowerHex32Shape()
        {
            string valid = StableId.New();
            Assert.That(StableId.IsValid(valid), Is.True);
            Assert.That(StableId.IsValid(null), Is.False);
            Assert.That(StableId.IsValid(string.Empty), Is.False);
            Assert.That(StableId.IsValid(" "), Is.False);
            Assert.That(StableId.IsValid(valid.Substring(0, 31)), Is.False, "Too short.");
            Assert.That(StableId.IsValid(valid + "0"), Is.False, "Too long.");
            Assert.That(StableId.IsValid(valid.ToUpperInvariant()), Is.False, "Uppercase hex must be rejected.");
            Assert.That(StableId.IsValid(new string('g', 32)), Is.False, "Non-hex characters must be rejected.");
            Assert.That(StableId.IsValid(new string('7', 32)), Is.True, "Numeric hex is valid.");
        }

        // ---- KeyOrigin provenance ----

        [Test]
        public void KeyOrigin_Manual_IsNotGenerated_NotOrphaned()
        {
            var manual = KeyOrigin.Manual;
            Assert.That(manual.Kind, Is.EqualTo(OriginKind.Manual));
            Assert.That(manual.IsGenerated, Is.False);
            Assert.That(manual.IsOrphaned, Is.False);
            Assert.That(manual.GenerationId, Is.Null.Or.Empty);
        }

        [Test]
        public void KeyOrigin_GeneratedWithId_IsGenerated_NotOrphaned()
        {
            var generated = new KeyOrigin(OriginKind.Blink, StableId.New());
            Assert.That(generated.IsGenerated, Is.True);
            Assert.That(generated.IsOrphaned, Is.False);
        }

        [Test]
        public void KeyOrigin_GeneratedWithoutId_IsOrphaned()
        {
            var orphaned = new KeyOrigin(OriginKind.Random, null);
            Assert.That(orphaned.IsGenerated, Is.True);
            Assert.That(orphaned.IsOrphaned, Is.True);
        }

        [Test]
        public void KeyOrigin_Equality_IsOrdinal()
        {
            string generationId = StableId.New();
            var sameA = new KeyOrigin(OriginKind.Preset, generationId);
            var sameB = new KeyOrigin(OriginKind.Preset, generationId);
            Assert.That(sameA.Equals(sameB), Is.True);
            Assert.That(sameA.GetHashCode(), Is.EqualTo(sameB.GetHashCode()));
            Assert.That(new KeyOrigin(OriginKind.Random, generationId).Equals(sameA), Is.False, "Kind participates.");
            Assert.That(new KeyOrigin(OriginKind.Preset, generationId.ToUpperInvariant()).Equals(sameA), Is.False, "Comparison is ordinal.");
        }

        // ---- Enum serialization stability ----

        [Test]
        public void Enums_SerializeWithStableValues()
        {
            Assert.That((int)TrackKind.BlendShape, Is.EqualTo(0));
            Assert.That((int)TrackKind.TransformPosition, Is.EqualTo(1));
            Assert.That((int)TrackKind.TransformRotation, Is.EqualTo(2));
            Assert.That((int)TrackKind.TransformScale, Is.EqualTo(3));

            Assert.That((int)InterpolationType.Hold, Is.EqualTo(0));
            Assert.That((int)InterpolationType.Linear, Is.EqualTo(1));
            Assert.That((int)InterpolationType.EaseIn, Is.EqualTo(2));
            Assert.That((int)InterpolationType.EaseOut, Is.EqualTo(3));
            Assert.That((int)InterpolationType.EaseInOut, Is.EqualTo(4));
            Assert.That((int)InterpolationType.Smooth, Is.EqualTo(5));

            Assert.That((int)OriginKind.Manual, Is.EqualTo(0));
            Assert.That((int)OriginKind.Preset, Is.EqualTo(1));
            Assert.That((int)OriginKind.Blink, Is.EqualTo(2));
            Assert.That((int)OriginKind.Random, Is.EqualTo(3));
            Assert.That((int)OriginKind.Imported, Is.EqualTo(4));

            Assert.That((int)GeneratorType.Preset, Is.EqualTo(0));
            Assert.That((int)GeneratorType.Blink, Is.EqualTo(1));
            Assert.That((int)GeneratorType.Random, Is.EqualTo(2));
            Assert.That((int)GeneratorType.Imported, Is.EqualTo(3));

            Assert.That((int)MotionRotationMode.ShortestQuaternion, Is.EqualTo(0));
            Assert.That((int)MotionRotationMode.EulerContinuous, Is.EqualTo(1));
        }

        [Test]
        public void TrackKinds_IsTransform_CoversInputDomain()
        {
            Assert.That(TrackKinds.IsTransform(TrackKind.TransformPosition), Is.True);
            Assert.That(TrackKinds.IsTransform(TrackKind.TransformRotation), Is.True);
            Assert.That(TrackKinds.IsTransform(TrackKind.TransformScale), Is.True);
            Assert.That(TrackKinds.IsTransform(TrackKind.BlendShape), Is.False);
        }

        // ---- Data factory conventions ----

        [Test]
        public void Timeline_Default_IsOneSecond60FpsNoLoop()
        {
            var timeline = FaceTimelineData.CreateDefault();
            Assert.That(timeline.Duration, Is.EqualTo(1f));
            Assert.That(timeline.FrameRate, Is.EqualTo(60f));
            Assert.That(timeline.Loop, Is.False);
            Assert.That(timeline.Tracks, Is.Empty);
        }

        [Test]
        public void Track_BlendShape_ActivePayloadMatchesKind()
        {
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            Assert.That(track.Kind, Is.EqualTo(TrackKind.BlendShape));
            Assert.That(track.Enabled, Is.True);
            Assert.That(track.BlendShape, Is.Not.Null);
            Assert.That(track.BlendShape.RendererPath, Is.EqualTo("Body/Renderer"));
            Assert.That(track.BlendShape.BlendShapeName, Is.EqualTo("Smile"));
            Assert.That(track.Transform, Is.Null);
            Assert.That(StableId.IsValid(track.TrackId), Is.True);
        }

        [Test]
        public void Track_Transform_ValidatesKindArgument()
        {
            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Head", true, MotionRotationMode.ShortestQuaternion);
            Assert.That(rotation.Kind, Is.EqualTo(TrackKind.TransformRotation));
            Assert.That(rotation.Transform, Is.Not.Null);
            Assert.That(rotation.Transform.TransformPath, Is.EqualTo("Head"));
            Assert.That(rotation.Transform.RotationMode, Is.EqualTo(MotionRotationMode.ShortestQuaternion));
            Assert.That(rotation.BlendShape, Is.Null);

            Assert.That(FaceTrackData.CreateTransform(TrackKind.TransformPosition, "Head").Kind, Is.EqualTo(TrackKind.TransformPosition));
            Assert.That(() => FaceTrackData.CreateTransform(TrackKind.BlendShape, "Head"), Throws.ArgumentException);
        }

        [Test]
        public void Track_TransformScale_CreatesScalablePayloadWithVector3Keys()
        {
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
            Assert.That(scale.Kind, Is.EqualTo(TrackKind.TransformScale));
            Assert.That(scale.Transform, Is.Not.Null);
            Assert.That(scale.Transform.TransformPath, Is.EqualTo("Head"));
            Assert.That(scale.BlendShape, Is.Null);
            Assert.That(StableId.IsValid(scale.TrackId), Is.True);

            scale.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.one, InterpolationType.Linear));
            scale.Transform.AddKey(Vector3KeyframeData.Create(1f, new Vector3(2f, 2f, 2f), InterpolationType.Smooth));
            Assert.That(scale.Transform.Keys.Count, Is.EqualTo(2));
            Assert.That(scale.Transform.Keys[1].Value, Is.EqualTo(new Vector3(2f, 2f, 2f)));
        }

        [Test]
        public void Key_Float_CreatesManualKeyWithDefaults()
        {
            var key = FloatKeyframeData.Create(0.5f, 42f, InterpolationType.Smooth);
            Assert.That(StableId.IsValid(key.KeyId), Is.True);
            Assert.That(key.Time, Is.EqualTo(0.5f));
            Assert.That(key.Value, Is.EqualTo(42f));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.Smooth));
            Assert.That(key.Origin.Kind, Is.EqualTo(OriginKind.Manual));
        }

        [Test]
        public void Key_Vector3_CreatesManualKeyWithDefaults()
        {
            var key = Vector3KeyframeData.Create(0.25f, new Vector3(1f, 2f, 3f));
            Assert.That(StableId.IsValid(key.KeyId), Is.True);
            Assert.That(key.Time, Is.EqualTo(0.25f));
            Assert.That(key.Value, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(key.Origin.Kind, Is.EqualTo(OriginKind.Manual));
        }

        [Test]
        public void Animation_Create_IssuesFreshIds()
        {
            var first = FaceMotionAnimationData.Create("First");
            var second = FaceMotionAnimationData.Create("Second");
            Assert.That(StableId.IsValid(first.AnimationId), Is.True);
            Assert.That(first.AnimationId, Is.Not.EqualTo(second.AnimationId));
            Assert.That(first.DisplayName, Is.EqualTo("First"));
            Assert.That(first.Timeline, Is.Not.Null);
        }

        [Test]
        public void Project_CreateNew_EstablishesIdsAndVersions()
        {
            var project = FaceMotionProject.CreateNew();
            Assert.That(StableId.IsValid(project.ProjectId), Is.True);
            Assert.That(project.SchemaVersion, Is.EqualTo(FaceMotionVersions.ProjectSchemaVersion));
            Assert.That(project.Animations, Is.Empty);
            Assert.That(project.Generations, Is.Empty);
        }

        [Test]
        public void Project_AddRemoveAndLookup_WorkById()
        {
            var project = FaceMotionProject.CreateNew();
            var first = FaceMotionAnimationData.Create("A");
            var second = FaceMotionAnimationData.Create("B");
            project.AddAnimation(first);
            project.AddAnimation(second);

            Assert.That(project.TryGetAnimation(first.AnimationId, out var found), Is.True);
            Assert.That(found, Is.SameAs(first));
            Assert.That(project.TryGetAnimation("missing-id", out _), Is.False);

            Assert.That(project.RemoveAnimation(first.AnimationId), Is.True);
            Assert.That(project.Animations.Count, Is.EqualTo(1));
            Assert.That(project.Animations[0], Is.SameAs(second));
            Assert.That(project.RemoveAnimation(first.AnimationId), Is.False);
        }

        [Test]
        public void Binding_BlendShape_Equality_IsValueBased()
        {
            var sameA = new BlendShapeBinding("Body/Renderer", "Smile");
            var sameB = new BlendShapeBinding("Body/Renderer", "Smile");
            var different = new BlendShapeBinding("Body/Renderer", "Blink");
            Assert.That(sameA.Equals(sameB), Is.True);
            Assert.That(sameA.GetHashCode(), Is.EqualTo(sameB.GetHashCode()));
            Assert.That(sameA.Equals(different), Is.False);
            Assert.That(sameA.Equals(default(BlendShapeBinding)), Is.False);
        }

        [Test]
        public void Binding_Transform_Equality_IsValueBased()
        {
            Assert.That(new TransformBinding("Head").Equals(new TransformBinding("Head")), Is.True);
            Assert.That(new TransformBinding("Head").Equals(new TransformBinding("Jaw")), Is.False);
        }

        // ---- Clone semantics: internal transactions preserve every ID ----

        private static FaceMotionAnimationData CreateSampleAnimation()
        {
            var animation = FaceMotionAnimationData.Create("Sample");
            var blend = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            blend.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f, InterpolationType.Linear));
            blend.BlendShape.AddKey(FloatKeyframeData.Create(1f, 100f, InterpolationType.EaseOut));
            animation.Timeline.AddTrack(blend);

            var rotation = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Head");
            rotation.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero));
            rotation.Transform.AddKey(Vector3KeyframeData.Create(1f, new Vector3(0f, 90f, 0f), InterpolationType.Smooth));
            animation.Timeline.AddTrack(rotation);
            return animation;
        }

        [Test]
        public void Clone_Animation_PreservesEveryIdAndOrigin()
        {
            var animation = CreateSampleAnimation();
            var clone = animation.Clone();

            Assert.That(clone, Is.Not.SameAs(animation));
            Assert.That(clone.AnimationId, Is.EqualTo(animation.AnimationId));
            Assert.That(clone.DisplayName, Is.EqualTo(animation.DisplayName));
            Assert.That(clone.Timeline.Tracks.Count, Is.EqualTo(2));

            Assert.That(clone.Timeline.Tracks[0].TrackId, Is.EqualTo(animation.Timeline.Tracks[0].TrackId));
            var sourceBlendKeys = animation.Timeline.Tracks[0].BlendShape.Keys;
            var cloneBlendKeys = clone.Timeline.Tracks[0].BlendShape.Keys;
            Assert.That(cloneBlendKeys.Count, Is.EqualTo(2));
            Assert.That(cloneBlendKeys[0].KeyId, Is.EqualTo(sourceBlendKeys[0].KeyId));
            Assert.That(cloneBlendKeys[1].Interpolation, Is.EqualTo(InterpolationType.EaseOut));

            Assert.That(clone.Timeline.Tracks[1].TrackId, Is.EqualTo(animation.Timeline.Tracks[1].TrackId));
            Assert.That(
                clone.Timeline.Tracks[1].Transform.Keys[1].KeyId,
                Is.EqualTo(animation.Timeline.Tracks[1].Transform.Keys[1].KeyId));
        }

        [Test]
        public void Project_Clone_PreservesIdAtEveryLevel()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = CreateSampleAnimation();
            project.AddAnimation(animation);
            var generation = GenerationRecord.Create(animation.AnimationId, GeneratorType.Blink, "preset-1", 1, "hash-1");
            project.AddGenerationRecord(generation);

            var clone = FaceMotionProject.CreateClone(project);

            Assert.That(clone, Is.Not.SameAs(project));
            Assert.That(clone.ProjectId, Is.EqualTo(project.ProjectId));
            Assert.That(clone.SchemaVersion, Is.EqualTo(project.SchemaVersion));
            Assert.That(clone.Animations.Count, Is.EqualTo(1));
            Assert.That(clone.Animations[0].AnimationId, Is.EqualTo(animation.AnimationId));
            Assert.That(clone.Animations[0].Timeline.Tracks[0].TrackId, Is.EqualTo(animation.Timeline.Tracks[0].TrackId));
            Assert.That(
                clone.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId,
                Is.EqualTo(animation.Timeline.Tracks[0].BlendShape.Keys[0].KeyId));
            Assert.That(clone.Generations.Count, Is.EqualTo(1));
            Assert.That(clone.Generations[0].GenerationId, Is.EqualTo(generation.GenerationId));
            Assert.That(clone.Generations[0].GeneratorType, Is.EqualTo(GeneratorType.Blink));
        }

        // ---- User-facing Duplicate: fresh IDs everywhere ----

        [Test]
        public void Duplicate_Animation_AssignsFreshIdsPreservingValues()
        {
            var animation = CreateSampleAnimation();
            var project = FaceMotionProject.CreateNew();
            project.AddAnimation(animation);
            var duplicate = project.DuplicateAnimation(animation.AnimationId);

            Assert.That(duplicate, Is.Not.Null);
            Assert.That(duplicate.AnimationId, Is.Not.EqualTo(animation.AnimationId));
            Assert.That(project.Animations.Count, Is.EqualTo(2));
            Assert.That(duplicate.Timeline.Tracks.Count, Is.EqualTo(2));
            Assert.That(duplicate.Timeline.Tracks[0].TrackId, Is.Not.EqualTo(animation.Timeline.Tracks[0].TrackId));

            var sourceKeys = animation.Timeline.Tracks[0].BlendShape.Keys;
            var duplicateKeys = duplicate.Timeline.Tracks[0].BlendShape.Keys;
            Assert.That(duplicateKeys.Count, Is.EqualTo(sourceKeys.Count));
            Assert.That(duplicateKeys[0].KeyId, Is.Not.EqualTo(sourceKeys[0].KeyId));
            Assert.That(duplicateKeys[0].Time, Is.EqualTo(sourceKeys[0].Time));
            Assert.That(duplicateKeys[0].Value, Is.EqualTo(sourceKeys[0].Value));

            var duplicateRotationKey = duplicate.Timeline.Tracks[1].Transform.Keys[1];
            Assert.That(duplicateRotationKey.Value, Is.EqualTo(new Vector3(0f, 90f, 0f)));
            Assert.That(duplicateRotationKey.Interpolation, Is.EqualTo(InterpolationType.Smooth));
        }

        [Test]
        public void Duplicate_TrackAndKey_PreserveOrderingAndMode()
        {
            var track = FaceTrackData.CreateTransform(TrackKind.TransformRotation, "Head", true, MotionRotationMode.ShortestQuaternion);
            track.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.zero));
            track.Transform.AddKey(Vector3KeyframeData.Create(1f, new Vector3(0f, 45f, 0f), InterpolationType.Linear));

            var duplicate = track.Duplicate();
            Assert.That(duplicate.TrackId, Is.Not.EqualTo(track.TrackId));
            Assert.That(duplicate.Kind, Is.EqualTo(TrackKind.TransformRotation));
            Assert.That(duplicate.Transform.RotationMode, Is.EqualTo(MotionRotationMode.ShortestQuaternion));
            Assert.That(duplicate.Transform.Keys[0].KeyId, Is.Not.EqualTo(track.Transform.Keys[0].KeyId));
            Assert.That(duplicate.Transform.Keys[0].Time, Is.EqualTo(0f));
        }

        [Test]
        public void Duplicate_Track_TransformScale_KeepsKindAndFreshIds()
        {
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
            scale.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.one));
            scale.Transform.AddKey(Vector3KeyframeData.Create(1f, new Vector3(2f, 2f, 2f)));
            var trackId = scale.TrackId;
            var keyId = scale.Transform.Keys[0].KeyId;

            var duplicate = scale.Duplicate();

            Assert.That(duplicate.TrackId, Is.Not.EqualTo(trackId));
            Assert.That(duplicate.Kind, Is.EqualTo(TrackKind.TransformScale));
            Assert.That(duplicate.Transform, Is.Not.Null);
            Assert.That(duplicate.Transform.TransformPath, Is.EqualTo("Head"));
            Assert.That(duplicate.Transform.Keys.Count, Is.EqualTo(2));
            Assert.That(duplicate.Transform.Keys[0].KeyId, Is.Not.EqualTo(keyId));
            Assert.That(duplicate.Transform.Keys[0].Value, Is.EqualTo(Vector3.one));
            Assert.That(duplicate.Transform.Keys[1].Value, Is.EqualTo(new Vector3(2f, 2f, 2f)));
        }

        [Test]
        public void Clone_Track_TransformScale_PreservesIds()
        {
            var scale = FaceTrackData.CreateTransform(TrackKind.TransformScale, "Head");
            scale.Transform.AddKey(Vector3KeyframeData.Create(0f, Vector3.one));
            var trackId = scale.TrackId;
            var keyId = scale.Transform.Keys[0].KeyId;

            var clone = scale.Clone();

            Assert.That(clone.TrackId, Is.EqualTo(trackId));
            Assert.That(clone.Kind, Is.EqualTo(TrackKind.TransformScale));
            Assert.That(clone.Transform.Keys[0].KeyId, Is.EqualTo(keyId));
            Assert.That(clone.Transform.Keys[0].Value, Is.EqualTo(Vector3.one));
        }
    }
}