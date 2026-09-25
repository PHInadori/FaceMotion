using System.Collections.Generic;
using System.Reflection;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>UX-2 shared-playhead and preview lifetime regression coverage.</summary>
    public sealed class UX2SynchronizationTests
    {
        [Test]
        public void TimelineScrub_UpdatesTheSessionPlayheadAndEvaluatesPreview()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            var input = new TimelineInputHandler(session, keys, tracks);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            session.ViewState.SnapEnabled = false;
            keys.AddKeyAt(0.5f, 0.8f, Vector3.zero, InterpolationType.Linear);
            using (var fixture = AvatarFixture.Create())
            {
                var preview = new PreviewSession();
                try
                {
                    preview.EnsureAvatar(fixture.Root);
                    session.Changed += () => preview.Evaluate(session.GetSelectedAnimation(), session.ViewState.CurrentTime);

                    input.ScrubTo(0.6f);

                    Assert.That(session.ViewState.CurrentTime, Is.EqualTo(0.6f).Within(1e-4f));
                    var clone = (GameObject)typeof(PreviewSession)
                        .GetField("_clone", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(preview)
                        .GetType()
                        .GetProperty("Root", BindingFlags.Instance | BindingFlags.Public)
                        .GetValue(typeof(PreviewSession)
                            .GetField("_clone", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(preview), null);
                    var renderer = clone.transform.Find(AvatarFixture.FaceRendererPath).GetComponent<SkinnedMeshRenderer>();
                    Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0.8f).Within(1e-4f));
                }
                finally
                {
                    preview.Dispose();
                }
            }
        }

        [Test]
        public void InspectorSelectionChange_ReloadsAllBuffersWhileThePriorFieldIsFocused()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack("Face", "Smile");
            string first = keys.AddKeyAt(0.1f, 0.2f, Vector3.one, InterpolationType.Hold);
            string second = keys.AddKeyAt(0.7f, 0.9f, Vector3.zero, InterpolationType.Smooth);
            var inspector = new KeyframeInspectorPanel(session, keys);
            session.Selection.SetSingle(first);
            inspector.Synchronize();

            SetBuffer(inspector, "_time", 4f);
            SetBuffer(inspector, "_floatValue", 4f);
            SetBuffer(inspector, "_vectorValue", Vector3.one * 4f);
            SetBuffer(inspector, "_interpolationIndex", (int)InterpolationType.EaseIn);
            session.Selection.SetSingle(second);
            EditorGUIUtility.editingTextField = true;
            try
            {
                inspector.Synchronize();
            }
            finally
            {
                EditorGUIUtility.editingTextField = false;
            }

            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(GetBuffer<float>(inspector, "_floatValue"), Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(GetBuffer<Vector3>(inspector, "_vectorValue"), Is.EqualTo(Vector3.zero));
            Assert.That(GetBuffer<int>(inspector, "_interpolationIndex"), Is.EqualTo((int)InterpolationType.Smooth));
        }

        [Test]
        public void PreviewSession_EnsureAvatarKeepsTheExistingCloneForTheSameSource()
        {
            using (var fixture = AvatarFixture.Create())
            {
                var preview = new PreviewSession();
                try
                {
                    preview.EnsureAvatar(fixture.Root);
                    preview.Evaluate(FaceMotionAnimationData.Create("Preview"), 0f);
                    preview.EnsureAvatar(fixture.Root);
                    preview.Evaluate(FaceMotionAnimationData.Create("Preview"), 0.75f);

                    Assert.That(preview.CloneCreationCount, Is.EqualTo(1));
                }
                finally
                {
                    preview.Dispose();
                }
            }
        }

        [Test]
        public void InspectorSelectionChange_UpdatesTimeWhenOnlyTimeDiffers()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack("Face", "Smile");
            string first = keys.AddKeyAt(0.2f, 0.5f, Vector3.one, InterpolationType.Hold);
            string second = keys.AddKeyAt(0.8f, 0.5f, Vector3.zero, InterpolationType.Linear);
            var inspector = new KeyframeInspectorPanel(session, keys);

            session.Selection.SetSingle(first);
            inspector.Synchronize();
            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0.2f).Within(1e-4f));

            session.Selection.SetSingle(second);
            EditorGUIUtility.editingTextField = true;
            try
            {
                inspector.Synchronize();
            }
            finally
            {
                EditorGUIUtility.editingTextField = false;
            }

            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(GetBuffer<float>(inspector, "_floatValue"), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(GetBuffer<int>(inspector, "_interpolationIndex"), Is.EqualTo((int)InterpolationType.Linear));
        }

        [Test]
        public void InspectorSelection_ResolvesBlendShapeBuffersFromTheSelectedKeyTrack()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack("Face", "Smile");
            string first = keys.AddKeyAt(0f, 0f, Vector3.zero, InterpolationType.Hold);
            string second = keys.AddKeyAt(0.5f, 100f, Vector3.zero, InterpolationType.EaseIn);
            string blendShapeTrackId = session.SelectedTrackId;
            tracks.AddTransformTrack(TrackKind.TransformPosition, "Head");
            var inspector = new KeyframeInspectorPanel(session, keys);

            session.Selection.SetSingle(first);
            inspector.Synchronize();
            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(GetBuffer<float>(inspector, "_floatValue"), Is.EqualTo(0f).Within(1e-4f));

            session.Selection.SetSingle(second);
            inspector.Synchronize();
            Assert.That(GetBuffer<string>(inspector, "_inspectedTrackId"), Is.EqualTo(blendShapeTrackId));
            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(GetBuffer<float>(inspector, "_floatValue"), Is.EqualTo(100f).Within(1e-4f));
            Assert.That(GetBuffer<int>(inspector, "_interpolationIndex"), Is.EqualTo((int)InterpolationType.EaseIn));
        }

        [Test]
        public void InspectorSelection_ResolvesTransformBuffersFromTheSelectedKeyTrack()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddTransformTrack(TrackKind.TransformPosition, "Head");
            string transformKey = keys.AddKeyAt(0.5f, 0f, new Vector3(1f, 2f, 3f), InterpolationType.Smooth);
            string transformTrackId = session.SelectedTrackId;
            tracks.AddBlendShapeTrack("Face", "Smile");
            var inspector = new KeyframeInspectorPanel(session, keys);

            session.Selection.SetSingle(transformKey);
            inspector.Synchronize();

            Assert.That(GetBuffer<string>(inspector, "_inspectedTrackId"), Is.EqualTo(transformTrackId));
            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(GetBuffer<Vector3>(inspector, "_vectorValue"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(GetBuffer<int>(inspector, "_interpolationIndex"), Is.EqualTo((int)InterpolationType.Smooth));
        }

        [Test]
        public void InspectorExternalRefresh_SameKeyKeepsDirtyBuffersWhileFocused()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack("Face", "Smile");
            string selected = keys.AddKeyAt(0.2f, 0.5f, Vector3.zero, InterpolationType.Hold);
            var inspector = new KeyframeInspectorPanel(session, keys);
            session.Selection.SetSingle(selected);
            inspector.Synchronize();

            SetBuffer(inspector, "_time", 3f);
            SetBuffer(inspector, "_floatValue", 0.1f);
            SetBuffer(inspector, "_interpolationIndex", (int)InterpolationType.EaseOut);

            session.NotifyChanged();
            EditorGUIUtility.editingTextField = true;
            try
            {
                inspector.Synchronize();
            }
            finally
            {
                EditorGUIUtility.editingTextField = false;
            }

            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(GetBuffer<float>(inspector, "_floatValue"), Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(GetBuffer<int>(inspector, "_interpolationIndex"), Is.EqualTo((int)InterpolationType.EaseOut));

            inspector.Synchronize();
            Assert.That(GetBuffer<float>(inspector, "_time"), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(GetBuffer<float>(inspector, "_floatValue"), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(GetBuffer<int>(inspector, "_interpolationIndex"), Is.EqualTo((int)InterpolationType.Hold));
        }

        [Test]
        public void SessionTimeChange_RequestsOneCoalescedPreviewRepaint()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var keys = new KeyframeController(session);
            var tracks = new TrackController(session);
            var scheduler = new PreviewRepaintScheduler();
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack(AvatarFixture.FaceRendererPath, "Mouth_Smile");
            keys.AddKeyAt(1f, 0.8f, Vector3.zero, InterpolationType.Linear);
            session.Changed += () => scheduler.Request();

            Assert.That(session.SetCurrentTime(0.5f), Is.True);
            Assert.That(session.SetCurrentTime(0.75f), Is.True);

            Assert.That(scheduler.RequestCount, Is.EqualTo(2));
            Assert.That(scheduler.Pending, Is.True);
            Assert.That(scheduler.Dispatch(), Is.True);
            Assert.That(scheduler.DispatchCount, Is.EqualTo(1));
            Assert.That(scheduler.Pending, Is.False);
        }

        [Test]
        public void PreviewRepaintScheduler_RepeatedRequestsCoalesceUntilDispatched()
        {
            var scheduler = new PreviewRepaintScheduler();

            Assert.That(scheduler.Request(), Is.True);
            Assert.That(scheduler.Request(), Is.False);
            Assert.That(scheduler.Request(), Is.False);
            Assert.That(scheduler.RequestCount, Is.EqualTo(3));
            Assert.That(scheduler.Dispatch(), Is.True);
            Assert.That(scheduler.DispatchCount, Is.EqualTo(1));
            Assert.That(scheduler.Request(), Is.True);
        }

        [Test]
        public void PlaybackUpdateGate_RegistersOnlyForActivePlayback()
        {
            var gate = new PlaybackUpdateGate();

            Assert.That(gate.Synchronize(false), Is.False);
            Assert.That(gate.Synchronize(true), Is.True);
            Assert.That(gate.IsRegistered, Is.True);
            Assert.That(gate.Synchronize(true), Is.False);
            Assert.That(gate.Reset(), Is.True);
            Assert.That(gate.IsRegistered, Is.False);
            Assert.That(gate.Reset(), Is.False);
        }

        private static T GetBuffer<T>(KeyframeInspectorPanel inspector, string name)
        {
            return (T)typeof(KeyframeInspectorPanel)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(inspector);
        }

        private static void SetBuffer(KeyframeInspectorPanel inspector, string name, object value)
        {
            typeof(KeyframeInspectorPanel)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(inspector, value);
        }
    }
}
