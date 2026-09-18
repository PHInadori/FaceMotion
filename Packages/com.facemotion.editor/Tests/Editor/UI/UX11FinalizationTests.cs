using System.Reflection;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
        /// <summary>UX-1 localization and timeline regression coverage.</summary>
    public sealed class UX11FinalizationTests
    {
        private TempFaceMotionAsset _temp;
        private FaceMotionEditorSession _session;
        private AnimationController _animations;
        private TrackController _tracks;
        private KeyframeController _keys;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _temp = new TempFaceMotionAsset();
            _session = new FaceMotionEditorSession();
            _animations = new AnimationController(_session);
            _tracks = new TrackController(_session);
            _keys = new KeyframeController(_session);
            var project = FaceMotionProject.CreateNew();
            AssetDatabase.CreateAsset(project, _temp.AssetPath("UX11"));
            _session.SetActiveProject(project, AssetDatabase.GetAssetPath(project));
            _animations.Add();
            Assert.That(_tracks.AddBlendShapeTrack("Face", "Smile"), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            _temp.Dispose();
            Undo.ClearAll();
        }

        [Test]
        public void InspectorSynchronize_ReloadsTheSelectedKeyAfterAnAbsoluteDrag()
        {
            _session.ViewState.SnapEnabled = false;
            string keyId = _keys.AddKeyAt(0.1f, 0.5f, Vector3.zero, InterpolationType.Linear);
            var panel = new KeyframeInspectorPanel(_session, _keys);
            panel.Synchronize();

            _keys.BeginKeyDrag();
            _keys.UpdateKeyDragAt(180f, 120f, 120f);
            _keys.EndKeyDrag();
            panel.Synchronize();

            var time = (float)typeof(KeyframeInspectorPanel)
                .GetField("_time", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(panel);
            Assert.That(keyId, Is.Not.Empty);
            Assert.That(time, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void TimelineGeometry_ScrolledPlotKeepsKeyHitAndPointerDragInTheSameCoordinateSpace()
        {
            _session.ViewState.SnapEnabled = false;
            Assert.That(_animations.SetDuration(3f), Is.True);
            string keyId = _keys.AddKeyAt(1.5f, 0.5f, Vector3.zero, InterpolationType.Linear);
            var animation = _session.GetSelectedAnimation();
            var layout = TimelineLayoutBuilder.Build(animation, null, 180f, 24f, 1f, 1f, animation.Timeline.Duration);
            float keyX = TimelineGeometry.TimeToPixel(1.5f, layout.ScrollTime, layout.PixelsPerSecond, layout.PlotLeft);

            Assert.That(TimelineHitTest.TryFindKeyAt(layout, layout.PlotLeft, keyX, 35f, out _, out string hitKey, out _), Is.True);
            Assert.That(hitKey, Is.EqualTo(keyId));

            _session.Selection.SetSingle(keyId);
            _keys.BeginKeyDrag();
            _keys.UpdateKeyDragAt(keyX + 60f, keyX, layout.PixelsPerSecond);
            _keys.EndKeyDrag();
            Assert.That(_session.GetSelectedTrack().BlendShape.Keys[0].Time, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void TrackListSnapshot_RemainsEnumerableWhenADeleteMutatesTheTimeline()
        {
            Assert.That(_tracks.AddBlendShapeTrack("Face", "Blink"), Is.True);
            var timeline = _session.GetSelectedAnimation().Timeline;
            var snapshot = TrackListPanel.SnapshotTracks(timeline.Tracks);

            Assert.That(timeline.RemoveTrack(snapshot[0].TrackId), Is.True);

            int renderedRows = 0;
            foreach (var track in snapshot)
            {
                if (track != null)
                {
                    renderedRows++;
                }
            }

            Assert.That(renderedRows, Is.EqualTo(2));
            Assert.That(timeline.Tracks, Has.Count.EqualTo(1));
        }

        [Test]
        public void LayoutAndLocalization_DefaultJapanesePreservesMinimumWorkspaceAndDiagnosticTranslation()
        {
            float left = FaceMotionWindow.CalculateLeftColumnWidth(FaceMotionWindow.MinimumWindowWidth, 1f);
            float preview = FaceMotionWindow.CalculatePreviewHeight(FaceMotionWindow.MinimumWindowHeight - 22f, 1f);

            Assert.That(FaceMotionWindow.MinimumWindowWidth - left - FaceMotionWindow.SplitterWidth, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineWidth));
            Assert.That(FaceMotionWindow.MinimumWindowHeight - 22f - preview - FaceMotionWindow.SplitterWidth - FaceMotionWindow.InspectorHeight, Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineHeight));
            Assert.That(FaceMotionUiText.Get("diagnostics"), Is.EqualTo("診断"));
            Assert.That(FaceMotionUiText.FormatDiagnostic("FM-EXPORT-SUCCEEDED", "AnimationClip exported successfully.", ""), Does.Contain("エクスポートしました"));
        }

        [Test]
        public void LocalizationAudit_PanelLabelsAndDiagnosticsHaveJapaneseDefaultAndEnglishFallback()
        {
            string[] panelKeys =
            {
                "project", "avatar", "animations", "tracks", "keyInspector",
                "animationClipExport", "directIntegration", "preview", "selectAvatarToPreview",
                "exportPathPrompt", "planIntegration", "advancedManualBinding"
            };

            for (int i = 0; i < panelKeys.Length; i++)
            {
                Assert.That(FaceMotionUiText.Get(panelKeys[i]), Is.Not.EqualTo(panelKeys[i]));
                Assert.That(FaceMotionUiText.Get(panelKeys[i], SystemLanguage.English), Is.Not.EqualTo(panelKeys[i]));
            }

            string japanese = FaceMotionUiText.FormatDiagnostic("FM-EXPORT-WRITE-FAILED", "The destination is not writable.", "Check permissions.");
            string english = FaceMotionUiText.FormatDiagnostic("FM-EXPORT-WRITE-FAILED", "The destination is not writable.", "Check permissions.", SystemLanguage.English);
            Assert.That(japanese, Does.Contain("FM-EXPORT-WRITE-FAILED").And.Not.Contain("not writable").And.Not.Contain("permissions"));
            Assert.That(english, Does.Contain("The destination is not writable.").And.Contain("Check permissions."));
        }
    }
}
