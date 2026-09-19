using System;
using UnityEngine;

namespace JapanMarket.Data
{

    [Serializable]
    public sealed class ItemGrid
    {
        [Tooltip("Units per axis. X = width, Y = height, Z = depth.")]
        [SerializeField] private Vector3Int _count = Vector3Int.one;

        [Tooltip("Distance between the centers of two neighboring units, per axis.")]
        [SerializeField] private Vector3 _spacing = Vector3.zero;

        [Tooltip("Offset of the first unit from the origin of the space.")]
        [SerializeField] private Vector3 _originOffset = Vector3.zero;

        [SerializeField] private Vector3 _itemRotation = Vector3.zero;
        [SerializeField] private Vector3 _itemScale = Vector3.one;

        public Vector3Int Count => _count;
        public Vector3 Spacing => _spacing;
        public Vector3 OriginOffset => _originOffset;
        public Vector3 ItemScale => _itemScale;

        public Quaternion Rotation => Quaternion.Euler(_itemRotation);

        public int Capacity =>
            (_count.x > 0 && _count.y > 0 && _count.z > 0) ? _count.x * _count.y * _count.z : 0;

        public bool IsValid => Capacity > 0;

        public Vector3 GetLocalPosition(int index)
        {
            int layerCapacity = _count.x * _count.z;
            if (layerCapacity <= 0 || _count.y <= 0 || index < 0) return _originOffset;

            int y = index / layerCapacity;
            int remainder = index % layerCapacity;
            int z = remainder / _count.x;
            int x = remainder % _count.x;

            return _originOffset + new Vector3(x * _spacing.x, y * _spacing.y, z * _spacing.z);
        }

        public void CopyFrom(Vector3Int count, Vector3 spacing, Vector3 originOffset,
                             Vector3 itemRotation, Vector3 itemScale)
        {
            _count        = count;
            _spacing      = spacing;
            _originOffset = originOffset;
            _itemRotation = itemRotation;
            _itemScale    = itemScale;
        }
    }
}
