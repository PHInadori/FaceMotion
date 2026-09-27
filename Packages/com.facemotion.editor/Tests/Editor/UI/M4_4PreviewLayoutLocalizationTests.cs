using System.Collections.Generic;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// M4-4 preview layout coverage. M4-4A: header title drives the isolation-note start
    /// through measured width (no fixed 65f/93f), and all 17 preview localization keys
    /// must resolve in both languages. M4-4B: controls height is derived from the rows of
    /// the actually visible controls (inactive = action row only; active adds the playback
    /// row), wrapping grows the required height, and the hint uses a named threshold.
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

        private static readonly float[] ControlWidths = { 537f, 500f, 400f, 320f, 180f };

        [Test]
        public void Controls_InactiveNormalWidth_UsesSingleRowHeight()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 537f, 400f),
                false, SystemLanguage.Japanese, false);
            PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, false);

            // One visible action row: height is exactly that row plus bottom padding, never the legacy 48f.
            Assert.That(layout.ControlsRect.height,
                Is.EqualTo(PreviewPanel.ControlHeight + PreviewPanel.ControlSpacing).Within(0.001f));
            Assert.That(layout.ControlsRect.height, Is.LessThan(PreviewPanel.ControlsHeight));
            Assert.That(controls.StopPreview.y, Is.EqualTo(controls.Start.y));
            Assert.That(controls.SceneApply.y, Is.EqualTo(controls.Start.y));

            // Undrawn playback controls must not contribute to the reserved height.
            Assert.That(controls.Play.width, Is.Zero);
            Assert.That(controls.Reset.width, Is.Zero);
            Assert.That(controls.Hint.width, Is.Zero);

            Assert.That(layout.RenderRect.y,
                Is.EqualTo(layout.ControlsRect.yMax + PreviewPanel.Padding).Within(0.001f));
        }

        [Test]
        public void Controls_ActiveNormalWidth_HeightMatchesVisibleRows()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 537f, 400f),
                false, SystemLanguage.Japanese, true);
            PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, true);

            float bottom = 0f;
            foreach (Rect rect in VisibleControlRects(controls))
            {
                bottom = Mathf.Max(bottom, rect.yMax);
            }

            Assert.That(bottom, Is.GreaterThan(0f));
            Assert.That(layout.ControlsRect.yMax, Is.EqualTo(bottom + PreviewPanel.ControlSpacing).Within(0.001f));
            foreach (Rect rect in VisibleControlRects(controls))
            {
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.yMax + 0.001f));
            }

            Assert.That(layout.RenderRect.y, Is.GreaterThanOrEqualTo(layout.ControlsRect.yMax));
        }

        [Test]
        public void Controls_NarrowWidth_WrapIncreasesRequiredHeight()
        {
            float normalHeight = 0f;
            foreach (float width in ControlWidths)
            {
                PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f),
                    false, SystemLanguage.Japanese, true);
                PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, true);

                Assert.That(layout.ControlsRect.height, Is.GreaterThan(0f), $"{width}");
                foreach (Rect rect in VisibleControlRects(controls))
                {
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(layout.ControlsRect.xMin - 0.001f), $"{width}");
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(layout.ControlsRect.xMax + 0.001f), $"{width}");
                    Assert.That(rect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.yMax + 0.001f), $"{width}");
                }

                if (width == 537f)
                {
                    normalHeight = layout.ControlsRect.height;
                }
            }

            PreviewPanelLayout narrow = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 180f, 400f),
                false, SystemLanguage.Japanese, true);
            Assert.That(narrow.ControlsRect.height, Is.GreaterThan(normalHeight));
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Controls_JapaneseAndEnglish_StayInsideBounds(SystemLanguage language)
        {
            foreach (float width in new[] { 537f, 400f, 180f })
            {
                PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f),
                    false, language, true);
                PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, true, language);

                foreach (Rect rect in VisibleControlRects(controls))
                {
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(layout.ControlsRect.xMax + 0.001f),
                        $"{width} {language}");
                    Assert.That(rect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.yMax + 0.001f),
                        $"{width} {language}");
                }

                Assert.That(layout.RenderRect.y, Is.GreaterThanOrEqualTo(layout.ControlsRect.yMax),
                    $"{width} {language}");
            }
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Controls_PairwiseNoOverlap(SystemLanguage language)
        {
            foreach (float width in ControlWidths)
            {
                PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f),
                    false, language, true);
                PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, true, language);
                Rect[] rects = VisibleControlRects(controls);
                for (int i = 0; i < rects.Length; i++)
                {
                    for (int j = i + 1; j < rects.Length; j++)
                    {
                        if (Mathf.Abs(rects[i].y - rects[j].y) > 0.001f)
                        {
                            continue;
                        }

                        bool separated = rects[i].xMax <= rects[j].xMin + 0.001f
                                         || rects[j].xMax <= rects[i].xMin + 0.001f;
                        Assert.That(separated, Is.True,
                            $"overlap at width {width} {language}: rect {i} {rects[i]} vs rect {j} {rects[j]}");
                    }
                }
            }
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void Controls_OrderPreservedAfterWrap(SystemLanguage language)
        {
            foreach (float width in ControlWidths)
            {
                PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f),
                    false, language, true);
                PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, true, language);
                Rect[] sequence =
                {
                    controls.Start, controls.StopPreview, controls.SceneApply, controls.Play,
                    controls.PlaybackStop, controls.Fit, controls.Reset,
                };

                for (int i = 1; i < sequence.Length; i++)
                {
                    bool laterRow = sequence[i].y > sequence[i - 1].y + 0.001f;
                    bool sameRowFurtherRight = Mathf.Abs(sequence[i].y - sequence[i - 1].y) <= 0.001f
                                               && sequence[i].x > sequence[i - 1].x + 0.001f;
                    Assert.That(laterRow || sameRowFurtherRight, Is.True,
                        $"logical order broken between {i - 1} and {i} at width {width} {language}");
                }
            }
        }

        [Test]
        public void HintThreshold_UsesNamedConstant()
        {
            Assert.That(PreviewPanel.PreviewHintMinimumWidth, Is.EqualTo(80f));

            // Derive the exact end of the placed controls so the residual space is real.
            PreviewControlsLayout reference = PreviewPanel.CalculateControlsLayout(new Rect(0f, 0f, 600f, 0f));
            Assert.That(reference.Hint.width, Is.GreaterThan(0f), "reference width should show the hint");
            float contentEnd = 600f - reference.Hint.width;

            PreviewControlsLayout hidden = PreviewPanel.CalculateControlsLayout(
                new Rect(0f, 0f, contentEnd + PreviewPanel.PreviewHintMinimumWidth - 1f, 0f));
            PreviewControlsLayout visible = PreviewPanel.CalculateControlsLayout(
                new Rect(0f, 0f, contentEnd + PreviewPanel.PreviewHintMinimumWidth, 0f));

            Assert.That(hidden.Hint.width, Is.Zero, "one below the threshold must hide the hint");
            Assert.That(visible.Hint.width,
                Is.EqualTo(PreviewPanel.PreviewHintMinimumWidth).Within(0.001f),
                "exactly at the threshold must show the hint");
        }

        [TestCase(180f)]
        [TestCase(100f)]
        [TestCase(50f)]
        [TestCase(0f)]
        public void Controls_ExtremeWidths_NoNegativeOrNonFiniteRects(float width)
        {
            foreach (bool previewActive in new[] { false, true })
            {
                string context = $"width {width} previewActive={previewActive}";
                PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f),
                    false, SystemLanguage.Japanese, previewActive);
                PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(
                    layout.ControlsRect, previewActive);

                AssertRect(layout.HeaderRect, context + " header");
                AssertRect(layout.ControlsRect, context + " controls");
                AssertRect(layout.RenderRect, context + " render");
                foreach (Rect rect in VisibleControlRects(controls))
                {
                    AssertRect(rect, context);
                }

                Assert.That(layout.HeaderRect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.y + 0.001f), context);
                Assert.That(layout.ControlsRect.yMax, Is.LessThanOrEqualTo(layout.RenderRect.y + 0.001f), context);
            }
        }

        [Test]
        public void Controls_NoAvatar_UsesZeroHeight()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 537f, 400f),
                false, SystemLanguage.Japanese, false, false);

            // No control is drawn without an avatar, so no action row may be reserved.
            Assert.That(layout.ControlsRect.height, Is.Zero);
            Assert.That(layout.ControlsRect.height,
                Is.Not.EqualTo(PreviewPanel.ControlHeight + PreviewPanel.ControlSpacing),
                "the no-avatar state must not reserve the invisible 24f action row");
            Assert.That(layout.ControlsRect.width, Is.GreaterThanOrEqualTo(0f));

            // Render starts right below the (zero-height) controls box, existing padding contract.
            Assert.That(layout.ControlsRect.y,
                Is.EqualTo(layout.HeaderRect.yMax + PreviewPanel.Padding).Within(0.001f));
            Assert.That(layout.RenderRect.y,
                Is.EqualTo(layout.ControlsRect.yMax + PreviewPanel.Padding).Within(0.001f));
            Assert.That(layout.RenderRect.height, Is.GreaterThan(0f));

            AssertRect(layout.HeaderRect, "no-avatar header");
            AssertRect(layout.ControlsRect, "no-avatar controls");
            AssertRect(layout.RenderRect, "no-avatar render");
        }

        [Test]
        public void Controls_NoAvatar_HelpOpenStillUsesZeroHeight()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 537f, 400f),
                true, SystemLanguage.Japanese, false, false);

            Assert.That(layout.ControlsRect.height, Is.Zero);
            Assert.That(layout.ControlsRect.height,
                Is.Not.EqualTo(PreviewPanel.ControlHeight + PreviewPanel.ControlSpacing));

            float helpHeight = PreviewPanel.HelpHeight(layout.HeaderRect.width, SystemLanguage.Japanese);
            float expectedControlsY = layout.HeaderRect.yMax + PreviewPanel.Padding + helpHeight + PreviewPanel.Padding;
            Assert.That(layout.ControlsRect.y, Is.EqualTo(expectedControlsY).Within(0.001f));
            Assert.That(layout.RenderRect.y,
                Is.EqualTo(layout.ControlsRect.yMax + PreviewPanel.Padding).Within(0.001f));

            Assert.That(layout.HeaderRect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.y + 0.001f));
            Assert.That(layout.ControlsRect.yMax, Is.LessThanOrEqualTo(layout.RenderRect.y + 0.001f));
            AssertRect(layout.ControlsRect, "no-avatar help-open controls");
            AssertRect(layout.RenderRect, "no-avatar help-open render");
        }

        [Test]
        public void Controls_SelectedInactive_StillUsesSingleRowHeight()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 537f, 400f),
                false, SystemLanguage.Japanese, false, true);
            PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, false);

            Assert.That(layout.ControlsRect.height,
                Is.EqualTo(PreviewPanel.ControlHeight + PreviewPanel.ControlSpacing).Within(0.001f));
            Assert.That(controls.Start.y, Is.EqualTo(controls.StopPreview.y));
            Assert.That(controls.SceneApply.y, Is.EqualTo(controls.StopPreview.y));
            Assert.That(controls.Play.width, Is.Zero);
            Assert.That(layout.RenderRect.y, Is.GreaterThanOrEqualTo(layout.ControlsRect.yMax));
        }

        [Test]
        public void Controls_SelectedActive_StillUsesVisibleRows()
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, 537f, 400f),
                false, SystemLanguage.Japanese, true, true);
            PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect, true);

            float bottom = 0f;
            foreach (Rect rect in VisibleControlRects(controls))
            {
                bottom = Mathf.Max(bottom, rect.yMax);
            }

            Assert.That(bottom, Is.GreaterThan(layout.ControlsRect.y));
            Assert.That(layout.ControlsRect.yMax,
                Is.EqualTo(bottom + PreviewPanel.ControlSpacing).Within(0.001f));
            Assert.That(layout.RenderRect.y, Is.GreaterThanOrEqualTo(layout.ControlsRect.yMax));
        }

        [TestCase(537f)]
        [TestCase(180f)]
        [TestCase(100f)]
        [TestCase(50f)]
        [TestCase(0f)]
        public void NoAvatar_ExtremeWidths_RemainFinite(float width)
        {
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(new Rect(0f, 0f, width, 400f),
                false, SystemLanguage.Japanese, false, false);

            Assert.That(layout.ControlsRect.height, Is.Zero, $"{width}");
            AssertRect(layout.HeaderRect, $"no-avatar header at {width}");
            AssertRect(layout.ControlsRect, $"no-avatar controls at {width}");
            AssertRect(layout.RenderRect, $"no-avatar render at {width}");
            Assert.That(layout.HeaderRect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.y + 0.001f), $"{width}");
            Assert.That(layout.ControlsRect.yMax, Is.LessThanOrEqualTo(layout.RenderRect.y + 0.001f), $"{width}");
        }

        private static Rect[] VisibleControlRects(PreviewControlsLayout controls)
        {
            var rects = new List<Rect>
            {
                controls.Start,
                controls.StopPreview,
                controls.SceneApply,
                controls.Play,
                controls.PlaybackStop,
                controls.Fit,
                controls.Reset,
            };

            if (controls.Hint.width > 0f)
            {
                rects.Add(controls.Hint);
            }

            return rects.ToArray();
        }

        private static void AssertRect(Rect rect, string context)
        {
            foreach (float value in new[] { rect.x, rect.y, rect.width, rect.height })
            {
                Assert.That(float.IsNaN(value), Is.False, $"NaN in {context}: {rect}");
                Assert.That(float.IsInfinity(value), Is.False, $"Infinity in {context}: {rect}");
                Assert.That(value, Is.GreaterThanOrEqualTo(0f), $"negative value in {context}: {rect}");
            }
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
