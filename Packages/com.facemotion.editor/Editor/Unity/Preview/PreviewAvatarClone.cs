using System;
using UnityEngine;

namespace FaceMotion.Editor.Preview
{
    /// <summary>A hidden, disposable avatar clone used exclusively by editor preview.</summary>
    public sealed class PreviewAvatarClone : IDisposable
    {
        public PreviewAvatarClone(GameObject source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(source);
                clone.name = source.name + " (FaceMotion Preview)";
                foreach (var transform in clone.GetComponentsInChildren<Transform>(true))
                {
                    transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }

                foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true))
                {
                    behaviour.enabled = false;
                }

                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.SetActive(true);
                Root = clone;
            }
            catch
            {
                if (clone != null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }

                throw;
            }
        }

        public GameObject Root { get; private set; }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
                Root = null;
            }
        }
    }
}
