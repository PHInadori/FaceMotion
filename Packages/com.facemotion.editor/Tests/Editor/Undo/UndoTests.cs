using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class UndoTests
    {
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        private static FaceMotionProject CreatePersistedProject(TempFaceMotionAsset temp, string fileName)
        {
            var project = FaceMotionProject.CreateNew();
            string assetPath = temp.AssetPath(fileName);
            EditorUtility.SetDirty(project);
            AssetDatabase.CreateAsset(project, assetPath);
            AssetDatabase.SaveAssets();
            return project;
        }

        [Test]
        public void AddAnimation_UndoRedo_RestoresIdenticalData()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = CreatePersistedProject(temp, "AddAnimation");

                using (var transaction = new UnityUndoTransaction())
                {
                    bool executed = ProjectCommandExecutor.TryExecute(new AddAnimationCommand("A"), project, transaction, out var error);
                    Assert.That(executed, Is.True);
                    Assert.That(error, Is.Null);
                }

                Assert.That(project.Animations.Count, Is.EqualTo(1));
                string animationId = project.Animations[0].AnimationId;
                string displayName = project.Animations[0].DisplayName;

                Undo.PerformUndo();
                Assert.That(project.Animations.Count, Is.EqualTo(0));

                Undo.PerformRedo();
                Assert.That(project.Animations.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].AnimationId, Is.EqualTo(animationId));
                Assert.That(project.Animations[0].DisplayName, Is.EqualTo(displayName));
            }
        }

        [Test]
        public void RemoveAnimation_UndoRedo_RestoresIdenticalData()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("ToRemove");
                project.AddAnimation(animation);
                string assetPath = temp.AssetPath("RemoveAnimation");
                EditorUtility.SetDirty(project);
                AssetDatabase.CreateAsset(project, assetPath);
                AssetDatabase.SaveAssets();

                using (var transaction = new UnityUndoTransaction())
                {
                    Assert.That(
                        ProjectCommandExecutor.TryExecute(new RemoveAnimationCommand(animation.AnimationId), project, transaction, out _),
                        Is.True);
                }
                Assert.That(project.Animations.Count, Is.EqualTo(0));

                Undo.PerformUndo();
                Assert.That(project.Animations.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].AnimationId, Is.EqualTo(animation.AnimationId));

                Undo.PerformRedo();
                Assert.That(project.Animations.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void AddTrackAndKey_UndoRedo_RestoresIdenticalData()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("A");
                project.AddAnimation(animation);
                string animationId = animation.AnimationId;
                string assetPath = temp.AssetPath("TrackAndKey");
                EditorUtility.SetDirty(project);
                AssetDatabase.CreateAsset(project, assetPath);
                AssetDatabase.SaveAssets();

                using (var transaction = new UnityUndoTransaction())
                {
                    Assert.That(
                        ProjectCommandExecutor.TryExecute(
                            new AddBlendShapeTrackCommand(animationId, "Body/Renderer", "Smile"), project, transaction, out _),
                        Is.True);
                }
                string trackId = project.Animations[0].Timeline.Tracks[0].TrackId;
                Undo.IncrementCurrentGroup();

                using (var transaction = new UnityUndoTransaction())
                {
                    Assert.That(
                        ProjectCommandExecutor.TryExecute(new AddFloatKeyCommand(animationId, trackId, 0.5f, 100f), project, transaction, out _),
                        Is.True);
                }

                var keys = project.Animations[0].Timeline.Tracks[0].BlendShape.Keys;
                Assert.That(keys.Count, Is.EqualTo(1));
                string keyId = keys[0].KeyId;

                Undo.PerformUndo();
                Assert.That(project.Animations[0].Timeline.Tracks[0].BlendShape.Keys.Count, Is.EqualTo(0));

                Undo.PerformUndo();
                Assert.That(project.Animations[0].Timeline.Tracks.Count, Is.EqualTo(0));

                Undo.PerformRedo();
                Assert.That(project.Animations[0].Timeline.Tracks.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].Timeline.Tracks[0].TrackId, Is.EqualTo(trackId));

                Undo.PerformRedo();
                Assert.That(project.Animations[0].Timeline.Tracks[0].BlendShape.Keys.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].Timeline.Tracks[0].BlendShape.Keys[0].KeyId, Is.EqualTo(keyId));
            }
        }

        [Test]
        public void AddVector3Key_FailsOnBlendShapeTrack_WithKindMismatch()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("A");
                project.AddAnimation(animation);
                string animationId = animation.AnimationId;
                string assetPath = temp.AssetPath("KindMismatch");
                EditorUtility.SetDirty(project);
                AssetDatabase.CreateAsset(project, assetPath);
                AssetDatabase.SaveAssets();

                string trackId;
                using (var transaction = new UnityUndoTransaction())
                {
                    Assert.That(
                        ProjectCommandExecutor.TryExecute(
                            new AddBlendShapeTrackCommand(animationId, "Body/Renderer", "Smile"), project, transaction, out _),
                        Is.True);
                    trackId = project.Animations[0].Timeline.Tracks[0].TrackId;
                }

                using (var transaction = new UnityUndoTransaction())
                {
                    bool executed = ProjectCommandExecutor.TryExecute(
                        new AddVector3KeyCommand(animationId, trackId, 0f, Vector3.zero), project, transaction, out var error);
                    Assert.That(executed, Is.False);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(error.Code, Is.EqualTo(FaceMotionDiagnosticCodes.CommandKindMismatch));
                }

                Assert.That(project.Animations[0].Timeline.Tracks[0].BlendShape.Keys.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void AddScaleTrackAndKey_UndoRedo_RestoresIdenticalData()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("A");
                project.AddAnimation(animation);
                string animationId = animation.AnimationId;
                string assetPath = temp.AssetPath("ScaleTrackKey");
                EditorUtility.SetDirty(project);
                AssetDatabase.CreateAsset(project, assetPath);
                AssetDatabase.SaveAssets();

                using (var transaction = new UnityUndoTransaction())
                {
                    Assert.That(
                        ProjectCommandExecutor.TryExecute(
                            new AddTransformTrackCommand(animationId, TrackKind.TransformScale, "Head"), project, transaction, out _),
                        Is.True);
                }
                Assert.That(project.Animations[0].Timeline.Tracks[0].Kind, Is.EqualTo(TrackKind.TransformScale));
                string trackId = project.Animations[0].Timeline.Tracks[0].TrackId;
                Undo.IncrementCurrentGroup();

                using (var transaction = new UnityUndoTransaction())
                {
                    Assert.That(
                        ProjectCommandExecutor.TryExecute(
                            new AddVector3KeyCommand(animationId, trackId, 0.5f, new Vector3(2f, 2f, 2f)), project, transaction, out _),
                        Is.True);
                }

                var keys = project.Animations[0].Timeline.Tracks[0].Transform.Keys;
                Assert.That(keys.Count, Is.EqualTo(1));
                string keyId = keys[0].KeyId;

                Undo.PerformUndo();
                Assert.That(project.Animations[0].Timeline.Tracks[0].Transform.Keys.Count, Is.EqualTo(0));

                Undo.PerformUndo();
                Assert.That(project.Animations[0].Timeline.Tracks.Count, Is.EqualTo(0));

                Undo.PerformRedo();
                Assert.That(project.Animations[0].Timeline.Tracks.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].Timeline.Tracks[0].Kind, Is.EqualTo(TrackKind.TransformScale));
                Assert.That(project.Animations[0].Timeline.Tracks[0].TrackId, Is.EqualTo(trackId));

                Undo.PerformRedo();
                Assert.That(project.Animations[0].Timeline.Tracks[0].Transform.Keys.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].Timeline.Tracks[0].Transform.Keys[0].KeyId, Is.EqualTo(keyId));
                Assert.That(project.Animations[0].Timeline.Tracks[0].Transform.Keys[0].Value, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            }
        }

        [Test]
        public void FailedValidation_LeavesProjectUntouchedWithoutUndoEntry()
        {
            using (var temp = new TempFaceMotionAsset())
            {
                var project = FaceMotionProject.CreateNew();
                var animation = FaceMotionAnimationData.Create("A");
                project.AddAnimation(animation);
                string assetPath = temp.AssetPath("NoUndo");
                EditorUtility.SetDirty(project);
                AssetDatabase.CreateAsset(project, assetPath);
                AssetDatabase.SaveAssets();

                string missingTrackId = StableId.New();
                using (var transaction = new UnityUndoTransaction())
                {
                    bool executed = ProjectCommandExecutor.TryExecute(
                        new AddFloatKeyCommand(animation.AnimationId, missingTrackId, 0f, 1f), project, transaction, out var error);
                    Assert.That(executed, Is.False);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(error.Code, Is.EqualTo(FaceMotionDiagnosticCodes.CommandTargetNotFound));
                }

                Assert.That(project.Animations.Count, Is.EqualTo(1));
                Assert.That(project.Animations[0].Timeline.Tracks.Count, Is.EqualTo(0));
            }
        }
    }
}