using System.Collections.Generic;
using FaceMotion.Animation;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Generation;
using FaceMotion.Timeline;
using FaceMotion.Editor.UI.Panels;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class BuiltinPresetGuidContractTests
    {
        [Test]
        public void Inventory_HasStableUniqueLowercaseGuidIds()
        {
            var presets = FaceMotionBuiltins.Presets;
            CollectionAssert.AreEqual(new[] { "c134e6e8df2a4bf1a08d7c95f3a6b201", "4a6c8e10b2d34f7591a3c5e7f9b0d412", "7d9f1b23c4e54680a2d5f8b1c3e6a724", "e2b4d6f8091a4c73b5e7f1a3d6c8b935", "19c3e5f7a8b24d60c1e4f6a9b2d7c846", "6f8a0c12d3e54b79a1c4e7f9b2d5a638", "b3d5f7a9012c4e68b1d4f6a8c2e9d750", "8e1a3c5d7f904b26a8c1e4f6b9d2a573" }, System.Array.ConvertAll(((FaceMotionBuiltinPreset[])presets), value => value.Id));
            var ids = new HashSet<string>();
            foreach (var preset in presets)
            {
                Assert.That(preset.Id, Does.Match("^[0-9a-f]{32}$"));
                Assert.That(ids.Add(preset.Id), Is.True);
                Assert.That(FaceMotionBuiltins.Find(preset.Id), Is.SameAs(preset));
                Assert.That(FaceMotionBuiltins.Find(preset.BuiltinKey), Is.SameAs(preset));
            }
            Assert.That(presets[0].Targets[1].Stagger, Is.EqualTo(.04f));
            Assert.That(presets[3].Targets[1].LogicalTargetId, Is.EqualTo("eye.close.right"));
            Assert.That(presets[6].Targets[2].Stagger, Is.EqualTo(.08f));
            Assert.That(presets[7].Targets[4].Optional, Is.True);
        }

        [Test]
        public void LegacyBuiltinKeys_NormalizePersistedRecordAndSnapshotToGuid()
        {
            var project = FaceMotionProject.CreateNew();
            var snapshot = new GenerationSettingsSnapshot { GeneratorType = GeneratorType.Preset, SourcePresetId = "builtin.blink" };
            project.AddGenerationRecord(GenerationRecord.Create("animation", GeneratorType.Preset, "builtin.blink", 1, "hash", "generation", snapshot.Serialize()));

            ProjectNormalizer.Normalize(project);

            var blinkId = FaceMotionBuiltins.Find("builtin.blink").Id;
            Assert.That(project.Generations[0].SourcePresetId, Is.EqualTo(blinkId));
            Assert.That(UnityEngine.JsonUtility.FromJson<GenerationSettingsSnapshot>(project.Generations[0].SettingsSnapshot).SourcePresetId, Is.EqualTo(blinkId));
        }
    }

    public sealed class PhaseE3MappingTests
    {
        [Test]
        public void Mapping_UsesTheFullTargetSuffixAndRequiresMandatoryBindings()
        {
            var shapes = new[] { new BlendShapeIndexEntry("Face", "Face", "CloseLeft", 0, 0f) };
            var index = AvatarIndex.Create(AvatarFingerprintBuilder.Compute(null, null, shapes), null, null, shapes);
            var suggestions = LogicalMappingSuggestions.Suggest(index);
            Assert.That(suggestions, Has.Count.EqualTo(1));
            Assert.That(suggestions[0].LogicalTargetId, Is.EqualTo("eye.close.left"));
            Assert.Throws<System.ArgumentException>(() => PhaseEGenerators.CreatePreset("id", FaceMotionBuiltins.Find("builtin.wink-left"), new Dictionary<string, BlendShapeBinding>()));
        }
    }

    public sealed class PhaseE3SnapshotTests
    {
        [Test]
        public void Snapshot_RestoresEveryPresetTarget()
        {
            var preset = FaceMotionBuiltins.Find("builtin.blink");
            var bindings = PhaseE3Bindings.Bind("eye.close.left", "Face", "Left", "eye.close.right", "Face", "Right");
            var snapshot = GenerationSettingsSnapshot.ForPreset(preset, bindings);
            Assert.That(snapshot.SourcePresetId, Is.EqualTo(preset.Id));
            Assert.That(GeneratedMotionRegenerator.TryCreate(GenerationRecord.Create("a", GeneratorType.Preset, preset.Id, 1, snapshot.Hash(), "g", snapshot.Serialize()), "again", out var motion), Is.True);
            Assert.That(motion.Tracks, Has.Count.EqualTo(2));
            Assert.That(motion.Tracks[1].FloatKeys[1].Time, Is.EqualTo(.14f));
        }
    }

    public sealed class PhaseE3MergeTests
    {
        [Test]
        public void Merge_RejectsGeneratedOverlayCollision()
        {
            var project = FaceMotionProject.CreateNew(); var animation = FaceMotionAnimationData.Create("A"); project.AddAnimation(animation);
            var track = FaceTrackData.CreateBlendShape("Face", "Left"); track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 10f, InterpolationType.Linear, new KeyOrigin(OriginKind.Preset, "old"))); animation.Timeline.AddTrack(track);
            var preset = FaceMotionBuiltins.Find("builtin.wink-left");
            var result = GeneratedMotionApplier.Apply(project, animation, PhaseEGenerators.CreatePreset("new", preset, PhaseE3Bindings.Bind("eye.close.left", "Face", "Left")), GeneratorType.Preset, preset.Id, "hash");
            Assert.That(result.AddedKeys, Is.EqualTo(2));
            Assert.That(result.ProtectedManualKeys, Is.Zero);
        }
    }

    public sealed class PhaseE3OverlayTests
    {
        [Test]
        public void Overlay_SkipsUnmappedOptionalTargets()
        {
            var motion = PhaseEGenerators.CreatePreset("id", FaceMotionBuiltins.Find("builtin.smile"), PhaseE3Bindings.Bind("mouth.smile", "Face", "Smile"));
            Assert.That(motion.Tracks, Has.Count.EqualTo(1));
            Assert.That(motion.Tracks[0].FloatKeys[0].Value, Is.Zero);
            Assert.That(motion.Tracks[0].FloatKeys[2].Value, Is.Zero);
        }
    }

    public sealed class PhaseE3BlinkRandomTests
    {
        [Test]
        public void BlinkAndRandom_RemainSeedDeterministic()
        {
            var blink = new BlinkGenerationSettings { Seed = 4, Duration = 4f, MinInterval = 1f, MaxInterval = 1f };
            var random = new RandomBlendShapeGenerationSettings { Seed = 4, Duration = .5f, Step = .25f };
            Assert.That(PhaseEGenerators.CreateBlink("a", blink, new BlendShapeBinding("F", "B")).Tracks[0].FloatKeys.Count, Is.EqualTo(PhaseEGenerators.CreateBlink("b", blink, new BlendShapeBinding("F", "B")).Tracks[0].FloatKeys.Count));
            Assert.That(PhaseEGenerators.CreateRandomBlendShape("a", random, new BlendShapeBinding("F", "B")).Tracks[0].FloatKeys[1].Value, Is.EqualTo(PhaseEGenerators.CreateRandomBlendShape("b", random, new BlendShapeBinding("F", "B")).Tracks[0].FloatKeys[1].Value));
        }
    }

    public sealed class PhaseE3UndoTests
    {
        [Test]
        public void Undo_RemovesOnePresetApplicationAndItsRecord()
        {
            UnityEditor.Undo.ClearAll();
            var project = FaceMotionProject.CreateNew(); var animation = FaceMotionAnimationData.Create("A"); project.AddAnimation(animation);
            UnityEditor.AssetDatabase.CreateAsset(project, "Assets/__FaceMotionTests/PhaseE3Undo.asset");
            var motion = PhaseEGenerators.CreatePreset("undo", FaceMotionBuiltins.Find("builtin.wink-left"), PhaseE3Bindings.Bind("eye.close.left", "Face", "Left"));
            global::FaceMotion.Editor.GeneratedMotionUndoService.Apply(project, animation, motion, GeneratorType.Preset, FaceMotionBuiltins.Find("builtin.wink-left").Id, "hash");
            UnityEditor.Undo.PerformUndo();
            Assert.That(project.Generations, Has.Count.EqualTo(0));
            Assert.That(animation.Timeline.Tracks, Has.Count.EqualTo(0));
            UnityEditor.AssetDatabase.DeleteAsset("Assets/__FaceMotionTests/PhaseE3Undo.asset");
        }
    }

    public sealed class PhaseE3UiReachabilityTests
    {
        [Test]
        public void GenerationPanel_ExposesTheCompleteBuiltinInventory()
        {
            Assert.That(GenerationPanel.Builtins, Is.SameAs(FaceMotionBuiltins.Presets));
            Assert.That(GenerationPanel.Builtins.Count, Is.EqualTo(8));
        }
    }

    internal static class PhaseE3Bindings
    {
        public static Dictionary<string, BlendShapeBinding> Bind(params string[] values)
        {
            var result = new Dictionary<string, BlendShapeBinding>();
            for (int i = 0; i < values.Length; i += 3) result.Add(values[i], new BlendShapeBinding(values[i + 1], values[i + 2]));
            return result;
        }
    }
}
