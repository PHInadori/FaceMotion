using System.Reflection;
using System.Linq;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class L1AuthoringPolishTests
    {
        [Test]
        public void TimelineSettingsBuffer_SwitchingAnimationsDiscardsUnappliedValues()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);

            string animationA = animations.Add();
            animations.SetDuration(10f);
            animations.SetFrameRate(60f);
            animations.SetLoop(true);
            string animationB = animations.Add();
            animations.SetDuration(1f);
            animations.SetFrameRate(30f);
            animations.SetLoop(false);
            var panel = new AnimationListPanel(session, animations);

            animations.Select(animationA);
            SynchronizeSettings(panel);
            SetBuffer(panel, "_durationBuffer", 99f);
            SetBuffer(panel, "_frameRateBuffer", 99f);
            SetBuffer(panel, "_loopBuffer", true);

            animations.Select(animationB);
            SynchronizeSettings(panel);

            Assert.That(GetBuffer<float>(panel, "_durationBuffer"), Is.EqualTo(1f));
            Assert.That(GetBuffer<float>(panel, "_frameRateBuffer"), Is.EqualTo(30f));
            Assert.That(GetBuffer<bool>(panel, "_loopBuffer"), Is.False);
            Assert.That(session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(1f));
            Assert.That(session.GetSelectedAnimation().Timeline.FrameRate, Is.EqualTo(30f));
            Assert.That(session.GetSelectedAnimation().Timeline.Loop, Is.False);
        }

        [Test]
        public void TimelineSettingsBuffer_EachAnimationReloadsItsSavedSettings()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);

            string animationA = animations.Add();
            animations.SetDuration(10f);
            animations.SetFrameRate(60f);
            string animationB = animations.Add();
            animations.SetDuration(1f);
            animations.SetFrameRate(30f);
            var panel = new AnimationListPanel(session, animations);

            animations.Select(animationA);
            SynchronizeSettings(panel);
            Assert.That(GetBuffer<float>(panel, "_durationBuffer"), Is.EqualTo(10f));
            Assert.That(GetBuffer<float>(panel, "_frameRateBuffer"), Is.EqualTo(60f));

            animations.Select(animationB);
            SynchronizeSettings(panel);
            animations.Select(animationA);
            SynchronizeSettings(panel);

            Assert.That(GetBuffer<float>(panel, "_durationBuffer"), Is.EqualTo(10f));
            Assert.That(GetBuffer<float>(panel, "_frameRateBuffer"), Is.EqualTo(60f));
        }

        [Test]
        public void TimelineSettingsApply_ChangesOnlyTheSelectedAnimation()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);

            string animationA = animations.Add();
            string animationB = animations.Add();
            animations.SetDuration(1f);
            animations.SetFrameRate(30f);

            animations.Select(animationA);
            Assert.That(animations.SetDuration(10f), Is.True);
            Assert.That(animations.SetFrameRate(60f), Is.True);
            animations.Select(animationB);

            Assert.That(session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(1f));
            Assert.That(session.GetSelectedAnimation().Timeline.FrameRate, Is.EqualTo(30f));
        }

        [Test]
        public void NewBlendShapeAuthoredKey_UsesOneHundredAfterBaseline()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var tracks = new TrackController(session);
            var keys = new KeyframeController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddBlendShapeTrack("Face", "Smile");

            string baseline = keys.AddKeyAt(0f, 37f, Vector3.zero, InterpolationType.Linear);
            var inspector = new KeyframeInspectorPanel(session, keys);
            session.Selection.Clear();
            inspector.Synchronize();
            float defaultValue = GetBuffer<float>(inspector, "_floatValue");
            session.SetCurrentTime(0.5f);
            string authored = keys.AddKeyAtCurrentTime(defaultValue, Vector3.zero, InterpolationType.Linear);
            var track = session.GetSelectedTrack();

            Assert.That(track.BlendShape.Keys.First(key => key.KeyId == baseline).Value, Is.EqualTo(37f));
            Assert.That(track.BlendShape.Keys.First(key => key.KeyId == authored).Value, Is.EqualTo(100f));
            Assert.That(defaultValue, Is.Not.EqualTo(1f));
        }

        [Test]
        public void NewTransformAuthoredKey_KeepsExistingZeroVectorDefault()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var tracks = new TrackController(session);
            var keys = new KeyframeController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            animations.Add();
            tracks.AddTransformTrack(TrackKind.TransformPosition, "Head");
            var inspector = new KeyframeInspectorPanel(session, keys);

            inspector.Synchronize();
            Vector3 defaultValue = GetBuffer<Vector3>(inspector, "_vectorValue");
            string authored = keys.AddKeyAtCurrentTime(0f, defaultValue, InterpolationType.Linear);

            Assert.That(defaultValue, Is.EqualTo(Vector3.zero));
            Assert.That(session.GetSelectedTrack().Transform.Keys.First(key => key.KeyId == authored).Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TextFocusGate_RecognizesOnlyThisPanelsControls()
        {
            Assert.That(AnimationListPanel.OwnsTextFocus("FaceMotion.AnimationList.duration"), Is.True);
            Assert.That(AnimationListPanel.OwnsTextFocus("FaceMotion.AnimationList.frameRate"), Is.True);
            Assert.That(AnimationListPanel.OwnsTextFocus("FaceMotion.AnimationList.rename"), Is.True);
            Assert.That(AnimationListPanel.OwnsTextFocus("FaceMotion.KeyInspector.time"), Is.False);
            Assert.That(AnimationListPanel.OwnsTextFocus(string.Empty), Is.False);
            Assert.That(AnimationListPanel.OwnsTextFocus(null), Is.False);
        }

        [Test]
        public void SettingsTextFocusGate_RecognizesOnlySettingsFields()
        {
            Assert.That(AnimationListPanel.OwnsSettingsTextFocus("FaceMotion.AnimationList.duration"), Is.True);
            Assert.That(AnimationListPanel.OwnsSettingsTextFocus("FaceMotion.AnimationList.frameRate"), Is.True);
            Assert.That(AnimationListPanel.OwnsSettingsTextFocus("FaceMotion.AnimationList.rename"), Is.False);
            Assert.That(AnimationListPanel.OwnsSettingsTextFocus("FaceMotion.KeyInspector.time"), Is.False);
            Assert.That(AnimationListPanel.OwnsSettingsTextFocus(null), Is.False);
        }

        [Test]
        public void ReleaseTextFocus_WhenPanelOwnsFocus_ClearsKeyboardAndEditingState()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var panel = new AnimationListPanel(session, animations);

            int previousKeyboard = GUIUtility.keyboardControl;
            bool previousEditing = EditorGUIUtility.editingTextField;
            try
            {
                GUIUtility.keyboardControl = 4711;
                EditorGUIUtility.editingTextField = true;

                panel.ReleaseTextFocus("FaceMotion.AnimationList.duration");

                Assert.That(GUIUtility.keyboardControl, Is.EqualTo(0));
                Assert.That(EditorGUIUtility.editingTextField, Is.False);
            }
            finally
            {
                GUIUtility.keyboardControl = previousKeyboard;
                EditorGUIUtility.editingTextField = previousEditing;
            }
        }

        [Test]
        public void ReleaseTextFocus_WhenForeignControlOwnsFocus_LeavesItUntouched()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            var panel = new AnimationListPanel(session, animations);

            int previousKeyboard = GUIUtility.keyboardControl;
            bool previousEditing = EditorGUIUtility.editingTextField;
            try
            {
                GUIUtility.keyboardControl = 4711;
                EditorGUIUtility.editingTextField = true;

                panel.ReleaseTextFocus("FaceMotion.KeyInspector.time");

                Assert.That(GUIUtility.keyboardControl, Is.EqualTo(4711));
                Assert.That(EditorGUIUtility.editingTextField, Is.True);
            }
            finally
            {
                GUIUtility.keyboardControl = previousKeyboard;
                EditorGUIUtility.editingTextField = previousEditing;
            }
        }

        [Test]
        public void TimelineSettingsBuffer_SameAnimationKeepsPendingValues()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            string animationA = animations.Add();
            animations.SetDuration(10f);
            var panel = new AnimationListPanel(session, animations);

            animations.Select(animationA);
            SynchronizeSettings(panel);
            SetBuffer(panel, "_durationBuffer", 99f);

            SynchronizeSettings(panel);

            Assert.That(GetBuffer<float>(panel, "_durationBuffer"), Is.EqualTo(99f));
        }

        [Test]
        public void PendingDurationEdit_AppliedAfterSwitch_WritesTheTargetAnimationOnly()
        {
            var session = new FaceMotionEditorSession();
            var animations = new AnimationController(session);
            session.SetActiveProject(FaceMotionProject.CreateNew(), null);
            string animationA = animations.Add();
            animations.SetDuration(10f);
            string animationB = animations.Add();
            animations.SetDuration(1f);
            var panel = new AnimationListPanel(session, animations);

            animations.Select(animationA);
            SynchronizeSettings(panel);
            SetBuffer(panel, "_durationBuffer", 99f);

            animations.Select(animationB);
            SynchronizeSettings(panel);
            float reloaded = GetBuffer<float>(panel, "_durationBuffer");
            animations.SetDuration(reloaded);

            Assert.That(reloaded, Is.EqualTo(1f));
            Assert.That(session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(1f));
            animations.Select(animationA);
            Assert.That(session.GetSelectedAnimation().Timeline.Duration, Is.EqualTo(10f));
        }

        private static void SynchronizeSettings(AnimationListPanel panel)
        {
            typeof(AnimationListPanel)
                .GetMethod("SynchronizeTimelineSettings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(panel, null);
        }

        private static T GetBuffer<T>(object panel, string name)
        {
            return (T)panel.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
        }

        private static void SetBuffer(object panel, string name, object value)
        {
            panel.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, value);
        }
    }
}
