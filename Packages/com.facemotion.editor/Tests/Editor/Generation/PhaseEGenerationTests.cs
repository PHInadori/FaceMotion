using FaceMotion.Animation;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Generation;
using FaceMotion.Editor;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using GeneratedMotionUndoService = global::FaceMotion.Editor.GeneratedMotionUndoService;

namespace FaceMotion.Editor.Tests
{
    public sealed class PhaseEGenerationTests
    {
        [Test]
        public void Blink_IsDeterministicForSeed()
        {
            var settings = new BlinkGenerationSettings { Seed = 42, Duration = 8f, MinInterval = 1f, MaxInterval = 2f };
            var a = PhaseEGenerators.CreateBlink("a", settings, new BlendShapeBinding("Face", "Blink"));
            var b = PhaseEGenerators.CreateBlink("b", settings, new BlendShapeBinding("Face", "Blink"));
            Assert.That(a.Tracks[0].FloatKeys.Count, Is.EqualTo(b.Tracks[0].FloatKeys.Count));
            for (int i = 0; i < a.Tracks[0].FloatKeys.Count; i++) Assert.That(a.Tracks[0].FloatKeys[i].Time, Is.EqualTo(b.Tracks[0].FloatKeys[i].Time));
        }

        [Test]
        public void Apply_ProtectsManualKeyAndKeepsProvenance()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("A");
            project.AddAnimation(animation);
            var track = FaceTrackData.CreateBlendShape("Face", "Blink");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 10f));
            animation.Timeline.AddTrack(track);
            var motion = PhaseEGenerators.CreateRandomBlendShape("generation", new RandomBlendShapeGenerationSettings { Duration = 0f }, new BlendShapeBinding("Face", "Blink"));
            var result = GeneratedMotionApplier.Apply(project, animation, motion, GeneratorType.Random, string.Empty, "settings");
            Assert.That(result.ProtectedManualKeys, Is.EqualTo(1));
            Assert.That(track.BlendShape.Keys.Count, Is.EqualTo(1));
        }

        [Test]
        public void RandomRotation_IsDeterministicAndUsesRotationTrack()
        {
            var settings = new RandomRotationGenerationSettings { Seed = 7, TransformPath = "Head", Duration = .5f, Step = .25f };
            var motion = PhaseEGenerators.CreateRandomRotation("generation", settings);
            Assert.That(motion.Tracks[0].Kind, Is.EqualTo(TrackKind.TransformRotation));
            Assert.That(motion.Tracks[0].Vector3Keys.Count, Is.EqualTo(3));
        }

        [Test]
        public void Apply_UndoRestoresGeneratedKeysAndGenerationRecord()
        {
            Undo.ClearAll();
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("A");
            project.AddAnimation(animation);
            AssetDatabase.CreateAsset(project, "Assets/__FaceMotionTests/PhaseEUndo.asset");
            var motion = PhaseEGenerators.CreateRandomBlendShape("phaseeundo", new RandomBlendShapeGenerationSettings { Duration = 0f }, new BlendShapeBinding("Face", "Smile"));
            GeneratedMotionUndoService.Apply(project, animation, motion, GeneratorType.Random, string.Empty, "settings");
            Assert.That(project.Generations.Count, Is.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(project.Generations.Count, Is.EqualTo(0));
            AssetDatabase.DeleteAsset("Assets/__FaceMotionTests/PhaseEUndo.asset");
        }

        [Test]
        public void Suggestions_ReturnOnlyUniqueLogicalTargetMatches()
        {
            var blendShapes = new[]
            {
                new BlendShapeIndexEntry("Face", "Face", "Smile", 0, 0f),
                new BlendShapeIndexEntry("Face", "Face", "Blink", 1, 0f),
                new BlendShapeIndexEntry("Cheek", "Cheek", "Blink", 0, 0f)
            };
            var index = AvatarIndex.Create(
                AvatarFingerprintBuilder.Compute(null, null, blendShapes),
                null,
                null,
                blendShapes);

            var suggestions = LogicalMappingSuggestions.Suggest(index);
            Assert.That(suggestions, Has.Count.EqualTo(1));
            Assert.That(suggestions[0].LogicalTargetId, Is.EqualTo("mouth.smile"));
            Assert.That(suggestions[0].BlendShape.Value, Is.EqualTo(new BlendShapeBinding("Face", "Smile")));
        }

        [Test]
        public void Apply_PresetRecordsPresetIdAndGeneratedOrigin()
        {
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("A");
            project.AddAnimation(animation);
            var motion = PhaseEGenerators.CreateRandomBlendShape("preset", new RandomBlendShapeGenerationSettings { Duration = 0f, MinValue = 100f, MaxValue = 100f }, new BlendShapeBinding("Face", "Smile"));

            var preset = FaceMotionBuiltins.Find("builtin.smile");
            GeneratedMotionApplier.Apply(project, animation, motion, GeneratorType.Preset, preset.Id, "settings");

            Assert.That(project.Generations[0].SourcePresetId, Is.EqualTo(preset.Id));
            Assert.That(animation.Timeline.Tracks[0].BlendShape.Keys[0].Origin.Kind, Is.EqualTo(OriginKind.Preset));
        }

        [Test]
        public void Builtins_ExposeEightStableLogicalTargets()
        {
            Assert.That(FaceMotionBuiltins.Presets.Count, Is.EqualTo(8));
            Assert.That(FaceMotionBuiltins.Presets[0].Targets[0].LogicalTargetId, Is.EqualTo("mouth.smile"));
            Assert.That(FaceMotionBuiltins.Presets[7].Targets[0].LogicalTargetId, Is.EqualTo("cheek.blush.left"));
        }

        [Test]
        public void Confirmation_OnlyPersistsCurrentUniqueCandidate()
        {
            var shapes = new[] { new BlendShapeIndexEntry("Face", "Face", "Smile", 0, 0f) };
            var index = AvatarIndex.Create(AvatarFingerprintBuilder.Compute(null, null, shapes), null, null, shapes);
            var candidate = LogicalMappingSuggestions.Suggest(index)[0];
            var profile = AvatarMappingProfile.CreateNew();

            Assert.That(LogicalMappingConfirmation.Confirm(profile, index, candidate), Is.True);
            Assert.That(LogicalMappingConfirmation.TryResolveConfirmed(profile, index, "mouth.smile", out var binding), Is.True);
            Assert.That(binding, Is.EqualTo(new BlendShapeBinding("Face", "Smile")));
            Assert.That(LogicalMappingConfirmation.Confirm(profile, index, candidate), Is.False);
        }

        [Test]
        public void Snapshot_RoundTripsAndRegeneratesPureMotion()
        {
            var settings = new RandomBlendShapeGenerationSettings { Seed = 19, StartTime = .5f, Duration = .5f, Step = .25f, MinValue = 2f, MaxValue = 8f };
            var binding = new BlendShapeBinding("Face", "Smile");
            var snapshot = GenerationSettingsSnapshot.ForRandomBlendShape(settings, binding);
            var project = FaceMotionProject.CreateNew();
            var animation = FaceMotionAnimationData.Create("A");
            project.AddAnimation(animation);
            var source = PhaseEGenerators.CreateRandomBlendShape("source", settings, binding);

            GeneratedMotionApplier.Apply(project, animation, source, snapshot.GeneratorType, snapshot.SourcePresetId, snapshot.Hash(), snapshot.Serialize());

            Assert.That(project.Generations[0].SettingsSnapshot, Is.Not.Empty);
            Assert.That(GeneratedMotionRegenerator.TryCreate(project.Generations[0], "again", out var regenerated), Is.True);
            Assert.That(regenerated.Tracks[0].FloatKeys, Has.Count.EqualTo(source.Tracks[0].FloatKeys.Count));
            for (int i = 0; i < source.Tracks[0].FloatKeys.Count; i++)
            {
                Assert.That(regenerated.Tracks[0].FloatKeys[i].Time, Is.EqualTo(source.Tracks[0].FloatKeys[i].Time));
                Assert.That(regenerated.Tracks[0].FloatKeys[i].Value, Is.EqualTo(source.Tracks[0].FloatKeys[i].Value));
            }
        }
    }
}
