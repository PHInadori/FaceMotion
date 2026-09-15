using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Serialization;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Serialization;
using UnityEditor;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>Project lifecycle: create, load, normalize, validate, save.</summary>
    public sealed class ProjectController
    {
        public const string DefaultProjectFolder = "Assets/FaceMotion/Projects";

        private readonly FaceMotionEditorSession _session;

        public ProjectController(FaceMotionEditorSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool HasOpenProject => _session.ActiveProject != null;

        public string ActiveProjectAssetPath => _session.ActiveProjectAssetPath;

        public OperationResult CreateProject()
        {
            EnsureDefaultFolder();
            string path = EditorUtility.SaveFilePanelInProject(
                "Create FaceMotion Project",
                "FaceMotionProject",
                "asset",
                "Create a new FaceMotion project",
                DefaultProjectFolder);

            if (string.IsNullOrEmpty(path))
            {
                SetOutcome(ControllerDiagnostics.Info("Project creation cancelled."));
                return new OperationResult();
            }

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                SetOutcome(ControllerDiagnostics.AssetCreateFailed(path));
                return new OperationResult();
            }

            var project = FaceMotionProject.CreateNew();
            AssetDatabase.CreateAsset(project, path);
            if (project == null || !AssetDatabase.Contains(project))
            {
                SetOutcome(ControllerDiagnostics.AssetCreateFailed(path));
                return new OperationResult();
            }

            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
            _session.SetActiveProject(project, path);
            return new OperationResult { Succeeded = true };
        }

        public void LoadProject(FaceMotionProject asset)
        {
            if (asset == null)
            {
                _session.ClearProject();
                return;
            }

            var migration = ProjectMigrationService.TryMigrateOnLoad(asset);
            if (IsRefused(migration.Status))
            {
                SetOutcome(FirstBlocking(migration.Diagnostics) ?? ControllerDiagnostics.Info("The project could not be opened in its current schema."));
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            _session.SetActiveProject(asset, string.IsNullOrEmpty(path) ? null : path);

            if (migration.Diagnostics != null && migration.Diagnostics.Count > 0)
            {
                _session.LastProjectValidation = new ValidationReport(migration.Diagnostics);
            }

            if (migration.Status == ProjectMigrationStatus.Migrated)
            {
                _session.SetLastOperationDiagnostic(ControllerDiagnostics.Info("Project was upgraded to the current schema."));
            }

            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        public void NormalizeProject()
        {
            var project = _session.ActiveProject;
            if (project == null)
            {
                SetOutcome(ControllerDiagnostics.NoProject());
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(project, "Normalize Project");
            ProjectNormalizer.Normalize(project);
            EditorUtility.SetDirty(project);
            _session.RefreshAll();
        }

        public void ValidateProject()
        {
            var project = _session.ActiveProject;
            if (project == null)
            {
                SetOutcome(ControllerDiagnostics.NoProject());
                return;
            }

            _session.LastProjectValidation = ProjectValidator.Validate(project);
            _session.SetLastOperationDiagnostic(null);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        public void SaveProject()
        {
            var project = _session.ActiveProject;
            if (project == null)
            {
                return;
            }

            var migration = ProjectMigrationService.TryMigrateOnLoad(project);
            if (IsRefused(migration.Status))
            {
                SetOutcome(FirstBlocking(migration.Diagnostics) ?? ControllerDiagnostics.Info("The project cannot be saved in its current schema."));
                return;
            }

            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
        }

        private static bool IsRefused(ProjectMigrationStatus status)
        {
            return status == ProjectMigrationStatus.FutureSchema
                || status == ProjectMigrationStatus.Uninitialized
                || status == ProjectMigrationStatus.VersionGap
                || status == ProjectMigrationStatus.MigrationFailed;
        }

        private static FaceMotionDiagnostic FirstBlocking(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            if (diagnostics != null)
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    if (diagnostics[i] != null && diagnostics[i].Blocking)
                    {
                        return diagnostics[i];
                    }
                }
            }

            return null;
        }

        private void SetOutcome(FaceMotion.Diagnostics.FaceMotionDiagnostic diagnostic)
        {
            _session.SetLastOperationDiagnostic(diagnostic);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        private static void EnsureDefaultFolder()
        {
            if (AssetDatabase.IsValidFolder(DefaultProjectFolder))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/FaceMotion"))
            {
                AssetDatabase.CreateFolder("Assets", "FaceMotion");
            }

            if (!AssetDatabase.IsValidFolder(DefaultProjectFolder))
            {
                AssetDatabase.CreateFolder("Assets/FaceMotion", "Projects");
            }

            AssetDatabase.SaveAssets();
        }
    }
}