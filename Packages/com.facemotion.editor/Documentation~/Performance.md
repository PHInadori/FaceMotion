# Performance and Stress Audit

This document records the Phase I.6 source audit and manual benchmark baseline. It is not an ordinary CI performance gate: editor wall-clock measurements vary by Unity version, package import state, hardware, and background activity.

## Measurement Scope

The manual probe is `Tests/Editor/Performance/OneOffPerformanceBenchmarks.cs`. It has no NUnit attributes and is run through Unity's `-executeMethod` option, so it never changes the normal EditMode test count or CI timing.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\performance\Run-I6Benchmark.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe"
```

The command writes a JSON report and Unity log to `Tools/performance/`. Those files are local evidence, not package contents. Run it from an otherwise idle editor machine and compare repeated runs rather than treating one result as a hard budget.

The synthetic workloads are fixed for comparability:

| Workload | Tracks | Total keys |
| --- | ---: | ---: |
| Small | 10 | 100 |
| Medium | 50 | 2,500 |
| Large | 200 | 20,000 |
| Extreme | 500 | 100,000 |

The evaluator workload uses sorted float keys and measures sequential and pseudo-random sample times. The avatar workload scans flat synthetic transform hierarchies. It intentionally does not create VRChat assets, export assets, mutate a scene, or benchmark actual IMGUI repaint rendering in batch mode.

## Baseline

Baseline captured on 2026-09-15 with Unity `2022.3.22f1`, Windows 11 x64, and 31,831 MB physical memory. Timings below are a single batch-mode reference run; allocations are managed bytes reported by `GC.GetAllocatedBytesForCurrentThread` during the measured action.

| Operation | Workload | Result | Managed allocation |
| --- | --- | ---: | ---: |
| Canonical evaluation, sequential | Small | 0.134 us / track evaluation | 0 B |
| Canonical evaluation, random | Small | 0.105 us / track evaluation | 0 B |
| Canonical evaluation, sequential | Medium | 0.129 us / track evaluation | 0 B |
| Canonical evaluation, random | Medium | 0.113 us / track evaluation | 0 B |
| Canonical evaluation, sequential | Large | 0.186 us / track evaluation | 0 B |
| Canonical evaluation, random | Large | 0.175 us / track evaluation | 0 B |
| Canonical evaluation, sequential | Extreme | 0.238 us / track evaluation | 0 B |
| Canonical evaluation, random | Extreme | 0.183 us / track evaluation | 0 B |
| Preview trace disabled | 10,000 calls | 0.00044 us / call | 0 B |
| Preview trace enabled | 8 calls | 29.96 us / call | 0 B reported |
| Avatar scan | 100 transforms | 0.344 ms | 0 B reported |
| Avatar scan | 1,000 transforms | 1.777 ms | 0 B reported |
| Avatar scan | 5,000 transforms | 8.443 ms | 0 B reported |

The scanner result is approximately linear for the tested flat hierarchy. Scanning is an explicit refresh operation, not an editor repaint or playback operation; users should not repeatedly refresh a very large avatar while scrubbing.

## Source Audit

| Area | Finding | Current decision |
| --- | --- | --- |
| Timeline IMGUI | `TimelineView.OnGUI` rebuilds layout and `TimelineRenderer` draws visible rows/keys per IMGUI event. Ruler/grid rendering is capped at 1,000 ticks. | Keep current behavior. Batch mode cannot represent editor repaint cost; profile a real editor session before changing rendering or interaction semantics. |
| Scrubbing and preview | A drag updates current time, applies preview through `PreviewMotionApplier`, and requests a coalesced delayed repaint. Evaluation uses the canonical evaluator and cached object bindings. | Keep current behavior. The evaluator baseline is allocation-free through the extreme workload. |
| Preview trace | `FaceMotionPreviewTrace` is off by default. The direct disabled-call measurement reports no managed allocation; enabled tracing intentionally logs and records a ring-buffer entry. | Keep current API and document that tracing is diagnostic-only. Do not enable it for normal playback profiling. |
| Avatar scan and mapping | `UnityAvatarScanner.Scan` is on-demand; `UnityAvatarObjectCache.Rebuild` creates the object lookup cache after a scan. | Keep current behavior. The 5,000-transform scan is below 10 ms in the baseline and is not on the repaint path. |
| BlendShape browser | Candidate categories, regions, search tokens, and conflict summaries are built from a cached avatar snapshot rather than recomputed for every IMGUI repaint. | Keep cache invalidation tied to avatar selection and explicit refresh; profile a representative large avatar before changing the model. |
| Modular Avatar manifest lookup | The optional MA backend can use `AssetDatabase.FindAssets` on a manifest-cache miss. | No cache added. The path is not per-frame, and a global cache needs robust invalidation across asset imports, deletes, and domain reloads. Measure a real manifest-heavy project first. |
| Export, Direct/MA apply, reset, persistence | `AssetDatabase.SaveAssets` and `Refresh` occur in explicit export/apply/remove/save workflows, not the playback loop. | Keep explicit synchronous behavior. It preserves asset and Undo correctness; do not move Unity asset APIs to background work. |
| Diagnostics and Undo | Diagnostics are built by validation/planning actions; Undo records mutation actions rather than preview evaluation. | Keep current action-bound work. Large validation or apply latency should be measured from a representative avatar/project before optimization. |

## Guardrails

- Preserve canonical evaluator results, interpolation rules, preview application, and Undo behavior before considering a performance change.
- Do not add global stale caches, schema changes, or background calls to Unity APIs for speculative speedups.
- Use the manual benchmark for a repeatable evaluator/scanner/trace baseline. Keep ordinary tests focused on correctness and small algorithmic/counter assertions rather than fragile wall-clock thresholds.
- If a real editor profile identifies timeline repaint, export, integration planning, diagnostics, or asset saving as a bottleneck, record the workload and before/after evidence here before changing production code.

## Outcome

Phase I.6 made no production performance optimization. The audited high-frequency evaluator and disabled trace path do not show a measurable managed-allocation problem in the baseline, and the remaining potentially expensive operations are explicit user actions or require real-editor profiling to represent accurately.
