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

        public void SetHover(BlendShapeBinding binding)
        {
            if (_binding.HasValue && _binding.Value.Equals(binding))
            {
                return;
            }

            _binding = binding;
            ChangeCount++;
        }

        public void Clear()
        {
            if (!_binding.HasValue)
            {
                return;
            }

            _binding = null;
            ChangeCount++;
        }
    }
}