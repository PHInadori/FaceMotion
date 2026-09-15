using System;

namespace FaceMotion.Data
{
    /// <summary>
    /// Generates and validates opaque stable IDs. A stable ID is a lowercase,
    /// separator-free, 32-character GUID string and carries no semantic meaning.
    /// </summary>
    public static class StableId
    {
        public const int Length = 32;

        /// <summary>Creates a new stable ID for a freshly authored object.</summary>
        public static string New()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>Returns true when the value is a lowercase 32-character GUID string.</summary>
        public static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != Length)
            {
                return false;
            }

            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool isLowerHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isLowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when the value is present but malformed and needs an explicit repair.</summary>
        public static bool RequiresRepair(string id)
        {
            return !string.IsNullOrEmpty(id) && !IsValid(id);
        }
    }
}