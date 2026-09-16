using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    [TestFixture]
    public class PricingServiceTests
    {
        private PricingService _pricing;

        [SetUp]
        public void SetUp()
        {
            _pricing = new PricingService();
        }

        /// <summary>
        /// Repare no nome do campo: <c>_yen</c>, que é o campo serializado de
        /// Money, e não <c>Yen</c>, que é a propriedade. JsonUtility casa por
        /// campo — escrever "Yen" faz o dinheiro chegar zerado e o teste passar
        /// (ou falhar) pelo motivo errado.
        /// </summary>
        private static ItemDefinition Product(long baseCostYen, long marketPriceYen)
        {
            ItemDefinition product = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(
                $@"{{""_baseCost"":{{""_yen"":{baseCostYen}}},""_marketPrice"":{{""_yen"":{marketPriceYen}}}}}",
                product);
            return product;
        }

        [Test]
        public void GetPricingData_ReturnsInitial_WhenNoDataExists()
        {
            ItemDefinition product = Product(100, 150);

            PricingData data = _pricing.GetPricingData(product);

            Assert.AreEqual(Money.FromYen(150), data.SellPrice);
            Assert.AreEqual(Money.FromYen(100), data.CurrentCost);
            Assert.AreEqual(Money.Zero, data.LastCost);
            Assert.AreEqual(Money.Zero, data.AverageCost);
            Assert.AreEqual(0, data.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(50), data.Profit);

            Object.DestroyImmediate(product);
        }

        [Test]
        public void SetSellPrice_UpdatesPriceAndDay()
        {
            ItemDefinition product = Product(100, 150);

            _pricing.SetSellPrice(product, Money.FromYen(200), 5);

            PricingData data = _pricing.GetPricingData(product);
            Assert.AreEqual(Money.FromYen(200), data.SellPrice);
            Assert.AreEqual(5, data.LastChangeDay);
            Assert.AreEqual(Money.FromYen(100), data.Profit);

            Object.DestroyImmediate(product);
        }

        [Test]
        public void RecordRestock_UpdatesCurrentAndAverageCost()
        {
            ItemDefinition product = Product(100, 150);

            _pricing.RecordRestock(product, Money.FromYen(100), 1);

            PricingData data1 = _pricing.GetPricingData(product);
            Assert.AreEqual(Money.FromYen(100), data1.CurrentCost);
            Assert.AreEqual(Money.FromYen(100), data1.AverageCost);
            Assert.AreEqual(1, data1.UnitsInAverage);
            Assert.AreEqual(Money.Zero, data1.LastCost,
                "Custo de tabela não é compra anterior: não havia nenhuma.");

            _pricing.RecordRestock(product, Money.FromYen(150), 1);

            PricingData data2 = _pricing.GetPricingData(product);
            Assert.AreEqual(Money.FromYen(100), data2.LastCost);
            Assert.AreEqual(Money.FromYen(150), data2.CurrentCost);

            Assert.AreEqual(Money.FromYen(125), data2.AverageCost);
            Assert.AreEqual(2, data2.UnitsInAverage);

            Object.DestroyImmediate(product);
        }

        [Test]
        public void O_parametro_e_o_total_pago_e_nao_o_custo_de_uma_unidade()
        {
            // ¥100 por uma caixa de 3: o unitário é ¥33, e é ¥100 que tem de
            // entrar na média. Reconstituir o total a partir do unitário
            // arredondado perde um ien por caixa, para sempre.
            ItemDefinition product = Product(100, 150);

            _pricing.RecordRestock(product, Money.FromYen(100), 3);

            PricingData data = _pricing.GetPricingData(product);

            Assert.AreEqual(Money.FromYen(100), data.TotalSpent);
            Assert.AreEqual(3, data.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(33), data.AverageCost, "100/3 = 33,33 → ¥33.");
            Assert.AreEqual(Money.FromYen(33), data.CurrentCost, "Unitário só para exibir.");

            Object.DestroyImmediate(product);
        }


        // ── o que faltava ────────────────────────────────────────────────────

        [Test]
        public void Custo_medio_nao_congela_depois_de_muitas_reposicoes()
        {
            // O defeito era pior que "erro acumulado": recalcular a média a
            // partir da própria média JÁ ARREDONDADA, com divisão inteira, faz
            // (m·n + c)/(n+1) voltar a m sempre que |c − m| < n+1. Com treze
            // unidades acumuladas a média PARAVA de andar.
            ItemDefinition product = Product(100, 150);

            _pricing.RecordRestock(product, Money.FromYen(270), 3);
            for (int i = 0; i < 100; i++) _pricing.RecordRestock(product, Money.FromYen(100), 1);

            PricingData data = _pricing.GetPricingData(product);

            Assert.AreEqual(103, data.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(100), data.AverageCost,
                "270 + 10000 = 10270 em 103 unidades = ¥99,7 → ¥100.");

            Object.DestroyImmediate(product);
        }

        [Test]
        public void Repor_estoque_nao_faz_o_produto_parecer_precificado()
        {
            // HasCustomPrice era _data.ContainsKey, e repor também cria entrada.
            ItemDefinition product = Product(100, 150);

            _pricing.RecordRestock(product, Money.FromYen(800), 8);

            Assert.IsFalse(_pricing.HasCustomPrice(product));
            Assert.AreEqual(Money.FromYen(150), _pricing.GetSellPrice(product),
                "Sem preço do jogador, vale o de mercado.");

            Object.DestroyImmediate(product);
        }

        [Test]
        public void Limpar_o_preco_limpa_a_flag_e_volta_ao_mercado()
        {
            ItemDefinition product = Product(100, 150);

            _pricing.SetSellPrice(product, Money.FromYen(200), 3);
            Assert.IsTrue(_pricing.HasCustomPrice(product));

            _pricing.ClearSellPrice(product, 4);

            Assert.IsFalse(_pricing.HasCustomPrice(product), "Limpar tem que limpar a flag.");
            Assert.AreEqual(Money.FromYen(150), _pricing.GetSellPrice(product));

            Object.DestroyImmediate(product);
        }

        [Test]
        public void Limpar_preco_nunca_definido_nao_avisa_ninguem()
        {
            ItemDefinition product = Product(100, 150);

            int fired = 0;
            _pricing.PriceChanged += (_, __) => fired++;

            _pricing.ClearSellPrice(product, 4);

            Assert.AreEqual(0, fired);
            Assert.AreEqual(0, _pricing.Table.Count, "E não cria linha na tabela.");

            Object.DestroyImmediate(product);
        }

        [Test]
        public void Preco_negativo_vira_zero_em_vez_de_pagar_o_cliente()
        {
            ItemDefinition product = Product(100, 150);

            _pricing.SetSellPrice(product, Money.FromYen(-50), 1);

            Assert.AreEqual(Money.Zero, _pricing.GetSellPrice(product));

            Object.DestroyImmediate(product);
        }

        [Test]
        public void A_tabela_lista_so_quem_tem_historia()
        {
            ItemDefinition comprado = Product(100, 150);
            ItemDefinition precificado = Product(50, 110);
            ItemDefinition intocado = Product(30, 80);

            _pricing.RecordRestock(comprado, Money.FromYen(800), 8);
            _pricing.SetSellPrice(precificado, Money.FromYen(120), 1);
            _pricing.GetPricingData(intocado);            // só consultado

            Assert.AreEqual(2, _pricing.Table.Count);

            Object.DestroyImmediate(comprado);
            Object.DestroyImmediate(precificado);
            Object.DestroyImmediate(intocado);
        }

        [Test]
        public void Restore_descarta_entrada_cujo_produto_foi_apagado()
        {
            // GetPricingData(null) devolve um default cujo Product é null;
            // indexar o dicionário com ele derrubaria o carregamento do save.
            Assert.DoesNotThrow(() => _pricing.Restore(new[] { default(PricingData) }));
            Assert.AreEqual(0, _pricing.Table.Count);
        }

        [Test]
        public void Produto_nulo_nao_lanca()
        {
            Assert.AreEqual(Money.Zero, _pricing.GetSellPrice(null));
            Assert.IsFalse(_pricing.HasCustomPrice(null));
            Assert.DoesNotThrow(() => _pricing.SetSellPrice(null, Money.FromYen(100), 1));
            Assert.DoesNotThrow(() => _pricing.ClearSellPrice(null, 1));
            Assert.DoesNotThrow(() => _pricing.RecordRestock(null, Money.FromYen(100), 1));
        }
    }
}