using FaceMotion.Data;

namespace FaceMotion.Editor.UI.Preview
{
    /// <summary>
    /// Transient, preview-clone-only blend shape override (hover). Exists purely in the
    /// editor UI; it never touches the active avatar, the scene, or project data.
    /// </summary>
    public sealed class PreviewOverrideState
    {
        private BlendShapeBinding? _binding;

        public BlendShapeBinding? Binding => _binding;

        public bool HasActive => _binding.HasValue;

        /// <summary>Monotonic change counter so panels can trigger a preview re-evaluation.</summary>
        public int ChangeCount { get; private set; }

        /// <returns>True when the preview override changed.</returns>
        public bool SetHover(BlendShapeBinding binding)
        {
            if (_binding.HasValue && _binding.Value.Equals(binding))
            {
                return false;
            }

            _binding = binding;
            ChangeCount++;
            return true;
        }

        /// <returns>True when an active preview override was cleared.</returns>
        public bool Clear()
        {
            if (!_binding.HasValue)
            {
                return false;
            }

            _binding = null;
            ChangeCount++;
            return true;
        }
    }
}
