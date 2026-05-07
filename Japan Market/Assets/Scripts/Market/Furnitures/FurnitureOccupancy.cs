using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente adicionado a cada FurnitureInstance (ou gerenciado pelo FurnitureManager).
/// Controla quantos NPCs podem interagir simultaneamente com uma prateleira
/// e oferece posições de espera distribuídas, evitando que todos fiquem no mesmo ponto.
/// 
/// Como usar: adicione este componente no prefab de FurnitureInstance.
/// </summary>
public class FurnitureOccupancy : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Quantos NPCs podem estar nesta furniture ao mesmo tempo.")]
    [SerializeField] private int _maxOccupants = 2;

    [Tooltip("Offsets locais para cada slot de interação (gerados automaticamente se vazios).")]
    [SerializeField] private Vector3[] _slotOffsets;

    private readonly HashSet<int> _occupiedSlots = new HashSet<int>();

    // ─── Public API ───────────────────────────────────────────────────────────

    public bool HasFreeSlot => _occupiedSlots.Count < _maxOccupants;

    /// <summary>
    /// Tenta reservar um slot. Retorna a posição mundial do slot ou null se cheio.
    /// </summary>
    public bool TryReserve(out Vector3 slotPosition)
    {
        for (int i = 0; i < _maxOccupants; i++)
        {
            if (_occupiedSlots.Contains(i)) continue;

            _occupiedSlots.Add(i);
            slotPosition = GetSlotWorldPosition(i);
            return true;
        }

        slotPosition = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Libera o slot associado à posição reservada.
    /// </summary>
    public void Release(Vector3 reservedPosition)
    {
        for (int i = 0; i < _maxOccupants; i++)
        {
            if (Vector3.Distance(GetSlotWorldPosition(i), reservedPosition) < 0.01f)
            {
                _occupiedSlots.Remove(i);
                return;
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Vector3 GetSlotWorldPosition(int slotIndex)
    {
        if (_slotOffsets != null && slotIndex < _slotOffsets.Length)
            return transform.TransformPoint(_slotOffsets[slotIndex]);

        // Fallback: distribui os slots lateralmente ao redor do centro
        float angle = slotIndex * (360f / _maxOccupants);
        float radius = 0.6f;
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
        return transform.position + offset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _maxOccupants; i++)
            Gizmos.DrawWireSphere(GetSlotWorldPosition(i), 0.15f);
    }
}