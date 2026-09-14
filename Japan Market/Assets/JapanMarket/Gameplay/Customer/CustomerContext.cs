using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// A memória compartilhada dos estados do cliente.
    ///
    /// Tudo o que o NpcTraject guardava em seis flags soltas — _isLeaving,
    /// _itemsPlaced, HasArrivedAtQueueTarget, _queueIndex, _currentOccupancy,
    /// _reservedSlotPosition — vive aqui, num lugar só. A diferença não é
    /// cosmética: com as flags espalhadas era possível o NPC estar
    /// simultaneamente "indo embora" e "com slot reservado", e nada no código
    /// impedia isso. Aqui o estado corrente da máquina é a verdade, e o
    /// contexto só carrega os dados que ele precisa.
    /// </summary>
    public sealed class CustomerContext
    {
        // ── componentes ──────────────────────────────────────────────────────
        public CustomerAgent Agent;
        public CustomerLocomotion Locomotion;
        public CustomerAnimation Animation;
        public CustomerBasket Basket;
        public CustomerProfileData Profile;

        // ── serviços ─────────────────────────────────────────────────────────
        public IFurnitureRegistry Furniture;
        public IEventBus Events;
        public IPricingService Pricing;

        /// <summary>Pode ser null até a Fase 8. Ausente = loja limpa.</summary>
        public IStoreCleanliness Cleanliness;

        // ── pontos da cena ───────────────────────────────────────────────────
        public Vector3 EntryPoint;
        public Vector3 ExitPoint;

        // ── alvo corrente ────────────────────────────────────────────────────
        public IProductStorage TargetShelf;
        public ISlotReservation Reservation;
        public ICheckoutStation Station;
        public int QueueIndex;

        // ── plano de compras ─────────────────────────────────────────────────
        public int ShelvesRemaining;

        // ── sinais do mundo ──────────────────────────────────────────────────
        public bool StoreClosed;
        public bool SaleFinished;
        public bool CheckoutLost;

        /// <summary>Motivo pendente de frustração. Null = nada errado.</summary>
        public CustomerLeaveReason? PendingFrustration;

        public bool FrustrationDone;
        public bool ReadyToDespawn;

        /// <summary>
        /// Já está a caminho da saída. Impede que uma frustração tardia arranque
        /// o cliente do estado de saída e o faça reclamar duas vezes.
        /// </summary>
        public bool IsLeaving;

        /// <summary>Segundos no estado atual. Zerado pela máquina a cada troca.</summary>
        public float StateTime;

        /// <summary>
        /// Até quando o estado atual pretende esperar, em segundos de StateTime.
        ///
        /// Fica no contexto, e não dentro do estado, porque quem LÊ isto é a
        /// tabela de transições — e a tabela só enxerga o contexto. O estado
        /// escreve no Enter; a transição compara com StateTime.
        /// </summary>
        public float WaitUntil;

        // ── consultas ────────────────────────────────────────────────────────

        public bool StoreIsTooDirty =>
            Cleanliness != null && Profile != null
            && Cleanliness.ActiveDirtCount >= Profile.DirtTolerance;

        public bool BasketIsFull =>
            Profile != null && Basket != null && Basket.Count >= Profile.BasketCapacity;

        /// <summary>
        /// A prateleira alvo ainda existe e ainda é usável?
        ///
        /// Esta pergunta é feita todo frame por uma transição global, e é o que
        /// impede o caso "jogador arranca a prateleira enquanto o NPC caminha
        /// até ela" de virar MissingReferenceException — que hoje mata a
        /// corrotina e deixa o NPC parado para sempre no meio da loja.
        /// </summary>
        public bool TargetShelfLost =>
            TargetShelf == null || TargetShelf.Owner == null || !TargetShelf.Owner.IsAlive;

        public bool ReservationLost => Reservation == null || !Reservation.IsValid;

        public bool StationLost =>
            Station == null || Station.Owner == null || !Station.Owner.IsAlive
            || !Station.IsOperational;

        /// <summary>Preço que este cliente aceita pagar por esse produto.</summary>
        public bool AcceptsPrice(ItemDefinition product, out Money price)
        {
            price = Pricing != null ? Pricing.GetSellPrice(product) : product.MarketPrice;
            if (Profile == null) return true;

            return price <= Profile.MaxAcceptablePrice(product.MarketPrice);
        }

        // ── mutações ─────────────────────────────────────────────────────────

        /// <summary>
        /// Libera o slot reservado. Chamado pelo Exit dos estados que reservam —
        /// é o que torna o vazamento de slot estruturalmente impossível.
        /// </summary>
        public void ReleaseReservation()
        {
            Reservation?.Dispose();
            Reservation = null;
        }

        public void ClearShelfTarget()
        {
            ReleaseReservation();
            TargetShelf = null;
        }

        /// <summary>Registra um motivo para desistir. A transição global cuida do resto.</summary>
        public void Frustrate(CustomerLeaveReason reason)
        {
            if (PendingFrustration.HasValue) return;

            PendingFrustration = reason;
            FrustrationDone = false;
        }

        public void ResetSignals()
        {
            StoreClosed = false;
            SaleFinished = false;
            CheckoutLost = false;
            PendingFrustration = null;
            FrustrationDone = false;
            ReadyToDespawn = false;
            IsLeaving = false;
        }
    }
}
