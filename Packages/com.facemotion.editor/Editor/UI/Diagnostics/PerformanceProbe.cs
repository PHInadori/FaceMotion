using System;
using System.Diagnostics;
using System.IO;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.VRChat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Diagnostics
{
    /// <summary>
    /// Batch-usable performance probe. Opens every scene additively, finds the first avatar
    /// with a VRCAvatarDescriptor, and records avatar scan / conflict index / browser snapshot /
    /// filter timing (plus coarse managed allocation) as JSON to %TEMP%\opencode\komane-perf.json.
    /// Used by the J3-FIX performance audit; not part of the shipped UI.
    /// </summary>
    public static class PerformanceProbe
    {
        private const string OutputRelative = "opencode\\komane-perf.json";

        [MenuItem("Tools/FaceMotion/Diagnostics/Benchmark Avatar")]
        public static void Run()
        {
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "opencode");
                Directory.CreateDirectory(directory);
                string outputPath = Path.Combine(directory, "komane-perf.json");
                RunCore(outputPath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("[J3FIX-PERF] probe failed: " + exception);
            }

            EditorApplication.Exit(0);
        }

        private static void RunCore(string outputPath)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            VRCAvatarDescriptor found = null;
            string scenePath = string.Empty;
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                try
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning("[J3FIX-PERF] scene open failed: " + path + " -> " + exception.Message);
                    continue;
                }

                VRCAvatarDescriptor[] descriptors = UnityEngine.Object.FindObjectsOfType<VRCAvatarDescriptor>(true);
                for (int d = 0; d < descriptors.Length; d++)
                {
                    if (descriptors[d] != null)
                    {
                        found = descriptors[d];
                        scenePath = path;
                        break;
                    }
                }

                if (found != null)
                {
                    break;
                }
            }

            if (found == null)
            {
                UnityEngine.Debug.LogError("[J3FIX-PERF] no VRCAvatarDescriptor found in any scene");
                File.WriteAllText(outputPath, "{\"error\":\"avatar not found\",\"scenes\":" + sceneGuids.Length + "}");
                return;
            }

            GameObject root = found.gameObject;
            Profile p = Measure(root, found);
            string json = "{"
                + "\"scene\":\"" + JsonEscape(scenePath) + "\","
                + "\"avatar\":\"" + JsonEscape(root.name) + "\","
                + "\"shapes\":" + p.Shapes + ","
                + "\"renderers\":" + p.Renderers + ","
                + "\"scanMs\":" + p.ScanMs.ToString("F2") + ","
                + "\"conflictIndexMs\":" + p.ConflictMs.ToString("F2") + ","
                + "\"snapshotMs\":" + p.SnapshotMs.ToString("F2") + ","
                + "\"filterEmptyMs\":" + p.FilterEmptyMs.ToString("F2") + ","
                + "\"filterHitMs\":" + p.FilterHitMs.ToString("F2") + ","
                + "\"filterHits\":" + p.FilterHits + ","
                + "\"separators\":" + p.Separators + ","
                + "\"allocBytesSnapshot\":" + p.AllocBytes
                + "}";
            File.WriteAllText(outputPath, json);
            UnityEngine.Debug.Log("[J3FIX-PERF] " + json);
        }

        private static Profile Measure(GameObject root, VRCAvatarDescriptor descriptor)
        {
            AvatarScanReport warm = UnityAvatarScanner.Scan(root);
            VrcBlendShapeConflictIndex.Build(descriptor);
            AvatarCandidateSnapshot.Build(warm.Index, descriptor);

            AvatarScanReport report = null;
            double scanMs = Multiple(3, () => report = UnityAvatarScanner.Scan(root));

            VrcBlendShapeConflictIndex conflictIndex = null;
            double conflictMs = Multiple(10, () => conflictIndex = VrcBlendShapeConflictIndex.Build(descriptor));

            long before = ManagedAllocatedBytes();
            AvatarCandidateSnapshot snapshot = null;
            double snapshotMs = Multiple(5, () => snapshot = AvatarCandidateSnapshot.Build(report.Index, descriptor));
            long after = ManagedAllocatedBytes();

            double filterEmptyMs = Multiple(20, () => snapshot.FilterBlendShapes(string.Empty));
            double filterHitMs = Multiple(20, () => snapshot.FilterBlendShapes("blink"));
            int hits = snapshot.FilterBlendShapes("blink").Count;

            int separators = 0;
            for (int i = 0; i < snapshot.BlendShapes.Count; i++)
            {
                if (snapshot.BlendShapes[i].IsSeparator)
                {
                    separators++;
                }
            }

            return new Profile(
                snapshot.BlendShapeCount,
                report.Index.RendererCount,
                scanMs,
                conflictMs,
                snapshotMs,
                filterEmptyMs,
                filterHitMs,
                hits,
                separators,
                Math.Max(0L, after - before));
        }

        private static double Multiple(int times, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < times; i++)
            {
                action();
            }

            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds / times;
        }

        private static long ManagedAllocatedBytes()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            return Profiler.GetTotalAllocatedMemoryLong();
        }

        private static string JsonEscape(string value)
        {
            return value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private readonly struct Profile
        {
            public Profile(
                int shapes,
                int renderers,
                double scanMs,
                double conflictMs,
                double snapshotMs,
                double filterEmptyMs,
                double filterHitMs,
                int filterHits,
                int separators,
                long allocBytes)
            {
                Shapes = shapes;
                Renderers = renderers;
                ScanMs = scanMs;
                ConflictMs = conflictMs;
                SnapshotMs = snapshotMs;
                FilterEmptyMs = filterEmptyMs;
                FilterHitMs = filterHitMs;
                FilterHits = filterHits;
                Separators = separators;
                AllocBytes = allocBytes;
            }

            public int Shapes { get; }
            public int Renderers { get; }
            public double ScanMs { get; }
            public double ConflictMs { get; }
            public double SnapshotMs { get; }
            public double FilterEmptyMs { get; }
            public double FilterHitMs { get; }
            public int FilterHits { get; }
            public int Separators { get; }
            public long AllocBytes { get; }
        }
    }
}