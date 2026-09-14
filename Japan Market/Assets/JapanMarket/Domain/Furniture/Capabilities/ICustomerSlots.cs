using System;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Pontos onde um cliente pode parar para usar o móvel.
    ///
    /// Substitui o <c>FurnitureOccupancy</c> atual, que vaza reservas de forma
    /// permanente: lá, <c>Release</c> recalcula a posição de cada slot a partir
    /// do transform vivo e compara com a posição guardada, com tolerância de 1 cm.
    /// Basta o jogador mover ou girar a prateleira entre reservar e liberar para
    /// que nada seja liberado. Depois de dois vazamentos a prateleira fica
    /// invisível para todos os NPCs, para sempre, sem nenhum erro no console.
    ///
    /// Aqui a reserva é um objeto com o ÍNDICE do slot. Mover o móvel não afeta
    /// nada, e descartar a reserva libera exatamente o slot certo.
    /// </summary>
    public interface ICustomerSlots : IFurnitureCapability
    {
        int Capacity { get; }
        int FreeCount { get; }
        bool HasFreeSlot { get; }

        /// <summary>
        /// Reserva um slot. Descarte a reserva para liberar — de preferência no
        /// <c>Exit()</c> do estado que reservou, para que nenhum caminho de saída
        /// consiga esquecer.
        /// </summary>
        bool TryReserve(out ISlotReservation reservation);
    }

    /// <summary>
    /// Uma reserva de slot. Descarte para liberar; descartar duas vezes é seguro.
    /// </summary>
    public interface ISlotReservation : IDisposable
    {
        int SlotIndex { get; }

        /// <summary>False se já foi liberada ou se o móvel deixou de existir.</summary>
        bool IsValid { get; }

        /// <summary>
        /// Posição do slot AGORA. Recalculada ao vivo de propósito: se o jogador
        /// mover o móvel enquanto o NPC caminha até ele, o destino acompanha em
        /// vez de o NPC andar até onde o móvel estava.
        /// </summary>
        Vector3 WorldPosition { get; }

        /// <summary>Direção que o cliente deve encarar ao chegar (para o móvel).</summary>
        Vector3 Facing { get; }
    }
}
