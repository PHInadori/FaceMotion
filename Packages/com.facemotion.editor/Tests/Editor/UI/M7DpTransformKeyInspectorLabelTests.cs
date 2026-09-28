using FaceMotion.Editor.UI.Panels;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class M7DpTransformKeyInspectorLabelTests
    {
        private const string PositionHeaderKey = "transformPositionHeader";
        private const string RotationHeaderKey = "transformRotationHeader";
        private const string ScaleHeaderKey = "transformScaleHeader";

        [Test]
        public void TransformKeyInspector_Position_UsesLocalizedLocalPositionLabel()
        {
            string ja = KeyframeInspectorPanel.GetTransformValueHeader(
                TrackKind.TransformPosition,
                SystemLanguage.Japanese);
            string en = KeyframeInspectorPanel.GetTransformValueHeader(
                TrackKind.TransformPosition,
                SystemLanguage.English);

            Assert.That(ja, Is.EqualTo("位置（ローカル） [m]"));
            Assert.That(en, Is.EqualTo("Position (Local) [m]"));
        }

        [Test]
        public void TransformKeyInspector_Rotation_UsesLocalizedLocalRotationLabel()
        {
            string ja = KeyframeInspectorPanel.GetTransformValueHeader(
                TrackKind.TransformRotation,
                SystemLanguage.Japanese);
            string en = KeyframeInspectorPanel.GetTransformValueHeader(
                TrackKind.TransformRotation,
                SystemLanguage.English);

            Assert.That(ja, Is.EqualTo("回転（ローカル） [°]"));
            Assert.That(en, Is.EqualTo("Rotation (Local) [°]"));
        }

        [Test]
        public void TransformKeyInspector_Scale_UsesLocalizedLocalScaleLabel()
        {
            string ja = KeyframeInspectorPanel.GetTransformValueHeader(
                TrackKind.TransformScale,
                SystemLanguage.Japanese);
            string en = KeyframeInspectorPanel.GetTransformValueHeader(
                TrackKind.TransformScale,
                SystemLanguage.English);

            Assert.That(ja, Is.EqualTo("スケール（ローカル）"));
            Assert.That(en, Is.EqualTo("Scale (Local)"));
        }

        [Test]
        public void TransformKeyInspector_Headers_DoNotFallBackToRawLocalizationKeys()
        {
            foreach (var language in new[]
                     {
                         SystemLanguage.Japanese,
                         SystemLanguage.English
                     })
            {
                Assert.That(
                    KeyframeInspectorPanel.GetTransformValueHeader(
                        TrackKind.TransformPosition,
                        language),
                    Is.Not.EqualTo(PositionHeaderKey));
                Assert.That(
                    KeyframeInspectorPanel.GetTransformValueHeader(
                        TrackKind.TransformRotation,
                        language),
                    Is.Not.EqualTo(RotationHeaderKey));
                Assert.That(
                    KeyframeInspectorPanel.GetTransformValueHeader(
                        TrackKind.TransformScale,
                        language),
                    Is.Not.EqualTo(ScaleHeaderKey));
            }
        }

        [Test]
        public void TransformKeyInspector_AxisLabels_DoNotRepeatLocalOrUnitHints()
        {
            Assert.That(KeyframeInspectorPanel.AxisXLabel, Is.EqualTo("X"));
            Assert.That(KeyframeInspectorPanel.AxisYLabel, Is.EqualTo("Y"));
            Assert.That(KeyframeInspectorPanel.AxisZLabel, Is.EqualTo("Z"));

            foreach (string axis in new[]
                     {
                         KeyframeInspectorPanel.AxisXLabel,
                         KeyframeInspectorPanel.AxisYLabel,
                         KeyframeInspectorPanel.AxisZLabel
                     })
            {
                Assert.That(axis, Does.Not.Contain("ローカル"));
                Assert.That(axis, Does.Not.Contain("local"));
                Assert.That(axis, Does.Not.Contain("["));
            }
        }

        [Test]
        public void TransformKeyInspector_Header_UsesJapaneseDefaultForDrawing()
        {
            foreach (TrackKind kind in new[]
                     {
                         TrackKind.TransformPosition,
                         TrackKind.TransformRotation,
                         TrackKind.TransformScale
                     })
            {
                Assert.That(
                    KeyframeInspectorPanel.GetTransformValueHeader(kind),
                    Is.EqualTo(
                        KeyframeInspectorPanel.GetTransformValueHeader(
                            kind,
                            SystemLanguage.Japanese)));
            }
        }

        [Test]
        public void TransformKeyInspector_BlendShapeTrack_KeepsGenericValueHeader()
        {
            Assert.That(
                KeyframeInspectorPanel.GetTransformValueHeader(
                    TrackKind.BlendShape,
                    SystemLanguage.Japanese),
                Is.EqualTo(
                    FaceMotion.Editor.UI.Localization.FaceMotionUiText.Get(
                        "value",
                        SystemLanguage.Japanese)));
        }
    }
}
