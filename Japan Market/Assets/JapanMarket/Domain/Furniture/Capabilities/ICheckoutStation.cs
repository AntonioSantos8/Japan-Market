using System;
using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um ponto de checkout da loja.
    ///
    /// É a capacidade que substitui o <c>CashRegister</c> como objeto especial:
    /// caixa deixa de ser "o" caixa e vira "um móvel que tem esta capacidade".
    /// Nenhum NPC guarda referência a uma estação específica — ele pede uma ao
    /// <see cref="ICheckoutService"/>, e devolve quando termina ou quando ela
    /// some.
    ///
    /// A estação é a dona da própria fila e da própria venda em andamento. Quem
    /// fecha a venda é o serviço, porque fechar envolve dinheiro e evento — mas
    /// é a estação que guarda o estado, e por isso o serviço a avisa por
    /// <see cref="CloseSession"/> em vez de mexer nele por fora.
    /// </summary>
    public interface ICheckoutStation : IFurnitureCapability
    {
        CheckoutStationState State { get; }

        /// <summary>
        /// A estação funciona? Falso se o jogador desligou ou se ela está sendo
        /// removida. Quem JÁ está na fila continua sendo atendido enquanto isto
        /// for verdadeiro.
        /// </summary>
        bool IsOperational { get; }

        /// <summary>
        /// Dá para entrar na fila agora? É <see cref="IsOperational"/> mais
        /// "ainda cabe gente".
        ///
        /// São duas perguntas diferentes de propósito: uma caixa com a fila
        /// cheia continua operante para quem está nela. Confundir as duas é o
        /// que faria o cliente escolher uma caixa lotada, levar um "não" do
        /// <see cref="TryJoinQueue"/> e voltar a procurar — em loop, porque a
        /// mesma caixa continuaria sendo a escolhida.
        /// </summary>
        bool AcceptsNewCustomers { get; }

        // ── fila ─────────────────────────────────────────────────────────────

        /// <summary>Clientes esperando, incluindo quem está sendo atendido.</summary>
        int QueueLength { get; }

        /// <summary>Onde a fila começa e para que lado ela cresce.</summary>
        Vector3 QueueAnchor { get; }
        Vector3 QueueDirection { get; }
        float QueueSpacing { get; }

        /// <summary>Onde o cliente coloca as compras.</summary>
        Vector3 CounterPosition { get; }

        /// <summary>
        /// Onde para o enésimo da fila. Centraliza a conta âncora + direção ×
        /// espaçamento × índice para que ela exista num lugar só — o código
        /// atual a repete no CashRegister e no NpcTraject, com um array fixo de
        /// pontos que estoura quando chega mais gente do que pontos.
        /// </summary>
        Vector3 GetQueuePosition(int index);

        /// <summary>Posição dele na fila, ou -1 se não estiver nela.</summary>
        int GetQueueIndex(ICustomer customer);

        bool IsFront(ICustomer customer);

        /// <summary>
        /// Entra na fila. Falso se a estação não aceita mais ninguém — o que
        /// faz o cliente procurar outra em vez de esperar por algo que não vem.
        /// </summary>
        bool TryJoinQueue(ICustomer customer, out int index);

        /// <summary>
        /// Sai da fila. Idempotente de propósito: é chamado pelo Exit do estado,
        /// que roda mesmo quando o cliente já tinha sido removido por outro
        /// caminho (venda concluída, estação removida).
        /// </summary>
        void LeaveQueue(ICustomer customer);

        // ── venda ────────────────────────────────────────────────────────────

        /// <summary>A venda em andamento, ou null se ninguém está sendo atendido.</summary>
        CheckoutSession CurrentSession { get; }

        /// <summary>
        /// Abre a venda do primeiro da fila. Falso se já há uma venda aberta,
        /// se o cliente não é o primeiro, ou se a estação saiu de operação.
        /// </summary>
        bool TryOpenSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                            PaymentMethod method, out CheckoutSession session);

        /// <summary>
        /// Encerra a venda corrente. Chamado pelo <see cref="ICheckoutService"/>
        /// depois de contabilizar, e pelo próprio cliente quando desiste.
        /// Ignora sessões que não são a corrente — assim uma chamada atrasada
        /// não derruba a venda seguinte.
        /// </summary>
        void CloseSession(CheckoutSession session, SessionCloseReason reason);

        event Action<ICheckoutStation> StateChanged;

        /// <summary>Uma venda foi aberta nesta estação. Para a UI do caixa (Fase 5b).</summary>
        event Action<ICheckoutStation, CheckoutSession> SessionOpened;

        /// <summary>Uma venda terminou, concluída ou não.</summary>
        event Action<ICheckoutStation, CheckoutSession, SessionCloseReason> SessionClosed;
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

    /// <summary>Por que a venda terminou. Decide quem é avisado e como.</summary>
    public enum SessionCloseReason
    {
        /// <summary>Paga. O cliente recebe <see cref="CustomerSignal.SaleFinished"/>.</summary>
        Completed = 0,

        /// <summary>O cliente desistiu ou foi embora. Ele já sabe; ninguém é avisado.</summary>
        Abandoned = 1,

        /// <summary>A estação saiu de operação no meio. O cliente recebe CheckoutLost.</summary>
        StationLost = 2,
    }
}
