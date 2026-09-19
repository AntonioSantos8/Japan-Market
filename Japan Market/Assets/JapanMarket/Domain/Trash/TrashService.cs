using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão da doca de reciclagem. C# puro.
    /// </summary>
    public sealed class TrashService : ITrashService
    {
        private readonly IEventBus _events;
        private readonly List<TrashBag> _pending = new();

        public TrashService(IEventBus events = null)
        {
            _events = events;
        }

        public string Label => "Reciclagem";
        public TransactionReason Reason => TransactionReason.RecycledTrash;

        public IReadOnlyList<TrashBag> Pending => _pending;
        public int PendingBags => _pending.Count;

        public Money PendingValue
        {
            get
            {
                Money total = Money.Zero;
                for (int i = 0; i < _pending.Count; i++) total += _pending[i].Value;

                return total;
            }
        }

        public event Action<int, Money> Collected;
        public event Action<TrashBag> Deposited;

        public bool TryDeposit(TrashBag bag)
        {
            if (bag == null || bag.IsEmpty) return false;

            // O MESMO saco duas vezes seria pagamento dobrado por um lixo só. Não
            // é hipótese: dois gatilhos de doca sobrepostos na cena disparam os
            // dois no mesmo frame para o objeto que o jogador soltou.
            if (_pending.Contains(bag)) return false;

            _pending.Add(bag);

            Deposited?.Invoke(bag);
            _events?.Publish(new TrashBagDeposited(bag.Count, bag.Value));

            return true;
        }

        /// <summary>
        /// O caminhão. Esvazia a doca e devolve o total — quem chama
        /// (<c>DayCycle</c>) é que deposita.
        ///
        /// Esvazia ANTES de avisar: um assinante de <see cref="Collected"/> que
        /// deposite um saco novo estaria depositando para o dia seguinte, e não
        /// pode encontrar a lista antiga ainda cheia.
        /// </summary>
        public Money Collect()
        {
            if (_pending.Count == 0) return Money.Zero;

            Money total = PendingValue;
            int bags = _pending.Count;

            _pending.Clear();

            Collected?.Invoke(bags, total);
            _events?.Publish(new TrashCollected(bags, total));

            return total;
        }

        /// <summary>
        /// Restaura a doca de um save. Sacos vazios e repetidos são descartados
        /// — os dois deixariam a doca num estado que o jogo não consegue criar.
        /// </summary>
        public void Restore(IEnumerable<TrashBag> bags)
        {
            _pending.Clear();
            if (bags == null) return;

            foreach (TrashBag bag in bags)
            {
                if (bag == null || bag.IsEmpty) continue;
                if (_pending.Contains(bag)) continue;

                _pending.Add(bag);
            }
        }
    }
}
