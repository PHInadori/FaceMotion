using System;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.VRChat;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    // Phase C.1: existing UI infrastructure connected to its controller/state contracts.
    public sealed class PhaseC1CompletionTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TempFaceMotionAsset _temp;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _temp = new TempFaceMotionAsset();
        }

        [TearDown]
        public void TearDown()
        {
            _temp?.Dispose();
            Undo.ClearAll();
        }

        [Test]
        public void BlendShapeCandidate_AddsExactBinding()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetupAnimation("BlendCandidate");
                SetAvatar(fixture);
                var candidate = FindBlendCandidate(AvatarFixture.FaceRendererPath, "Mouth_Smile");

                Assert.That(_tracks.AddBlendShapeTrack(candidate), Is.True);
                var track = _session.GetSelectedTrack();
                Assert.That(track.BlendShape.RendererPath, Is.EqualTo(AvatarFixture.FaceRendererPath));
                Assert.That(track.BlendShape.BlendShapeName, Is.EqualTo("Mouth_Smile"));
            }
        }

        [Test]
        public void TransformCandidate_AddsExactBinding()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetupAnimation("TransformCandidate");
                SetAvatar(fixture);
                var candidate = FindTransformCandidate(AvatarFixture.HeadPath);

                Assert.That(_tracks.AddTransformTrack(TrackKind.TransformRotation, candidate), Is.True);
                var track = _session.GetSelectedTrack();
                Assert.That(track.Kind, Is.EqualTo(TrackKind.TransformRotation));
                Assert.That(track.Transform.TransformPath, Is.EqualTo(AvatarFixture.HeadPath));
            }
        }

        [Test]
        public void CandidateSearch_IsCaseInsensitiveContains()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetAvatar(fixture);
                var candidates = _session.Candidates.FilterBlendShapes("mOuTh_sMiLe");
                Assert.That(candidates.Count, Is.EqualTo(1));
                Assert.That(candidates[0].RendererPath, Is.EqualTo(AvatarFixture.FaceRendererPath));
            }
        }

        [Test]
        public void CandidateSelection_IsSafeWithoutAvatar()
        {
            SetupAnimation("NoAvatarCandidate");
            Assert.That(_session.Candidates, Is.Null);
            Assert.That(_tracks.AddBlendShapeTrack((AvatarCandidateSnapshot.BlendShapeCandidate)null), Is.False);
            Assert.That(_tracks.AddTransformTrack(TrackKind.TransformPosition, (AvatarCandidateSnapshot.TransformCandidate)null), Is.False);
        }

        [Test]
        public void CandidateDisplay_UsesPathToDistinguishDuplicateNames()
        {
            using (var fixture = AvatarFixture.Create())
            {
                SetAvatar(fixture);
                string faceLabel = null;
                string cheekLabel = null;
                var candidates = _session.Candidates.FilterBlendShapes("Smile");
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].BlendShapeName != "Smile")
                    {
                        continue;
                    }

                    if (candidates[i].RendererPath == AvatarFixture.FaceRendererPath) faceLabel = candidates[i].DisplayLabel;
                    if (candidates[i].RendererPath == AvatarFixture.CheekRendererPath) cheekLabel = candidates[i].DisplayLabel;
                }

                Assert.That(faceLabel, Does.Contain(AvatarFixture.FaceRendererPath));
                Assert.That(cheekLabel, Does.Contain(AvatarFixture.CheekRendererPath));
                Assert.That(faceLabel, Is.Not.EqualTo(cheekLabel));
            }
        }

        [Test]
        public void Duration_CommitUpdatesTimeline()
        {
            SetupAnimation("DurationCommit");
            Assert.That(_animations.SetDuration(2.5f), Is.True);
            Assert.That(_session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(2.5f));
        }

        [Test]
        public void Duration_InvalidValueDoesNotCommit()
        {
            SetupAnimation("DurationInvalid");
            float before = _session.GetSelectedDuration();
            Assert.That(_animations.SetDuration(float.NaN), Is.False);
            Assert.That(_session.GetSelectedDuration(), Is.EqualTo(before));
        }

        [Test]
        public void FrameRate_CommitUpdatesTimeline()
        {
            SetupAnimation("FrameRateCommit");
            Assert.That(_animations.SetFrameRate(24f), Is.True);
            Assert.That(_session.GetSelectedFrameRate(), Is.EqualTo(24f));
        }

        [Test]
        public void FrameRate_InvalidValueDoesNotCommit()
        {
            SetupAnimation("FrameRateInvalid");
            float before = _session.GetSelectedFrameRate();
            Assert.That(_animations.SetFrameRate(0f), Is.False);
            Assert.That(_session.GetSelectedFrameRate(), Is.EqualTo(before));
        }

        [Test]
        public void Loop_CommitUpdatesTimeline()
        {
            SetupAnimation("LoopCommit");
            Assert.That(_animations.SetLoop(true), Is.True);
            Assert.That(_session.GetSelectedAnimation().Timeline.Loop, Is.True);
        }

        [Test]
        public void DurationChange_ClampsCurrentTimeAndReportsOutOfRangeKeys()
        {
            SetupBlendKey("DurationClamp", 0.8f, out _);
            _session.ViewState.CurrentTime = 0.9f;
            Assert.That(_animations.SetDuration(0.5f), Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.5f));
            Assert.That(_session.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == FaceMotion.Diagnostics.FaceMotionDiagnosticCodes.KeyBeyondDuration));
        }

        [Test]
        public void SnapToggle_ChangesEditorOnlyState()
        {
            SetupAnimation("SnapToggle");
            var timeline = new TimelineView(_session, _keys, _tracks);
            timeline.SetSnapEnabled(false);
            Assert.That(_session.ViewState.SnapEnabled, Is.False);
            timeline.SetSnapEnabled(true);
            Assert.That(_session.ViewState.SnapEnabled, Is.True);
        }

        [Test]
        public void SnapOn_AddKeyUsesFrameGrid()
        {
            SetupBlendKey("SnapOnAdd", 0f, out _);
            _session.GetSelectedTrack().BlendShape.RemoveKey(_session.GetSelectedTrack().BlendShape.Keys[0].KeyId);
            _session.ViewState.SnapEnabled = true;
            _keys.AddKeyAt(0.013f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(_session.GetSelectedTrack().BlendShape.Keys[0].Time, Is.EqualTo(1f / 60f).Within(1e-4f));
        }

        [Test]
        public void SnapOff_AddKeyKeepsAuthoredTime()
        {
            SetupBlendKey("SnapOffAdd", 0f, out _);
            _session.GetSelectedTrack().BlendShape.RemoveKey(_session.GetSelectedTrack().BlendShape.Keys[0].KeyId);
            _session.ViewState.SnapEnabled = false;
            _keys.AddKeyAt(0.013f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(_session.GetSelectedTrack().BlendShape.Keys[0].Time, Is.EqualTo(0.013f).Within(1e-4f));
        }

        [Test]
        public void SnapOn_DragUsesFrameGrid()
        {
            SetupBlendKey("SnapDrag", 0.1f, out string keyId);
            _session.Selection.SetSingle(keyId);
            _session.ViewState.SnapEnabled = true;
            _keys.BeginKeyDrag();
            _keys.UpdateKeyDrag(2f, 120f);
            _keys.EndKeyDrag();
            float time = _session.GetSelectedTrack().BlendShape.Keys[0].Time;
            Assert.That(time, Is.EqualTo(7f / 60f).Within(1e-4f));
        }

        [Test]
        public void DragAtAbsolutePointer_DoesNotAccumulateMovement()
        {
            SetupBlendKey("AbsoluteDrag", 0.1f, out string keyId);
            _session.Selection.SetSingle(keyId);
            _session.ViewState.SnapEnabled = false;

            _keys.BeginKeyDrag();
            _keys.UpdateKeyDragAt(150f, 100f, 100f);
            _keys.UpdateKeyDragAt(150f, 100f, 100f);
            _keys.EndKeyDrag();

            Assert.That(_session.GetSelectedTrack().BlendShape.Keys[0].Time, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void UiText_UsesJapaneseTableAndEnglishFallback()
        {
            Assert.That(FaceMotionUiText.Get("addKey", SystemLanguage.Japanese), Is.EqualTo("キーを追加"));
            Assert.That(FaceMotionUiText.Get("addKey", SystemLanguage.English), Is.EqualTo("Add Key"));
        }

        [Test]
        public void InspectorApply_ChangesTimeValueAndInterpolationTogether()
        {
            SetupBlendKey("InspectorApply", 0.1f, out string keyId);
            Assert.That(_keys.ApplyKeyEdits(keyId, 0.2f, 0.9f, Vector3.zero, InterpolationType.Smooth), Is.True);
            var key = _session.GetSelectedTrack().BlendShape.Keys[0];
            Assert.That(key.Time, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(key.Value, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.Smooth));
        }

        [Test]
        public void InspectorApply_UsesOneUndoForAllFields()
        {
            SetupBlendKey("InspectorUndo", 0.1f, out string keyId);
            Undo.ClearAll();
            _keys.ApplyKeyEdits(keyId, 0.2f, 0.9f, Vector3.zero, InterpolationType.Smooth);
            Undo.PerformUndo();

            var key = _session.GetSelectedTrack().BlendShape.Keys[0];
            Assert.That(key.Time, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(key.Value, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.Linear));
        }

        [Test]
        public void InspectorApply_RedoRestoresAllFields()
        {
            SetupBlendKey("InspectorRedo", 0.1f, out string keyId);
            Undo.ClearAll();
            _keys.ApplyKeyEdits(keyId, 0.2f, 0.9f, Vector3.zero, InterpolationType.Smooth);
            Undo.PerformUndo();
            Undo.PerformRedo();

            var key = _session.GetSelectedTrack().BlendShape.Keys[0];
            Assert.That(key.Time, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(key.Value, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.Smooth));
        }

        [Test]
        public void InspectorBuffer_IsNonDestructiveUntilApply()
        {
            SetupBlendKey("InspectorRevert", 0.1f, out _);
            var key = _session.GetSelectedTrack().BlendShape.Keys[0];
            float bufferedTime = 0.7f;
            float bufferedValue = 0.2f;
            var bufferedInterpolation = InterpolationType.Hold;

            Assert.That(key.Time, Is.Not.EqualTo(bufferedTime));
            Assert.That(key.Value, Is.Not.EqualTo(bufferedValue));
            Assert.That(key.Interpolation, Is.Not.EqualTo(bufferedInterpolation));
        }

        [Test]
        public void InspectorApply_InvalidValueLeavesDataUnchanged()
        {
            SetupBlendKey("InspectorInvalid", 0.1f, out string keyId);
            Assert.That(_keys.ApplyKeyEdits(keyId, 0.2f, float.NaN, Vector3.zero, InterpolationType.Smooth), Is.False);
            var key = _session.GetSelectedTrack().BlendShape.Keys[0];
            Assert.That(key.Time, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(key.Value, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(key.Interpolation, Is.EqualTo(InterpolationType.Linear));
        }

        private void SetupAnimation(string name)
        {
            var project = CreateProject(name);
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            _animations.Add();
        }

        private void SetupBlendKey(string name, float time, out string keyId)
        {
            SetupAnimation(name);
            Assert.That(_tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile"), Is.True);
            _session.ViewState.SnapEnabled = false;
            keyId = _keys.AddKeyAt(time, 0.5f, Vector3.zero, InterpolationType.Linear);
            _session.ViewState.SnapEnabled = true;
        }

        private AvatarCandidateSnapshot.BlendShapeCandidate FindBlendCandidate(string rendererPath, string blendShapeName)
        {
            var candidates = _session.Candidates.FilterBlendShapes(blendShapeName);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].RendererPath == rendererPath && candidates[i].BlendShapeName == blendShapeName)
                {
                    return candidates[i];
                }
            }

            Assert.Fail("Expected blend shape candidate was not found.");
            return null;
        }

        private AvatarCandidateSnapshot.TransformCandidate FindTransformCandidate(string path)
        {
            var candidates = _session.Candidates.FilterTransforms(path);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].RelativePath == path)
                {
                    return candidates[i];
                }
            }

            Assert.Fail("Expected transform candidate was not found.");
            return null;
        }

        private void SetAvatar(AvatarFixture fixture)
        {
            var validation = VRCAvatarDescriptorAdapter.Validate(fixture.Descriptor);
            var cache = new UnityAvatarObjectCache();
            var report = cache.Rebuild(fixture.Root);
            _session.SetAvatar(fixture.Descriptor, fixture.Root, validation, report, cache, AvatarCandidateSnapshot.Build(report.Index));
        }

        private FaceMotionProject CreateProject(string fileName)
        {
            var project = FaceMotionProject.CreateNew();
            AssetDatabase.CreateAsset(project, _temp.AssetPath(fileName));
            AssetDatabase.SaveAssets();
            return project;
        }
    }
}
