using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão do app Mercado. C# puro.
    /// </summary>
    public sealed class MarketOrderService : IMarketOrderService, IDisposable
    {
        private readonly ILedger _ledger;
        private readonly IPricingService _pricing;
        private readonly IGameClock _clock;
        private readonly IUnlockContext _unlocks;
        private readonly IItemCatalog _catalog;
        private readonly IEventBus _events;

        private readonly List<ItemDefinition> _available = new();

        private readonly List<MarketOrder> _pending = new();
        private readonly List<MarketOrder> _history = new();
        private readonly List<MarketOrder> _arrived = new();

        private int _nextOrderId = 1;
        private bool _sweeping;
        private bool _disposed;

        public MarketOrderService(ILedger ledger, IPricingService pricing, IGameClock clock,
                                  IUnlockContext unlocks = null, IItemCatalog catalog = null,
                                  IEventBus events = null)
        {
            _ledger = ledger;
            _pricing = pricing;
            _clock = clock;
            _unlocks = unlocks;
            _catalog = catalog;
            _events = events;

            if (_clock != null) _clock.TimeChanged += OnTimeChanged;
        }

        public DeliveryQueue DeliveryQueue { get; } = new();

        public IReadOnlyList<MarketOrder> Pending => _pending;
        public IReadOnlyList<MarketOrder> History => _history;

        public int MaxPendingOrders { get; set; } = 3;
        public float DeliveryHours { get; set; } = 2f;

        public event Action<MarketOrder> OrderPlaced;
        public event Action<MarketOrder> OrderDelivered;

        /// <summary>
        /// Produtos compráveis agora, já filtrados por desbloqueio. Recusar o
        /// produto travado só na hora do pagamento é tarde: a tela tem que nem
        /// oferecê-lo.
        ///
        /// A lista é reaproveitada entre leituras — leia agora, não guarde. Para
        /// segurar o resultado, use a sobrecarga que preenche a sua lista.
        ///
        /// Não é o <c>IItemCatalog.UnlockedFor</c>: aquele aloca uma lista nova
        /// por chamada, e a vitrine do app é justamente o lugar que relê a cada
        /// frame enquanto a tela está aberta. Mesma regra, sem lixo por frame.
        /// </summary>
        public IReadOnlyList<ItemDefinition> AvailableProducts
        {
            get { GetAvailableProducts(_available); return _available; }
        }

        public void GetAvailableProducts(List<ItemDefinition> results)
        {
            if (results == null) return;

            results.Clear();
            if (_catalog == null) return;

            IReadOnlyList<ItemDefinition> all = _catalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                ItemDefinition product = all[i];
                if (product == null || !product.IsUnlocked(_unlocks)) continue;

                results.Add(product);
            }
        }

        public bool CanAfford(MarketCart cart) =>
            cart != null && _ledger != null && _ledger.CanAfford(cart.TotalCost);

        // ── pedido ───────────────────────────────────────────────────────────

        public MarketOrderResult TryCheckout(MarketCart cart, out MarketOrder order)
        {
            order = null;

            if (_ledger == null) return MarketOrderResult.Unavailable;

            // Sem relógio nada faz o prazo vencer: o pedido ficaria pendente para
            // sempre e o jogador teria pago por uma entrega que nunca chega.
            if (_clock == null && DeliveryHours > 0f) return MarketOrderResult.Unavailable;

            if (cart == null || cart.TotalBoxes == 0) return MarketOrderResult.EmptyCart;
            if (_pending.Count >= MaxPendingOrders) return MarketOrderResult.TooManyPendingOrders;

            // TODAS as validações antes de qualquer efeito. Debitar e só então
            // descobrir que um produto está travado deixaria o jogador sem o
            // dinheiro e sem a mercadoria.
            // Lista LOCAL, e não um campo reaproveitado. O `TryWithdraw` abaixo
            // publica BalanceChanged e TransactionRecorded de forma síncrona, e
            // qualquer assinante desses (o MarketManager legado assina) pode
            // chamar TryCheckout de volta. Com um campo, o pedido de fora seria
            // construído com as linhas do pedido de dentro: produtos errados
            // entregues e a tabela de custo alimentada no produto errado.
            var lines = new List<MarketOrderLine>();
            Money total = Money.Zero;

            foreach (KeyValuePair<ItemDefinition, int> entry in cart.Items)
            {
                ItemDefinition product = entry.Key;

                if (product == null) return MarketOrderResult.ProductLocked;

                // O elo que faltava: sem isto, um produto com
                // StoreLevelUnlock(10) é comprável no nível 1 e metade da
                // progressão da fase não tem efeito nenhum.
                if (!product.IsUnlocked(_unlocks)) return MarketOrderResult.ProductLocked;

                var line = new MarketOrderLine(product, entry.Value,
                                               product.BoxCost, product.UnitsPerBox);
                lines.Add(line);
                total += line.Total;
            }

            // Frete e itens adicionais são cobrados uma única vez por pedido.
            total += cart.ShippingFee + cart.AdditionalCost;

            // `IsPositive` antes de sacar: o livro-razão recusa valor não
            // positivo (e com razão), mas um carrinho só de brindes custa ¥0 e
            // seria reportado como "sem dinheiro" com o caixa cheio.
            if (total.IsPositive
                && !_ledger.TryWithdraw(total, TransactionReason.StockPurchase, Describe(lines)))
                return MarketOrderResult.NotEnoughMoney;

            float now = _clock?.TotalHours ?? 0f;

            order = new MarketOrder(_nextOrderId++, lines, total,
                                    _clock?.Day ?? 0, now + Math.Max(0f, DeliveryHours));

            _pending.Add(order);

            // O custo é registrado no PEDIDO, não na entrega: é agora que o
            // dinheiro saiu, e é isso que "custo atual" significa na tela de
            // Preços. Esperar a entrega faria a tabela mentir durante o prazo.
            RecordCosts(order.Lines);

            OrderPlaced?.Invoke(order);
            _events?.Publish(new StockOrderPlaced(order.Id, total, order.TotalBoxes,
                                                  order.DayPlaced));

            // Prazo zero entrega no mesmo instante, sem depender de o relógio
            // andar nem de um TimeChanged acontecer.
            if (order.HasArrived(now)) Deliver(order);

            return MarketOrderResult.Ok;
        }

        private void RecordCosts(IReadOnlyList<MarketOrderLine> lines)
        {
            if (_pricing == null) return;

            for (int i = 0; i < lines.Count; i++)
            {
                MarketOrderLine line = lines[i];
                if (line.Product == null || line.Units <= 0) continue;

                // O TOTAL da linha, não o unitário × quantidade.
                //
                // Hoje os dois dariam no mesmo, porque BoxCost é derivado
                // (custo unitário × unidades) e a divisão volta exata. É uma
                // igualdade por acidente: no dia em que a caixa tiver preço
                // próprio — desconto por volume é o próximo pedido óbvio — ¥100
                // por 3 unidades vira ¥33 no unitário, e ¥33 × 3 = ¥99 contra
                // ¥100 debitados. Um ien por caixa, acumulando em silêncio, com
                // a tabela de Preços desencontrada do livro-razão.
                _pricing.RecordRestock(line.Product, line.Total, line.Units);
            }
        }

        private static string Describe(IReadOnlyList<MarketOrderLine> lines) =>
            lines.Count == 1 && lines[0].Product != null
                ? $"{lines[0].Product.name} × {lines[0].Boxes} cx"
                : $"{lines.Count} produtos";

        // ── entrega ──────────────────────────────────────────────────────────

        private void OnTimeChanged(IGameClock clock)
        {
            // Reentrância: entregar pode virar o dia, que dispara TimeChanged de
            // novo. A varredura de fora termina o serviço.
            if (_sweeping || _pending.Count == 0) return;

            _sweeping = true;
            try { Sweep(clock.TotalHours); }
            finally { _sweeping = false; _arrived.Clear(); }
        }

        /// <summary>
        /// Coleta primeiro, entrega depois. Percorrer a lista removendo durante a
        /// iteração quebra assim que um assinante de OrderDelivered remove OUTRO
        /// pedido — e o Remove dentro de Deliver já protege contra entrega dupla.
        /// </summary>
        private void Sweep(float now)
        {
            _arrived.Clear();

            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].HasArrived(now)) _arrived.Add(_pending[i]);

            for (int i = 0; i < _arrived.Count; i++) Deliver(_arrived[i]);
        }

        private void Deliver(MarketOrder order)
        {
            if (!_pending.Remove(order)) return;

            order.MarkDelivered();
            _history.Add(order);

            IReadOnlyList<MarketOrderLine> lines = order.Lines;
            for (int i = 0; i < lines.Count; i++)
                DeliveryQueue.Enqueue(lines[i].Product, lines[i].Boxes);

            OrderDelivered?.Invoke(order);
            _events?.Publish(new StockOrderDelivered(order.Id, order.TotalBoxes));
        }

        /// <summary>
        /// Próximo número de pedido. O save guarda para que dois pedidos de
        /// sessões diferentes não nasçam com o mesmo id.
        /// </summary>
        public int NextOrderId => _nextOrderId;

        /// <summary>
        /// Restaura os pedidos a caminho e as caixas ainda por materializar.
        ///
        /// NÃO entrega nada aqui, nem mesmo o que já venceu: o prazo é comparado
        /// contra <c>TotalHours</c>, e o relógio pode ainda não ter sido
        /// restaurado quando este método roda. O primeiro TimeChanged depois do
        /// carregamento varre e entrega o que estiver vencido, pelo caminho
        /// normal — um só, e com os eventos na ordem certa.
        ///
        /// O <paramref name="nextOrderId"/> nunca ANDA PARA TRÁS: um save antigo
        /// carregado por cima de uma sessão mais adiantada reusaria ids que já
        /// existem, e dois pedidos com o mesmo id se confundem na tela e no
        /// histórico.
        /// </summary>
        public void Restore(IEnumerable<MarketOrder> pending, IEnumerable<DeliveryLot> lots,
                            int nextOrderId)
        {
            _pending.Clear();
            _history.Clear();
            _arrived.Clear();

            if (pending != null)
                foreach (MarketOrder order in pending)
                {
                    if (order == null || order.Delivered) continue;

                    _pending.Add(order);
                    if (order.Id >= _nextOrderId) _nextOrderId = order.Id + 1;
                }

            if (nextOrderId > _nextOrderId) _nextOrderId = nextOrderId;

            DeliveryQueue.Restore(lots);
        }

        /// <summary>Entrega tudo agora. Atalho de depuração para a Sandbox.</summary>
        public void DeliverAllNow()
        {
            MarketOrder[] snapshot = _pending.ToArray();
            for (int i = 0; i < snapshot.Length; i++) Deliver(snapshot[i]);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_clock != null) _clock.TimeChanged -= OnTimeChanged;
        }
    }
}
