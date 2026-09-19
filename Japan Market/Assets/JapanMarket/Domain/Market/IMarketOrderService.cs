using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>Por que um pedido não foi feito. A tela precisa dizer qual.</summary>
    public enum MarketOrderResult
    {
        Ok = 0,
        EmptyCart = 1,

        /// <summary>Saldo insuficiente. Nada foi debitado.</summary>
        NotEnoughMoney = 2,

        /// <summary>Algum produto do carrinho ainda não está desbloqueado.</summary>
        ProductLocked = 3,

        /// <summary>Já há entregas demais a caminho para o tamanho do depósito.</summary>
        TooManyPendingOrders = 4,

        /// <summary>Sem livro-razão ou sem relógio — cena sem GameContext.</summary>
        Unavailable = 5,
    }

    /// <summary>
    /// Um pedido feito ao fornecedor. Imutável: o dinheiro já saiu, e um pedido
    /// editável depois de pago é um pedido que um dia entrega mais do que foi
    /// cobrado.
    ///
    /// O prazo é guardado em horas ABSOLUTAS (<c>IGameClock.TotalHours</c>) e
    /// não em hora do dia: um pedido feito às 23h com duas horas de prazo chega
    /// "às 25h", e comparando hora do dia isso nunca acontece.
    /// </summary>
    public sealed class MarketOrder
    {
        private readonly List<MarketOrderLine> _lines;

        public MarketOrder(int id, IReadOnlyList<MarketOrderLine> lines, Money total,
                           int dayPlaced, float arrivesAtTotalHours)
        {
            Id = id;
            _lines = new List<MarketOrderLine>(lines);
            Total = total;
            DayPlaced = dayPlaced;
            ArrivesAtTotalHours = arrivesAtTotalHours;
        }

        public int Id { get; }
        public IReadOnlyList<MarketOrderLine> Lines => _lines;
        public Money Total { get; }
        public int DayPlaced { get; }
        public float ArrivesAtTotalHours { get; }

        public bool Delivered { get; private set; }

        public int TotalBoxes
        {
            get
            {
                int boxes = 0;
                for (int i = 0; i < _lines.Count; i++) boxes += _lines[i].Boxes;
                return boxes;
            }
        }

        public float HoursRemaining(float now) => Math.Max(0f, ArrivesAtTotalHours - now);
        public bool HasArrived(float now) => now >= ArrivesAtTotalHours;

        internal void MarkDelivered() => Delivered = true;

        public override string ToString() =>
            $"Pedido #{Id}: {_lines.Count} produto(s), {TotalBoxes} caixa(s), {Total}" +
            (Delivered ? " — entregue" : " — a caminho");
    }

    /// <summary>
    /// Uma linha do pedido, com o custo congelado no momento da compra — o custo
    /// base pode mudar, e um pedido já pago não é recotado.
    /// </summary>
    public readonly struct MarketOrderLine
    {
        public readonly ItemDefinition Product;
        public readonly int Boxes;
        public readonly Money BoxCost;
        public readonly int UnitsPerBox;

        public MarketOrderLine(ItemDefinition product, int boxes, Money boxCost, int unitsPerBox)
        {
            Product = product;
            Boxes = boxes;
            BoxCost = boxCost;
            UnitsPerBox = unitsPerBox < 1 ? 1 : unitsPerBox;
        }

        public Money Total => BoxCost * Boxes;
        public int Units => Boxes * UnitsPerBox;
        public Money UnitCost => Money.FromYen(BoxCost.Yen / (double)UnitsPerBox);
    }

    /// <summary>
    /// O app Mercado, sem tela: escolher produtos, pagar, e esperar a entrega.
    ///
    /// O que este serviço deliberadamente NÃO faz: instanciar caixa na cena. Ele
    /// enfileira em <see cref="DeliveryQueue"/> quando o prazo vence, e quem tem
    /// cena materializa — é o que mantém a compra testável sem abrir o Unity.
    /// </summary>
    public interface IMarketOrderService
    {
        /// <summary>
        /// Produtos compráveis agora, já filtrados por desbloqueio. A lista é
        /// reaproveitada entre leituras — leia agora, não guarde.
        /// </summary>
        IReadOnlyList<ItemDefinition> AvailableProducts { get; }

        /// <summary>Mesma consulta, escrevendo na lista de quem chamou.</summary>
        void GetAvailableProducts(List<ItemDefinition> results);

        DeliveryQueue DeliveryQueue { get; }

        /// <summary>Pedidos pagos e a caminho.</summary>
        IReadOnlyList<MarketOrder> Pending { get; }

        IReadOnlyList<MarketOrder> History { get; }

        /// <summary>Quantos pedidos cabem a caminho ao mesmo tempo.</summary>
        int MaxPendingOrders { get; set; }

        /// <summary>Prazo de entrega, em horas de jogo. 0 entrega na hora.</summary>
        float DeliveryHours { get; set; }

        bool CanAfford(MarketCart cart);

        /// <summary>
        /// Valida, cobra e cria o pedido. Em qualquer resultado diferente de
        /// <see cref="MarketOrderResult.Ok"/>, NADA é debitado e nada é criado.
        /// O carrinho NÃO é limpo aqui — quem chamou decide, porque a tela pode
        /// querer mostrar o que acabou de ser pedido.
        /// </summary>
        MarketOrderResult TryCheckout(MarketCart cart, out MarketOrder order);

        event Action<MarketOrder> OrderPlaced;
        event Action<MarketOrder> OrderDelivered;
    }
}
