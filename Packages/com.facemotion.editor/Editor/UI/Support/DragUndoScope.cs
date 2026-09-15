using FaceMotion.Data;
using UnityEditor;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>
    /// One Undo transaction for an interactive drag. The snapshot is recorded once on the
    /// first Begin; every intermediate update mutates the same Undo record, and Commit seals
    /// the record with FlushUndoRecordObjects. This keeps an entire mouse drag as one Undo
    /// entry even though data changes every frame.
    /// </summary>
    public sealed class DragUndoScope
    {
        private FaceMotionProject _project;
        private bool _begun;
        private int _undoGroup = -1;

        public bool IsBegun => _begun;

        public void Begin(FaceMotionProject project, string label)
        {
            if (_begun)
            {
                return;
            }

            _project = project;
            if (_project != null)
            {
                Undo.IncrementCurrentGroup();
                _undoGroup = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(_project, label ?? "FaceMotion drag");
                _begun = true;
            }
        }

        public void Commit()
        {
            if (_begun && _project != null)
            {
                EditorUtility.SetDirty(_project);
                Undo.FlushUndoRecordObjects();
            }

            Close();
        }

        public void Rollback()
        {
            if (_begun && _project != null)
            {
                // RegisterCompleteObjectUndo captured the state at drag start. Revert the
                // isolated group so Escape/cancel does not leave the previewed move authored.
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(_undoGroup);
            }

            Close();
        }

        private void Close()
        {
            _begun = false;
            _project = null;
            _undoGroup = -1;
        }
    }
}
