using System.Collections.Generic;
using System.Diagnostics;
using FaceMotion.Animation;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class EvaluationTests
    {
        // ---- Easing primitives ----

        [Test]
        public void Ease_Linear_MapsIdentity()
        {
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Linear, 0f), Is.EqualTo(0f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Linear, 1f), Is.EqualTo(1f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Linear, 0.5f), Is.EqualTo(0.5f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Linear, 0.25f), Is.EqualTo(0.25f));
        }

        [Test]
        public void Ease_AllCurves_PinEndpoints()
        {
            InterpolationType[] types =
            {
                InterpolationType.Linear,
                InterpolationType.EaseIn,
                InterpolationType.EaseOut,
                InterpolationType.EaseInOut,
                InterpolationType.Smooth
            };
            for (int i = 0; i < types.Length; i++)
            {
                Assert.That(InterpolationEase.Evaluate(types[i], 0f), Is.EqualTo(0f), types[i].ToString());
                Assert.That(InterpolationEase.Evaluate(types[i], 1f), Is.EqualTo(1f), types[i].ToString());
            }
        }

        [Test]
        public void Ease_SpecificMidpoints()
        {
            Assert.That(InterpolationEase.Evaluate(InterpolationType.EaseIn, 0.5f), Is.EqualTo(0.25f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.EaseOut, 0.5f), Is.EqualTo(0.75f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.EaseInOut, 0.25f), Is.EqualTo(0.125f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.EaseInOut, 0.75f), Is.EqualTo(0.875f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Smooth, 0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void Ease_Hold_IsConstantZero()
        {
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Hold, 0f), Is.EqualTo(0f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Hold, 0.5f), Is.EqualTo(0f));
            Assert.That(InterpolationEase.Evaluate(InterpolationType.Hold, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void Hold_Float_KeepsLeftValueUntilTheRightKeyTime()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.Hold),
                FloatKeyframeData.Create(0.5f, 50f, InterpolationType.Hold),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };

            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.25f, out var early), Is.True);
            Assert.That(early, Is.EqualTo(0f));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var boundary), Is.True);
            Assert.That(boundary, Is.EqualTo(50f), "The value must switch exactly at the right key time.");
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.99f, out var late), Is.True);
            Assert.That(late, Is.EqualTo(50f));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 1f, out var end), Is.True);
            Assert.That(end, Is.EqualTo(100f));
        }

        [Test]
        public void Hold_Vector3_KeepsLeftValueUntilTheRightKeyTime()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.one, InterpolationType.Hold),
                Vector3KeyframeData.Create(1f, Vector3.one * 2f, InterpolationType.Linear)
            };

            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateScale(keys, 0.5f, out var mid), Is.True);
            Assert.That(mid, Is.EqualTo(Vector3.one));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateScale(keys, 0.999f, out var late), Is.True);
            Assert.That(late, Is.EqualTo(Vector3.one));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateScale(keys, 1f, out var end), Is.True);
            Assert.That(end, Is.EqualTo(Vector3.one * 2f));
        }

        [Test]
        public void TransformScale_Evaluator_Midpoint()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.one, InterpolationType.EaseIn),
                Vector3KeyframeData.Create(1f, Vector3.one * 2f, InterpolationType.Linear)
            };
            // EaseIn at the midpoint is 0.25: 1 + 0.25 * 1 = 1.25 on every axis.
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateScale(keys, 0.5f, out var value), Is.True);
            Assert.That(value, Is.EqualTo(new Vector3(1.25f, 1.25f, 1.25f)).Within(1e-4f));
        }

        [Test]
        public void Ease_SameCoefficient_FloatPositionAndScale()
        {
            var floatKeys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.EaseOut),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            var vectorKeys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.zero, InterpolationType.EaseOut),
                Vector3KeyframeData.Create(1f, Vector3.one * 10f, InterpolationType.Linear)
            };

            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(floatKeys, 0.5f, out var floatValue), Is.True);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluatePosition(vectorKeys, 0.5f, out var position), Is.True);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateScale(vectorKeys, 0.5f, out var scale), Is.True);

            Assert.That(floatValue, Is.EqualTo(75f).Within(1e-4f));
            Assert.That(position.x, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(position.y, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(position.z, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(scale, Is.EqualTo(new Vector3(7.5f, 7.5f, 7.5f)).Within(1e-4f));
        }

        [Test]
        public void Ease_SameCoefficient_RotationUsesEasedSlerp()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.zero, InterpolationType.EaseOut),
                Vector3KeyframeData.Create(1f, new Vector3(0f, 90f, 0f), InterpolationType.Linear)
            };

            // EaseOut applied to the 0..0.5 normalized offset gives eased 0.75, so the
            // slerp angle is 0.75 * 90 = 67.5 degrees, not the linear 45.
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, 0.5f, out var rotation), Is.True);
            float y = rotation.eulerAngles.y;
            Assert.That(Mathf.Abs(y - 67.5f), Is.LessThan(1f));
        }

        // ---- Float evaluation ----

        [Test]
        public void Float_Linear_MidpointIsAverage()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.Linear),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var value), Is.True);
            Assert.That(value, Is.EqualTo(50f));
        }

        [Test]
        public void Float_Eased_MidpointUsesLeftKeyInterpolation()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.EaseIn),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var value), Is.True);
            Assert.That(value, Is.EqualTo(25f));
        }

        [Test]
        public void Float_Smooth_QuarterFollowsSmoothCurve()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.Smooth),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.25f, out var value), Is.True);
            Assert.That(value, Is.EqualTo(100f * 0.15625f).Within(1e-4f));
        }

