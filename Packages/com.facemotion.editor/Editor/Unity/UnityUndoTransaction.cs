using FaceMotion.Data;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor
{
    /// <summary>
    /// UnityEditor Undo adapter. Begins a complete-object snapshot of the project, commits
    /// by marking the asset dirty, and stays structurally decoupled from Core.
    /// </summary>
    public sealed class UnityUndoTransaction : IUndoTransaction
    {
        private FaceMotionProject _project;

        public void Begin(FaceMotionProject project, string undoLabel)
        {
            _project = project;
            Undo.RegisterCompleteObjectUndo(project, undoLabel ?? "FaceMotion change");
        }

        public void Commit()
        {
            if (_project != null)
            {
                EditorUtility.SetDirty(_project);
            }

            Close();
        }

        public void Rollback()
        {
            if (_project != null)
            {
                EditorUtility.SetDirty(_project);
            }

            Close();
        }

        public void Dispose()
        {
            Close();
        }

        private void Close()
        {
            _project = null;
        }
    }
}