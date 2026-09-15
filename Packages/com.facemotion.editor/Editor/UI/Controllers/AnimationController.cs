using System;
using FaceMotion.Data;
using FaceMotion.Editor.UI.Session;
using UnityEngine;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>Animation list operations. Selection is always held as a stable animation ID.</summary>
    public sealed class AnimationController
    {
        private readonly FaceMotionEditorSession _session;

        public AnimationController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public FaceMotionAnimationData GetSelected()
        {
            return _session.GetSelectedAnimation();
        }

        public string Select(string animationId)
        {
            if (_session.ActiveProject == null
                || !_session.ActiveProject.TryGetAnimation(animationId, out _))
            {
                return null;
            }

            _session.SelectedAnimationId = animationId;
            _session.SelectedTrackId = null;
            _session.Selection.Clear();
            _session.NotifyChanged();
            return animationId;
        }

        public string Add()
        {
            var project = _session.ActiveProject;
            if (project == null)
            {
                SetOutcome(ControllerDiagnostics.NoProject());
                return null;
            }

            string name = "Animation " + (project.Animations.Count + 1);
            var result = UICommandRunner.Run(_session, new AddAnimationCommand(name));
            if (!result.Succeeded)
            {
                return null;
            }

            int count = project.Animations.Count;
            if (count > 0)
            {
                string id = project.Animations[count - 1].AnimationId;
                Select(id);
                return id;
            }

            return null;
        }

        public string Duplicate()
        {
            string id = _session.SelectedAnimationId;
            if (string.IsNullOrEmpty(id))
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return null;
            }

            var command = new DuplicateAnimationCommand(id);
            var result = UICommandRunner.Run(_session, command);
            if (!result.Succeeded)
            {
                return null;
            }

            string createdId = command.CreatedAnimationId;
            if (!string.IsNullOrEmpty(createdId))
            {
                Select(createdId);
            }

            return createdId;
        }

        public bool Rename(string displayName)
        {
            string id = _session.SelectedAnimationId;
            if (string.IsNullOrEmpty(id))
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            var result = UICommandRunner.Run(_session, new RenameAnimationCommand(id, displayName));
            return result.Succeeded;
        }

        public bool Delete()
        {
            string id = _session.SelectedAnimationId;
            if (string.IsNullOrEmpty(id))
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            _session.SelectedAnimationId = null;
            _session.SelectedTrackId = null;
            _session.Selection.Clear();
            var result = UICommandRunner.Run(_session, new RemoveAnimationCommand(id));
            return result.Succeeded;
        }

        public bool SetDuration(float duration)
        {
            string id = _session.SelectedAnimationId;
            if (string.IsNullOrEmpty(id))
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            if (!IsPositiveFinite(duration))
            {
                SetOutcome(ControllerDiagnostics.InvalidValue("Duration must be a finite value greater than zero."));
                return false;
            }

            var result = UICommandRunner.Run(_session, new SetTimelineDurationCommand(id, duration));
            if (result.Succeeded)
            {
                _session.SetCurrentTime(Mathf.Min(_session.ViewState.CurrentTime, duration));
                _session.ViewState.ScrollTime = Mathf.Min(_session.ViewState.ScrollTime, duration);
                _session.LastProjectValidation = ProjectValidator.Validate(_session.ActiveProject);
                _session.RecomputeDiagnostics();
                _session.NotifyChanged();
            }

            return result.Succeeded;
        }

        public bool SetFrameRate(float frameRate)
        {
            string id = _session.SelectedAnimationId;
            if (string.IsNullOrEmpty(id))
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            if (!IsPositiveFinite(frameRate))
            {
                SetOutcome(ControllerDiagnostics.InvalidValue("Frame rate must be a finite value greater than zero."));
                return false;
            }

            var result = UICommandRunner.Run(_session, new SetTimelineFrameRateCommand(id, frameRate));
            return result.Succeeded;
        }

        public bool SetLoop(bool loop)
        {
            string id = _session.SelectedAnimationId;
            if (string.IsNullOrEmpty(id))
            {
                SetOutcome(ControllerDiagnostics.NoSelection("animation"));
                return false;
            }

            var result = UICommandRunner.Run(_session, new SetTimelineLoopCommand(id, loop));
            return result.Succeeded;
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private void SetOutcome(FaceMotion.Diagnostics.FaceMotionDiagnostic diagnostic)
        {
            _session.SetLastOperationDiagnostic(diagnostic);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }
    }
}
