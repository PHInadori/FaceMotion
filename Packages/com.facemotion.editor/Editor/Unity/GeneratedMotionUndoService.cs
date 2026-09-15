using System;
using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Generation;

namespace FaceMotion.Editor
{
    /// <summary>Unity boundary for applying one generated pass as one reversible asset change.</summary>
    public static class GeneratedMotionUndoService
    {
        public static GeneratedMotionApplyResult Apply(
            FaceMotionProject project,
            FaceMotionAnimationData animation,
            GeneratedMotion motion,
            GeneratorType type,
            string sourcePresetId,
            string settingsHash,
            string settingsSnapshot = null,
            string undoLabel = "Apply FaceMotion generation")
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            using (var transaction = new UnityUndoTransaction())
            {
                transaction.Begin(project, undoLabel);
                var result = GeneratedMotionApplier.Apply(project, animation, motion, type, sourcePresetId, settingsHash, settingsSnapshot);
                transaction.Commit();
                return result;
            }
        }
    }
}
