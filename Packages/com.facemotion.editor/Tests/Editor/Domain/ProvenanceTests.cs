using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class ProvenanceTests
    {
        [Test]
        public void Registry_AddFindRemove_RoundTrip()
        {
            var project = FaceMotionProject.CreateNew();
            var record = GenerationRecord.Create("animation-1", GeneratorType.Blink, "preset-1", 1, "hash-1");
            project.AddGenerationRecord(record);

            Assert.That(project.Generations.Count, Is.EqualTo(1));
            Assert.That(project.HasGeneration(record.GenerationId), Is.True);
            Assert.That(project.TryGetGeneration(record.GenerationId, out var found), Is.True);
            Assert.That(found, Is.SameAs(record));
            Assert.That(found.AnimationId, Is.EqualTo("animation-1"));
            Assert.That(found.SourcePresetId, Is.EqualTo("preset-1"));
            Assert.That(found.AlgorithmVersion, Is.EqualTo(1));
            Assert.That(found.SettingsHash, Is.EqualTo("hash-1"));

            Assert.That(project.TryGetGeneration("missing-id", out _), Is.False);
            Assert.That(project.RemoveGenerationRecord(record.GenerationId), Is.True);
            Assert.That(project.HasGeneration(record.GenerationId), Is.False);
            Assert.That(project.RemoveGenerationRecord(record.GenerationId), Is.False);
        }

        [Test]
        public void Registry_Clone_PreservesFullProvenance()
        {
            var project = FaceMotionProject.CreateNew();
            var record = GenerationRecord.Create("animation-1", GeneratorType.Random, "preset-9", 3, "hash-9");
            project.AddGenerationRecord(record);

            var clone = FaceMotionProject.CreateClone(project);
            Assert.That(clone.Generations.Count, Is.EqualTo(1));
            Assert.That(clone.Generations[0].GenerationId, Is.EqualTo(record.GenerationId));
            Assert.That(clone.Generations[0].GeneratorType, Is.EqualTo(GeneratorType.Random));
            Assert.That(clone.Generations[0].SourcePresetId, Is.EqualTo("preset-9"));
            Assert.That(clone.Generations[0].AlgorithmVersion, Is.EqualTo(3));
        }

        [Test]
        public void Validation_GeneratedKeyWithMissingRecord_WarnsAsOrphan()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            var orphanedId = StableId.New();
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Blink, orphanedId)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            var diagnostic = TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.OrphanedGenerationKey);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic.Blocking, Is.False);
        }

        [Test]
        public void Validation_GeneratedKeyWithRecord_IsClean()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            var generation = GenerationRecord.Create(animation.AnimationId, GeneratorType.Blink, "preset-1", 1, "hash-1");
            project.AddGenerationRecord(generation);

            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Blink, generation.GenerationId)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            Assert.That(report.HasBlocking, Is.False);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.OrphanedGenerationKey), Is.Null);
        }

        [Test]
        public void Validation_OrphanedGeneratedKeyWithoutId_Warns()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Random, null)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            var diagnostic = TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.OrphanedGenerationKey);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic.Blocking, Is.False);
        }

        [Test]
        public void Validation_ManualKeyWithGenerationId_Warns()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Manual");
            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 10f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Manual, StableId.New())));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var report = ProjectValidator.Validate(project);
            var diagnostic = TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.ManualKeyWithGenerationId);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic.Blocking, Is.False);
        }

        [Test]
        public void DuplicateAnimation_GeneratedKeyGenesis_RemapsGenerationId()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var generation = GenerationRecord.Create(animation.AnimationId, GeneratorType.Random, "preset-1", 1, "hash-1");
            project.AddGenerationRecord(generation);

            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Random, generation.GenerationId)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var duplicate = project.DuplicateAnimation(animation.AnimationId);
            var duplicateKey = duplicate.Timeline.Tracks[0].BlendShape.Keys[0];

            Assert.That(duplicateKey.Origin.Kind, Is.EqualTo(OriginKind.Random));
            Assert.That(duplicateKey.Origin.GenerationId, Is.Not.EqualTo(generation.GenerationId),
                "The duplicated animation must not share the source generation id.");
        }

        [Test]
        public void DuplicateAnimation_OriginalGenerationRecord_IsUntouched()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var generation = GenerationRecord.Create(animation.AnimationId, GeneratorType.Random, "preset-1", 1, "hash-1");
            project.AddGenerationRecord(generation);

            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Random, generation.GenerationId)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var duplicate = project.DuplicateAnimation(animation.AnimationId);

            Assert.That(project.HasGeneration(generation.GenerationId), Is.True);
            var current = project.Generations[0];
            Assert.That(current.AnimationId, Is.EqualTo(animation.AnimationId));
            Assert.That(current.GeneratorType, Is.EqualTo(GeneratorType.Random));
            Assert.That(current.SourcePresetId, Is.EqualTo("preset-1"));
            Assert.That(current.AlgorithmVersion, Is.EqualTo(1));
            Assert.That(current.SettingsHash, Is.EqualTo("hash-1"));
            Assert.That(project.Generations.Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateAnimation_DuplicateSide_IsIndependentlyManaged()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var generation = GenerationRecord.Create(animation.AnimationId, GeneratorType.Blink, "preset-01", 2, "hash-01");
            project.AddGenerationRecord(generation);

            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Blink, generation.GenerationId)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var duplicate = project.DuplicateAnimation(animation.AnimationId);
            var duplicateGeneration = project.Generations[1];

            Assert.That(duplicateGeneration.AnimationId, Is.EqualTo(duplicate.AnimationId));
            Assert.That(duplicateGeneration.GenerationId, Is.Not.EqualTo(generation.GenerationId));
            Assert.That(duplicateGeneration.GeneratorType, Is.EqualTo(GeneratorType.Blink));
            Assert.That(duplicateGeneration.SourcePresetId, Is.EqualTo("preset-01"));
            Assert.That(duplicateGeneration.AlgorithmVersion, Is.EqualTo(2));
            Assert.That(duplicateGeneration.SettingsHash, Is.EqualTo("hash-01"));
            Assert.That(duplicateGeneration.GenerationId, Is.EqualTo(duplicate.Timeline.Tracks[0].BlendShape.Keys[0].Origin.GenerationId),
                "The duplicated key must point at the duplicate's own generation record.");

            var report = ProjectValidator.Validate(project);
            Assert.That(report.HasBlocking, Is.False);
            Assert.That(TestHelpers.FindDiagnostic(report, FaceMotionDiagnosticCodes.OrphanedGenerationKey), Is.Null);
        }

        [Test]
        public void DuplicateAnimation_MissingAnimation_ReturnsNull()
        {
            var project = FaceMotionProject.CreateNew();
            Assert.That(project.DuplicateAnimation(StableId.New()), Is.Null);
        }

        [Test]
        public void DuplicateTrack_ConvertedGeneratedKeysToManual()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("Gen");
            var generation = GenerationRecord.Create(animation.AnimationId, GeneratorType.Blink, "preset-1", 1, "hash-1");
            project.AddGenerationRecord(generation);

            var track = FaceTrackData.CreateBlendShape("Body/Renderer", "Blink");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 80f, InterpolationType.Linear,
                new KeyOrigin(OriginKind.Blink, generation.GenerationId)));
            animation.Timeline.AddTrack(track);
            project.AddAnimation(animation);

            var duplicate = track.Duplicate();
            var duplicateKey = duplicate.BlendShape.Keys[0];

            Assert.That(duplicate.TrackId, Is.Not.EqualTo(track.TrackId));
            Assert.That(duplicateKey.KeyId, Is.Not.EqualTo(track.BlendShape.Keys[0].KeyId));
            Assert.That(duplicateKey.Origin.Kind, Is.EqualTo(OriginKind.Manual),
                "A single track duplicate must not fabricate a partial generation record.");
            Assert.That(duplicateKey.Origin.GenerationId, Is.Null.Or.Empty);
            Assert.That(track.BlendShape.Keys[0].Origin.GenerationId, Is.EqualTo(generation.GenerationId),
                "The source track must keep its generated origin.");
        }

        [Test]
        public void MergeRules_DefaultPolicy_PreservesEveryExistingKey()
        {
            var manual = KeyOrigin.Manual;
            var generated = new KeyOrigin(OriginKind.Random, StableId.New());

            Assert.That(MergeRulesManualKeyIsProtected(manual), Is.True);
            Assert.That(MotionMergeRules.ManualKeyIsProtected(generated), Is.False);
            Assert.That(MotionMergeRules.Decide(manual, generated), Is.EqualTo(MotionMergeDecision.Reject));
            Assert.That(MotionMergeRules.Decide(generated, manual), Is.EqualTo(MotionMergeDecision.Reject));
            Assert.That(MotionMergeRules.Decide(generated, generated), Is.EqualTo(MotionMergeDecision.Reject));
            Assert.That(MotionMergeRules.ShouldSkipIncoming(manual, generated), Is.True);
            Assert.That(MotionMergeRules.ShouldSkipIncoming(generated, generated), Is.True);
        }

        [Test]
        public void MergeRules_Spec_IsDocumentedPhasesPolicy()
        {
            Assert.That(MotionMergeRules.Spec, Is.Not.Null.And.Not.Empty);
            StringAssert.Contains("Reject", MotionMergeRules.Spec);
        }

        private static bool MergeRulesManualKeyIsProtected(KeyOrigin origin)
        {
            return MotionMergeRules.ManualKeyIsProtected(origin);
        }
    }
}