[Test]
        public void Float_MultiSegment_UsesInteriorSegment()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.EaseIn),
                FloatKeyframeData.Create(0.5f, 50f, InterpolationType.EaseOut),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            // Segment 0.5 -> 1.0 uses the EaseOut key at 0.5 (eased alpha 0.75).
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.75f, out var value), Is.True);
            Assert.That(value, Is.EqualTo(87.5f).Within(1e-4f));
        }

        [Test]
        public void Float_EaseInOut_Tenuation()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.EaseInOut),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            // EaseInOut at midpoint is still 0.5, so a half offset lands on the same value
            // as Linear; the curve differs only away from the center.
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.25f, out var quarter), Is.True);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.75f, out var threeQuarter), Is.True);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var half), Is.True);

            Assert.That(quarter, Is.EqualTo(100f * 0.125f).Within(1e-4f));
            Assert.That(half, Is.EqualTo(50f).Within(1e-4f));
            Assert.That(threeQuarter, Is.EqualTo(100f * 0.875f).Within(1e-4f));
        }

        [Test]
        public void Float_EaseOut_Tenuation()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.EaseOut),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var value), Is.True);
            Assert.That(value, Is.EqualTo(75f).Within(1e-4f));
        }

        [Test]
        public void Float_SingleKey_AlwaysReturnsThatValue()
        {
            var keys = new List<FloatKeyframeData> { FloatKeyframeData.Create(1f, 42f) };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, -10f, out var before), Is.True);
            Assert.That(before, Is.EqualTo(42f));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 10f, out var after), Is.True);
            Assert.That(after, Is.EqualTo(42f));
        }

        [Test]
        public void Float_OutsideDomain_ReturnsNearestEndpoint()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.Linear),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, -5f, out var before), Is.True);
            Assert.That(before, Is.EqualTo(0f));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 5f, out var after), Is.True);
            Assert.That(after, Is.EqualTo(100f));
        }

        [Test]
        public void Float_InvalidInputs_ReturnFalse()
        {
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(null, 0f, out _), Is.False);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(new List<FloatKeyframeData>(), 0f, out _), Is.False);

            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f),
                FloatKeyframeData.Create(1f, 1f)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, float.NaN, out _), Is.False);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, float.PositiveInfinity, out _), Is.False);
        }

        // ---- Vector3 evaluation ----

        [Test]
        public void Vector3_Linear_MidpointIsComponentAverage()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.zero, InterpolationType.Linear),
                Vector3KeyframeData.Create(1f, new Vector3(0f, 10f, -20f), InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluatePosition(keys, 0.5f, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(0f, 5f, -10f)));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateScale(keys, 0.5f, out var scale), Is.True);
            Assert.That(scale, Is.EqualTo(new Vector3(0f, 5f, -10f)));
        }

        [Test]
        public void Vector3_Eased_AppliesCurvePerAxis()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.zero, InterpolationType.EaseIn),
                Vector3KeyframeData.Create(1f, new Vector3(10f, 10f, 10f), InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluatePosition(keys, 0.5f, out var value), Is.True);
            Assert.That(value.x, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(value.y, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(value.z, Is.EqualTo(2.5f).Within(1e-4f));
        }

        // ---- Rotation evaluation (shortest-path quaternion) ----

        [Test]
        public void Rotation_Quadrant_MidpointIs45Degrees()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.zero, InterpolationType.Linear),
                Vector3KeyframeData.Create(1f, new Vector3(0f, 90f, 0f), InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, 0.5f, out var rotation), Is.True);
            var euler = rotation.eulerAngles;
            Assert.That(Mathf.Abs(euler.y - 45f), Is.LessThan(1f));
        }

        [Test]
        public void Rotation_MacroQuarter_StaysNear180Degrees()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, new Vector3(0f, 179f, 0f), InterpolationType.Linear),
                Vector3KeyframeData.Create(1f, new Vector3(0f, -179f, 0f), InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, 0.5f, out var rotation), Is.True);
            float y = rotation.eulerAngles.y;
            if (y > 180f)
            {
                y -= 360f;
            }

            Assert.That(Mathf.Abs(Mathf.Abs(y) - 180f), Is.LessThan(1f));
        }

        [Test]
        public void Rotation_FullTurn_DoesNotTakeLPackLongWay()
        {
            // 360 degrees is the identity; a spin cannot be represented.
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, new Vector3(0f, 360f, 0f), InterpolationType.Linear),
                Vector3KeyframeData.Create(1f, Vector3.zero, InterpolationType.Linear)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, 0.5f, out var rotation), Is.True);
            var euler = rotation.eulerAngles;
            Assert.That(Mathf.Abs(euler.x), Is.LessThan(1f));
            Assert.That(Mathf.Abs(euler.y), Is.LessThan(1f));
            Assert.That(Mathf.Abs(euler.z), Is.LessThan(1f));
        }

        [Test]
        public void Rotation_EndpointClamping_UsesKeyQuaternions()
        {
            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, new Vector3(0f, 30f, 0f)),
                Vector3KeyframeData.Create(1f, new Vector3(0f, 120f, 0f))
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, -1f, out var before), Is.True);
            Assert.That(Mathf.Abs(before.eulerAngles.y - 30f), Is.LessThan(1f));
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, 2f, out var after), Is.True);
            Assert.That(Mathf.Abs(after.eulerAngles.y - 120f), Is.LessThan(1f));
        }

        [Test]
        public void Rotation_InvalidInput_ReturnsFalse()
        {
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(null, 0f, out _), Is.False);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(new List<Vector3KeyframeData>(), 0f, out _), Is.False);

            var keys = new List<Vector3KeyframeData>
            {
                Vector3KeyframeData.Create(0f, Vector3.zero),
                Vector3KeyframeData.Create(1f, Vector3.one)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, float.NaN, out _), Is.False);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateRotation(keys, float.NegativeInfinity, out _), Is.False);
        }

        // ---- Segment search ----

        [Test]
        public void SegmentSearch_FindsInteriorSegment()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f),
                FloatKeyframeData.Create(0.5f, 50f),
                FloatKeyframeData.Create(1f, 100f)
            };
            Assert.That(SegmentSearch.TryFindFloatSegment(keys, 0.75f, out var left, out var right, out float alpha), Is.True);
            Assert.That(left.Time, Is.EqualTo(0.5f));
            Assert.That(right.Time, Is.EqualTo(1f));
            Assert.That(alpha, Is.EqualTo(0.5f));
        }

        [Test]
        public void SegmentSearch_RejectsEndpointsAndInvalidInput()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f),
                FloatKeyframeData.Create(1f, 100f)
            };
            Assert.That(SegmentSearch.TryFindFloatSegment(keys, 0f, out _, out _, out _), Is.False);
            Assert.That(SegmentSearch.TryFindFloatSegment(keys, 1f, out _, out _, out _), Is.False);
            Assert.That(SegmentSearch.TryFindFloatSegment(keys, float.NaN, out _, out _, out _), Is.False);
            Assert.That(SegmentSearch.TryFindFloatSegment(null, 0.5f, out _, out _, out _), Is.False);

            var single = new List<FloatKeyframeData> { FloatKeyframeData.Create(0f, 0f) };
            Assert.That(SegmentSearch.TryFindFloatSegment(single, 0.5f, out _, out _, out _), Is.False);
        }

        // ---- Frame snapping ----

        [Test]
        public void Snap_RoundsToNearestFrame()
        {
            Assert.That(FrameSnapper.Snap(0.5167001f, 60f), Is.EqualTo(0.5166667f).Within(1e-4f));
            Assert.That(FrameSnapper.Snap(0.5f, 30f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(FrameSnapper.Snap(0.40001f, 60f), Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void Snap_ClampsIntoDurationDomain()
        {
            Assert.That(FrameSnapper.Snap(2f, 60f, 1f), Is.EqualTo(1f));
            Assert.That(FrameSnapper.Snap(-0.5f, 60f, 1f), Is.EqualTo(0f));
            Assert.That(FrameSnapper.Snap(0.999999f, 60f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void Snap_InvalidInput_IsNonDestructive()
        {
            Assert.That(FrameSnapper.Snap(0.5f, 0f), Is.EqualTo(0.5f));
            Assert.That(FrameSnapper.Snap(0.5f, -60f), Is.EqualTo(0.5f));
            Assert.That(FrameSnapper.Snap(float.NaN, 60f), Is.EqualTo(float.NaN));
            Assert.That(FrameSnapper.Snap(float.PositiveInfinity, 60f), Is.EqualTo(float.PositiveInfinity));
        }

        // ---- Sampling grid ----

        [Test]
        public void SampleTimes_FrameAligned_IncludesGridAndEndpoints()
        {
            var times = MotionSampler.CreateSampleTimes(1f, 60f);
            Assert.That(times.Length, Is.EqualTo(61));
            Assert.That(times[0], Is.EqualTo(0f));
            Assert.That(times[times.Length - 1], Is.EqualTo(1f));
            Assert.That(times[30], Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void SampleTimes_NonFrameBoundary_AppendsExactDuration()
        {
            var times = MotionSampler.CreateSampleTimes(0.51f, 60f);
            Assert.That(times[times.Length - 1], Is.EqualTo(0.51f));
            Assert.That(times[1] - times[0], Is.EqualTo(1f / 60f).Within(1e-5f));
        }

        [Test]
        public void SampleTimes_InvalidInput_ReturnsSafeDefaults()
        {
            Assert.That(MotionSampler.CreateSampleTimes(0f, 60f), Is.EqualTo(new[] { 0f }));
            Assert.That(MotionSampler.CreateSampleTimes(float.NaN, 60f), Is.EqualTo(new[] { 0f }));
            Assert.That(MotionSampler.CreateSampleTimes(1f, 0f), Is.EqualTo(new[] { 1f }));
        }

        // ---- Determinism and throughput ----

        [Test]
        public void Evaluator_IsDeterministic_AcrossRepeatedCalls()
        {
            var keys = new List<FloatKeyframeData>
            {
                FloatKeyframeData.Create(0f, 0f, InterpolationType.Smooth),
                FloatKeyframeData.Create(1f, 100f, InterpolationType.Smooth)
            };
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var first), Is.True);
            Assert.That(CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, 0.5f, out var second), Is.True);
            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void Performance_LargeCurve_EvaluatesWithinBudget()
        {
            var keys = new List<FloatKeyframeData>(1000);
            for (int i = 0; i < 1000; i++)
            {
                keys.Add(FloatKeyframeData.Create(i / 1000f, Mathf.Sin(i * 0.1f), InterpolationType.Smooth));
            }

            var stopwatch = Stopwatch.StartNew();
            float cumulative = 0f;
            int hits = 0;
            for (int i = 0; i < 10000; i++)
            {
                float time = (i % 1000) / 1000f;
                if (CanonicalMotionEvaluator.Instance.TryEvaluateFloat(keys, time, out var value))
                {
                    cumulative += value;
                    hits++;
                }
            }

            stopwatch.Stop();
            Assert.That(hits, Is.EqualTo(10000));
            Assert.That(cumulative, Is.Not.EqualTo(0f));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(20000));
        }
    }
}