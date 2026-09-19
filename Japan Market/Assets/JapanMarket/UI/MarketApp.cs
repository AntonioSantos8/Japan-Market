using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// O app Mercado: escolher produtos, ver o total, pedir, e acompanhar o que
    /// está a caminho.
    ///
    /// A vitrine vem de <c>IMarketOrderService.AvailableProducts</c>, que já
    /// filtra por desbloqueio — esta tela não sabe que existe nível de loja. Um
    /// produto novo aparece aqui porque virou asset, não porque alguém
    /// acrescentou uma linha.
    ///
    /// O carrinho vive aqui, e não no serviço, de propósito: fechar a tela sem
    /// comprar tem que descartar a escolha, e um carrinho guardado no serviço
    /// sobreviveria à tela, ao dia e ao save.
    /// </summary>
    public sealed class MarketApp : ComputerApp
    {
        public override string Title => "Mercado";

        private readonly MarketCart _cart = new();
        private readonly List<ItemDefinition> _available = new();

        /// <summary>O contador de cada linha, para atualizar sem redesenhar a lista.</summary>
        private readonly Dictionary<ItemDefinition, TextMeshProUGUI> _counters = new();

        private RectTransform _list;
        private RectTransform _pending;
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _message;
        private Button _buy;

        private IMarketOrderService _market;
        private ILedger _ledger;

        protected override void Build()
        {
            RectTransform column = UIKit.Column("Mercado", Content, 8f,
                                                new RectOffset(10, 10, 10, 10));
            UIKit.Stretch(column);

            RectTransform header = UIKit.Row("Cabeçalho", column, 26f);
            UIKit.Label("c1", header, "PRODUTO", UIKit.SmallSize, UIKit.TextDim).Grow(2f);
            UIKit.Label("c2", header, "CAIXA", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(90f);
            UIKit.Label("c3", header, "UN/CX", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(70f);
            UIKit.Label("c4", header, "QUANTIDADE", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Center).Width(150f);

            Image listPanel = UIKit.Panel("Lista", column, new Color(0f, 0f, 0f, 0.12f));
            listPanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 3f;
            _list = UIKit.ScrollList("Rolagem", listPanel.transform, out _);

            UIKit.Label("A caminho", column, "A CAMINHO", UIKit.SmallSize, UIKit.TextDim)
                 .Width(200f);

            Image pendingPanel = UIKit.Panel("Pendentes", column, new Color(0f, 0f, 0f, 0.12f));
            pendingPanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _pending = UIKit.ScrollList("RolagemPendentes", pendingPanel.transform, out _);

            RectTransform footer = UIKit.Row("Rodapé", column, 46f);
            _message = UIKit.Label("Mensagem", footer, string.Empty, UIKit.BodySize,
                                   UIKit.TextDim).Grow();
            _total = UIKit.Label("Total", footer, "¥0", UIKit.HeadingSize, UIKit.Text,
                                 TextAlignmentOptions.Right).Width(150f);

            UIKit.Button("Limpar", footer, "Limpar", ClearCart).Width(110f);
            _buy = UIKit.Button("Comprar", footer, "Comprar", Buy, UIKit.BodySize, UIKit.Accent)
                        .Width(140f);
        }

        protected override void Subscribe()
        {
            if (!TryGet(out _market)) return;

            TryGet(out _ledger);

            _market.OrderPlaced += OnOrdersChanged;
            _market.OrderDelivered += OnOrdersChanged;
        }

        protected override void Unsubscribe()
        {
            if (_market == null) return;

            _market.OrderPlaced -= OnOrdersChanged;
            _market.OrderDelivered -= OnOrdersChanged;
        }

        private void OnOrdersChanged(MarketOrder _) => Refresh();

        // ── desenho ──────────────────────────────────────────────────────────

        public override void Refresh()
        {
            if (_list == null) return;
            if (_market == null && !TryGet(out _market))
            {
                _message.text = "Mercado indisponível: a cena não tem GameContext.";
                return;
            }

            DrawCatalog();
            DrawPending();
            RefreshTotal();
        }

        private void DrawCatalog()
        {
            UIKit.Clear(_list);
            _counters.Clear();

            _market.GetAvailableProducts(_available);

            if (_available.Count == 0)
            {
                UIKit.Label("Vazio", _list,
                            "Nenhum produto disponível. Suba o nível da loja para liberar.",
                            UIKit.BodySize, UIKit.TextDim);
                return;
            }

            for (int i = 0; i < _available.Count; i++) DrawProductRow(_available[i]);
        }

        private void DrawProductRow(ItemDefinition product)
        {
            // Sem painel de fundo na linha: o HorizontalLayoutGroup trataria o
            // fundo como mais uma coluna e empurraria o conteúdo para o lado.
            // Quem quiser faixa zebrada põe uma Image no PAI da linha.
            RectTransform row = UIKit.Row($"Produto {product.name}", _list, 38f);

            string name = product.DisplayName.IsEmpty ? product.name : product.DisplayName.Value;

            UIKit.Label("Nome", row, name, UIKit.BodySize, UIKit.Text).Grow(2f);
            UIKit.Label("Custo", row, product.BoxCost.ToString(), UIKit.BodySize, UIKit.Text,
                        TextAlignmentOptions.Right).Width(90f);
            UIKit.Label("Un", row, product.UnitsPerBox.ToString(), UIKit.BodySize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(70f);

            RectTransform stepper = UIKit.Row("Quantidade", row, 30f, 4f,
                                              new RectOffset(0, 0, 0, 0));
            stepper.Width(150f);

            UIKit.Button("-", stepper, "−", () => Change(product, -1)).Width(34f);

            TextMeshProUGUI count = UIKit.Label("Qtd", stepper,
                                                Boxes(product).ToString(), UIKit.BodySize,
                                                UIKit.Text, TextAlignmentOptions.Center);
            count.Width(50f);

            UIKit.Button("+", stepper, "+", () => Change(product, +1)).Width(34f);

            _counters[product] = count;
        }

        private void DrawPending()
        {
            UIKit.Clear(_pending);

            IReadOnlyList<MarketOrder> orders = _market.Pending;

            if (orders.Count == 0)
            {
                UIKit.Label("Vazio", _pending, "Nada a caminho.", UIKit.BodySize, UIKit.TextDim);
                return;
            }

            float now = 0f;
            if (TryGet(out IGameClock clock)) now = clock.TotalHours;

            for (int i = 0; i < orders.Count; i++)
            {
                MarketOrder order = orders[i];
                RectTransform row = UIKit.Row($"Pedido {order.Id}", _pending, 30f);

                UIKit.Label("Texto", row,
                            $"Pedido #{order.Id} — {order.TotalBoxes} caixa(s), {order.Total}",
                            UIKit.BodySize, UIKit.Text).Grow();

                float hours = order.HoursRemaining(now);
                string eta = hours <= 0f ? "chegando" : $"em {hours:0.#} h";

                UIKit.Label("Prazo", row, eta, UIKit.BodySize, UIKit.TextDim,
                            TextAlignmentOptions.Right).Width(120f);
            }
        }

        // ── carrinho ─────────────────────────────────────────────────────────

        private int Boxes(ItemDefinition product) =>
            _cart.Items.TryGetValue(product, out int boxes) ? boxes : 0;

        private void Change(ItemDefinition product, int delta)
        {
            if (delta > 0) _cart.AddBoxes(product, delta);
            else _cart.RemoveBoxes(product, -delta);

            if (_counters.TryGetValue(product, out TextMeshProUGUI count) && count != null)
                count.text = Boxes(product).ToString();

            _message.text = string.Empty;
            RefreshTotal();
        }

        private void ClearCart()
        {
            _cart.Clear();
            _message.text = string.Empty;

            foreach (KeyValuePair<ItemDefinition, TextMeshProUGUI> entry in _counters)
                if (entry.Value != null) entry.Value.text = "0";

            RefreshTotal();
        }

        private void RefreshTotal()
        {
            Money total = _cart.TotalCost;
            _total.text = total.ToString();

            bool affordable = _ledger == null || _ledger.CanAfford(total);

            // O total fica vermelho ANTES de tentar comprar. Descobrir que falta
            // dinheiro só no clique é o tipo de coisa que faz o jogador achar que
            // o botão está quebrado.
            _total.color = affordable ? UIKit.Text : UIKit.Bad;
            _buy.interactable = _cart.TotalBoxes > 0;
        }

        private void Buy()
        {
            MarketOrderResult result = _market.TryCheckout(_cart, out MarketOrder order);

            if (result == MarketOrderResult.Ok)
            {
                _message.text = $"Pedido #{order.Id} feito: {order.TotalBoxes} caixa(s).";
                _message.color = UIKit.Good;

                // Só limpa quando deu certo. Falhou, o carrinho fica como estava
                // para o jogador ajustar em vez de montar tudo de novo.
                _cart.Clear();
                Refresh();
                return;
            }

            _message.color = UIKit.Bad;
            _message.text = Explain(result);
        }

        /// <summary>
        /// O serviço devolve o MOTIVO, e a tela traduz. É por isso que
        /// <c>TryCheckout</c> não devolve um bool: "não deu" não diz ao jogador o
        /// que fazer a seguir.
        /// </summary>
        private static string Explain(MarketOrderResult result) => result switch
        {
            MarketOrderResult.EmptyCart => "O carrinho está vazio.",
            MarketOrderResult.NotEnoughMoney => "Saldo insuficiente. Nada foi debitado.",
            MarketOrderResult.ProductLocked => "Um dos produtos ainda não está liberado.",
            MarketOrderResult.TooManyPendingOrders =>
                "Já há entregas demais a caminho. Espere as caixas chegarem.",
            MarketOrderResult.Unavailable => "O mercado está indisponível nesta cena.",
            _ => "Não foi possível fazer o pedido.",
        };
    }
}
