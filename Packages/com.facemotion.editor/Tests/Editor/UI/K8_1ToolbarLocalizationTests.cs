using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Window;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Section 9 toolbar/guidance localization. Labels must never be clipped by the
    /// English-era fixed widths: buttons take their preferred (CalcSize) width, floors
    /// keep the old constants as minimums, and CalculateToolbarWidths shrinks
    /// proportionally only when a narrow window cannot fit the minimums. EditorStyles
    /// getters are unusable in batch test runs, so text metrics are probed with a
    /// hand-built 12px style; production DrawToolbar measures with the real toolbar
    /// styles inside OnGUI, where CalcSize can never be narrower than the label text.
    /// </summary>
    public sealed class K8_1ToolbarLocalizationTests
    {
        private static readonly string[] ToolbarKeys =
        {
            "file", "avatar", "play", "pause", "fit", "zoomOutLabel", "zoomInLabel",
            "fileTooltip", "avatarTooltip", "playTooltip", "fitTooltip", "zoomOut", "zoomIn",
        };

        private static readonly string[] GuidanceKeys =
        {
            "guidanceStepFormat", "guidanceNextAction", "guidanceCompleteBadge",
        };

        private static readonly string[] ButtonLabelKeys =
        {
            "file", "avatar", "play", "pause", "fit", "zoomOutLabel", "zoomInLabel",
        };

        [Test]
        public void ToolbarAndGuidanceKeys_ExistInBothLanguages()
        {
            foreach (string key in ToolbarKeys)
            {
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.Japanese), Is.Not.EqualTo(key), key);
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.English), Is.Not.EqualTo(key), key);
            }

            foreach (string key in GuidanceKeys)
            {
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.Japanese), Is.Not.EqualTo(key), key);
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.English), Is.Not.EqualTo(key), key);
            }
        }

        [Test]
        public void ToolbarButtonLabels_Localize_TextDiffersBetweenLanguages()
        {
            foreach (string key in ButtonLabelKeys)
            {
                Assert.That(
                    FaceMotionUiText.Get(key, SystemLanguage.Japanese),
                    Is.Not.EqualTo(FaceMotionUiText.Get(key, SystemLanguage.English)),
                    key);
            }
        }

        [Test]
        public void JapaneseLabels_OverflowTheOldFixedWidths_ThatUsedToClip()
        {
            // The old constants (fit 42, pause 48) clipped Japanese text; DrawToolbar now
            // derives every width from CalcSize with those constants as floors, and a
            // 12px probe style confirms the Japanese labels are materially wider.
            GUIStyle probe = CreateProbeStyle();
            float fitJapanese = probe.CalcSize(
                new GUIContent(FaceMotionUiText.Get("fit", SystemLanguage.Japanese))).x;
            Assert.That(fitJapanese, Is.GreaterThan(42f), "Japanese 'fit' width=" + fitJapanese);

            float pauseJapanese = probe.CalcSize(
                new GUIContent(FaceMotionUiText.Get("pause", SystemLanguage.Japanese))).x;
            Assert.That(pauseJapanese, Is.GreaterThan(48f), "Japanese 'pause' width=" + pauseJapanese);
        }

        [Test]
        public void CalculateToolbarWidths_RoomyWindow_KeepsPreferredWidths()
        {
            var preferred = new[] { 60f, 80f, 50f, 45f, 60f, 60f };
            var minimums = new[] { 54f, 70f, 48f, 42f, 54f, 54f };

            float[] widths = FaceMotionWindow.CalculateToolbarWidths(preferred, minimums, 1200f);

            Assert.That(widths, Is.EqualTo(preferred));
        }

        [Test]
        public void CalculateToolbarWidths_TightWindow_FitsExactlyAtAvailableKeepingMinimums()
        {
            var preferred = new[] { 100f, 120f, 80f, 70f, 90f, 90f };
            var minimums = new[] { 54f, 70f, 48f, 42f, 54f, 54f };
            const float available = 400f;

            float[] widths = FaceMotionWindow.CalculateToolbarWidths(preferred, minimums, available);

            float sum = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                Assert.That(widths[i], Is.GreaterThanOrEqualTo(minimums[i]), "element " + i);
                sum += widths[i];
            }

            Assert.That(sum, Is.EqualTo(available).Within(0.01f));
        }

        [Test]
        public void CalculateToolbarWidths_ExtremelyNarrow_ScalesProportionallyWithoutNegatives()
        {
            var preferred = new[] { 100f, 120f, 80f, 70f, 90f, 90f };
            var minimums = new[] { 54f, 70f, 48f, 42f, 54f, 54f };
            const float available = 100f;

            float[] widths = FaceMotionWindow.CalculateToolbarWidths(preferred, minimums, available);

            float sum = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                Assert.That(widths[i], Is.GreaterThan(0f), "element " + i);
                sum += widths[i];
            }

            Assert.That(sum, Is.EqualTo(available).Within(0.01f));
        }

        [Test]
        public void CalculateToolbarWidths_EdgeCases_AreSafe()
        {
            Assert.That(FaceMotionWindow.CalculateToolbarWidths(new float[0], new float[0], 500f), Is.Empty);
            Assert.That(
                FaceMotionWindow.CalculateToolbarWidths(null, null, 500f),
                Is.Empty);

            float[] zeroAvailable = FaceMotionWindow.CalculateToolbarWidths(
                new[] { 60f, 80f },
                new[] { 54f, 70f },
                -10f);
            Assert.That(zeroAvailable, Is.EqualTo(new[] { 0f, 0f }));

            float[] negativePreferred = FaceMotionWindow.CalculateToolbarWidths(
                new[] { -10f, 0f },
                new[] { 50f, 40f },
                500f);
            Assert.That(negativePreferred, Is.EqualTo(new[] { 50f, 40f }));

            Assert.That(
                () => FaceMotionWindow.CalculateToolbarWidths(new[] { 10f }, new[] { 5f, 6f }, 100f),
                Throws.ArgumentException);
        }

        private static GUIStyle CreateProbeStyle()
        {
            var style = new GUIStyle
            {
                fontSize = 12,
                padding = new RectOffset(4, 4, 0, 0),
                wordWrap = false,
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
            };
            return style;
        }
    }
}
