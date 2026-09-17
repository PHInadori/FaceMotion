using System.Reflection;
using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Integration;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    public sealed class J1BasicOperationsRegressionTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TimelineInputHandler _input;
        private TempFaceMotionAsset _temp;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            _input = new TimelineInputHandler(_session, _keys, _tracks);
            _temp = new TempFaceMotionAsset();
        }

        [TearDown]
        public void TearDown()
        {
            _temp?.Dispose();
            Undo.ClearAll();
        }

        [Test]
        public void EditingTextField_BackspaceAndDelete_DoNotDeleteSelectedKey()
        {
            SetupBlendTrack();
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            var layout = new TimelineLayoutSnapshot();
            bool previous = EditorGUIUtility.editingTextField;
            try
            {
                EditorGUIUtility.editingTextField = true;
                Assert.That(_input.HandleEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace }, new Rect(0f, 0f, 100f, 100f), layout), Is.False);
                Assert.That(_input.HandleEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete }, new Rect(0f, 0f, 100f, 100f), layout), Is.False);
            }
            finally
            {
                EditorGUIUtility.editingTextField = previous;
            }

            Assert.That(KeyCount(), Is.EqualTo(1));
        }

        [Test]
        public void TimelineDelete_DeletesSelectedKeyWhenNotEditingText()
        {
            SetupBlendTrack();
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            bool previous = EditorGUIUtility.editingTextField;
            try
            {
                EditorGUIUtility.editingTextField = false;
                Assert.That(_input.HandleEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete }, new Rect(0f, 0f, 100f, 100f), new TimelineLayoutSnapshot()), Is.True);
            }
            finally
            {
                EditorGUIUtility.editingTextField = previous;
            }

            Assert.That(KeyCount(), Is.Zero);
        }

        [Test]
        public void WindowOwnedInspectorFocus_PreventsTimelineDeleteShortcut()
        {
            SetupBlendTrack();
            _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);

            Assert.That(
                _input.HandleEvent(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete },
                    new Rect(0f, 0f, 100f, 100f),
                    new TimelineLayoutSnapshot(),
                    true),
                Is.False);
            Assert.That(KeyCount(), Is.EqualTo(1));
        }

        [TestCase(30f, 13.4f)]
        [TestCase(60f, 29.6f)]
        public void AddKeyAtCurrentTime_UsesSnappedPlayheadRegardlessOfZoomOrScroll(float frameRate, float authoredFrame)
        {
            SetupBlendTrack();
            var timeline = _session.GetSelectedAnimation().Timeline;
            timeline.FrameRate = frameRate;
            _session.ViewState.Zoom = 2.6f;
            _session.ViewState.ScrollTime = 0.31f;
            float playhead = authoredFrame / frameRate;
            _session.SetCurrentTime(playhead);

            string keyId = _keys.AddKeyAtCurrentTime(0.25f, Vector3.zero, InterpolationType.Linear);

            Assert.That(keyId, Is.Not.Null);
            Assert.That(GetOnlyKey().Time, Is.EqualTo(FrameSnapper.Snap(playhead, frameRate, timeline.Duration)).Within(1e-5f));
        }

        [Test]
        public void AddKeyAtCurrentTime_SelectsNewKeyWithoutOverwritingPreviouslyAppliedKey()
        {
            SetupBlendTrack();
            string keyA = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            Assert.That(_keys.ApplyKeyEdits(keyA, 0.1f, 0.8f, Vector3.zero, InterpolationType.Smooth), Is.True);
            _session.SetCurrentTime(0.5f);

            string keyB = _keys.AddKeyAtCurrentTime(0.2f, Vector3.zero, InterpolationType.Linear);

            Assert.That(keyB, Is.Not.EqualTo(keyA));
            Assert.That(_session.Selection.KeyIds[0], Is.EqualTo(keyB));
            Assert.That(KeyCount(), Is.EqualTo(2));
            Assert.That(FindKey(keyA).Value, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void CtrlWheel_ZoomsAtCursorAndPlainWheelPreservesScrollHandling()
        {
            SetupBlendTrack();
            var plot = new Rect(20f, 10f, 300f, 100f);
            _session.ViewState.Zoom = 1f;
            float initial = _session.ViewState.Zoom;

            Assert.That(_input.HandleEvent(new Event { type = EventType.ScrollWheel, control = true, mousePosition = new Vector2(120f, 30f), delta = new Vector2(0f, -1f) }, plot, new TimelineLayoutSnapshot()), Is.True);
            Assert.That(_session.ViewState.Zoom, Is.GreaterThan(initial));
            float zoomed = _session.ViewState.Zoom;
            Assert.That(_input.HandleEvent(new Event { type = EventType.ScrollWheel, mousePosition = new Vector2(120f, 30f), delta = new Vector2(0f, -1f) }, plot, new TimelineLayoutSnapshot()), Is.False);
            Assert.That(_session.ViewState.Zoom, Is.EqualTo(zoomed));
            Assert.That(_input.HandleEvent(new Event { type = EventType.ScrollWheel, control = true, mousePosition = new Vector2(5f, 30f), delta = new Vector2(0f, 1f) }, plot, new TimelineLayoutSnapshot()), Is.False);
            Assert.That(_session.ViewState.Zoom, Is.EqualTo(zoomed));
        }

        [TestCase(0.13f, 0.5f, 0.1f)]
        [TestCase(3.06f, 2f, 0.25f)]
        public void FractionalScroll_UsesAlignedTicksWithVisibleMajorLabels(float scroll, float major, float minor)
        {
            float first = TimelineGeometry.FirstVisibleTick(scroll, minor);
            Assert.That(first, Is.GreaterThanOrEqualTo(scroll - 1e-4f));
            Assert.That(first / minor, Is.EqualTo(Mathf.Round(first / minor)).Within(1e-4f));

            bool hasMajor = false;
            for (float tick = first; tick <= scroll + major + 1e-4f; tick += minor)
            {
                Assert.That(float.IsNaN(tick) || float.IsInfinity(tick), Is.False);
                hasMajor |= TimelineGeometry.IsMajorTick(tick, major);
            }

            Assert.That(hasMajor, Is.True);
        }

        [Test]
        public void AvatarGlobalObjectId_RestoresValidSceneDescriptorAndRejectsInvalidId()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string scenePath = _temp.Folder + "/J1Avatar.unity";
            var root = new GameObject("FaceMotion J1 Avatar");
            SceneManager.MoveGameObjectToScene(root, scene);
            var descriptor = root.AddComponent<VRCAvatarDescriptor>();
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                string id = GlobalObjectId.GetGlobalObjectIdSlow(descriptor).ToString();
                Assert.That(FaceMotionWindow.TryResolveAvatarDescriptor(id, out var restored), Is.True);
                Assert.That(restored, Is.SameAs(descriptor));
                Assert.That(FaceMotionWindow.TryResolveAvatarDescriptor("not-a-global-object-id", out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void AvatarGlobalObjectId_RestoresDescriptorWhenSavedSceneMatches()
        {
            Scene avatarScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string avatarScenePath = _temp.Folder + "/J1InactiveAvatar.unity";
            var root = new GameObject("FaceMotion J1 Inactive Avatar");
            SceneManager.MoveGameObjectToScene(root, avatarScene);
            var descriptor = root.AddComponent<VRCAvatarDescriptor>();
            try
            {
                Assert.That(EditorSceneManager.SaveScene(avatarScene, avatarScenePath), Is.True);
                string id = GlobalObjectId.GetGlobalObjectIdSlow(descriptor).ToString();
                Assert.That(FaceMotionWindow.TryResolveAvatarDescriptor(id, avatarScenePath, out var restored), Is.True);
                Assert.That(restored, Is.SameAs(descriptor));
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void WindowCloseAndReopen_RestoresAvatarThroughSelectionController()
        {
            Scene avatarScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FaceMotionSessionStateStore.Load(out string oldProject, out string oldAnimation, out float oldTime, out float oldZoom, out string oldAvatar, out string oldScene);
            string avatarScenePath = _temp.Folder + "/J1WindowAvatar.unity";
            var root = new GameObject("FaceMotion J1 Window Avatar");
            SceneManager.MoveGameObjectToScene(root, avatarScene);
            var descriptor = root.AddComponent<VRCAvatarDescriptor>();
            FaceMotionWindow first = null;
            FaceMotionWindow reopened = null;
            try
            {
                Assert.That(EditorSceneManager.SaveScene(avatarScene, avatarScenePath), Is.True);
                first = EditorWindow.GetWindow<FaceMotionWindow>();
                GetPrivateField<AvatarController>(first, "_avatar").SetDescriptor(descriptor);
                first.Close();
                first = null;

                reopened = EditorWindow.GetWindow<FaceMotionWindow>();
                Assert.That(GetPrivateField<FaceMotionEditorSession>(reopened, "_session").ActiveDescriptor, Is.SameAs(descriptor));
            }
            finally
            {
                reopened?.Close();
                first?.Close();
                Object.DestroyImmediate(root);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                FaceMotionSessionStateStore.Save(oldProject, oldAnimation, oldTime, oldZoom, oldAvatar, oldScene);
            }
        }

        [Test]
        public void SessionStateStore_PersistsAvatarIdentityWithoutProjectReference()
        {
            FaceMotionSessionStateStore.Load(out string oldProject, out string oldAnimation, out float oldTime, out float oldZoom, out string oldAvatar, out string oldScene);
            try
            {
                FaceMotionSessionStateStore.Save("Assets/J1.asset", "animation", 0.5f, 1.5f, "GlobalObjectId_V1-2-test", "Assets/J1.unity");
                FaceMotionSessionStateStore.Load(out string project, out string animation, out float time, out float zoom, out string avatar, out string scene);
                Assert.That(project, Is.EqualTo("Assets/J1.asset"));
                Assert.That(animation, Is.EqualTo("animation"));
                Assert.That(time, Is.EqualTo(0.5f));
                Assert.That(zoom, Is.EqualTo(1.5f));
                Assert.That(avatar, Is.EqualTo("GlobalObjectId_V1-2-test"));
                Assert.That(scene, Is.EqualTo("Assets/J1.unity"));
            }
            finally
            {
                FaceMotionSessionStateStore.Save(oldProject, oldAnimation, oldTime, oldZoom, oldAvatar, oldScene);
            }
        }

        [Test]
        public void BackendDefault_UsesMaWhenAvailableAndPreservesExplicitSelection()
        {
            Assert.That(DirectVRChatIntegrationPanel.ResolveInitialBackend(false, IntegrationBackendSelection.Direct, true), Is.EqualTo(IntegrationBackendSelection.ModularAvatar));
            Assert.That(DirectVRChatIntegrationPanel.ResolveInitialBackend(false, IntegrationBackendSelection.ModularAvatar, false), Is.EqualTo(IntegrationBackendSelection.Direct));
            Assert.That(DirectVRChatIntegrationPanel.ResolveInitialBackend(true, IntegrationBackendSelection.Direct, true), Is.EqualTo(IntegrationBackendSelection.Direct));
            Assert.That(DirectVRChatIntegrationPanel.ResolveInitialBackend(true, IntegrationBackendSelection.ModularAvatar, false), Is.EqualTo(IntegrationBackendSelection.ModularAvatar));
        }

        [Test]
        public void TrackCandidateListHeight_IsExpandedWithoutChangingWindowMinimums()
        {
            Assert.That(TrackListPanel.DefaultCandidateListHeight, Is.GreaterThan(110f));
            Assert.That(ResizableVerticalSplitter.Clamp(0f, TrackListPanel.MinimumCandidateListHeight, TrackListPanel.MaximumCandidateListHeight), Is.EqualTo(TrackListPanel.MinimumCandidateListHeight));
            Assert.That(ResizableVerticalSplitter.Clamp(999f, TrackListPanel.MinimumCandidateListHeight, TrackListPanel.MaximumCandidateListHeight), Is.EqualTo(TrackListPanel.MaximumCandidateListHeight));
            Assert.That(FaceMotionWindow.MinimumWindowWidth, Is.EqualTo(900f));
            Assert.That(FaceMotionWindow.MinimumTimelineWidth, Is.GreaterThanOrEqualTo(460f));
        }

        private void SetupBlendTrack()
        {
            var project = FaceMotionProject.CreateNew();
            string path = _temp.AssetPath("J1Project");
            AssetDatabase.CreateAsset(project, path);
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, path);
            _animations.Add();
            Assert.That(_tracks.AddBlendShapeTrack("Body/Face", "Mouth_Smile"), Is.True);
        }

        private int KeyCount()
        {
            return _session.GetSelectedTrack().BlendShape.Keys.Count;
        }

        private FloatKeyframeData GetOnlyKey()
        {
            return _session.GetSelectedTrack().BlendShape.Keys[0];
        }

        private FloatKeyframeData FindKey(string keyId)
        {
            foreach (var key in _session.GetSelectedTrack().BlendShape.Keys)
            {
                if (key.KeyId == keyId) return key;
            }

            return null;
        }

        private static T GetPrivateField<T>(object instance, string name) where T : class
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field: " + name);
            return field.GetValue(instance) as T;
        }
    }
}
