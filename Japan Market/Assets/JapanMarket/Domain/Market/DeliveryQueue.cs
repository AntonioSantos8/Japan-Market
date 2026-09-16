using System;
using System.Collections.Generic;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um lote a entregar: tantas caixas de um produto.
    ///
    /// Um LOTE, e não um objeto por caixa. A versão anterior alocava um
    /// <c>DeliveryBox</c> dentro de um laço sem teto, e como nada limitava a
    /// quantidade no carrinho, um campo de texto com 999999 travava a Unity
    /// alocando um milhão de objetos que nunca eram liberados.
    /// </summary>
    public sealed class DeliveryLot
    {
        public DeliveryLot(ItemDefinition product, int boxes)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            Boxes = boxes < 1 ? 1 : boxes;
        }

        public ItemDefinition Product { get; }

        /// <summary>Caixas ainda por retirar deste lote.</summary>
        public int Boxes { get; private set; }

        internal bool TakeOne()
        {
            if (Boxes <= 0) return false;

            Boxes--;
            return true;
        }
    }

    /// <summary>
    /// O que já foi pago e está esperando para aparecer no depósito.
    ///
    /// Domain não instancia nada na cena: quem tem cena assina
    /// <see cref="LotEnqueued"/> ou chama <see cref="TryTakeBox"/> e materializa
    /// a caixa. Ver <c>DeliverySpawner</c>.
    /// </summary>
    public sealed class DeliveryQueue
    {
        private readonly List<DeliveryLot> _lots = new();

        /// <summary>Caixas somadas de todos os lotes pendentes.</summary>
        public int PendingBoxes
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _lots.Count; i++) total += _lots[i].Boxes;
                return total;
            }
        }

        public int PendingLots => _lots.Count;
        public IReadOnlyList<DeliveryLot> Lots => _lots;

        public event Action<DeliveryLot> LotEnqueued;

        /// <summary>Uma caixa foi retirada da fila e materializada.</summary>
        public event Action<ItemDefinition> BoxTaken;

        public void Enqueue(ItemDefinition product, int boxes)
        {
            if (product == null || boxes <= 0) return;

            var lot = new DeliveryLot(product, boxes);
            _lots.Add(lot);

            LotEnqueued?.Invoke(lot);
        }

        /// <summary>
        /// Retira UMA caixa, do lote mais antigo. Devolve false com a fila
        /// vazia. Uma por chamada de propósito: quem materializa quer espalhar o
        /// spawn ao longo de vários frames, não instanciar trezentas de uma vez.
        /// </summary>
        public bool TryTakeBox(out ItemDefinition product)
        {
            product = null;

            while (_lots.Count > 0)
            {
                DeliveryLot lot = _lots[0];

                if (lot.TakeOne())
                {
                    product = lot.Product;
                    if (lot.Boxes <= 0) _lots.RemoveAt(0);

                    BoxTaken?.Invoke(product);
                    return true;
                }

                _lots.RemoveAt(0);
            }

            return false;
        }

        public void Clear() => _lots.Clear();

        public void Restore(IEnumerable<DeliveryLot> lots)
        {
            _lots.Clear();
            if (lots == null) return;

            foreach (DeliveryLot lot in lots)
                if (lot != null && lot.Product != null && lot.Boxes > 0) _lots.Add(lot);
        }
    }
}
