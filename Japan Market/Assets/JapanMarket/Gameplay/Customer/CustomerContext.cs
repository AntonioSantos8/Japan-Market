using System.Collections.Generic;
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
        public ICheckoutService Checkout;

        /// <summary>Pode ser null até a Fase 8. Ausente = loja limpa.</summary>
        public IStoreCleanliness Cleanliness;

        // ── pontos da cena ───────────────────────────────────────────────────
        public Vector3 EntryPoint;
        public Vector3 ExitPoint;

        // ── alvo corrente ────────────────────────────────────────────────────
        public IProductStorage TargetShelf;
        public ISlotReservation Reservation;
        public ICheckoutStation Station;
        public CheckoutSession Session;

        /// <summary>
        /// Posição na fila. É ESCRITA pelo estado a partir de uma consulta à
        /// estação, e nunca empurrada de fora.
        ///
        /// A alternativa — a fila chamar um SetQueueIndex em cada cliente — é o
        /// que o CashRegister faz hoje, e é como um aviso perdido deixa o NPC
        /// parado num lugar que já não é o dele. Uma consulta não se perde e não
        /// chega fora de ordem.
        /// </summary>
        public int QueueIndex;

        /// <summary>Já chegou e parou no lugar dele na fila.</summary>
        public bool QueueSettled;

        /// <summary>
        /// Tempo acumulado procurando caixa, ZERADO só quando ele consegue
        /// entrar numa fila.
        ///
        /// Não dá para usar o StateTime aqui: ele reinicia a cada troca de
        /// estado, e o vaivém Seeking → Queueing → Seeking (a fila encheu entre
        /// a escolha e a chegada) nunca acumularia o tempo de desistência — o
        /// cliente oscilaria para sempre sem enfileirar nem ir embora.
        /// </summary>
        public float CheckoutSearchTime;

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
            !StationAlive || !Station.IsOperational;

        /// <summary>
        /// A estação ainda é um objeto válido?
        ///
        /// Diferente de <see cref="StationLost"/>, que também cobre "desligada
        /// pelo jogador". A distinção importa na hora de sair da fila: um caixa
        /// desligado continua sendo um objeto com quem dá para conversar, e
        /// deixar de avisá-lo faria o cliente vazar dentro da fila dele.
        /// </summary>
        public bool StationAlive =>
            Station != null && Station.Owner != null && Station.Owner.IsAlive;

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

        /// <summary>
        /// Abandona a estação de checkout: encerra a venda se houver, sai da
        /// fila e esquece a referência.
        ///
        /// Fica aqui, e não no Exit do QueueingState, por um motivo específico:
        /// a transição Queueing → AtCounter NÃO pode sair da fila — sair seria
        /// perder o lugar no exato momento de ser atendido. Então quem solta a
        /// estação são os estados que encerram o episódio de checkout (Leaving,
        /// Frustrated, SeekingCheckout) e a destruição do cliente. Uma única
        /// regra, nos quatro caminhos que existem para fora do balcão.
        /// </summary>
        public void ReleaseStation()
        {
            if (Station == null) { Session = null; return; }

            if (StationAlive)
            {
                if (Session != null && !Session.IsComplete)
                    Station.CloseSession(Session, SessionCloseReason.Abandoned);

                Station.LeaveQueue(Agent);
            }

            Session = null;
            Station = null;
            QueueIndex = 0;
            QueueSettled = false;
        }

        /// <summary>
        /// Devolve para as prateleiras o que ele pegou e não pagou.
        ///
        /// Fica no contexto porque DOIS estados de saída precisam dela — o
        /// cliente que desiste (Frustrated) e o que é expulso pelo fechamento da
        /// loja (Leaving). Quando só o Frustrated devolvia, toda vez que a loja
        /// fechava com gente dentro o jogador perdia mercadoria: as unidades já
        /// tinham saído da prateleira e sumiam junto com o cliente, sem uma
        /// linha no console.
        ///
        /// Se a prateleira de origem sumiu ou encheu, a unidade evapora mesmo —
        /// melhor perder uma unidade do que travar o cliente tentando devolver.
        /// </summary>
        public void ReturnBasketToShelves()
        {
            if (Basket == null || Basket.IsEmpty) return;

            while (Basket.TryTakeFirst(out CustomerBasket.Entry entry))
                ReturnOne(entry);

            if (Animation != null) Animation.SetCarrying(false);
        }

        private void ReturnOne(CustomerBasket.Entry entry)
        {
            if (Furniture == null || entry.Product == null) return;

            IReadOnlyList<IProductStorage> shelves = Furniture.WithCapability<IProductStorage>();

            for (int i = 0; i < shelves.Count; i++)
            {
                IProductStorage shelf = shelves[i];

                if (shelf?.Owner == null || !shelf.Owner.IsAlive) continue;
                if (!shelf.Accepts(entry.Product)) continue;
                if (shelf.TryPlace(entry.Product, out _)) return;
            }
        }

        /// <summary>Registra um motivo para desistir. A transição global cuida do resto.</summary>
        public void Frustrate(CustomerLeaveReason reason)
        {
            if (PendingFrustration.HasValue) return;

            PendingFrustration = reason;
            FrustrationDone = false;
        }

    }
}
