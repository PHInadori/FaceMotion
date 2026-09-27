using System;
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
    /// <summary>Regression coverage for M1 timeline pointer ownership and plot boundaries.</summary>
    public sealed class M1TimelineInputReliabilityTests
    {
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;
        private TimelineInputHandler _input;
        private TempFaceMotionAsset _temp;
        private int _previousHotControl;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _previousHotControl = GUIUtility.hotControl;
            GUIUtility.hotControl = 0;
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
            GUIUtility.hotControl = _previousHotControl;
            _temp?.Dispose();
            _temp = null;
            Undo.ClearAll();
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void TEST1_KeyAtCurrentPlayheadTime_WinsOverScrub(float time)
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, time);
            _session.SetCurrentTime(time);
            TimelineLayoutSnapshot layout = BuildLayout();

            Assert.That(Route(Pointer(EventType.MouseDown, KeyX(layout, time), RowY(layout, trackId)), layout), Is.True);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
            Assert.That(_session.Selection.Contains(keyId), Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(time).Within(1e-5f));
        }

        [Test]
        public void TEST2_EmptyPlotMouseDown_StartsAndImmediatelyUpdatesScrub()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            AddKey(trackId, 0.8f);
            TimelineLayoutSnapshot layout = BuildLayout();

            Assert.That(Route(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), RowY(layout, trackId)), layout), Is.True);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void TEST3_ScrubDragContinuouslyFollowsMouseAndMouseUpEndsIt()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            Route(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), y), layout);
            Assert.That(Route(Pointer(EventType.MouseDrag, KeyX(layout, 0.75f), y), layout), Is.True);
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(Route(Pointer(EventType.MouseUp, KeyX(layout, 0.75f), y), layout), Is.True);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(GUIUtility.hotControl, Is.Zero);
        }

        [TestCase(30f)]
        [TestCase(60f)]
        public void TEST4_ScrubUsesCanonicalFrameSnapping(float frameRate)
        {
            SetupTimeline(frameRate: frameRate);
            string trackId = AddBlendTrack("Smile");
            _session.ViewState.SnapEnabled = true;
            TimelineLayoutSnapshot layout = BuildLayout();
            float requested = 0.347f;

            Handle(Pointer(EventType.MouseDown, KeyX(layout, requested), RowY(layout, trackId)), layout);

            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(_keys.SnapTime(requested)).Within(1e-5f));
        }

        [Test]
        public void TEST5_LostHotControlScrubMouseUp_EndsWithoutChangingCurrentTime()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            Route(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), y), layout);
            Route(Pointer(EventType.MouseDrag, KeyX(layout, 0.75f), y), layout);
            GUIUtility.hotControl = 0;

            Assert.That(Route(Pointer(EventType.MouseUp, KeyX(layout, 0.75f), y), layout), Is.False);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void TEST6_LostHotControlKeyDrag_CancelsMoveAndClearsGesture()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float x = KeyX(layout, 0.5f);
            float y = RowY(layout, trackId);

            Route(Pointer(EventType.MouseDown, x, y), layout);
            Route(Pointer(EventType.MouseDrag, x + 30f, y), layout);
            GUIUtility.hotControl = 0;
            Route(Pointer(EventType.MouseUp, x + 30f, y), layout);

            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(TimeOf(trackId, keyId), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void TEST7_NextMouseDownRecoversStaleKeyGestureBeforeStartingScrub()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float keyX = KeyX(layout, 0.5f);
            float y = RowY(layout, trackId);

            Handle(Pointer(EventType.MouseDown, keyX, y), layout);
            Handle(Pointer(EventType.MouseDrag, keyX + 30f, y), layout);
            Handle(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), y), layout);

            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
            Assert.That(TimeOf(trackId, keyId), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void TEST8_RapidScrubThenKeyDrag_DoesNotInheritScrubState()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            Handle(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), y), layout);
            Handle(Pointer(EventType.MouseDrag, KeyX(layout, 0.4f), y), layout);
            Handle(Pointer(EventType.MouseUp, KeyX(layout, 0.4f), y), layout);
            Handle(Pointer(EventType.MouseDown, KeyX(layout, 0.5f), y), layout);
            Handle(Pointer(EventType.MouseDrag, KeyX(layout, 0.6f), y), layout);
            Handle(Pointer(EventType.MouseUp, KeyX(layout, 0.6f), y), layout);

            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(TimeOf(trackId, keyId), Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void TEST9_RapidKeyDragThenScrub_DoesNotInheritKeyAnchor()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            Handle(Pointer(EventType.MouseDown, KeyX(layout, 0.5f), y), layout);
            Handle(Pointer(EventType.MouseDrag, KeyX(layout, 0.6f), y), layout);
            Handle(Pointer(EventType.MouseUp, KeyX(layout, 0.6f), y), layout);
            Handle(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), y), layout);

            Assert.That(TimeOf(trackId, keyId), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
            Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void TEST10_SelectedGroupDrag_RemainsIntact()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string a = AddKey(trackId, 0.2f);
            string b = AddKey(trackId, 0.4f);
            string c = AddKey(trackId, 0.6f);
            _session.Selection.SetSelection(new[] { a, b, c });
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            Handle(Pointer(EventType.MouseDown, KeyX(layout, 0.4f), y), layout);
            Handle(Pointer(EventType.MouseDrag, KeyX(layout, 0.5f), y), layout);
            Handle(Pointer(EventType.MouseUp, KeyX(layout, 0.5f), y), layout);

            Assert.That(_session.Selection.KeyIds, Is.EquivalentTo(new[] { a, b, c }));
            Assert.That(TimeOf(trackId, b), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void TEST11_NearbyKeys_SelectNearestWithinHitRadius()
        {
            SetupTimeline();
            foreach (int gapPixels in new[] { 2, 4, 8 })
            {
                string trackId = AddBlendTrack("Dense" + gapPixels);
                TimelineLayoutSnapshot layout = BuildLayout();
                float nearDelta = gapPixels / layout.PixelsPerSecond;
                string first = AddKey(trackId, 0.5f);
                string second = AddKey(trackId, 0.5f + nearDelta);
                layout = BuildLayout();
                Rect plot = PlotRect(layout);
                float firstX = KeyX(layout, 0.5f);
                float secondX = KeyX(layout, 0.5f + nearDelta);
                float y = RowY(layout, trackId);

                Assert.That(TimelineHitTest.TryFindKeyAt(layout, plot, firstX, y, out _, out string exactFirst, out _), Is.True);
                Assert.That(exactFirst, Is.EqualTo(first), gapPixels + " px exact first-key hit");
                Assert.That(TimelineHitTest.TryFindKeyAt(layout, plot, secondX, y, out _, out string exactSecond, out _), Is.True);
                Assert.That(exactSecond, Is.EqualTo(second), gapPixels + " px exact second-key hit");
                Assert.That(TimelineHitTest.TryFindKeyAt(layout, plot, firstX - TimelineHitTest.KeyHitRadiusPixels, y, out _, out string edge, out _), Is.True);
                Assert.That(edge, Is.EqualTo(first), gapPixels + " px hit-radius edge");
                Assert.That(TimelineHitTest.TryFindKeyAt(layout, plot, firstX - TimelineHitTest.KeyHitRadiusPixels - 0.01f, y, out _, out _, out _), Is.False);

                float midpoint = (firstX + secondX) * 0.5f;
                string expectedTie = string.CompareOrdinal(first, second) <= 0 ? first : second;
                Assert.That(TimelineHitTest.TryFindKeyAt(layout, plot, midpoint, y, out _, out string midpointHit, out _), Is.True);
                Assert.That(midpointHit, Is.EqualTo(expectedTie), gapPixels + " px midpoint tie must be stable.");
            }
        }

        [Test]
        public void TEST12_KeyCannotCrossHitAnotherTrackRow()
        {
            SetupTimeline();
            string firstTrack = AddBlendTrack("Smile");
            string secondTrack = AddBlendTrack("Blink");
            AddKey(firstTrack, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();

            Assert.That(TimelineHitTest.TryFindKeyAt(layout, PlotRect(layout), KeyX(layout, 0.5f), RowY(layout, secondTrack), out _, out _, out _), Is.False);
        }

        [Test]
        public void TEST13_TimeZeroKeyCannotHitFromTrackLabelArea()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            AddKey(trackId, 0f);
            TimelineLayoutSnapshot layout = BuildLayout();
            Rect plot = PlotRect(layout);

            Assert.That(TimelineHitTest.TryFindKeyAt(layout, plot, plot.xMin - 1f, RowY(layout, trackId), out _, out _, out _), Is.False);
        }

        [Test]
        public void TEST14_DurationEndpointKeyRemainsHittable()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 1f);
            TimelineLayoutSnapshot layout = BuildLayout();

            Assert.That(TimelineHitTest.TryFindKeyAt(layout, PlotRect(layout), KeyX(layout, 1f), RowY(layout, trackId), out _, out string hit, out _), Is.True);
            Assert.That(hit, Is.EqualTo(keyId));
        }

        [Test]
        public void TEST15_TimeDomainDrawingGeometryIsClippedToPlot()
        {
            var plot = new Rect(180f, 24f, 400f, 120f);
            Rect unselected = TimelineRenderer.GetKeyMarkerRect(300f, 80f, false);
            Rect selected = TimelineRenderer.GetKeyMarkerRect(300f, 80f, true);
            Rect leftSource = TimelineRenderer.GetKeyMarkerRect(plot.xMin, 80f, false);
            Rect rightSource = TimelineRenderer.GetKeyMarkerRect(plot.xMax, 80f, true);
            Rect left = TimelineRenderer.ClipTimeDomainRect(leftSource, plot);
            Rect right = TimelineRenderer.ClipTimeDomainRect(rightSource, plot);
            Rect cursor = TimelineRenderer.ClipTimeDomainRect(new Rect(179.5f, 24f, 1f, 120f), plot);

            Assert.That(unselected.size, Is.EqualTo(new Vector2(9f, 9f)));
            Assert.That(selected.size, Is.EqualTo(new Vector2(11f, 11f)));
            Assert.That(unselected.center, Is.EqualTo(new Vector2(300f, 80f)));
            Assert.That(selected.center, Is.EqualTo(new Vector2(300f, 80f)));
            Assert.That(leftSource.center.x, Is.EqualTo(plot.xMin));
            Assert.That(rightSource.center.x, Is.EqualTo(plot.xMax));
            Assert.That(left.xMin, Is.EqualTo(plot.xMin));
            Assert.That(left.xMax, Is.EqualTo(plot.xMin + 4.5f));
            Assert.That(right.xMin, Is.EqualTo(plot.xMax - 5.5f));
            Assert.That(right.xMax, Is.EqualTo(plot.xMax));
            Assert.That(cursor.xMin, Is.EqualTo(plot.xMin));
            Assert.That(cursor.xMax, Is.EqualTo(plot.xMin + 0.5f));
        }

        [Test]
        public void TEST16_StressScrubThenKeyDrag_AlwaysStartsTheNextGestureImmediately()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.4f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            for (int i = 0; i < 50; i++)
            {
                Assert.That(Route(Pointer(EventType.MouseDown, KeyX(layout, 0.2f), y), layout), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
                Route(Pointer(EventType.MouseDrag, KeyX(layout, 0.3f), y), layout);
                Route(Pointer(EventType.MouseUp, KeyX(layout, 0.3f), y), layout);
                AssertCleanPointerState();

                float keyTime = TimeOf(trackId, keyId);
                float keyX = KeyX(layout, keyTime);
                Assert.That(Route(Pointer(EventType.MouseDown, keyX, y), layout), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
                Route(Pointer(EventType.MouseDrag, keyX + 1f, y), layout);
                Route(Pointer(EventType.MouseUp, keyX + 1f, y), layout);
                AssertCleanPointerState();
            }
        }

        [Test]
        public void TEST17_StressKeyDragThenScrub_AlwaysStartsTheNextGestureImmediately()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.4f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            for (int i = 0; i < 50; i++)
            {
                float keyTime = TimeOf(trackId, keyId);
                float keyX = KeyX(layout, keyTime);
                Assert.That(Route(Pointer(EventType.MouseDown, keyX, y), layout), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
                Route(Pointer(EventType.MouseDrag, keyX + 1f, y), layout);
                Route(Pointer(EventType.MouseUp, keyX + 1f, y), layout);
                AssertCleanPointerState();

                Assert.That(Route(Pointer(EventType.MouseDown, KeyX(layout, 0.2f), y), layout), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
                Route(Pointer(EventType.MouseDrag, KeyX(layout, 0.3f), y), layout);
                Route(Pointer(EventType.MouseUp, KeyX(layout, 0.3f), y), layout);
                AssertCleanPointerState();
            }
        }

        [Test]
        public void TEST18_StressLostHotControl_HandoffRecoversOnTheFirstNextMouseDown()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float y = RowY(layout, trackId);

            for (int i = 0; i < 20; i++)
            {
                Route(Pointer(EventType.MouseDown, KeyX(layout, 0.2f), y), layout);
                Route(Pointer(EventType.MouseDrag, KeyX(layout, 0.3f), y), layout);
                if ((i & 1) == 0)
                {
                    GUIUtility.hotControl = 0;
                }

                Route(Pointer(EventType.MouseUp, KeyX(layout, 0.3f), y), layout);
                AssertCleanPointerState();

                float keyTime = TimeOf(trackId, keyId);
                float keyX = KeyX(layout, keyTime);
                Route(Pointer(EventType.MouseDown, keyX, y), layout);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
                Route(Pointer(EventType.MouseDrag, keyX + 1f, y), layout);
                if ((i & 1) != 0)
                {
                    GUIUtility.hotControl = 0;
                }

                Route(Pointer(EventType.MouseUp, keyX + 1f, y), layout);
                AssertCleanPointerState();
            }
        }

        [Test]
        public void TEST19_StressRepeatedNormalKeyHit_AlwaysBeginsKeyMove()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float x = KeyX(layout, 0.5f) + TimelineHitTest.KeyHitRadiusPixels - 0.25f;
            float y = RowY(layout, trackId);

            for (int i = 0; i < 50; i++)
            {
                Assert.That(Route(Pointer(EventType.MouseDown, x, y), layout), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
                Route(Pointer(EventType.MouseUp, x, y), layout);
                AssertCleanPointerState();
            }
        }

        [Test]
        public void TEST20_FocusedNumericField_FirstTimelineKeyMouseDownStartsDrag()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            bool previousEditing = EditorGUIUtility.editingTextField;
            int previousKeyboardControl = GUIUtility.keyboardControl;
            try
            {
                EditorGUIUtility.editingTextField = true;
                GUIUtility.keyboardControl = 1234;
                float x = KeyX(layout, 0.5f);
                float y = RowY(layout, trackId);

                Assert.That(Route(Pointer(EventType.MouseDown, x, y), layout, true), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
                Assert.That(EditorGUIUtility.editingTextField, Is.False);
                Assert.That(GUIUtility.keyboardControl, Is.Zero);
                Assert.That(Route(Pointer(EventType.MouseDrag, x + 20f, y), layout, true), Is.True);
                Assert.That(TimeOf(trackId, keyId), Is.GreaterThan(0.5f));
            }
            finally
            {
                EditorGUIUtility.editingTextField = previousEditing;
                GUIUtility.keyboardControl = previousKeyboardControl;
            }
        }

        [Test]
        public void TEST21_FocusedNumericField_FirstEmptyTimelineMouseDownStartsScrub()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            AddKey(trackId, 0.8f);
            TimelineLayoutSnapshot layout = BuildLayout();
            bool previousEditing = EditorGUIUtility.editingTextField;
            int previousKeyboardControl = GUIUtility.keyboardControl;
            try
            {
                EditorGUIUtility.editingTextField = true;
                GUIUtility.keyboardControl = 1234;

                Assert.That(Route(Pointer(EventType.MouseDown, KeyX(layout, 0.25f), RowY(layout, trackId)), layout, true), Is.True);
                Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.Scrub));
                Assert.That(_session.ViewState.CurrentTime, Is.EqualTo(0.25f).Within(1e-5f));
                Assert.That(EditorGUIUtility.editingTextField, Is.False);
                Assert.That(GUIUtility.keyboardControl, Is.Zero);
            }
            finally
            {
                EditorGUIUtility.editingTextField = previousEditing;
                GUIUtility.keyboardControl = previousKeyboardControl;
            }
        }

        [Test]
        public void TEST22_PointerControlId_FrozenWhileGestureOwned()
        {
            Assert.That(
                TimelineView.ResolvePointerControlId(7011, 0, 0),
                Is.EqualTo(7011),
                "no pointer capture yet -> adopt the fresh allocation");

            Assert.That(
                TimelineView.ResolvePointerControlId(7777, 7011, 7011),
                Is.EqualTo(7011),
                "this view owns the gesture -> the captured ID must survive an allocation shift");

            Assert.That(
                TimelineView.ResolvePointerControlId(7777, 4242, 7011),
                Is.EqualTo(7777),
                "a foreign hot control -> adopt the fresh allocation");
        }

        [Test]
        public void TEST23_MouseUpAfterControlIdShift_KeepsOwnershipAndSelection()
        {
            SetupTimeline();
            string trackId = AddBlendTrack("Smile");
            string keyId = AddKey(trackId, 0.5f);
            TimelineLayoutSnapshot layout = BuildLayout();
            float x = KeyX(layout, 0.5f);
            float y = RowY(layout, trackId);

            int pointerId = TimelineView.ResolvePointerControlId(7011, GUIUtility.hotControl, 0);
            Assert.That(Route(Pointer(EventType.MouseDown, x, y), layout, false, pointerId), Is.True);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.MoveKeys));
            Assert.That(_session.Selection.Contains(keyId), Is.True);
            Assert.That(GUIUtility.hotControl, Is.EqualTo(pointerId));

            int shiftedId = TimelineView.ResolvePointerControlId(7777, GUIUtility.hotControl, pointerId);
            Assert.That(shiftedId, Is.EqualTo(pointerId), "capture must not rebind when earlier control counts shift");

            Assert.That(Route(Pointer(EventType.MouseUp, x, y), layout, false, shiftedId), Is.True);
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(GUIUtility.hotControl, Is.Zero);
            Assert.That(_session.Selection.Contains(keyId), Is.True, "a completed click must keep its selection");
        }

        private void SetupTimeline(float duration = 1f, float frameRate = 60f)
        {
            var project = FaceMotionProject.CreateNew();
            EditorUtility.SetDirty(project);
            AssetDatabase.CreateAsset(project, _temp.AssetPath("M1TimelineInputReliability"));
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            _animations.Add();
            _session.GetSelectedAnimation().Timeline.Duration = duration;
            _session.GetSelectedAnimation().Timeline.FrameRate = frameRate;
            _session.ViewState.SnapEnabled = false;
        }

        private string AddBlendTrack(string name)
        {
            _tracks.AddBlendShapeTrack("Face", name);
            return _session.SelectedTrackId;
        }

        private string AddKey(string trackId, float time)
        {
            _tracks.Select(trackId);
            string keyId = _keys.AddKeyAt(time, 50f, Vector3.zero, InterpolationType.Linear);
            Assert.That(keyId, Is.Not.Null);
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

        private static Rect PlotRect(TimelineLayoutSnapshot layout)
        {
            return new Rect(layout.PlotLeft, 0f, 800f, 300f);
        }

        private static float KeyX(TimelineLayoutSnapshot layout, float time)
        {
            return TimelineGeometry.TimeToPixel(time, layout.ScrollTime, layout.PixelsPerSecond, layout.PlotLeft);
        }

        private static float RowY(TimelineLayoutSnapshot layout, string trackId)
        {
            return layout.Rows.Single(row => string.Equals(row.Track.TrackId, trackId, StringComparison.Ordinal)).Y + TimelineGeometry.RowHeight * 0.5f;
        }

        private static Event Pointer(EventType type, float x, float y)
        {
            return new Event { type = type, button = 0, mousePosition = new Vector2(x, y) };
        }

        private bool Handle(Event e, TimelineLayoutSnapshot layout)
        {
            EventType eventType = e.rawType;
            if (eventType == EventType.Ignore || eventType == EventType.Used)
            {
                eventType = e.type;
            }

            return _input.HandleEvent(e, eventType, PlotRect(layout), layout, false);
        }

        private bool Route(Event e, TimelineLayoutSnapshot layout, bool textControlOwnsKeyboard = false, int controlId = 7011)
        {
            return TimelineView.RoutePointerEvent(
                e,
                controlId,
                new Rect(0f, 0f, 980f, 300f),
                _input,
                _session.ViewState,
                PlotRect(layout),
                layout,
                textControlOwnsKeyboard);
        }

        private void AssertCleanPointerState()
        {
            Assert.That(_session.ViewState.DragMode, Is.EqualTo(TimelineDragMode.None));
            Assert.That(GUIUtility.hotControl, Is.Zero);
        }

        private float TimeOf(string trackId, string keyId)
        {
            return _session.GetSelectedAnimation().Timeline.TryGetTrack(trackId, out FaceTrackData track)
                ? track.BlendShape.Keys.Single(key => key.KeyId == keyId).Time
                : throw new AssertionException("Track was not found.");
        }
    }
}
