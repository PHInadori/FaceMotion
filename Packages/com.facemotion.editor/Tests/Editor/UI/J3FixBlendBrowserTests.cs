using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.VRChat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    public sealed class J3FixBlendBrowserTests
    {
        [Test]
        public void VrcExplicitEyelidConfig_RemainsHighestPriority()
        {
            GameObject root = CreateAvatarRoot(out VRCAvatarDescriptor descriptor, out SkinnedMeshRenderer renderer);
            try
            {
                renderer.sharedMesh = CreateMeshWithShapes("EyeBlink_L", "Smile");
                descriptor.customEyeLookSettings = new VRCAvatarDescriptor.CustomEyeLookSettings();
                descriptor.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                descriptor.customEyeLookSettings.eyelidsSkinnedMesh = renderer;
                descriptor.customEyeLookSettings.eyelidsBlendshapes = new[] { 0 };

                VrcBlendShapeConflictIndex conflicts = VrcBlendShapeConflictIndex.Build(descriptor);
                var binding = new BlendShapeBinding(rendererPath(root, renderer), "EyeBlink_L");

                AvatarCandidateSnapshot.BlendShapeCategory actual = AvatarCandidateSnapshot.Classify(
                    binding.RendererPath,
                    renderer.gameObject.name,
                    "EyeBlink_LSmile",
                    conflicts,
                    binding,
                    out string reason);

                Assert.That(actual, Is.EqualTo(AvatarCandidateSnapshot.BlendShapeCategory.Blink));
                Assert.That(reason, Is.EqualTo("VRChat Eyelids"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CandidateIndex_IsNotRebuiltBetweenRepaints()
        {
            AvatarCandidateSnapshot snapshot = BuildSnapshot(100);
            var first = snapshot.FilterBlendShapes(string.Empty);
            var second = snapshot.FilterBlendShapes(string.Empty);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(ReferenceEquals(first[i], second[i]), Is.True, "candidate instances must be reused");
            }
        }

        [Test]
        public void ConflictIndex_IsStableAcrossRepeatedQueries()
        {
            VrcBlendShapeConflictIndex index = VrcBlendShapeConflictIndex.Build(null);
            var binding = new BlendShapeBinding("Body/Face", "Smile");
            VrcBlendShapeConflict before = index.Get(binding);
            for (int i = 0; i < 1000; i++)
            {
                VrcBlendShapeConflict current = index.Get(binding);
                Assert.That(current.IsConflict, Is.EqualTo(before.IsConflict));
                Assert.That(current.IsWarning, Is.EqualTo(before.IsWarning));
            }
        }

        [Test]
        public void Filtering_ReusesCachedCandidateObjects_WithoutRebuildAllocation()
        {
            AvatarCandidateSnapshot snapshot = BuildSnapshot(500);
            var all = snapshot.FilterBlendShapes(string.Empty);
            var filtered = snapshot.FilterBlendShapes("eye");

            Assert.That(all.Count, Is.EqualTo(snapshot.BlendShapes.Count), "empty query must return every cached usable candidate");
            Assert.That(filtered, Is.Not.Null);
        }

        [Test]
        public void AvatarChange_RebuildsSnapshotOnce()
        {
            GameObject root = CreateAvatarRoot(out VRCAvatarDescriptor descriptor, out SkinnedMeshRenderer renderer);
            try
            {
                renderer.sharedMesh = CreateMeshWithShapes("EyeBlink_L", "BrowUp");
                var session = new FaceMotionEditorSession();
                var controller = new AvatarController(session);
                controller.SetDescriptor(descriptor);

                AvatarCandidateSnapshot first = session.Candidates;
                Assert.That(first, Is.Not.Null, "session candidates must be populated after BuildAvatar");
                Assert.That(first.BlendShapeCount, Is.EqualTo(2));

                controller.SetDescriptor(descriptor);

                Assert.That(ReferenceEquals(session.Candidates, first), Is.True, "same descriptor must not rebuild the snapshot");

                controller.RebuildIndex();

                Assert.That(ReferenceEquals(session.Candidates, first), Is.False, "explicit rebuild must produce a fresh snapshot");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Benchmark_SyntheticScaling_WritesCsv()
        {
            string directory = Path.Combine(Path.GetTempPath(), "opencode");
            Directory.CreateDirectory(directory);
            string csvPath = Path.Combine(directory, "j3fix-bench.csv");
            bool firstRun = !File.Exists(csvPath);
            using (var writer = new StreamWriter(csvPath, true))
            {
                if (firstRun)
                {
                    writer.WriteLine("shapes,scanMs,conflictIndexMs,snapshotMs,filterEmptyMs,filterHitMs,resultSize,allocBytesSnapshot");
                }

                foreach (int shapes in new[] { 100, 500, 1000, 2500, 5000 })
                {
                    MeasureSize(writer, shapes);
                }
            }
        }

        [Test]
        public void Benchmark_SyntheticScaling_PrintsSummary()
        {
            foreach (int shapes in new[] { 100, 500, 1000, 2500, 5000 })
            {
                (double scanMs, double conflictMs, double snapshotMs, double filterEmptyMs, double filterHitMs, int resultSize) = Measure(shapes);
                UnityEngine.Debug.Log(
                    string.Format("[J3FIX-PERF] shapes={0} scan={1:F2}ms conflict={2:F2}ms snapshot={3:F2}ms filterEmpty={4:F2}ms filterHit={5:F2}ms hits={6}",
                        shapes, scanMs, conflictMs, snapshotMs, filterEmptyMs, filterHitMs, resultSize));
            }
        }

        private static AvatarCandidateSnapshot BuildSnapshot(int shapes)
        {
            GameObject root = CreateSyntheticAvatar(shapes, out VRCAvatarDescriptor descriptor);
            try
            {
                AvatarScanReport report = UnityAvatarScanner.Scan(root);
                return AvatarCandidateSnapshot.Build(report.Index, descriptor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void MeasureSize(StreamWriter writer, int shapes)
        {
            GameObject root = CreateSyntheticAvatar(shapes, out VRCAvatarDescriptor descriptor);
            try
            {
                (double scanMs, double conflictMs, double snapshotMs, double filterEmptyMs, double filterHitMs, int resultSize, long alloc) = Measure(root, descriptor);
                writer.WriteLine(string.Format("{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6},{7}",
                    shapes, scanMs, conflictMs, snapshotMs, filterEmptyMs, filterHitMs, resultSize, alloc));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static (double Scan, double Conflict, double Snapshot, double FilterEmpty, double FilterHit, int ResultSize) Measure(int shapes)
        {
            GameObject root = CreateSyntheticAvatar(shapes, out VRCAvatarDescriptor descriptor);
            try
            {
                (double scanMs, double conflictMs, double snapshotMs, double filterEmptyMs, double filterHitMs, int hits, long ignored) = Measure(root, descriptor);
                return (scanMs, conflictMs, snapshotMs, filterEmptyMs, filterHitMs, hits);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static (double Scan, double Conflict, double Snapshot, double FilterEmpty, double FilterHit, int ResultSize, long Alloc) Measure(GameObject root, VRCAvatarDescriptor descriptor)
        {
            WarmUp(root, descriptor);

            AvatarScanReport report = null;
            double scanMs = Multiple(3, () => report = UnityAvatarScanner.Scan(root));

            VrcBlendShapeConflictIndex conflictIndex = null;
            double conflictMs = Multiple(10, () => conflictIndex = VrcBlendShapeConflictIndex.Build(descriptor));

            long allocBefore = GetManagedAllocatedBytes();
            AvatarCandidateSnapshot snapshot = null;
            double snapshotMs = Multiple(5, () => snapshot = AvatarCandidateSnapshot.Build(report.Index, descriptor));
            long allocAfter = GetManagedAllocatedBytes();
            long alloc = Math.Max(0L, allocAfter - allocBefore);

            double filterEmptyMs = Multiple(20, () => snapshot.FilterBlendShapes(string.Empty));
            double filterHitMs = Multiple(20, () => snapshot.FilterBlendShapes("eye"));
            int hits = snapshot.FilterBlendShapes("eye").Count;

            return (scanMs, conflictMs, snapshotMs, filterEmptyMs, filterHitMs, hits, alloc);
        }

        private static void WarmUp(GameObject root, VRCAvatarDescriptor descriptor)
        {
            AvatarScanReport report = UnityAvatarScanner.Scan(root);
            VrcBlendShapeConflictIndex.Build(descriptor);
            AvatarCandidateSnapshot.Build(report.Index, descriptor);
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

        private static long GetManagedAllocatedBytes()
        {
            // Coarse engine-side estimate of managed allocation; used only for the scaling trend.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            return Profiler.GetTotalAllocatedMemoryLong();
        }

        private static GameObject CreateSyntheticAvatar(int shapes, out VRCAvatarDescriptor descriptor)
        {
            int renderers = Mathf.Max(5, shapes / 25);
            GameObject root = CreateAvatarRoot(out descriptor, out SkinnedMeshRenderer ignored);
            int perRenderer = shapes / renderers;
            int idx = 0;
            for (int r = 0; r < renderers; r++)
            {
                var child = new GameObject("Renderer_" + r);
                child.transform.SetParent(root.transform, false);
                var rendererComponent = child.AddComponent<SkinnedMeshRenderer>();
                string rendererPathName = r == 0 ? "Face" : r % 4 == 1 ? "Hair_Back" : r % 4 == 2 ? "Outfit" : "Accessory";
                child.name = rendererPathName;
                rendererComponent.sharedMesh = CreateMeshWithShapes(MakeShapeNames(perRenderer, idx));
                idx += perRenderer;
            }

            return root;
        }

        private static string[] MakeShapeNames(int count, int globalStart)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                int slot = i % 12;
                names[i] = i >= 12 ? "B" + (globalStart + i)
                    : slot == 0 ? "EyeBlink_L"
                    : slot == 1 ? "BrowUp"
                    : slot == 2 ? "Mouth_Smile"
                    : slot == 3 ? "HairFlow"
                    : slot == 4 ? "=====VRC empty====="
                    : slot == 5 ? "option_contour_thick"
                    : slot == 6 ? "option_face_on"
                    : slot == 7 ? "Iris_Small"
                    : slot == 8 ? "CheekPuff"
                    : slot == 9 ? "JacketHider"
                    : slot == 10 ? "Slim"
                    : "B" + i;
            }

            return names;
        }

        private static GameObject CreateAvatarRoot(out VRCAvatarDescriptor descriptor, out SkinnedMeshRenderer renderer)
        {
            var root = new GameObject("Avatar");
            descriptor = root.AddComponent<VRCAvatarDescriptor>();
            var child = new GameObject("FaceMesh");
            child.transform.SetParent(root.transform, false);
            renderer = child.AddComponent<SkinnedMeshRenderer>();
            return root;
        }

        private static string rendererPath(GameObject root, SkinnedMeshRenderer renderer)
        {
            return RelativePathUtility.GetRelativePath(root.transform, renderer.transform);
        }

        private static Mesh CreateMeshWithShapes(params string[] shapeNames)
        {
            var mesh = new Mesh { name = "BenchMesh" };
            mesh.vertices = new[] { Vector3.zero };
            var frame = new[] { new Vector3(0.01f, 0f, 0f) };
            for (int i = 0; i < shapeNames.Length; i++)
            {
                mesh.AddBlendShapeFrame(shapeNames[i], 0f, frame, null, null);
            }

            return mesh;
        }
    }
}
