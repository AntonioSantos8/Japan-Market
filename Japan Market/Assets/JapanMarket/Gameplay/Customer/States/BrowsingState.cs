using System.Collections.Generic;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Decide para onde ir agora. Não anda: escolhe.
    ///
    /// Separar a decisão do deslocamento é o que permite reagir a um móvel que
    /// sumiu — o cliente volta para cá e escolhe outro, em vez de continuar
    /// caminhando até onde a prateleira estava.
    /// </summary>
    public sealed class BrowsingState : CustomerStateBase
    {
        private readonly List<IProductStorage> _candidates = new();

        /// <summary>Tempo procurando antes de concluir que não há o que comprar.</summary>
        private const float GiveUpAfterSeconds = 5f;

        public override void Enter(CustomerContext context)
        {
            context.ClearShelfTarget();
            context.Locomotion.Halt();
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            // A lista acabou ou a cesta encheu: hora de pagar — ou de ir embora
            // de mãos vazias. Sem este ramo, um cliente cuja última prateleira
            // esvaziou na mão dele ficava parado no chão da loja para sempre:
            // FoundShelf falso, DoneShopping falso, e nenhuma global disparando.
            if (context.ShelvesRemaining <= 0 || context.BasketIsFull)
            {
                if (context.Basket.IsEmpty) GiveUpIfOutOfTime(context);
                return;
            }

            if (context.TargetShelf != null) return;

            // Uma tentativa por tick: uma prateleira pode ter liberado slot
            // enquanto ele pensava. A consulta ao registro é O(1) e o filtro só
            // percorre quem tem a capacidade.
            TryPickShelf(context);
        }

        private void TryPickShelf(CustomerContext context)
        {
            _candidates.Clear();

            IReadOnlyList<IProductStorage> all = context.Furniture.WithCapability<IProductStorage>();

            for (int i = 0; i < all.Count; i++)
            {
                IProductStorage storage = all[i];

                if (storage?.Owner == null || !storage.Owner.IsAlive) continue;
                if (storage.IsEmpty) continue;
                if (!context.AcceptsPrice(storage.CurrentProduct, out _)) continue;

                // Precisa de um lugar para parar, e o lugar precisa estar livre.
                if (!storage.Owner.TryGetCapability(out ICustomerSlots slots)) continue;
                if (!slots.HasFreeSlot) continue;

                _candidates.Add(storage);
            }

            if (_candidates.Count == 0)
            {
                // Nada disponível AGORA. Se já tem algo na cesta, vai pagar; se
                // não, espera um pouco antes de desistir — todas as prateleiras
                // podem estar apenas ocupadas por outro cliente, e desistir no
                // primeiro frame faria o segundo cliente da loja dar meia-volta.
                if (context.Basket.IsEmpty) GiveUpIfOutOfTime(context);
                else context.ShelvesRemaining = 0;

                return;
            }

            IProductStorage chosen = _candidates[UnityEngine.Random.Range(0, _candidates.Count)];

            if (!chosen.Owner.TryGetCapability(out ICustomerSlots chosenSlots)) return;
            if (!chosenSlots.TryReserve(out ISlotReservation reservation)) return;

            // Reservar não basta: se não há caminho até o slot, devolve na hora
            // em vez de deixar o cliente encalhado tentando chegar.
            if (!context.Locomotion.CanReach(reservation.WorldPosition))
            {
                reservation.Dispose();
                return;
            }

            context.TargetShelf = chosen;
            context.Reservation = reservation;
        }

        private static void GiveUpIfOutOfTime(CustomerContext context)
        {
            if (context.StateTime < GiveUpAfterSeconds) return;
            context.Frustrate(Core.CustomerLeaveReason.NothingToBuy);
        }

        public override void Exit(CustomerContext context) => _candidates.Clear();

        // ── condições da tabela ──────────────────────────────────────────────

        public static bool FoundShelf(CustomerContext context) =>
            context.TargetShelf != null && context.Reservation != null;

        public static bool DoneShopping(CustomerContext context) =>
            context.TargetShelf == null
            && !context.Basket.IsEmpty
            && (context.ShelvesRemaining <= 0 || context.BasketIsFull);
    }
}
