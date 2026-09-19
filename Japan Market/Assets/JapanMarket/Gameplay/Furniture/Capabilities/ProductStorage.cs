using System;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class ProductStorage : FurnitureCapabilityBehaviour, IProductStorage
    {
        [Tooltip("Condition this furniture offers. Only products that require it can enter.")]
        [SerializeField] private StorageTrait _providedStorage;

        [Tooltip("Uma âncora por seção. Vazio = uma seção, na origem do móvel. " +
                 "Quatro âncoras fazem a prateleira quádrupla, sem código novo.")]
        [SerializeField] private Transform[] _sectionAnchors;

        [Tooltip("Instancia o prefab do produto nos slots. Desligue quando a " +
                 "apresentação for feita por outro componente.")]
        [SerializeField] private bool _spawnVisuals = true;

        private ItemDefinition _product;
        private bool[] _occupied;
        private Transform[] _slotVisuals;
        private int _count;

        public event Action<IProductStorage> ContentsChanged;

        public StorageTrait ProvidedStorage => _providedStorage;
        public ItemDefinition CurrentProduct => _product;
        public int Count => _count;
        public bool IsEmpty => _count == 0;

        public int Capacity => _product == null
            ? 0
            : _product.ShelfGrid.Capacity * SectionCount;

        public bool IsFull => _product != null && _count >= Capacity;

        private int SectionCount =>
            _sectionAnchors != null && _sectionAnchors.Length > 0 ? _sectionAnchors.Length : 1;

        public bool Accepts(ItemDefinition product)
        {
            if (product == null) return false;

            if (_providedStorage != null && !product.FitsStorage(_providedStorage)) return false;

            if (_product == null) return product.ShelfGrid.Capacity > 0;

            return product == _product && !IsFull;
        }

        public bool TryPlace(ItemDefinition product, out int slotIndex)
        {
            slotIndex = -1;
            if (!Accepts(product)) return false;

            if (_product == null) Adopt(product);

            slotIndex = FindFreeSlot();
            if (slotIndex < 0) return false;

            _occupied[slotIndex] = true;
            _count++;

            if (_spawnVisuals) SpawnVisual(product, slotIndex);

            ContentsChanged?.Invoke(this);
            return true;
        }

        public bool TryTakeOne(out ItemDefinition product)
        {
            product = null;
            if (_product == null || _count == 0) return false;

            for (int i = _occupied.Length - 1; i >= 0; i--)
            {
                if (!_occupied[i]) continue;

                product = _product;
                _occupied[i] = false;
                DespawnVisual(i);
                _count--;

                if (_count == 0) Clear();

                ContentsChanged?.Invoke(this);
                return true;
            }

            Debug.LogWarning($"[ProductStorage] '{name}': contagem {_count} sem slot " +
                             "ocupado correspondente. Estoque zerado.", this);
            Clear();
            ContentsChanged?.Invoke(this);
            return false;
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (this == null) return Vector3.zero;
            if (_product == null) return transform.position;

            int perSection = Mathf.Max(1, _product.ShelfGrid.Capacity);
            int section = Mathf.Clamp(slotIndex / perSection, 0, SectionCount - 1);
            int local = slotIndex % perSection;

            Transform anchor = GetSectionAnchor(section);
            return anchor.TransformPoint(_product.ShelfGrid.GetLocalPosition(local));
        }

        private Transform GetSectionAnchor(int section)
        {
            if (_sectionAnchors != null && section >= 0 && section < _sectionAnchors.Length
                && _sectionAnchors[section] != null)
                return _sectionAnchors[section];

            return transform;
        }

        private void Adopt(ItemDefinition product)
        {
            _product = product;
            _occupied = new bool[Capacity];
            _slotVisuals = new Transform[Capacity];
            _count = 0;
        }

        private void Clear()
        {
            _product = null;
            _occupied = null;
            _slotVisuals = null;
            _count = 0;
        }

        private int FindFreeSlot()
        {
            if (_occupied == null) return -1;

            for (int i = 0; i < _occupied.Length; i++)
                if (!_occupied[i]) return i;

            return -1;
        }

        private void SpawnVisual(ItemDefinition product, int slotIndex)
        {
            if (product.ItemPrefab == null) return;

            int perSection = Mathf.Max(1, product.ShelfGrid.Capacity);
            Transform anchor = GetSectionAnchor(slotIndex / perSection);

            GameObject spawned = Instantiate(product.ItemPrefab, anchor);
            Transform t = spawned.transform;

            t.localPosition = product.ShelfGrid.GetLocalPosition(slotIndex % perSection);
            t.localRotation = product.ShelfGrid.Rotation;
            t.localScale = product.ShelfGrid.ItemScale;

            if (spawned.TryGetComponent(out Rigidbody body)) body.isKinematic = true;

            _slotVisuals[slotIndex] = t;
        }

        private void DespawnVisual(int slotIndex)
        {
            if (_slotVisuals == null) return;

            Transform visual = _slotVisuals[slotIndex];
            _slotVisuals[slotIndex] = null;

            if (visual != null) Destroy(visual.gameObject);
        }

    }
}
