using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// M4-4A preview header measurement and localization coverage. The header title
    /// drives the isolation-note start through measured width (no fixed 65f/93f), so
    /// Japanese and English titles must both stay overlap-free at every panel width,
    /// and all 17 preview localization keys must resolve in both languages.
    /// </summary>
    public sealed class M4_4PreviewLayoutLocalizationTests
    {
        private static readonly string[] PreviewKeys =
        {
            "preview",
            "previewIsolation",
            "selectAvatarToPreview",
            "rebuildPreview",
            "startPreview",
            "stopPreview",
            "previewPlay",
            "previewPause",
            "previewStop",
            "stopSceneApply",
            "applyToScene",
            "fitAvatar",
            "resetView",
            "previewControls",
            "previewNoRenderers",
            "contextHelpTooltip",
            "contextHelpPreview",
        };

        private static readonly float[] HeaderWidths = { 537f, 400f, 320f, 180f, 100f };

        [Test]
        public void PreviewLocalizationKeys_ResolveInJapaneseAndEnglish_WithoutKeyFallback()
        {
            foreach (string key in PreviewKeys)
            {
                foreach (SystemLanguage language in new[] { SystemLanguage.Japanese, SystemLanguage.English })
                {
                    string value = FaceMotionUiText.Get(key, language);
                    Assert.That(value, Is.Not.EqualTo(key), $"{key} ({language}) fell back to the raw key");
                    Assert.That(value, Is.Not.Null.And.Not.Empty, $"{key} ({language})");
                    Assert.That(value.Trim(), Is.Not.Empty, $"{key} ({language}) whitespace only");
                }
            }
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Header_TitleDoesNotOverlapIsolation_AtAnyWidth(SystemLanguage language)
        {
            foreach (float width in HeaderWidths)
            {
                PreviewHeaderLayout header = HeaderFor(width, language);
                AssertFinite(header, width, language);
                Assert.That(header.TitleRect.width, Is.GreaterThanOrEqualTo(0f), $"{width} {language}");
                Assert.That(header.TitleRect.xMin, Is.LessThanOrEqualTo(header.TitleRect.xMax), $"{width} {language}");
                Assert.That(header.TitleRect.xMax, Is.LessThanOrEqualTo(header.IsolationRect.x + 0.001f),
                    $"title overlaps isolation at {width} {language}");
            }
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Header_IsolationDoesNotOverlapHelpButton_AtAnyWidth(SystemLanguage language)
        {
            foreach (float width in HeaderWidths)
            {
                PreviewHeaderLayout header = HeaderFor(width, language);
                Assert.That(header.IsolationRect.width, Is.GreaterThanOrEqualTo(0f), $"{width} {language}");
                if (header.IsolationRect.width > 0f)
                {
                    Assert.That(header.IsolationRect.xMax, Is.LessThanOrEqualTo(header.HelpRect.x + 0.001f),
                        $"isolation overlaps help at {width} {language}");
                }
            }
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Header_VeryNarrowWidth_ClampsIsolationWithoutNegativeRect(SystemLanguage language)
        {
            foreach (float width in new[] { 180f, 100f })
            {
                PreviewHeaderLayout header = HeaderFor(width, language);

                Assert.That(header.IsolationRect.width, Is.GreaterThanOrEqualTo(0f), $"{width} {language}");
                Assert.That(header.TitleRect.width, Is.GreaterThanOrEqualTo(0f), $"{width} {language}");
                Assert.That(header.HelpRect.width, Is.EqualTo(PreviewPanel.HeaderHelpButtonWidth), $"{width} {language}");
                Assert.That(header.TitleRect.xMin, Is.LessThanOrEqualTo(header.TitleRect.xMax), $"{width} {language}");
                Assert.That(header.IsolationRect.xMin, Is.LessThanOrEqualTo(header.IsolationRect.xMax + 0.001f),
                    $"{width} {language}");
                AssertFinite(header, width, language);
            }
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Header_NormalWidth_PreservesTitleIsolationHelpOrder(SystemLanguage language)
        {
            PreviewHeaderLayout header = HeaderFor(537f, language);

            Assert.That(header.TitleRect.xMin, Is.LessThanOrEqualTo(header.IsolationRect.x));
            Assert.That(header.IsolationRect.width, Is.GreaterThan(0f));
            Assert.That(header.IsolationRect.xMax, Is.LessThanOrEqualTo(header.HelpRect.x + 0.001f));
            Assert.That(header.HelpRect.width, Is.EqualTo(PreviewPanel.HeaderHelpButtonWidth));
            Assert.That(header.HelpRect.xMax, Is.EqualTo(537f - PreviewPanel.Padding * 2f + PreviewPanel.Padding).Within(0.001f));
        }

        [Test]
        public void Header_MeasuredTitleWidth_ReplacesFixed65Contract()
        {
            const float width = 537f;
            Rect headerRect = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f)).HeaderRect;
            PreviewHeaderLayout japanese = PreviewPanel.CalculateHeaderLayout(headerRect, SystemLanguage.Japanese);
            PreviewHeaderLayout english = PreviewPanel.CalculateHeaderLayout(headerRect, SystemLanguage.English);

            // Isolation starts from the measured title edge plus spacing, not a fixed offset.
            Assert.That(japanese.IsolationRect.x,
                Is.EqualTo(japanese.TitleRect.xMax + PreviewPanel.HeaderSpacing).Within(0.001f));
            Assert.That(english.IsolationRect.x,
                Is.EqualTo(english.TitleRect.xMax + PreviewPanel.HeaderSpacing).Within(0.001f));

            // Title width equals the measurement of each localized title string (fallback path in tests).
            string jpTitle = FaceMotionUiText.Get("preview", SystemLanguage.Japanese);
            string enTitle = FaceMotionUiText.Get("preview", SystemLanguage.English);
            Assert.That(japanese.TitleRect.width, Is.EqualTo(PreviewPanel.MeasureHeaderTextWidth(jpTitle, null)).Within(0.001f));
            Assert.That(english.TitleRect.width, Is.EqualTo(PreviewPanel.MeasureHeaderTextWidth(enTitle, null)).Within(0.001f));

            // The fallback metric is string-length dependent, never the removed 65f constant.
            float shortText = PreviewPanel.EstimateHeaderTextWidth("preview");
            float longText = PreviewPanel.EstimateHeaderTextWidth("preview preview preview");
            Assert.That(longText, Is.GreaterThan(shortText));
            Assert.That(PreviewPanel.EstimateHeaderTextWidth(string.Empty), Is.Zero);
        }

        private static PreviewHeaderLayout HeaderFor(float width, SystemLanguage language)
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f));
            return PreviewPanel.CalculateHeaderLayout(layout.HeaderRect, language);
        }

        private static void AssertFinite(PreviewHeaderLayout header, float width, SystemLanguage language)
        {
            foreach (float value in new[]
                     {
                         header.TitleRect.x, header.TitleRect.y, header.TitleRect.width, header.TitleRect.height,
                         header.IsolationRect.x, header.IsolationRect.y, header.IsolationRect.width, header.IsolationRect.height,
                         header.HelpRect.x, header.HelpRect.y, header.HelpRect.width, header.HelpRect.height,
                     })
            {
                Assert.That(float.IsNaN(value), Is.False, $"NaN at {width} {language}");
                Assert.That(float.IsInfinity(value), Is.False, $"Infinity at {width} {language}");
            }
        }
    }
}
