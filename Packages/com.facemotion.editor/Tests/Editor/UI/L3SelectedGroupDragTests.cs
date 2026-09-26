using System.Linq;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// L3 pointer-selection polish: grabbing any member of a blue working group keeps
    /// the group selected and drags it as one unit, exercised through the real
    /// Timeline input path (TimelineInputHandler.HandleEvent).
    /// </summary>
    public sealed class L3SelectedGroupDragTests
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
            _temp = null;
            Undo.ClearAll();
        }

        // ----- TEST 1 --------------------------------------------------------------------

        [Test]
        public void TEST1_MouseDownOnAlreadySelectedKey_KeepsTheWholeSelection()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            string c = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSelection(new[] { a, b, c });

            TimelineLayoutSnapshot layout = BuildLayout();
            Assert.That(MouseDown(layout, KeyX(layout, 0.2f), trackId), Is.True);

            Assert.That(_session.Selection.Count, Is.EqualTo(3));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b, c }));
        }

        // ----- TEST 2 --------------------------------------------------------------------

        [Test]
        public void TEST2_DragFromSelectedMember_MovesTheWholeGroupByOneCommonDelta()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            string c = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSelection(new[] { a, b, c });

            TimelineLayoutSnapshot layout = BuildLayout();
            float anchor = KeyX(layout, 0.2f);
            const float dragPixels = 30f;

            Assert.That(MouseDown(layout, anchor, trackId), Is.True);
            Assert.That(Drag(layout, anchor + dragPixels, RowY(layout, trackId)), Is.True);
            Assert.That(MouseUp(layout), Is.True);

            float delta = dragPixels / layout.PixelsPerSecond;
            Assert.That(TimeOf(trackId, a), Is.EqualTo(0.1f + delta).Within(1e-3f));
            Assert.That(TimeOf(trackId, b), Is.EqualTo(0.2f + delta).Within(1e-3f));
            Assert.That(TimeOf(trackId, c), Is.EqualTo(0.3f + delta).Within(1e-3f));

            Assert.That(
                TimeOf(trackId, b) - TimeOf(trackId, a),
                Is.EqualTo(0.1f).Within(1e-3f),
                "The group must keep its original spacing (one common delta).");

            Assert.That(_session.Selection.Count, Is.EqualTo(3));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b, c }));
        }

        // ----- TEST 3 --------------------------------------------------------------------

        [Test]
        public void TEST3_MouseDownOnUnselectedKey_ReplacesSelectionWithThatKeyOnly()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            string c = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSelection(new[] { a, b });

            TimelineLayoutSnapshot layout = BuildLayout();
            Assert.That(MouseDown(layout, KeyX(layout, 0.3f), trackId), Is.True);
            Assert.That(MouseUp(layout), Is.True);

            Assert.That(_session.Selection.Count, Is.EqualTo(1));
            Assert.That(_session.Selection.KeyIds, Is.EqualTo(new[] { c }));
        }

        // ----- TEST 4 --------------------------------------------------------------------

        [Test]
        public void TEST4_PlainClickOnSelectedMemberWithoutDrag_KeepsTheMultiSelection()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            _session.Selection.SetSelection(new[] { a, b });

            TimelineLayoutSnapshot layout = BuildLayout();
            Assert.That(MouseDown(layout, KeyX(layout, 0.1f), trackId), Is.True);
            Assert.That(MouseUp(layout), Is.True);

            Assert.That(_session.Selection.Count, Is.EqualTo(2));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b }));
            Assert.That(TimeOf(trackId, a), Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(TimeOf(trackId, b), Is.EqualTo(0.2f).Within(1e-4f));
        }

        // ----- TEST 5 --------------------------------------------------------------------

        [Test]
        public void TEST5_CtrlClick_ToggleBehaviorIsUnchanged()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            string c = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSelection(new[] { a, b });

            TimelineLayoutSnapshot layout = BuildLayout();

            // Ctrl/Cmd + click on a selected key removes it.
            Assert.That(MouseDown(layout, KeyX(layout, 0.1f), trackId, ctrl: true), Is.True);
            Assert.That(MouseUp(layout), Is.True);
            Assert.That(_session.Selection.KeyIds, Is.EqualTo(new[] { b }));

            // Ctrl/Cmd + click on a deselected key adds it back.
            Assert.That(MouseDown(layout, KeyX(layout, 0.1f), trackId, ctrl: true), Is.True);
            Assert.That(MouseUp(layout), Is.True);
            Assert.That(_session.Selection.Count, Is.EqualTo(2));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b }));

            // Ctrl/Cmd + click on an unselected key adds it.
            Assert.That(MouseDown(layout, KeyX(layout, 0.3f), trackId, ctrl: true), Is.True);
            Assert.That(MouseUp(layout), Is.True);
            Assert.That(_session.Selection.Count, Is.EqualTo(3));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b, c }));
        }

        // ----- TEST 6 --------------------------------------------------------------------

        [Test]
        public void TEST6_MultiTrackSelectedDrag_MovesEveryTrackTogether()
        {
            SetupTimeline();
            string blendA = AddBlendTrack();
            string blendB = AddBlendTrack();
            string transformTrack = AddTransformTrack(TrackKind.TransformPosition);

            string keyA = AddKey(blendA, 0.2f, 55f);
            string keyB = AddKey(blendB, 0.25f, 66f);
            string keyC = AddKey(transformTrack, 0.3f, 0f, new Vector3(1f, 2f, 3f));
            _session.Selection.SetSelection(new[] { keyA, keyB, keyC });

            TimelineLayoutSnapshot layout = BuildLayout();
            float anchor = KeyX(layout, 0.3f);
            const float dragPixels = 30f;

            Assert.That(MouseDown(layout, anchor, transformTrack), Is.True);
            Assert.That(Drag(layout, anchor + dragPixels, RowY(layout, transformTrack)), Is.True);
            Assert.That(MouseUp(layout), Is.True);

            float delta = dragPixels / layout.PixelsPerSecond;
            Assert.That(TimeOf(blendA, keyA), Is.EqualTo(0.2f + delta).Within(1e-3f));
            Assert.That(TimeOf(blendB, keyB), Is.EqualTo(0.25f + delta).Within(1e-3f));
            Assert.That(TimeOf(transformTrack, keyC), Is.EqualTo(0.3f + delta).Within(1e-3f));

            Assert.That(FloatValueOf(blendA, keyA), Is.EqualTo(55f));
            Assert.That(FloatValueOf(blendB, keyB), Is.EqualTo(66f));
            Assert.That(VectorValueOf(transformTrack, keyC), Is.EqualTo(new Vector3(1f, 2f, 3f)));

            Assert.That(_session.Selection.Count, Is.EqualTo(3));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { keyA, keyB, keyC }));
        }

        // ----- TEST 7 --------------------------------------------------------------------

        [Test]
        public void TEST7_UndoAfterGroupDrag_RestoresEveryOriginalTime()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            string c = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSelection(new[] { a, b, c });

            TimelineLayoutSnapshot layout = BuildLayout();
            float anchor = KeyX(layout, 0.2f);

            MouseDown(layout, anchor, trackId);
            Drag(layout, anchor + 30f, RowY(layout, trackId));
            MouseUp(layout);

            Assert.That(TimeOf(trackId, a), Is.Not.EqualTo(0.1f).Within(1e-4f));

            Undo.PerformUndo();
            _session.RefreshAfterUndo();

            Assert.That(TimeOf(trackId, a), Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(TimeOf(trackId, b), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(TimeOf(trackId, c), Is.EqualTo(0.3f).Within(1e-4f));
        }

        // ----- TEST 8 --------------------------------------------------------------------

        [Test]
        public void TEST8_GroupDrag_CollisionPath_MatchesKeyMovePlanner()
        {
            SetupTimeline(duration: 1f, frameRate: 60f);
            string trackId = AddBlendTrack();
            string movingA = AddKey(trackId, 0.1f, 10f);
            string obstacle = AddKey(trackId, 0.15f, 99f);
            string movingB = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSelection(new[] { movingA, movingB });

            TimelineLayoutSnapshot layout = BuildLayout();
            float anchor = KeyX(layout, 0.1f);
            float dragPixels = 0.05f * layout.PixelsPerSecond;

            MouseDown(layout, anchor, trackId);
            Drag(layout, anchor + dragPixels, RowY(layout, trackId));
            MouseUp(layout);

            float delta = dragPixels / layout.PixelsPerSecond;
            float[] expected = KeyMovePlanner.PlanTrack(
                new[] { 0.1f, 0.3f },
                new[] { 0.15f },
                delta,
                1f,
                60f,
                _session.ViewState.SnapEnabled);

            Assert.That(TimeOf(trackId, movingA), Is.EqualTo(expected[0]).Within(1e-4f));
            Assert.That(TimeOf(trackId, movingB), Is.EqualTo(expected[1]).Within(1e-4f));
            Assert.That(TimeOf(trackId, obstacle), Is.EqualTo(0.15f).Within(1e-4f), "An unselected neighbour must never be moved.");
            Assert.That(Track(trackId).BlendShape.Keys, Has.Count.EqualTo(3));
        }

        [Test]
        public void TEST8b_GroupDrag_DurationBoundary_MatchesGroupConstraintAndPlanner()
        {
            SetupTimeline(duration: 1f, frameRate: 60f);
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.9f, 10f);
            string b = AddKey(trackId, 0.95f, 30f);
            _session.Selection.SetSelection(new[] { a, b });

            TimelineLayoutSnapshot layout = BuildLayout();
            float anchor = KeyX(layout, 0.9f);
            float dragPixels = 0.2f * layout.PixelsPerSecond;

            MouseDown(layout, anchor, trackId);
            Drag(layout, anchor + dragPixels, RowY(layout, trackId));
            MouseUp(layout);

            float rawDelta = dragPixels / layout.PixelsPerSecond;
            float constrainedDelta = TimelineTimeDomain.ConstrainGroupDelta(
                0.9f,
                0.95f,
                rawDelta,
                1f);
            float[] expected = KeyMovePlanner.PlanTrack(
                new[] { 0.9f, 0.95f },
                new float[0],
                constrainedDelta,
                1f,
                60f,
                _session.ViewState.SnapEnabled);

            Assert.That(TimeOf(trackId, a), Is.EqualTo(expected[0]).Within(1e-4f));
            Assert.That(TimeOf(trackId, b), Is.EqualTo(expected[1]).Within(1e-4f));
            Assert.That(TimeOf(trackId, b), Is.LessThanOrEqualTo(1f + 1e-4f), "The group must stay inside the duration.");
            Assert.That(
                TimeOf(trackId, b) - TimeOf(trackId, a),
                Is.EqualTo(0.05f).Within(1e-3f),
                "The boundary clamp must preserve group spacing.");
        }

        // ----- TEST 9 / 10 (preserved behaviors) ------------------------------------------

        [Test]
        public void TEST9_ShiftRangeSelection_IsUnchanged()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            string c = AddKey(trackId, 0.3f, 30f);
            _session.Selection.SetSingle(a);
            _session.Selection.SetPrimaryKeyId(a);

            TimelineLayoutSnapshot layout = BuildLayout();
            Event down = Pointer(EventType.MouseDown, KeyX(layout, 0.3f), RowY(layout, trackId));
            down.shift = true;
            Assert.That(HandlePointer(down, layout), Is.True);
            Assert.That(MouseUp(layout), Is.True);

            Assert.That(_session.Selection.Count, Is.EqualTo(3));
            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b, c }));
        }

        [Test]
        public void TEST10_EmptySpaceClick_StillClearsTheSelection()
        {
            SetupTimeline();
            string trackId = AddBlendTrack();
            string a = AddKey(trackId, 0.1f, 10f);
            string b = AddKey(trackId, 0.2f, 20f);
            _session.Selection.SetSelection(new[] { a, b });

            TimelineLayoutSnapshot layout = BuildLayout();
            Assert.That(MouseDown(layout, KeyX(layout, 0.6f), trackId), Is.True);

            Assert.That(_session.Selection.Count, Is.Zero);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));

            Assert.That(MouseUp(layout), Is.True);
            Assert.That(_session.Selection.Count, Is.Zero);
        }

        // ----- Helpers -------------------------------------------------------------------

        private void SetupTimeline(float duration = 1f, float frameRate = 60f)
        {
            var project = FaceMotionProject.CreateNew();
            EditorUtility.SetDirty(project);
            AssetDatabase.CreateAsset(project, _temp.AssetPath("L3SelectedGroupDrag"));
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            _animations.Add();

            var timeline = _session.GetSelectedAnimation().Timeline;
            timeline.Duration = duration;
            timeline.FrameRate = frameRate;
            _session.ViewState.SnapEnabled = false;
        }

        private string AddBlendTrack()
        {
            _tracks.AddBlendShapeTrack("Face", "Smile");
            return _session.SelectedTrackId;
        }

        private string AddTransformTrack(TrackKind kind)
        {
            _tracks.AddTransformTrack(kind, "Head");
            return _session.SelectedTrackId;
        }

        private string AddKey(
            string trackId,
            float time,
            float floatValue,
            Vector3 vectorValue = default,
            InterpolationType interpolation = InterpolationType.Linear)
        {
            _tracks.Select(trackId);
            string keyId = _keys.AddKeyAt(time, floatValue, vectorValue, interpolation);
            Assert.That(keyId, Is.Not.Null, "Test setup failed to author a key.");
            return keyId;
        }

        private TimelineLayoutSnapshot BuildLayout()
        {
            FaceMotionAnimationData animation = _session.GetSelectedAnimation();
            return TimelineLayoutBuilder.Build(
                animation,
                null,
                180f,
                TimelineGeometry.RulerHeight,
                _session.ViewState.ScrollTime,
                _session.ViewState.Zoom,
                animation.Timeline.Duration);
        }

        private static float KeyX(TimelineLayoutSnapshot layout, float time)
        {
            return TimelineGeometry.TimeToPixel(
                time,
                layout.ScrollTime,
                layout.PixelsPerSecond,
                layout.PlotLeft);
        }

        private static float RowY(TimelineLayoutSnapshot layout, string trackId)
        {
            TimelineRow row = layout.Rows.Single(candidate =>
                candidate.Track != null &&
                string.Equals(candidate.Track.TrackId, trackId, System.StringComparison.Ordinal));
            return row.Y + row.Height * 0.5f;
        }

        private static Rect PlotRect(TimelineLayoutSnapshot layout)
        {
            return new Rect(layout.PlotLeft, 0f, 800f, 600f);
        }

        private static Event Pointer(EventType type, float x, float y, bool ctrl = false)
        {
            return new Event
            {
                type = type,
                button = 0,
                mousePosition = new Vector2(x, y),
                control = ctrl,
                command = ctrl
            };
        }

        private bool MouseDown(TimelineLayoutSnapshot layout, float x, string trackId, bool ctrl = false)
        {
            return HandlePointer(
                Pointer(EventType.MouseDown, x, RowY(layout, trackId), ctrl),
                layout);
        }

        private bool Drag(TimelineLayoutSnapshot layout, float x, float y)
        {
            return HandlePointer(
                Pointer(EventType.MouseDrag, x, y),
                layout);
        }

        private bool MouseUp(TimelineLayoutSnapshot layout)
        {
            return HandlePointer(
                Pointer(EventType.MouseUp, 0f, 0f),
                layout);
        }

        /// <summary>
        /// Routes a synthetic pointer event exactly the way TimelineView does: rawType is
        /// authoritative (Event.type reports Ignore for synthetic MouseDown/MouseUp events).
        /// </summary>
        private bool HandlePointer(Event e, TimelineLayoutSnapshot layout)
        {
            EventType eventType = e.rawType;
            if (eventType == EventType.Ignore || eventType == EventType.Used)
            {
                eventType = e.type;
            }

            return _input.HandleEvent(
                e,
                eventType,
                PlotRect(layout),
                layout,
                false);
        }

        private FaceTrackData Track(string trackId)
        {
            bool found = _session.GetSelectedAnimation().Timeline.TryGetTrack(trackId, out FaceTrackData track);
            Assert.That(found, Is.True, $"Track {trackId} must exist.");
            return track;
        }

        private float TimeOf(string trackId, string keyId)
        {
            FloatKeyframeData blendKey = Track(trackId).BlendShape?.Keys
                .FirstOrDefault(key => key.KeyId == keyId);
            if (blendKey != null)
            {
                return blendKey.Time;
            }

            Vector3KeyframeData transformKey = Track(trackId).Transform.Keys
                .FirstOrDefault(key => key.KeyId == keyId);
            Assert.That(transformKey, Is.Not.Null, $"Key {keyId} must exist.");
            return transformKey.Time;
        }

        private float FloatValueOf(string trackId, string keyId)
        {
            return Track(trackId).BlendShape.Keys.Single(key => key.KeyId == keyId).Value;
        }

        private Vector3 VectorValueOf(string trackId, string keyId)
        {
            return Track(trackId).Transform.Keys.Single(key => key.KeyId == keyId).Value;
        }
    }
}
