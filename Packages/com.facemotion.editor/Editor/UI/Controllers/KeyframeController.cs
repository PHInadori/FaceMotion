using System;
using System.Collections.Generic;
using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.UI.Timeline;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>
    /// Timeline key editing: add/update at inspector time, delete, move (single undo),
    /// interactive drag (one undo record for the whole drag), copy/paste, and scalar edits.
    /// All project mutations go through commands; the drag path reuses the same planner and
    /// TrackKeyMover so behavior stays identical.
    /// </summary>
    public sealed class KeyframeController
    {
        public sealed class PasteResult
        {
            public bool Succeeded;

            public int Pasted;

            public int Skipped;

            public string PastedKeyId;
        }

        private const float TimeTolerance = 1e-4f;

        private readonly FaceMotionEditorSession _session;
        private readonly DragUndoScope _drag = new DragUndoScope();
        private List<TrackMoveInput> _dragInputs;
        private float _legacyDragDelta;

        public KeyframeController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public FaceTrackData GetSelectedTrack()
        {
            return _session.GetSelectedTrack();
        }

        public bool HasSelection => _session.Selection.Count > 0;

        public float SnapTime(float time)
        {
            float duration = _session.GetSelectedDuration();
            if (_session.ViewState.SnapEnabled)
            {
                return FrameSnapper.Snap(time, _session.GetSelectedFrameRate(), duration);
            }

            return Mathf.Clamp(time, 0f, duration);
        }

        /// <summary>Adds or updates a key at the given time using the inspector values.</summary>
        public string AddKeyAt(float time, float floatValue, Vector3 vectorValue, InterpolationType interpolation)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return null;
            }

            var track = _session.GetSelectedTrack();
            if (track == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("track"));
                return null;
            }

            time = SnapTime(time);

            if (track.Kind == TrackKind.BlendShape)
            {
                string existing = FindKeyAtTime(track, time);
                if (existing != null)
                {
                    if (!UICommandRunner.Run(_session, new UpdateFloatKeyValueCommand(animation.AnimationId, track.TrackId, existing, floatValue)).Succeeded)
                    {
                        return null;
                    }

                    _session.Selection.SetSingle(existing);
                    _session.NotifyChanged();
                    return existing;
                }

                var add = UICommandRunner.Run(
                    _session,
                    new AddFloatKeyCommand(animation.AnimationId, track.TrackId, time, floatValue, interpolation));
                if (!add.Succeeded)
                {
                    return null;
                }
            }
            else if (TrackKinds.IsTransform(track.Kind))
            {
                string existing = FindKeyAtTime(track, time);
                if (existing != null)
                {
                    if (!UICommandRunner.Run(_session, new UpdateVector3KeyValueCommand(animation.AnimationId, track.TrackId, existing, vectorValue)).Succeeded)
                    {
                        return null;
                    }

                    _session.Selection.SetSingle(existing);
                    _session.NotifyChanged();
                    return existing;
                }

                var add = UICommandRunner.Run(
                    _session,
                    new AddVector3KeyCommand(animation.AnimationId, track.TrackId, time, vectorValue, interpolation));
                if (!add.Succeeded)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            string created = FindKeyAtTime(track, time);
            if (created != null)
            {
                _session.Selection.SetSingle(created);
                _session.NotifyChanged();
            }

            return created;
        }

        public bool DeleteSelectedKeys()
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            List<KeyTarget> targets = CollectSelectedTargets(animation);
            if (targets.Count == 0)
            {
                return false;
            }

            var result = UICommandRunner.Run(_session, new RemoveKeysCommand(animation.AnimationId, targets));
            if (result.Succeeded)
            {
                _session.Selection.Clear();
                _session.NotifyChanged();
            }

            return result.Succeeded;
        }

        /// <summary>Moves the whole selection by an offset in one Undo step.</summary>
        public bool MoveSelectedKeysBy(float deltaTime, bool snapEnabled)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            List<TrackMoveInput> inputs = BuildMoveInputs(animation);
            if (inputs.Count == 0 || !HasAnySelectedKeys(inputs))
            {
                return false;
            }

            float duration = animation.Timeline.Duration;
            float frameRate = animation.Timeline.FrameRate;
            var commands = new List<IProjectCommand>(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].KeyIds.Count == 0)
                {
                    continue;
                }

                float[] planned = KeyMovePlanner.PlanTrack(
                    inputs[i].CurrentTimes,
                    inputs[i].Occupied,
                    deltaTime,
                    duration,
                    frameRate,
                    snapEnabled);
                commands.Add(new MoveKeysToTimesCommand(animation.AnimationId, inputs[i].TrackId, inputs[i].KeyIds, planned));
            }

            if (commands.Count == 0)
            {
                return false;
            }

            return UICommandRunner.RunBatch(_session, commands, "Move Keys").Succeeded;
        }

        public bool CanBeginKeyDrag()
        {
            return _session.ActiveProject != null && HasSelection;
        }

        public void BeginKeyDrag()
        {
            if (!CanBeginKeyDrag())
            {
                _drag.Rollback();
                _dragInputs = null;
                return;
            }

            _drag.Begin(_session.ActiveProject, "Move Keys");
            _dragInputs = BuildMoveInputs(_session.GetSelectedAnimation());
            _legacyDragDelta = 0f;
        }

        public void UpdateKeyDrag(float pixelDelta, float pixelsPerSecond)
        {
            if (!_drag.IsBegun)
            {
                return;
            }

            _legacyDragDelta += pixelDelta;
            UpdateKeyDragAt(_legacyDragDelta, 0f, pixelsPerSecond);
        }

        /// <summary>Moves from the pointer's absolute position relative to its mouse-down anchor.</summary>
        public void UpdateKeyDragAt(float pointerPixelX, float anchorPixelX, float pixelsPerSecond)
        {
            if (!_drag.IsBegun)
            {
                return;
            }

            float deltaTime = pixelsPerSecond > 0f ? (pointerPixelX - anchorPixelX) / pixelsPerSecond : 0f;
            ApplyDragMove(deltaTime, _session.ViewState.SnapEnabled);
        }

        public void EndKeyDrag()
        {
            _drag.Commit();
            _dragInputs = null;
            _session.RefreshAll();
        }

        public void CancelKeyDrag()
        {
            _drag.Rollback();
            _dragInputs = null;
            _session.RefreshAll();
        }

        public bool SetKeyTime(string keyId, float time)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                return false;
            }

            var track = FindTrackForKey(animation, keyId);
            if (track == null)
            {
                return false;
            }

            time = SnapTime(time);
            var result = UICommandRunner.Run(
                _session,
                new MoveKeysToTimesCommand(animation.AnimationId, track.TrackId, new[] { keyId }, new[] { time }));
            return result.Succeeded;
        }

        public bool SetKeyValue(string keyId, float floatValue, Vector3 vectorValue)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                return false;
            }

            var track = FindTrackForKey(animation, keyId);
            if (track == null)
            {
                return false;
            }

            if (track.Kind == TrackKind.BlendShape)
            {
                return UICommandRunner.Run(_session, new UpdateFloatKeyValueCommand(animation.AnimationId, track.TrackId, keyId, floatValue)).Succeeded;
            }

            if (TrackKinds.IsTransform(track.Kind))
            {
                return UICommandRunner.Run(_session, new UpdateVector3KeyValueCommand(animation.AnimationId, track.TrackId, keyId, vectorValue)).Succeeded;
            }

            return false;
        }

        public bool SetKeyInterpolation(string keyId, InterpolationType interpolation)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                return false;
            }

            var track = FindTrackForKey(animation, keyId);
            if (track == null)
            {
                return false;
            }

            var result = UICommandRunner.Run(_session, new SetKeyInterpolationCommand(animation.AnimationId, track.TrackId, keyId, interpolation));
            return result.Succeeded;
        }

        /// <summary>
        /// Commits all inspector fields for one key as one validated Undo transaction.
        /// The panel owns the temporary edit buffer; this method only applies its snapshot.
        /// </summary>
        public bool ApplyKeyEdits(
            string keyId,
            float time,
            float floatValue,
            Vector3 vectorValue,
            InterpolationType interpolation)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            var track = FindTrackForKey(animation, keyId);
            if (track == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("key"));
                return false;
            }

            time = SnapTime(time);
            var commands = new List<IProjectCommand>
            {
                new MoveKeysToTimesCommand(animation.AnimationId, track.TrackId, new[] { keyId }, new[] { time }),
                new SetKeyInterpolationCommand(animation.AnimationId, track.TrackId, keyId, interpolation)
            };

            if (track.Kind == TrackKind.BlendShape)
            {
                commands.Insert(1, new UpdateFloatKeyValueCommand(animation.AnimationId, track.TrackId, keyId, floatValue));
            }
            else if (TrackKinds.IsTransform(track.Kind))
            {
                commands.Insert(1, new UpdateVector3KeyValueCommand(animation.AnimationId, track.TrackId, keyId, vectorValue));
            }
            else
            {
                return false;
            }

            return UICommandRunner.RunBatch(_session, commands, "Edit Key").Succeeded;
        }

        public void SelectAllKeys()
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                return;
            }

            var ids = new List<string>();
            foreach (var track in animation.Timeline.Tracks)
            {
                CollectKeyIds(track, ids);
            }

            _session.Selection.SetSelection(ids);
            _session.NotifyChanged();
        }

        public void CopySelection()
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null || !HasSelection)
            {
                return;
            }

            var items = new List<TimelineClipboard.ClipboardItem>();
            float minTime = float.PositiveInfinity;
            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                {
                    foreach (var key in track.BlendShape.Keys)
                    {
                        if (key != null && _session.Selection.Contains(key.KeyId))
                        {
                            items.Add(NewItem(track, key.KeyId, key.Time, key.Interpolation, key.Value, Vector3.zero));
                            minTime = Mathf.Min(minTime, key.Time);
                        }
                    }
                }
                else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                {
                    foreach (var key in track.Transform.Keys)
                    {
                        if (key != null && _session.Selection.Contains(key.KeyId))
                        {
                            items.Add(NewItem(track, key.KeyId, key.Time, key.Interpolation, 0f, key.Value));
                            minTime = Mathf.Min(minTime, key.Time);
                        }
                    }
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                items[i].RelativeTime = items[i].RelativeTime - minTime;
            }

            _session.Clipboard.Set(items);
            _session.NotifyChanged();
        }

        public PasteResult PasteAt(float time)
        {
            var result = new PasteResult();
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return result;
            }

            if (!_session.Clipboard.HasItems)
            {
                return result;
            }

            float duration = animation.Timeline.Duration;
            float frameRate = animation.Timeline.FrameRate;
            float cursor = SnapTime(time);

            var specs = new List<PasteKeySpec>(_session.Clipboard.Count);
            var occupiedPerTrack = new Dictionary<string, List<float>>(StringComparer.Ordinal);
            int skipped = 0;
            for (int i = 0; i < _session.Clipboard.Count; i++)
            {
                TimelineClipboard.ClipboardItem item = _session.Clipboard.Items[i];
                if (!animation.Timeline.TryGetTrack(item.TrackId, out var track) || track.Kind != item.Kind)
                {
                    SetOutcome(ControllerDiagnostics.PasteTrackMissing(item.TrackId));
                    return result;
                }

                float absolute = cursor + item.RelativeTime;
                float targetTime = SnapTime(absolute);

                if (!occupiedPerTrack.TryGetValue(item.TrackId, out var occupied))
                {
                    occupied = CollectTrackTimes(track);
                    occupiedPerTrack.Add(item.TrackId, occupied);
                }

                if (ContainsTime(occupied, targetTime))
                {
                    skipped++;
                    continue;
                }

                occupied.Add(targetTime);
                specs.Add(new PasteKeySpec
                {
                    TrackId = item.TrackId,
                    Kind = item.Kind,
                    Time = targetTime,
                    FloatValue = item.FloatValue,
                    VectorValue = item.VectorValue,
                    Interpolation = item.Interpolation
                });
            }

            if (specs.Count == 0)
            {
                result.Skipped = skipped;
                SetOutcome(ControllerDiagnostics.PasteCollision(skipped));
                return result;
            }

            var command = new PasteKeysCommand(animation.AnimationId, specs);
            var run = UICommandRunner.Run(_session, command);
            if (!run.Succeeded)
            {
                return result;
            }

            result.Succeeded = true;
            result.Pasted = specs.Count;
            result.Skipped = skipped;
            if (command.CreatedKeyIds != null && command.CreatedKeyIds.Count > 0)
            {
                result.PastedKeyId = command.CreatedKeyIds[0];
                _session.Selection.SetSelection(command.CreatedKeyIds);
            }

            if (skipped > 0)
            {
                SetOutcome(ControllerDiagnostics.PasteCollision(skipped));
            }
            else
            {
                _session.NotifyChanged();
            }

            return result;
        }

        private void ApplyDragMove(float deltaTime, bool snapEnabled)
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null || animation.Timeline == null)
            {
                return;
            }

            List<TrackMoveInput> inputs = _dragInputs ?? BuildMoveInputs(animation);
            if (inputs.Count == 0)
            {
                return;
            }

            float duration = animation.Timeline.Duration;
            float frameRate = animation.Timeline.FrameRate;
            bool changed = false;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].KeyIds.Count == 0)
                {
                    continue;
                }

                float[] planned = KeyMovePlanner.PlanTrack(
                    inputs[i].CurrentTimes,
                    inputs[i].Occupied,
                    deltaTime,
                    duration,
                    frameRate,
                    snapEnabled);
                TrackKeyMover.Move(inputs[i].Track, inputs[i].KeyIds, planned);
                changed = true;
            }

            if (changed)
            {
                _session.NotifyChanged();
            }
        }

        private sealed class TrackMoveInput
        {
            public FaceTrackData Track;

            public string TrackId;

            public readonly List<string> KeyIds = new List<string>();

            public readonly List<float> CurrentTimes = new List<float>();

            public readonly List<float> Occupied = new List<float>();
        }

        private List<TrackMoveInput> BuildMoveInputs(FaceMotionAnimationData animation)
        {
            var inputs = new List<TrackMoveInput>();
            if (animation.Timeline == null)
            {
                return inputs;
            }

            var byTrack = new Dictionary<string, TrackMoveInput>(StringComparer.Ordinal);
            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                var input = new TrackMoveInput { Track = track, TrackId = track.TrackId };
                byTrack[track.TrackId] = input;
                inputs.Add(input);

                if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                {
                    foreach (var key in track.BlendShape.Keys)
                    {
                        if (key == null)
                        {
                            continue;
                        }

                        if (_session.Selection.Contains(key.KeyId))
                        {
                            input.KeyIds.Add(key.KeyId);
                            input.CurrentTimes.Add(key.Time);
                        }
                        else
                        {
                            input.Occupied.Add(key.Time);
                        }
                    }
                }
                else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                {
                    foreach (var key in track.Transform.Keys)
                    {
                        if (key == null)
                        {
                            continue;
                        }

                        if (_session.Selection.Contains(key.KeyId))
                        {
                            input.KeyIds.Add(key.KeyId);
                            input.CurrentTimes.Add(key.Time);
                        }
                        else
                        {
                            input.Occupied.Add(key.Time);
                        }
                    }
                }
            }

            return inputs;
        }

        private static bool HasAnySelectedKeys(List<TrackMoveInput> inputs)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].KeyIds.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private List<KeyTarget> CollectSelectedTargets(FaceMotionAnimationData animation)
        {
            var targets = new List<KeyTarget>();
            if (animation.Timeline == null)
            {
                return targets;
            }

            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                var ids = new List<string>();
                CollectKeyIds(track, ids);
                for (int i = 0; i < ids.Count; i++)
                {
                    if (_session.Selection.Contains(ids[i]))
                    {
                        targets.Add(new KeyTarget(track.TrackId, ids[i]));
                    }
                }
            }

            return targets;
        }

        private static void CollectKeyIds(FaceTrackData track, List<string> ids)
        {
            if (track == null)
            {
                return;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                foreach (var key in track.BlendShape.Keys)
                {
                    if (key != null)
                    {
                        ids.Add(key.KeyId);
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                foreach (var key in track.Transform.Keys)
                {
                    if (key != null)
                    {
                        ids.Add(key.KeyId);
                    }
                }
            }
        }

        private static FaceTrackData FindTrackForKey(FaceMotionAnimationData animation, string keyId)
        {
            if (animation.Timeline == null)
            {
                return null;
            }

            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                {
                    foreach (var key in track.BlendShape.Keys)
                    {
                        if (key != null && string.Equals(key.KeyId, keyId, StringComparison.Ordinal))
                        {
                            return track;
                        }
                    }
                }
                else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                {
                    foreach (var key in track.Transform.Keys)
                    {
                        if (key != null && string.Equals(key.KeyId, keyId, StringComparison.Ordinal))
                        {
                            return track;
                        }
                    }
                }
            }

            return null;
        }

        private static string FindKeyAtTime(FaceTrackData track, float time)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                foreach (var key in track.BlendShape.Keys)
                {
                    if (key != null && Math.Abs(key.Time - time) <= TimeTolerance)
                    {
                        return key.KeyId;
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                foreach (var key in track.Transform.Keys)
                {
                    if (key != null && Math.Abs(key.Time - time) <= TimeTolerance)
                    {
                        return key.KeyId;
                    }
                }
            }

            return null;
        }

        private static List<float> CollectTrackTimes(FaceTrackData track)
        {
            var times = new List<float>();
            if (track == null)
            {
                return times;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                foreach (var key in track.BlendShape.Keys)
                {
                    if (key != null)
                    {
                        times.Add(key.Time);
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                foreach (var key in track.Transform.Keys)
                {
                    if (key != null)
                    {
                        times.Add(key.Time);
                    }
                }
            }

            return times;
        }

        private static bool ContainsTime(List<float> times, float target)
        {
            for (int i = 0; i < times.Count; i++)
            {
                if (Math.Abs(times[i] - target) <= TimeTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static TimelineClipboard.ClipboardItem NewItem(
            FaceTrackData track,
            string keyId,
            float time,
            InterpolationType interpolation,
            float floatValue,
            Vector3 vectorValue)
        {
            return new TimelineClipboard.ClipboardItem
            {
                TrackId = track.TrackId,
                Kind = track.Kind,
                RelativeTime = time,
                FloatValue = floatValue,
                VectorValue = vectorValue,
                Interpolation = interpolation
            };
        }

        private void SetOutcome(FaceMotion.Diagnostics.FaceMotionDiagnostic diagnostic)
        {
            _session.SetLastOperationDiagnostic(diagnostic);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }
    }
}
