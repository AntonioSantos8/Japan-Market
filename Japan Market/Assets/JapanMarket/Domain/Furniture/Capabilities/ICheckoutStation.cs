using System;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um ponto de checkout da loja.
    ///
    /// Este contrato existe desde já, mesmo com a implementação só chegando na
    /// Fase 5, por um motivo prático: é ele que permite a Fase 4 escrever o
    /// estado <c>SeekingCheckout</c> do NPC contra algo estável, e é ele que
    /// transforma "a caixa registradora" em "uma das caixas registradoras".
    ///
    /// Nenhum NPC guarda referência a uma estação específica. Ele pede uma ao
    /// serviço, e devolve quando termina ou quando ela some.
    /// </summary>
    public interface ICheckoutStation : IFurnitureCapability
    {
        CheckoutStationState State { get; }

        /// <summary>Aceita clientes agora? Falso se o jogador desligou ou se está removendo.</summary>
        bool IsOperational { get; }

        /// <summary>Clientes esperando, incluindo quem está sendo atendido.</summary>
        int QueueLength { get; }

        /// <summary>Onde a fila começa e para que lado ela cresce.</summary>
        Vector3 QueueAnchor { get; }
        Vector3 QueueDirection { get; }
        float QueueSpacing { get; }

        /// <summary>Onde o cliente coloca as compras.</summary>
        Vector3 CounterPosition { get; }

        event Action<ICheckoutStation> StateChanged;
    }

    public enum CheckoutStationState
    {
        /// <summary>Livre, sem ninguém na fila.</summary>
        Idle = 0,

        /// <summary>Tem fila, mas o jogador não está no caixa.</summary>
        Waiting = 1,

        /// <summary>O jogador está atendendo.</summary>
        Serving = 2,

        /// <summary>Desligada pelo jogador ou saindo da loja. Não aceita mais ninguém.</summary>
        Unavailable = 3,
    }
}
