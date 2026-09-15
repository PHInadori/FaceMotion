using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FaceMotion.Animation;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.Diagnostics;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor.Tests.Performance
{
    /// <summary>
    /// Manual performance probe invoked with Unity's -executeMethod option. It deliberately has
    /// no NUnit attributes so normal test runs remain deterministic and fast.
    /// </summary>
    public static class OneOffPerformanceBenchmarks
    {
        private static readonly Workload[] Workloads =
        {
            new Workload("small", 10, 100),
            new Workload("medium", 50, 2500),
            new Workload("large", 200, 20000),
            new Workload("extreme", 500, 100000)
        };

        public static void Run()
        {
            string outputPath = GetArgument("-performanceResults");
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("Pass -performanceResults <absolute json path> when running this benchmark.");
            }

            var report = new BenchmarkReport
            {
                unityVersion = Application.unityVersion,
                utcTimestamp = DateTime.UtcNow.ToString("o"),
                measurements = new List<Measurement>()
            };

            for (int i = 0; i < Workloads.Length; i++)
            {
                AddEvaluationMeasurements(report.measurements, Workloads[i]);
            }

            AddTraceMeasurements(report.measurements);
            AddAvatarScanMeasurements(report.measurements);

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("FaceMotion I.6 performance report: " + outputPath);
        }

        private static void AddEvaluationMeasurements(List<Measurement> measurements, Workload workload)
        {
            List<List<FloatKeyframeData>> tracks = BuildFloatTracks(workload.trackCount, workload.keyCount);
            int samples = workload.trackCount <= 50 ? 200 : 40;
            int randomState = 17;

            measurements.Add(Measure("evaluation.sequential", workload.name, workload.trackCount, workload.keyCount,
                samples * tracks.Count, delegate
                {
                    for (int sample = 0; sample < samples; sample++)
                    {
                        EvaluateAll(tracks, (float)sample / (samples - 1));
                    }
                }));

            measurements.Add(Measure("evaluation.random", workload.name, workload.trackCount, workload.keyCount,
                samples * tracks.Count, delegate
                {
                    for (int sample = 0; sample < samples; sample++)
                    {
                        randomState = unchecked(randomState * 1103515245 + 12345);
                        EvaluateAll(tracks, (randomState & 0x7fffffff) / 2147483647f);
                    }
                }));
        }

        private static void AddTraceMeasurements(List<Measurement> measurements)
        {
            FaceMotionPreviewTrace.Clear();
            FaceMotionPreviewTrace.SetEnabled(false);
            int index = 0;
            measurements.Add(Measure("trace.disabled", "trace", 0, 0, 10000, delegate
            {
                FaceMotionPreviewTrace.Trace("I6", "Value {0}", index++);
            }));

            FaceMotionPreviewTrace.ClearAndEnable();
            index = 0;
            measurements.Add(Measure("trace.enabled", "trace", 0, 0, 8, delegate
            {
                FaceMotionPreviewTrace.Trace("I6", "Value {0}", index++);
            }));
            FaceMotionPreviewTrace.SetEnabled(false);
            FaceMotionPreviewTrace.Clear();
        }

        private static void AddAvatarScanMeasurements(List<Measurement> measurements)
        {
            int[] transformCounts = { 100, 1000, 5000 };
            for (int i = 0; i < transformCounts.Length; i++)
            {
                int count = transformCounts[i];
                GameObject root = BuildAvatarHierarchy(count);
                try
                {
                    measurements.Add(Measure("avatar.scan", "transforms-" + count, count, 0, 1, delegate
                    {
                        UnityAvatarScanner.Scan(root);
                    }));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static List<List<FloatKeyframeData>> BuildFloatTracks(int trackCount, int totalKeyCount)
        {
            var tracks = new List<List<FloatKeyframeData>>(trackCount);
            int baseCount = totalKeyCount / trackCount;
            int remainder = totalKeyCount % trackCount;
            for (int track = 0; track < trackCount; track++)
            {
                int keyCount = baseCount + (track < remainder ? 1 : 0);
                var keys = new List<FloatKeyframeData>(keyCount);
                for (int key = 0; key < keyCount; key++)
                {
                    float time = keyCount == 1 ? 0f : (float)key / (keyCount - 1);
                    keys.Add(FloatKeyframeData.Create(time, key + track * 0.01f, InterpolationType.Linear));
                }

                tracks.Add(keys);
            }

            return tracks;
        }

        private static void EvaluateAll(List<List<FloatKeyframeData>> tracks, float time)
        {
            for (int track = 0; track < tracks.Count; track++)
            {
                CanonicalMotionEvaluator.Instance.TryEvaluateFloat(tracks[track], time, out var ignored);
            }
        }

        private static GameObject BuildAvatarHierarchy(int transformCount)
        {
            var root = new GameObject("I6 Benchmark Avatar");
            for (int i = 0; i < transformCount; i++)
            {
                var child = new GameObject("Bone_" + i);
                child.transform.SetParent(root.transform, false);
            }

            return root;
        }

        private static Measurement Measure(string name, string workload, int trackCount, int keyCount, int operations, Action action)
        {
            action();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
            return new Measurement
            {
                name = name,
                workload = workload,
                trackCount = trackCount,
                keyCount = keyCount,
                operations = operations,
                elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                microsecondsPerOperation = stopwatch.Elapsed.TotalMilliseconds * 1000d / operations,
                allocatedBytes = allocatedBytes
            };
        }

        private static string GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }

        private sealed class Workload
        {
            public readonly string name;
            public readonly int trackCount;
            public readonly int keyCount;

            public Workload(string name, int trackCount, int keyCount)
            {
                this.name = name;
                this.trackCount = trackCount;
                this.keyCount = keyCount;
            }
        }

        [Serializable]
        private sealed class BenchmarkReport
        {
            public string unityVersion;
            public string utcTimestamp;
            public List<Measurement> measurements;
        }

        [Serializable]
        private sealed class Measurement
        {
            public string name;
            public string workload;
            public int trackCount;
            public int keyCount;
            public int operations;
            public double elapsedMilliseconds;
            public double microsecondsPerOperation;
            public long allocatedBytes;
        }
    }
}
