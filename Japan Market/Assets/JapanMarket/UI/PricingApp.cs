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
    /// O app Preços: quanto custou, quanto vale no mercado, por quanto você vende.
    ///
    /// Só lista quem tem HISTÓRIA — produto nunca comprado nem precificado não
    /// aparece. É o filtro do próprio <c>IPricingService.Table</c>, e evita uma
    /// tela com cem linhas zeradas no primeiro dia.
    ///
    /// O preço é ajustado em degraus e não digitado. Um campo de texto aqui
    /// significaria validar entrada, tratar vírgula, e decidir o que fazer com
    /// "abc" — e o jogo já tem a regra de que preço negativo vira zero. Degrau
    /// não precisa de nada disso e é mais rápido de usar com o mouse.
    /// </summary>
    public sealed class PricingApp : ComputerApp
    {
        public override string Title => "Preços";

        private const int Step = 10;

        private RectTransform _list;
        private TextMeshProUGUI _hint;

        private IPricingService _pricing;
        private IGameClock _clock;

        protected override void Build()
        {
            RectTransform column = UIKit.Column("Preços", Content, 8f,
                                                new RectOffset(10, 10, 10, 10));
            UIKit.Stretch(column);

            RectTransform header = UIKit.Row("Cabeçalho", column, 26f);
            UIKit.Label("c1", header, "PRODUTO", UIKit.SmallSize, UIKit.TextDim).Grow(2f);
            UIKit.Label("c2", header, "CUSTO MÉDIO", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(110f);
            UIKit.Label("c3", header, "MERCADO", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(90f);
            UIKit.Label("c4", header, "LUCRO", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(90f);
            UIKit.Label("c5", header, "SEU PREÇO", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Center).Width(230f);

            Image panel = UIKit.Panel("Lista", column, new Color(0f, 0f, 0f, 0.12f));
            panel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _list = UIKit.ScrollList("Rolagem", panel.transform, out _);

            _hint = UIKit.Label("Dica", column,
                                "Vender abaixo do custo médio dá prejuízo em toda venda.",
                                UIKit.SmallSize, UIKit.TextDim);
            _hint.Width(600f);
        }

        protected override void Subscribe()
        {
            if (!TryGet(out _pricing)) return;

            TryGet(out _clock);
            _pricing.PriceChanged += OnPriceChanged;
        }

        protected override void Unsubscribe()
        {
            if (_pricing == null) return;

            _pricing.PriceChanged -= OnPriceChanged;
        }

        private void OnPriceChanged(ItemDefinition _, Money __) => Refresh();

        public override void Refresh()
        {
            if (_list == null) return;
            if (_pricing == null && !TryGet(out _pricing)) return;

            UIKit.Clear(_list);

            // A tabela é reaproveitada entre leituras pelo serviço, então é
            // copiada antes de desenhar: cada botão criado aqui captura o
            // produto, e a lista pode ser reescrita por baixo antes do clique.
            var rows = new List<PricingData>(_pricing.Table);

            if (rows.Count == 0)
            {
                UIKit.Label("Vazio", _list,
                            "Nenhum produto com histórico ainda. Compre estoque no Mercado.",
                            UIKit.BodySize, UIKit.TextDim);
                return;
            }

            for (int i = 0; i < rows.Count; i++) DrawRow(rows[i]);
        }

        private void DrawRow(PricingData data)
        {
            ItemDefinition product = data.Product;
            if (product == null) return;

            RectTransform row = UIKit.Row($"Preço {product.name}", _list, 38f);

            string name = product.DisplayName.IsEmpty ? product.name : product.DisplayName.Value;
            UIKit.Label("Nome", row, name, UIKit.BodySize, UIKit.Text).Grow(2f);

            UIKit.Label("Custo", row, data.CostBasis.ToString(), UIKit.BodySize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(110f);
            UIKit.Label("Mercado", row, data.MarketPrice.ToString(), UIKit.BodySize,
                        UIKit.TextDim, TextAlignmentOptions.Right).Width(90f);

            // O lucro é por unidade, e colorido: é o único número desta tela que
            // o jogador precisa ler de relance para saber se está errando.
            UIKit.Label("Lucro", row, data.Profit.ToString(), UIKit.BodySize,
                        data.Profit.IsNegative ? UIKit.Bad : UIKit.Good,
                        TextAlignmentOptions.Right).Width(90f);

            RectTransform stepper = UIKit.Row("Preço", row, 30f, 4f, new RectOffset(0, 0, 0, 0));
            stepper.Width(230f);

            UIKit.Button("-", stepper, "−", () => Nudge(product, -Step)).Width(34f);

            UIKit.Label("Valor", stepper, data.SellPrice.ToString(), UIKit.BodySize,
                        data.HasCustomPrice ? UIKit.Text : UIKit.TextDim,
                        TextAlignmentOptions.Center).Width(80f);

            UIKit.Button("+", stepper, "+", () => Nudge(product, +Step)).Width(34f);

            // "Mercado" limpa o preço do jogador e devolve o produto ao preço de
            // tabela. Sem este botão, não existe caminho de volta depois do
            // primeiro ajuste — só adivinhar qual era o número.
            UIKit.Button("Mercado", stepper, "Mercado", () => ResetToMarket(product),
                         UIKit.SmallSize).Width(70f);
        }

        private int Today => _clock?.Day ?? 0;

        private void Nudge(ItemDefinition product, int delta)
        {
            if (_pricing == null) return;

            Money current = _pricing.GetPricingData(product).SellPrice;
            _pricing.SetSellPrice(product, current + Money.FromYen(delta), Today);
        }

        private void ResetToMarket(ItemDefinition product) =>
            _pricing?.ClearSellPrice(product, Today);
    }
}
