using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Versioning;
using UnityEngine;

namespace FaceMotion.Avatar
{
    /// <summary>
    /// An avatar-specific binding table. It is a separate ScriptableObject from the
    /// FaceMotionProject and from shared presets so that avatar-specific knowledge never
    /// pollutes the project graph. The profile knows which avatar structure it was built
    /// for through AvatarFingerprint; the fingerprint is advisory for staleness detection
    /// while bindings are re-validated individually.
    /// </summary>
    public sealed class AvatarMappingProfile : ScriptableObject
    {
        [SerializeField] private string _profileId;
        [SerializeField] private int _schemaVersion;
        [SerializeField] private AvatarFingerprint _avatarFingerprint;
        [SerializeField] private string _displayName;
        [SerializeField] private List<BindingMappingEntry> _mappings = new List<BindingMappingEntry>();

        public string ProfileId => _profileId;

        public int SchemaVersion => _schemaVersion;

        public AvatarFingerprint AvatarFingerprint => _avatarFingerprint;

        public string DisplayName => _displayName;

        public IReadOnlyList<BindingMappingEntry> Mappings => _mappings;

        public static AvatarMappingProfile CreateNew(string displayName = null)
        {
            var profile = ScriptableObject.CreateInstance<AvatarMappingProfile>();
            profile._profileId = StableId.New();
            profile._schemaVersion = FaceMotionVersions.MappingProfileSchemaVersion;
            profile._displayName = displayName ?? string.Empty;
            profile._mappings = new List<BindingMappingEntry>();
            return profile;
        }

        /// <summary>Deep copy preserving every ID (internal transactions).</summary>
        public static AvatarMappingProfile CreateClone(AvatarMappingProfile source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var clone = ScriptableObject.CreateInstance<AvatarMappingProfile>();
            clone._profileId = source._profileId;
            clone._schemaVersion = source._schemaVersion;
            clone._avatarFingerprint = source._avatarFingerprint == null ? null : source._avatarFingerprint.Clone();
            clone._displayName = source._displayName;
            var mappings = source._mappings ?? new List<BindingMappingEntry>();
            clone._mappings = new List<BindingMappingEntry>(mappings.Count);
            for (int i = 0; i < mappings.Count; i++)
            {
                clone._mappings.Add(mappings[i] == null ? null : mappings[i].Clone());
            }

            return clone;
        }

        /// <summary>
        /// User-facing duplicate: new profile ID and new entry IDs while the fingerprint,
        /// display name, and binding content are preserved.
        /// </summary>
        public static AvatarMappingProfile CreateDuplicate(AvatarMappingProfile source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var duplicate = ScriptableObject.CreateInstance<AvatarMappingProfile>();
            duplicate._profileId = StableId.New();
            duplicate._schemaVersion = FaceMotionVersions.MappingProfileSchemaVersion;
            duplicate._avatarFingerprint = source._avatarFingerprint == null ? null : source._avatarFingerprint.Clone();
            duplicate._displayName = source._displayName;
            duplicate._mappings = new List<BindingMappingEntry>(source._mappings.Count);
            for (int i = 0; i < source._mappings.Count; i++)
            {
                duplicate._mappings.Add(source._mappings[i] == null ? null : source._mappings[i].Duplicate());
            }

            return duplicate;
        }

        /// <summary>Authoring entry point for binding the profile to a scanned avatar.</summary>
        public void SetAvatarFingerprint(AvatarFingerprint fingerprint)
        {
            _avatarFingerprint = fingerprint == null ? null : fingerprint.Clone();
        }

        public void AddMapping(BindingMappingEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _mappings.Add(entry);
        }

        public bool RemoveMapping(string entryId)
        {
            int index = FindMappingIndex(entryId);
            if (index < 0)
            {
                return false;
            }

            _mappings.RemoveAt(index);
            return true;
        }

        /// <summary>Mechanical repairs only: recreates a missing list and removes null entries.</summary>
        public void NormalizeStructure()
        {
            if (_mappings == null)
            {
                _mappings = new List<BindingMappingEntry>();
            }

            for (int i = _mappings.Count - 1; i >= 0; i--)
            {
                if (_mappings[i] == null)
                {
                    _mappings.RemoveAt(i);
                }
            }
        }

        internal void SetProfileIdForMigration(string id)
        {
            _profileId = id ?? string.Empty;
        }

        internal void SetSchemaVersionForMigration(int version)
        {
            _schemaVersion = version;
        }

        internal int FindMappingIndex(string entryId)
        {
            if (string.IsNullOrEmpty(entryId))
            {
                return -1;
            }

            for (int i = 0; i < _mappings.Count; i++)
            {
                if (_mappings[i] != null
                    && string.Equals(_mappings[i].EntryId, entryId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}