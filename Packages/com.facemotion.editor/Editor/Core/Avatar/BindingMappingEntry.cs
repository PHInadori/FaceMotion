using System;
using FaceMotion.Data;
using UnityEngine;

namespace FaceMotion.Avatar
{
    /// <summary>
    /// Kind of a logical target. Additive: future kinds (Parameter, Material, Constraint)
    /// append new values and never reorder existing ones.
    /// </summary>
    public enum LogicalTargetKind
    {
        BlendShape = 0,
        Transform = 1
    }

    /// <summary>
    /// One mapping entry: a stable logical target ID mapping to a concrete, SDK-neutral
    /// avatar binding. The logical target ID is authoring identity ("mouth.smile"), not a
    /// display name. Exactly one binding class is populated to match the target kind; the
    /// other is left empty. Entry IDs are stable IDs and are intended for Undo and future
    /// UI editing.
    /// </summary>
    [Serializable]
    public sealed class BindingMappingEntry
    {
        [SerializeField] private string _entryId;
        [SerializeField] private string _logicalTargetId;
        [SerializeField] private LogicalTargetKind _targetKind = LogicalTargetKind.BlendShape;
        [SerializeField] private string _rendererPath;
        [SerializeField] private string _blendShapeName;
        [SerializeField] private string _transformPath;
        [SerializeField] private string _rendererNameHint;
        [SerializeField] private string _meshNameHint;
        [SerializeField] private string _transformNameHint;

        internal BindingMappingEntry()
        {
        }

        public string EntryId => _entryId;

        public string LogicalTargetId => _logicalTargetId;

        public LogicalTargetKind TargetKind => _targetKind;

        public string RendererNameHint => _rendererNameHint;

        public string MeshNameHint => _meshNameHint;

        public string TransformNameHint => _transformNameHint;

        /// <summary>The blend shape binding when this entry targets a blend shape.</summary>
        public BlendShapeBinding? BlendShape =>
            _targetKind == LogicalTargetKind.BlendShape
            && _rendererPath.Length > 0
            && _blendShapeName.Length > 0
                ? new BlendShapeBinding(_rendererPath, _blendShapeName, _rendererNameHint, _meshNameHint)
                : (BlendShapeBinding?)null;

        /// <summary>The transform binding when this entry targets a transform.</summary>
        public TransformBinding? Transform =>
            _targetKind == LogicalTargetKind.Transform && _transformPath.Length > 0
                ? new TransformBinding(_transformPath, _transformNameHint)
                : (TransformBinding?)null;

        /// <summary>True when the target kind holds a populated binding.</summary>
        public bool HasBinding => BlendShape.HasValue || Transform.HasValue;

        public static BindingMappingEntry CreateBlendShape(
            string logicalTargetId,
            string rendererPath,
            string blendShapeName,
            string rendererNameHint = null,
            string meshNameHint = null,
            string entryId = null)
        {
            if (string.IsNullOrWhiteSpace(logicalTargetId))
            {
                throw new ArgumentException("A logical target ID is required.", nameof(logicalTargetId));
            }

            if (string.IsNullOrEmpty(rendererPath) || string.IsNullOrEmpty(blendShapeName))
            {
                throw new ArgumentException("A renderer path and blend shape name are required.", nameof(rendererPath));
            }

            return new BindingMappingEntry
            {
                _entryId = entryId ?? StableId.New(),
                _logicalTargetId = logicalTargetId,
                _targetKind = LogicalTargetKind.BlendShape,
                _rendererPath = rendererPath,
                _blendShapeName = blendShapeName,
                _transformPath = string.Empty,
                _rendererNameHint = rendererNameHint ?? string.Empty,
                _meshNameHint = meshNameHint ?? string.Empty,
                _transformNameHint = string.Empty
            };
        }

        public static BindingMappingEntry CreateTransform(
            string logicalTargetId,
            string transformPath,
            string transformNameHint = null,
            string entryId = null)
        {
            if (string.IsNullOrWhiteSpace(logicalTargetId))
            {
                throw new ArgumentException("A logical target ID is required.", nameof(logicalTargetId));
            }

            if (string.IsNullOrEmpty(transformPath))
            {
                throw new ArgumentException("A transform path is required.", nameof(transformPath));
            }

            return new BindingMappingEntry
            {
                _entryId = entryId ?? StableId.New(),
                _logicalTargetId = logicalTargetId,
                _targetKind = LogicalTargetKind.Transform,
                _rendererPath = string.Empty,
                _blendShapeName = string.Empty,
                _transformPath = transformPath,
                _rendererNameHint = string.Empty,
                _meshNameHint = string.Empty,
                _transformNameHint = transformNameHint ?? string.Empty
            };
        }

        /// <summary>Deep copy preserving the entry ID (internal transactions, clone).</summary>
        public BindingMappingEntry Clone()
        {
            return new BindingMappingEntry
            {
                _entryId = _entryId,
                _logicalTargetId = _logicalTargetId,
                _targetKind = _targetKind,
                _rendererPath = _rendererPath,
                _blendShapeName = _blendShapeName,
                _transformPath = _transformPath,
                _rendererNameHint = _rendererNameHint,
                _meshNameHint = _meshNameHint,
                _transformNameHint = _transformNameHint
            };
        }

        internal void SetEntryIdForMigration(string id)
        {
            _entryId = id ?? string.Empty;
        }

        /// <summary>User-facing duplicate: new entry ID, preserved logical ID and binding.</summary>
        public BindingMappingEntry Duplicate(string newEntryId = null)
        {
            var duplicate = Clone();
            duplicate._entryId = newEntryId ?? StableId.New();
            return duplicate;
        }
    }
}