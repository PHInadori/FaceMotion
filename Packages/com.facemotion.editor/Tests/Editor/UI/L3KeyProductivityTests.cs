using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// L3 key productivity specification matrix: copy, paste (insert + in-place update),
    /// duplicate, nudge, native shortcut registration and focus gating, and clipboard
    /// lifetime. Every rejection case asserts zero project mutation.
    /// </summary>
    public sealed class L3KeyProductivityTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TempFaceMotionAsset _temp;
        private FaceMotionProject _project;

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
            _temp = null;
            Undo.ClearAll();
        }

        // ----- COPY ----------------------------------------------------------------------

        [Test]
        public void Copy_SingleBlendShapeKey_PreservesValueInterpolationAndTrack()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.3f, 42f, Vector3.zero, InterpolationType.EaseInOut);
            _session.Selection.SetSingle(keyId);

            _keys.CopySelection();

            Assert.That(_session.Clipboard.Count, Is.EqualTo(1));
            TimelineClipboard.ClipboardItem item = _session.Clipboard.Items[0];
            Assert.That(item.TrackId, Is.EqualTo(trackId));
            Assert.That(item.Kind, Is.EqualTo(TrackKind.BlendShape));
            Assert.That(item.RelativeTime, Is.Zero);
            Assert.That(item.FloatValue, Is.EqualTo(42f));
            Assert.That(item.VectorValue, Is.EqualTo(Vector3.zero));
            Assert.That(item.Interpolation, Is.EqualTo(InterpolationType.EaseInOut));
            Assert.That(_session.Clipboard.SourceAnimationId, Is.EqualTo(_session.SelectedAnimationId));
        }

        [Test]
        public void Copy_MultipleKeys_PreserveRelativeSpacing()
        {
            string trackId = SetupBlendShapeTrack();
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.5f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });

            _keys.CopySelection();

            float[] relatives = _session.Clipboard.Items
                .Select(item => item.RelativeTime)
                .OrderBy(time => time)
                .ToArray();

            Assert.That(relatives, Is.EqualTo(new[] { 0f, 0.3f }).Within(1e-4f));
        }

        [Test]
        public void Copy_MultiTrackSelection_PreservesTrackIdsKindAndValues()
        {
            SetupTimeline();
            string blendTrackId = AddBlendShapeTrack();
            string transformTrackId = AddTransformTrack(TrackKind.TransformPosition);
            string blendKey = AddKeyToTrack(blendTrackId, 0.3f, 60f, Vector3.zero, InterpolationType.Linear);
            string transformKey = AddKeyToTrack(transformTrackId, 0.4f, 0f, new Vector3(1f, 2f, 3f), InterpolationType.EaseIn);
            _session.Selection.SetSelection(new[] { blendKey, transformKey });

            _keys.CopySelection();

            Assert.That(_session.Clipboard.Count, Is.EqualTo(2));

            TimelineClipboard.ClipboardItem fromBlend =
                _session.Clipboard.Items.Single(item => item.TrackId == blendTrackId);
            Assert.That(fromBlend.Kind, Is.EqualTo(TrackKind.BlendShape));
            Assert.That(fromBlend.FloatValue, Is.EqualTo(60f));

            TimelineClipboard.ClipboardItem fromTransform =
                _session.Clipboard.Items.Single(item => item.TrackId == transformTrackId);
            Assert.That(fromTransform.Kind, Is.EqualTo(TrackKind.TransformPosition));
            Assert.That(fromTransform.VectorValue, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(fromTransform.Interpolation, Is.EqualTo(InterpolationType.EaseIn));

            float[] relatives = _session.Clipboard.Items
                .Select(item => item.RelativeTime)
                .OrderBy(time => time)
                .ToArray();
            Assert.That(relatives, Is.EqualTo(new[] { 0f, 0.1f }).Within(1e-4f));
        }

        [Test]
        public void Copy_DoesNotMutateProjectAndCreatesNoUndoEntry()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.3f, 42f, Vector3.zero, InterpolationType.Linear);
            string stateBefore = State();
            string jsonBefore = EditorJsonUtility.ToJson(_project);

            Undo.ClearAll();
            int groupBefore = Undo.GetCurrentGroup();
            _session.Selection.SetSingle(keyId);
            _keys.CopySelection();

            Assert.That(_session.Clipboard.Count, Is.EqualTo(1), "Copy must fill the clipboard.");
            Assert.That(State(), Is.EqualTo(stateBefore), "Copy must not touch the project.");
            Assert.That(EditorJsonUtility.ToJson(_project), Is.EqualTo(jsonBefore), "Copy must not persist into the project or schema.");
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(groupBefore), "Copy must not open an Undo group.");
        }

        [Test]
        public void Copy_WithoutSelection_IsSafeNoOpAndKeepsPreviousClipboard()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.3f, 42f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);
            _keys.CopySelection();
            string stateBefore = State();

            _session.Selection.Clear();
            _keys.CopySelection();

            Assert.That(_session.Clipboard.Count, Is.EqualTo(1));
            Assert.That(_session.Clipboard.Items[0].FloatValue, Is.EqualTo(42f));
            Assert.That(State(), Is.EqualTo(stateBefore));
        }

        // ----- PASTE ---------------------------------------------------------------------

        [Test]
        public void Paste_AnchorsCopiedGroupAtPlayheadKeepingSpacing()
        {
            string trackId = SetupBlendShapeTrack(duration: 2f);
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.3f, 20f, Vector3.zero, InterpolationType.Linear);
            string third = AddKeyToTrack(trackId, 0.5f, 30f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second, third });
            _keys.CopySelection();

            _session.SetCurrentTime(1f);
            var result = _keys.PasteAt(1f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Pasted, Is.EqualTo(3));
            Assert.That(result.Updated, Is.Zero);
            Assert.That(
                FloatKeys(trackId).Select(key => key.Time).OrderBy(time => time).ToArray(),
                Is.EqualTo(new[] { 0.2f, 0.3f, 0.5f, 1f, 1.1f, 1.3f }).Within(1e-4f));
        }

        [Test]
        public void Paste_InsertsFreshIdsAndSelectsEveryResultingKey()
        {
            string trackId = SetupBlendShapeTrack(duration: 2f);
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.5f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });
            _keys.CopySelection();

            var result = _keys.PasteAt(1f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ResultKeyIds, Has.Count.EqualTo(2));
            Assert.That(result.ResultKeyIds, Does.Not.Contain(first));
            Assert.That(result.ResultKeyIds, Does.Not.Contain(second));
            Assert.That(result.ResultKeyIds.Distinct().Count(), Is.EqualTo(2));
            foreach (string id in result.ResultKeyIds)
            {
                Assert.That(FloatKeys(trackId).Select(key => key.KeyId), Does.Contain(id));
            }

            Assert.That(_session.Selection.Count, Is.EqualTo(2));
            foreach (string id in result.ResultKeyIds)
            {
                Assert.That(_session.Selection.Contains(id), Is.True);
            }
        }

        [Test]
        public void Paste_MultiTrack_PastesIntoEachSourceTrackWithItsOwnKind()
        {
            SetupTimeline(duration: 2f);
            string blendTrackId = AddBlendShapeTrack();
            string transformTrackId = AddTransformTrack(TrackKind.TransformPosition);
            string blendKey = AddKeyToTrack(blendTrackId, 0.3f, 60f, Vector3.zero, InterpolationType.Linear);
            string transformKey = AddKeyToTrack(transformTrackId, 0.4f, 0f, new Vector3(4f, 5f, 6f), InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { blendKey, transformKey });
            _keys.CopySelection();

            var result = _keys.PasteAt(1f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ResultKeyIds, Has.Count.EqualTo(2));
            Assert.That(FloatKeys(blendTrackId).Count, Is.EqualTo(2));
            Assert.That(VectorKeys(transformTrackId).Count, Is.EqualTo(2));
            Assert.That(FloatKeys(blendTrackId).Select(key => key.Value), Does.Contain(60f));
            Assert.That(VectorKeys(transformTrackId).Select(key => key.Value), Does.Contain(new Vector3(4f, 5f, 6f)));

            string blendedId = result.ResultKeyIds.Single(id => FloatKeys(blendTrackId).Any(key => key.KeyId == id));
            string transformId = result.ResultKeyIds.Single(id => VectorKeys(transformTrackId).Any(key => key.KeyId == id));
            Assert.That(blendedId, Is.Not.Null);
            Assert.That(transformId, Is.Not.Null);
        }

        [TestCase(TrackKind.TransformPosition)]
        [TestCase(TrackKind.TransformRotation)]
        [TestCase(TrackKind.TransformScale)]
        public void Paste_TransformKind_PreservesVectorValueAndInterpolation(TrackKind kind)
        {
            SetupTimeline(duration: 2f);
            string trackId = AddTransformTrack(kind);
            string keyId = AddKeyToTrack(trackId, 0.4f, 0f, new Vector3(1f, 2f, 3f), InterpolationType.EaseOut);
            _session.Selection.SetSingle(keyId);
            _keys.CopySelection();

            var result = _keys.PasteAt(1f);

            Assert.That(result.Succeeded, Is.True);
            var keys = VectorKeys(trackId);
            Assert.That(keys, Has.Count.EqualTo(2));
            Assert.That(keys.Select(key => key.Value), Has.All.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(keys.Select(key => key.Interpolation), Has.All.EqualTo(InterpolationType.EaseOut));
            Assert.That(keys.Select(key => key.KeyId).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void Paste_ExistingDestinationKey_IsUpdatedInPlaceWithoutDuplicateTimestamp()
        {
            string trackId = SetupBlendShapeTrack();
            string sourceId = AddKeyToTrack(trackId, 0.2f, 42f, Vector3.zero, InterpolationType.EaseInOut);
            string destinationId = AddKeyToTrack(trackId, 0.5f, 99f, Vector3.zero, InterpolationType.Hold);
            _session.Selection.SetSingle(sourceId);
            _keys.CopySelection();

            var result = _keys.PasteAt(0.5f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Pasted, Is.Zero);
            Assert.That(result.Updated, Is.EqualTo(1));

            var keys = FloatKeys(trackId);
            Assert.That(keys, Has.Count.EqualTo(2), "An occupied destination must be updated, never duplicated.");
            FloatKeyframeData destination = keys.Single(key => key.Time > 0.4f && key.Time < 0.6f);
            Assert.That(destination.KeyId, Is.EqualTo(destinationId));
            Assert.That(destination.Value, Is.EqualTo(42f));
            Assert.That(destination.Interpolation, Is.EqualTo(InterpolationType.EaseInOut));
            Assert.That(keys.Select(key => key.Time).Distinct().Count(), Is.EqualTo(2));
            Assert.That(result.ResultKeyIds, Is.EqualTo(new[] { destinationId }));
            Assert.That(_session.Selection.Contains(destinationId), Is.True);
            Assert.That(_session.Selection.Count, Is.EqualTo(1));
        }

        [Test]
        public void Paste_MixedInsertAndUpdate_ReportsBothAndKeepsTimestampsUnique()
        {
            string trackId = SetupBlendShapeTrack();
            string firstSource = AddKeyToTrack(trackId, 0.1f, 10f, Vector3.zero, InterpolationType.Linear);
            string secondSource = AddKeyToTrack(trackId, 0.4f, 20f, Vector3.zero, InterpolationType.Linear);
            string occupiedId = AddKeyToTrack(trackId, 0.5f, 77f, Vector3.zero, InterpolationType.Hold);
            _session.Selection.SetSelection(new[] { firstSource, secondSource });
            _keys.CopySelection();

            var result = _keys.PasteAt(0.5f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Pasted, Is.EqualTo(1));
            Assert.That(result.Updated, Is.EqualTo(1));

            var keys = FloatKeys(trackId);
            Assert.That(keys, Has.Count.EqualTo(4), "One insert plus one in-place update on top of the three source keys.");
            Assert.That(keys.Select(key => key.Time).Distinct().Count(), Is.EqualTo(4));
            Assert.That(keys.Single(key => key.Time > 0.4f && key.Time < 0.6f).Value, Is.EqualTo(10f));
            Assert.That(keys.Single(key => key.Time > 0.7f && key.Time < 0.9f).Value, Is.EqualTo(20f));
            Assert.That(keys.Single(key => key.KeyId == occupiedId).Value, Is.EqualTo(10f));
            Assert.That(_session.Selection.Count, Is.EqualTo(2));
            Assert.That(result.ResultKeyIds, Does.Contain(occupiedId));
        }

        [Test]
        public void Paste_GroupShiftsAsOneUnitAtTheDurationBoundary()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f);
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.5f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });
            _keys.CopySelection();

            var result = _keys.PasteAt(0.8f);

            Assert.That(result.Succeeded, Is.True);
            float[] times = FloatKeys(trackId).Select(key => key.Time).OrderBy(time => time).ToArray();
            Assert.That(times, Is.EqualTo(new[] { 0.2f, 0.5f, 0.7f, 1f }).Within(1e-4f));
            Assert.That(times[3] - times[2], Is.EqualTo(0.3f).Within(1e-4f), "Group spacing must survive the boundary shift.");
            Assert.That(_session.Selection.Count, Is.EqualTo(2));
        }

        [Test]
        public void Paste_CopiedSpanLargerThanDuration_RejectsWithoutMutation()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f);
            string first = AddKeyToTrack(trackId, 0.1f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.6f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });
            _keys.CopySelection();

            _session.GetSelectedAnimation().Timeline.Duration = 0.4f;
            string stateBefore = State();

            var result = _keys.PasteAt(0f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ResultKeyIds, Is.Null);
            Assert.That(State(), Is.EqualTo(stateBefore));
            Assert.That(_session.Clipboard.Count, Is.EqualTo(2));
        }

        [Test]
        public void Paste_MissingTrack_RejectsWithoutMutation()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);
            _keys.CopySelection();

            _tracks.RemoveTrack(trackId);
            string stateBefore = State();

            var result = _keys.PasteAt(0.5f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(State(), Is.EqualTo(stateBefore));
            Assert.That(_session.Clipboard.Count, Is.EqualTo(1));
        }

        [Test]
        public void Paste_MismatchedTrackKind_RejectsWithoutMutation()
        {
            SetupTimeline();
            string blendTrackId = AddBlendShapeTrack();
            string transformTrackId = AddTransformTrack(TrackKind.TransformPosition);
            AddKeyToTrack(transformTrackId, 0.4f, 0f, new Vector3(1f, 1f, 1f), InterpolationType.Linear);
            string stateBefore = State();

            // Clipboard claims a blend shape destination while the target track is a transform track.
            _session.Clipboard.Set(
                _session.SelectedAnimationId,
                new[]
                {
                    new TimelineClipboard.ClipboardItem
                    {
                        TrackId = transformTrackId,
                        Kind = TrackKind.BlendShape,
                        RelativeTime = 0f,
                        FloatValue = 50f,
                        Interpolation = InterpolationType.Linear
                    }
                });

            var result = _keys.PasteAt(0.5f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(State(), Is.EqualTo(stateBefore));
        }

        [Test]
        public void Paste_TwoClipboardItemsOnOneDestination_RejectsAmbiguousPlanWithoutMutation()
        {
            string trackId = SetupBlendShapeTrack();
            AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string stateBefore = State();

            // Two items that snap onto one track and one timestamp: no unique destination.
            _session.Clipboard.Set(
                _session.SelectedAnimationId,
                new[]
                {
                    new TimelineClipboard.ClipboardItem
                    {
                        TrackId = trackId,
                        Kind = TrackKind.BlendShape,
                        RelativeTime = 0.2f,
                        FloatValue = 10f,
                        Interpolation = InterpolationType.Linear
                    },
                    new TimelineClipboard.ClipboardItem
                    {
                        TrackId = trackId,
                        Kind = TrackKind.BlendShape,
                        RelativeTime = 0.20001f,
                        FloatValue = 20f,
                        Interpolation = InterpolationType.Hold
                    }
                });

            var result = _keys.PasteAt(0.5f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(State(), Is.EqualTo(stateBefore));
        }

        [Test]
        public void Paste_AfterSwitchingAnimation_RejectsWithoutMutation()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string sourceAnimationId = _session.SelectedAnimationId;
            _session.Selection.SetSingle(keyId);
            _keys.CopySelection();

            _animations.Add();
            string secondTrackId = AddBlendShapeTrack();
            string stateBefore = State();

            var result = _keys.PasteAt(0.5f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(State(), Is.EqualTo(stateBefore));
            Assert.That(FloatKeys(secondTrackId), Is.Empty);
            Assert.That(_session.Clipboard.SourceAnimationId, Is.EqualTo(sourceAnimationId), "The clipboard keeps its source animation identity.");
        }

        [Test]
        public void Paste_UndoRestoresExactPrePasteStateAndRedoReappliesIt()
        {
            string trackId = SetupBlendShapeTrack();
            string firstSource = AddKeyToTrack(trackId, 0.1f, 42f, Vector3.zero, InterpolationType.EaseInOut);
            string secondSource = AddKeyToTrack(trackId, 0.4f, 20f, Vector3.zero, InterpolationType.Linear);
            string occupiedId = AddKeyToTrack(trackId, 0.5f, 77f, Vector3.zero, InterpolationType.Hold);
            _session.Selection.SetSelection(new[] { firstSource, secondSource });
            _keys.CopySelection();

            string before = State();
            var result = _keys.PasteAt(0.5f);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Pasted, Is.EqualTo(1));
            Assert.That(result.Updated, Is.EqualTo(1));
            string during = State();
            Assert.That(during, Is.Not.EqualTo(before));

            Undo.PerformUndo();
            _session.RefreshAfterUndo();
            Assert.That(State(), Is.EqualTo(before), "Undo must remove inserts and restore overwritten values.");
            Assert.That(_session.Selection.Contains(occupiedId), Is.True, "Preserved destination keys stay selected.");
            Assert.That(_session.Selection.Contains(result.ResultKeyIds.First(id => id != occupiedId)), Is.False,
                "Inserted keys that no longer exist are pruned by the existing selection architecture.");

            Undo.PerformRedo();
            _session.RefreshAfterUndo();
            Assert.That(State(), Is.EqualTo(during), "Redo must reapply the whole paste as one step.");
        }

        // ----- DUPLICATE -----------------------------------------------------------------

        [Test]
        public void Duplicate_SingleKey_PlacesCopyOneFrameLaterWithFreshId()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 60f);
            string sourceId = AddKeyToTrack(trackId, 0.5f, 40f, Vector3.zero, InterpolationType.EaseInOut);
            _session.Selection.SetSingle(sourceId);

            Assert.That(_keys.DuplicateSelection(), Is.True);

            var keys = FloatKeys(trackId);
            Assert.That(keys, Has.Count.EqualTo(2));
            float[] times = keys.Select(key => key.Time).OrderBy(time => time).ToArray();
            Assert.That(times, Is.EqualTo(new[] { 0.5f, 0.5f + 1f / 60f }).Within(1e-4f));
            FloatKeyframeData copy = keys.Single(key => key.KeyId != sourceId);
            Assert.That(copy.Value, Is.EqualTo(40f));
            Assert.That(copy.Interpolation, Is.EqualTo(InterpolationType.EaseInOut));
            Assert.That(_session.Selection.Count, Is.EqualTo(1));
            Assert.That(_session.Selection.Contains(copy.KeyId), Is.True);
        }

        [Test]
        public void Duplicate_MultipleKeys_PreserveRelativeSpacing()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 60f);
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.5f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });

            Assert.That(_keys.DuplicateSelection(), Is.True);

            float[] times = FloatKeys(trackId).Select(key => key.Time).OrderBy(time => time).ToArray();
            Assert.That(
                times,
                Is.EqualTo(new[] { 0.2f, 0.2f + 1f / 60f, 0.5f, 0.5f + 1f / 60f }).Within(1e-4f));
            Assert.That(times[2] - times[0], Is.EqualTo(times[3] - times[1]).Within(1e-4f));
        }

        [Test]
        public void Duplicate_MultiTrack_KeepsTrackIdsAndTransformValue()
        {
            SetupTimeline(frameRate: 60f);
            string blendTrackId = AddBlendShapeTrack();
            string transformTrackId = AddTransformTrack(TrackKind.TransformRotation);
            string blendKey = AddKeyToTrack(blendTrackId, 0.3f, 65f, Vector3.zero, InterpolationType.Linear);
            string transformKey = AddKeyToTrack(transformTrackId, 0.3f, 0f, new Vector3(10f, 20f, 30f), InterpolationType.EaseIn);
            _session.Selection.SetSelection(new[] { blendKey, transformKey });

            Assert.That(_keys.DuplicateSelection(), Is.True);

            Assert.That(FloatKeys(blendTrackId), Has.Count.EqualTo(2));
            Assert.That(VectorKeys(transformTrackId), Has.Count.EqualTo(2));
            Assert.That(FloatKeys(blendTrackId).Select(key => key.Value), Does.Contain(65f));
            Assert.That(VectorKeys(transformTrackId).Select(key => key.Value), Does.Contain(new Vector3(10f, 20f, 30f)));
            Assert.That(
                FloatKeys(blendTrackId).Select(key => key.Time).OrderBy(time => time).ToArray(),
                Is.EqualTo(new[] { 0.3f, 0.3f + 1f / 60f }).Within(1e-4f));
        }

        [Test]
        public void Duplicate_NearDuration_ConstrainsTheWholeGroupToTheEndpoint()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f, frameRate: 60f);
            _session.ViewState.SnapEnabled = false;
            string sourceId = AddKeyToTrack(trackId, 0.99f, 25f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(sourceId);

            Assert.That(_keys.DuplicateSelection(), Is.True);

            var keys = FloatKeys(trackId);
            Assert.That(keys, Has.Count.EqualTo(2));
            Assert.That(keys.Single(key => key.KeyId == sourceId).Time, Is.EqualTo(0.99f).Within(1e-4f));
            Assert.That(keys.Single(key => key.KeyId != sourceId).Time, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Duplicate_AtEndpoint_RejectsWithoutMutation()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f, frameRate: 60f);
            string sourceId = AddKeyToTrack(trackId, 1f, 25f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(sourceId);
            string stateBefore = State();

            Assert.That(_keys.DuplicateSelection(), Is.False);
            Assert.That(State(), Is.EqualTo(stateBefore));
        }

        [Test]
        public void Duplicate_WithOccupiedDestination_RejectsTheWholeGroupWithoutMutation()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f, frameRate: 60f);
            string first = AddKeyToTrack(trackId, 0.5f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 31f / 60f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });
            string stateBefore = State();

            Assert.That(_keys.DuplicateSelection(), Is.False, "A duplicate never overwrites an unrelated key.");
            Assert.That(State(), Is.EqualTo(stateBefore));
        }

        [Test]
        public void Duplicate_Undo_RemovesTheDuplicatedKeys()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 60f);
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.5f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });
            string before = State();

            Assert.That(_keys.DuplicateSelection(), Is.True);
            Assert.That(FloatKeys(trackId), Has.Count.EqualTo(4));

            Undo.PerformUndo();
            _session.RefreshAfterUndo();
            Assert.That(State(), Is.EqualTo(before));
        }

        // ----- NUDGE ---------------------------------------------------------------------

        [Test]
        public void Nudge_60FramesPerSecond_MovesByOneAndFiveFrames()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 60f);
            string keyId = AddKeyToTrack(trackId, 0.5f, 10f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);

            Assert.That(_keys.NudgeSelectedKeys(1), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f + 1f / 60f).Within(1e-4f));

            Assert.That(_keys.NudgeSelectedKeys(5), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f + 6f / 60f).Within(1e-4f));

            Assert.That(_keys.NudgeSelectedKeys(-1), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f + 5f / 60f).Within(1e-4f));

            Assert.That(_keys.NudgeSelectedKeys(-5), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Nudge_30FramesPerSecond_MovesByOneAndFiveFrames()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 30f);
            string keyId = AddKeyToTrack(trackId, 0.5f, 10f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);

            Assert.That(_keys.NudgeSelectedKeys(1), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f + 1f / 30f).Within(1e-4f));

            Assert.That(_keys.NudgeSelectedKeys(5), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f + 6f / 30f).Within(1e-4f));

            Assert.That(_keys.NudgeSelectedKeys(-5), Is.True);
            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(0.5f + 1f / 30f).Within(1e-4f));
        }

        [Test]
        public void Nudge_MultiKeyMultiTrackSelection_MovesTogetherAndKeepsSpacing()
        {
            SetupTimeline(frameRate: 60f);
            string blendTrackId = AddBlendShapeTrack();
            string transformTrackId = AddTransformTrack(TrackKind.TransformPosition);
            string first = AddKeyToTrack(blendTrackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(blendTrackId, 0.4f, 20f, Vector3.zero, InterpolationType.Linear);
            string transformKey = AddKeyToTrack(transformTrackId, 0.3f, 0f, new Vector3(7f, 8f, 9f), InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second, transformKey });
            string stateBefore = State();

            Assert.That(_keys.NudgeSelectedKeys(5), Is.True);

            float[] blendTimes = FloatKeys(blendTrackId).Select(key => key.Time).OrderBy(time => time).ToArray();
            Assert.That(blendTimes, Is.EqualTo(new[] { 0.2f + 5f / 60f, 0.4f + 5f / 60f }).Within(1e-4f));
            Assert.That(blendTimes[1] - blendTimes[0], Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(VectorKeys(transformTrackId).Single().Time, Is.EqualTo(0.3f + 5f / 60f).Within(1e-4f));
            Assert.That(VectorKeys(transformTrackId).Single().Value, Is.EqualTo(new Vector3(7f, 8f, 9f)));

            Undo.PerformUndo();
            _session.RefreshAfterUndo();
            Assert.That(State(), Is.EqualTo(stateBefore), "One nudge is one Undo step.");
        }

        [Test]
        public void Nudge_LeftBoundary_ConstrainsTheWholeGroupToZero()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f, frameRate: 60f);
            string first = AddKeyToTrack(trackId, 1f / 60f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 7f / 60f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });

            Assert.That(_keys.NudgeSelectedKeys(-5), Is.True);

            float[] times = FloatKeys(trackId).Select(key => key.Time).OrderBy(time => time).ToArray();
            Assert.That(times, Is.EqualTo(new[] { 0f, 6f / 60f }).Within(1e-4f));
        }

        [Test]
        public void Nudge_RightBoundary_ConstrainsTheWholeGroupToDuration()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f, frameRate: 60f);
            string keyId = AddKeyToTrack(trackId, 59f / 60f, 10f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);

            Assert.That(_keys.NudgeSelectedKeys(5), Is.True);

            Assert.That(FloatKeys(trackId).Single().Time, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Nudge_CollisionResolution_MatchesTheSharedKeyMovePlanner()
        {
            string trackId = SetupBlendShapeTrack(duration: 1f, frameRate: 60f);
            string movingId = AddKeyToTrack(trackId, 0.5f, 10f, Vector3.zero, InterpolationType.Linear);
            string obstacleId = AddKeyToTrack(trackId, 31f / 60f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(movingId);

            float expected = KeyMovePlanner.PlanTrack(
                new[] { 0.5f },
                new[] { 31f / 60f },
                1f / 60f,
                1f,
                60f,
                true)[0];

            Assert.That(_keys.NudgeSelectedKeys(1), Is.True);

            FloatKeyframeData moved = FloatKeys(trackId).Single(key => key.KeyId == movingId);
            Assert.That(moved.Time, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(moved.Time, Is.EqualTo(32f / 60f).Within(1e-4f));
            Assert.That(FloatKeys(trackId).Single(key => key.KeyId == obstacleId).Time, Is.EqualTo(31f / 60f).Within(1e-4f));
            Assert.That(FloatKeys(trackId), Has.Count.EqualTo(2), "Nudging must never create or drop keys.");
        }

        [Test]
        public void Nudge_Undo_RestoresExactOriginalTimes()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 60f);
            string first = AddKeyToTrack(trackId, 0.2f, 10f, Vector3.zero, InterpolationType.Linear);
            string second = AddKeyToTrack(trackId, 0.4f, 20f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSelection(new[] { first, second });
            string before = State();

            Assert.That(_keys.NudgeSelectedKeys(5), Is.True);
            Assert.That(State(), Is.Not.EqualTo(before));

            Undo.PerformUndo();
            _session.RefreshAfterUndo();
            Assert.That(State(), Is.EqualTo(before));
        }

        [Test]
        public void Nudge_WithoutSelection_IsASafeNoOp()
        {
            string trackId = SetupBlendShapeTrack(frameRate: 60f);
            AddKeyToTrack(trackId, 0.5f, 10f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.Clear();
            string stateBefore = State();

            Assert.That(_keys.NudgeSelectedKeys(1), Is.False);
            Assert.That(State(), Is.EqualTo(stateBefore));
        }

        // ----- SHORTCUT / INPUT ----------------------------------------------------------

        [TestCase("InvokeCopyKeysShortcut")]
        [TestCase("InvokePasteKeysShortcut")]
        [TestCase("InvokeDuplicateKeysShortcut")]
        [TestCase("InvokeNudgeLeftShortcut")]
        [TestCase("InvokeNudgeRightShortcut")]
        [TestCase("InvokeNudgeLeftFiveShortcut")]
        [TestCase("InvokeNudgeRightFiveShortcut")]
        public void Shortcut_EveryKeyProductivityActionIsRegisteredOnce(string methodName)
        {
            MethodInfo method = typeof(FaceMotionWindow).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, methodName);
            ShortcutAttribute[] attributes = (ShortcutAttribute[])method
                .GetCustomAttributes(typeof(ShortcutAttribute), false);
            Assert.That(attributes, Has.Length.EqualTo(1), methodName);

            List<string> ids = FlattenAttributeState(attributes[0])
                .Select(entry => entry.Value)
                .OfType<string>()
                .Where(value => value.IndexOf("FaceMotion/", StringComparison.Ordinal) >= 0)
                .ToList();

            Assert.That(ids, Is.Not.Empty, $"{methodName} must declare a FaceMotion/... shortcut id.");
            Assert.That(ids, Has.All.Contain("FaceMotion/Timeline"), methodName);
        }

        [Test]
        public void Shortcut_NoRegistrationShipsADefaultKeyBinding()
        {
            string[] methodNames =
            {
                "InvokeCopyKeysShortcut",
                "InvokePasteKeysShortcut",
                "InvokeDuplicateKeysShortcut",
                "InvokeNudgeLeftShortcut",
                "InvokeNudgeRightShortcut",
                "InvokeNudgeLeftFiveShortcut",
                "InvokeNudgeRightFiveShortcut"
            };

            foreach (string methodName in methodNames)
            {
                MethodInfo method = typeof(FaceMotionWindow).GetMethod(
                    methodName,
                    BindingFlags.Static | BindingFlags.NonPublic);
                var attribute = (ShortcutAttribute)method.GetCustomAttributes(typeof(ShortcutAttribute), false)[0];

                List<(string Path, object Value)> state = FlattenAttributeState(attribute);

                foreach ((string path, object value) in state)
                {
                    if (value is KeyCode keyCode)
                    {
                        Assert.That(
                            keyCode,
                            Is.EqualTo(KeyCode.None),
                            $"{methodName}{path} must not ship a default key.");
                    }
                    else if (value != null &&
                             value.GetType().IsEnum &&
                             string.Equals(value.GetType().Name, "ShortcutModifiers", StringComparison.Ordinal))
                    {
                        Assert.That(
                            Convert.ToInt32(value),
                            Is.Zero,
                            $"{methodName}{path} must not ship default modifiers.");
                    }
                }
            }
        }

        /// <summary>
        /// Every instance field on the attribute (and one level into its members), named by path.
        /// Unity stores the shortcut id and any default binding in private attribute state, so the
        /// test inspects it structurally instead of relying on a particular public API surface.
        /// </summary>
        private static List<(string Path, object Value)> FlattenAttributeState(object root, int maxDepth = 2)
        {
            var results = new List<(string Path, object Value)>();

            void Walk(object instance, string path, int depth)
            {
                if (instance == null || depth < 0)
                {
                    return;
                }

                Type type = instance.GetType();
                if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(Delegate).IsAssignableFrom(type))
                {
                    return;
                }

                for (Type current = type; current != null; current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(
                                 BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic |
                                 BindingFlags.DeclaredOnly))
                    {
                        object value;
                        try
                        {
                            value = field.GetValue(instance);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        string fieldPath = path + "." + field.Name;
                        results.Add((fieldPath, value));

                        if (value != null &&
                            depth > 0 &&
                            !field.FieldType.IsPrimitive &&
                            !field.FieldType.IsEnum &&
                            field.FieldType != typeof(string) &&
                            !field.FieldType.IsArray)
                        {
                            Walk(value, fieldPath, depth - 1);
                        }
                    }
                }
            }

            Walk(root, root.GetType().Name, maxDepth);
            return results;
        }

        [Test]
        public void Shortcut_TextAndNumericEditing_BlocksEveryKeyAction()
        {
            // Unnamed IMGUI text fields (track search, browser search) own the keyboard.
            Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(true, null), Is.False);
            Assert.That(FaceMotionWindow.CanHandleQuickKeyShortcut(true, string.Empty), Is.False);

            // Timeline settings: duration and frame rate editing.
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, AnimationListPanel.EditableControlPrefix + "duration"),
                Is.False);
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, AnimationListPanel.EditableControlPrefix + "frameRate"),
                Is.False);
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, AnimationListPanel.EditableControlPrefix + "rename"),
                Is.False);

            // Key inspector: time, value and vector components.
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, KeyframeInspectorPanel.EditableControlPrefix + "time"),
                Is.False);
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, KeyframeInspectorPanel.EditableControlPrefix + "blendShape"),
                Is.False);
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, KeyframeInspectorPanel.EditableControlPrefix + "x"),
                Is.False);

            // Quick Key value editing.
            Assert.That(
                FaceMotionWindow.CanHandleQuickKeyShortcut(false, KeyframeInspectorPanel.EditableControlPrefix + "quickKeyValue"),
                Is.False);
        }

        [Test]
        public void Shortcut_ArrowKeyStyleNudge_IsBlockedWhileNumericOrTextEditingIsLive()
        {
            bool previousEditing = EditorGUIUtility.editingTextField;
            int previousKeyboardControl = GUIUtility.keyboardControl;
            try
            {
                EditorGUIUtility.editingTextField = true;
                GUIUtility.keyboardControl = 0;
                Assert.That(
                    FaceMotionWindow.CanHandleQuickKeyShortcut(
                        EditorGUIUtility.editingTextField,
                        GUI.GetNameOfFocusedControl()),
                    Is.False,
                    "A numeric/text field keeps arrow keys for itself.");

                EditorGUIUtility.editingTextField = false;
                GUIUtility.keyboardControl = 0;
                Assert.That(
                    FaceMotionWindow.CanHandleQuickKeyShortcut(
                        EditorGUIUtility.editingTextField,
                        GUI.GetNameOfFocusedControl()),
                    Is.True,
                    "Released focus re-enables the key actions.");
            }
            finally
            {
                EditorGUIUtility.editingTextField = previousEditing;
                GUIUtility.keyboardControl = previousKeyboardControl;
            }
        }

        [Test]
        public void Shortcut_BackgroundFocusRelease_RestoresKeyActionAvailability()
        {
            int previousKeyboardControl = GUIUtility.keyboardControl;
            bool previousEditing = EditorGUIUtility.editingTextField;
            try
            {
                GUIUtility.keyboardControl = 4712;
                EditorGUIUtility.editingTextField = true;

                Assert.That(
                    FaceMotionWindow.ReleaseTextFocusOnBackgroundMouseDown(EventType.MouseDown, 0, 0, true),
                    Is.True);
                Assert.That(EditorGUIUtility.editingTextField, Is.False);
                Assert.That(
                    FaceMotionWindow.CanHandleQuickKeyShortcut(EditorGUIUtility.editingTextField, string.Empty),
                    Is.True);
            }
            finally
            {
                GUIUtility.keyboardControl = previousKeyboardControl;
                EditorGUIUtility.editingTextField = previousEditing;
            }
        }

        [Test]
        public void Shortcut_TimelineKeyboardRoute_IsBlockedWhileAControlOwnsTheKeyboard()
        {
            Assert.That(TimelineInputHandler.CanHandleKeyboardShortcut(false), Is.True);
            Assert.That(TimelineInputHandler.CanHandleKeyboardShortcut(true), Is.False);
            Assert.That(TimelineInputHandler.CanHandleKeyboardShortcut(false, true), Is.False);
            Assert.That(TimelineInputHandler.CanHandleKeyboardShortcut(true, true), Is.False);
        }

        // ----- CLIPBOARD -----------------------------------------------------------------

        [Test]
        public void Clipboard_LivesOnlyInMemoryAndSurvivesAcrossTheSessionNotTheProject()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.3f, 42f, Vector3.zero, InterpolationType.Linear);
            _session.Selection.SetSingle(keyId);
            string jsonBefore = EditorJsonUtility.ToJson(_project);
            _keys.CopySelection();

            Assert.That(_session.Clipboard.HasItems, Is.True);
            Assert.That(EditorJsonUtility.ToJson(_project), Is.EqualTo(jsonBefore), "Clipboard content never lands in the project/schema.");
            Assert.That(typeof(TimelineClipboard).IsSerializable, Is.False, "The clipboard is not a serialized type.");

            var recreatedSession = new FaceMotionEditorSession();
            Assert.That(recreatedSession.Clipboard.HasItems, Is.False, "A new session starts with an empty clipboard.");
            Assert.That(_session.Clipboard.HasItems, Is.True, "The source session keeps its own in-memory clipboard.");
        }

        [Test]
        public void Clipboard_RetainsSourceAnimationIdentityUntilExplicitlyCleared()
        {
            string trackId = SetupBlendShapeTrack();
            string keyId = AddKeyToTrack(trackId, 0.3f, 42f, Vector3.zero, InterpolationType.Linear);
            string sourceAnimationId = _session.SelectedAnimationId;
            _session.Selection.SetSingle(keyId);
            _keys.CopySelection();

            _animations.Add();
            Assert.That(_session.Clipboard.SourceAnimationId, Is.EqualTo(sourceAnimationId));

            _session.Clipboard.Clear();
            Assert.That(_session.Clipboard.HasItems, Is.False);
            Assert.That(_session.Clipboard.SourceAnimationId, Is.Null);
        }

        // ----- Helpers -------------------------------------------------------------------

        private void SetupTimeline(float duration = 1f, float frameRate = 60f)
        {
            _project = FaceMotionProject.CreateNew();
            EditorUtility.SetDirty(_project);
            AssetDatabase.CreateAsset(_project, _temp.AssetPath("L3KeyProductivity"));
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(_project, AssetDatabase.GetAssetPath(_project));
            _animations.Add();

            var timeline = _session.GetSelectedAnimation().Timeline;
            timeline.Duration = duration;
            timeline.FrameRate = frameRate;
        }

        private string SetupBlendShapeTrack(float duration = 1f, float frameRate = 60f)
        {
            SetupTimeline(duration, frameRate);
            return AddBlendShapeTrack();
        }

        private string AddBlendShapeTrack()
        {
            _tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile");
            return _session.SelectedTrackId;
        }

        private string AddTransformTrack(TrackKind kind)
        {
            _tracks.AddTransformTrack(kind, "Head");
            return _session.SelectedTrackId;
        }

        private string AddKeyToTrack(
            string trackId,
            float time,
            float floatValue,
            Vector3 vectorValue,
            InterpolationType interpolation)
        {
            _tracks.Select(trackId);
            string keyId = _keys.AddKeyAt(time, floatValue, vectorValue, interpolation);
            Assert.That(keyId, Is.Not.Null, "Test setup failed to author a key.");
            return keyId;
        }

        private FaceTrackData Track(string trackId)
        {
            bool found = _session.GetSelectedAnimation().Timeline.TryGetTrack(trackId, out FaceTrackData track);
            Assert.That(found, Is.True, $"Track {trackId} must exist.");
            return track;
        }

        private List<FloatKeyframeData> FloatKeys(string trackId)
        {
            return Track(trackId).BlendShape.Keys.ToList();
        }

        private List<Vector3KeyframeData> VectorKeys(string trackId)
        {
            return Track(trackId).Transform.Keys.ToList();
        }

        /// <summary>Exact, comparable snapshot of the selected animation's timeline.</summary>
        private string State()
        {
            FaceMotionAnimationData animation = _session.GetSelectedAnimation();
            var builder = new StringBuilder();
            builder.Append("animation=").Append(animation?.AnimationId).Append('\n');

            if (animation?.Timeline == null)
            {
                return builder.ToString();
            }

            builder.Append("duration=").Append(animation.Timeline.Duration.ToString("R")).Append('\n');
            builder.Append("frameRate=").Append(animation.Timeline.FrameRate.ToString("R")).Append('\n');

            foreach (FaceTrackData track in animation.Timeline.Tracks.OrderBy(item => item?.TrackId, StringComparer.Ordinal))
            {
                if (track == null)
                {
                    continue;
                }

                builder.Append(track.TrackId).Append(':').Append(track.Kind).Append('[');

                if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                {
                    foreach (FloatKeyframeData key in track.BlendShape.Keys
                                 .OrderBy(item => item.Time)
                                 .ThenBy(item => item.KeyId, StringComparer.Ordinal))
                    {
                        builder.Append(key.KeyId)
                            .Append('@').Append(key.Time.ToString("R"))
                            .Append('=').Append(key.Value.ToString("R"))
                            .Append(':').Append((int)key.Interpolation)
                            .Append(';');
                    }
                }
                else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                {
                    foreach (Vector3KeyframeData key in track.Transform.Keys
                                 .OrderBy(item => item.Time)
                                 .ThenBy(item => item.KeyId, StringComparer.Ordinal))
                    {
                        builder.Append(key.KeyId)
                            .Append('@').Append(key.Time.ToString("R"))
                            .Append('=').Append(key.Value.ToString("R"))
                            .Append(':').Append((int)key.Interpolation)
                            .Append(';');
                    }
                }

                builder.Append("]\n");
            }

            return builder.ToString();
        }
    }
}
