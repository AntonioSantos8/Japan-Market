using System;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Como as unidades de um produto se organizam num espaço — prateleira ou caixa.
    ///
    /// Porte direto do <c>ItemGridSettings</c> atual, que funciona bem. Duas
    /// mudanças: entrou num namespace (para não colidir com o legado durante a
    /// migração) e ganhou validação de eixo, para que um grid mal configurado
    /// apareça no import em vez de virar uma prateleira que não aceita nada.
    /// </summary>
    [Serializable]
    public sealed class ItemGrid
    {
        [Tooltip("Unidades por eixo. X = largura, Y = altura, Z = profundidade.")]
        [SerializeField] private Vector3Int _count = Vector3Int.one;

        [Tooltip("Distância entre os centros de duas unidades vizinhas, por eixo.")]
        [SerializeField] private Vector3 _spacing = Vector3.zero;

        [Tooltip("Deslocamento da primeira unidade a partir da origem do espaço.")]
        [SerializeField] private Vector3 _originOffset = Vector3.zero;

        [SerializeField] private Vector3 _itemRotation = Vector3.zero;
        [SerializeField] private Vector3 _itemScale = Vector3.one;

        public Vector3Int Count => _count;
        public Vector3 Spacing => _spacing;
        public Vector3 OriginOffset => _originOffset;
        public Vector3 ItemScale => _itemScale;

        public Quaternion Rotation => Quaternion.Euler(_itemRotation);

        /// <summary>Zero se qualquer eixo for inválido — nunca um número negativo.</summary>
        public int Capacity =>
            (_count.x > 0 && _count.y > 0 && _count.z > 0) ? _count.x * _count.y * _count.z : 0;

        public bool IsValid => Capacity > 0;

        /// <summary>
        /// Posição local do slot <paramref name="index"/>. Preenche camada por
        /// camada: completa a base X×Z antes de subir em Y.
        /// </summary>
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

        /// <summary>Cópia dos valores de um grid legado, durante a migração.</summary>
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
