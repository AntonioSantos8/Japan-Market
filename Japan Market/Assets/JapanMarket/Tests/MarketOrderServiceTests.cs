using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O app Mercado sem cena.
    ///
    /// Os produtos são montados com <c>JsonUtility.FromJsonOverwrite</c> sobre um
    /// ScriptableObject em memória. Repare no nome dos campos: é <c>_yen</c>, o
    /// campo serializado de <see cref="Money"/>, e não <c>Yen</c>, que é a
    /// propriedade. JsonUtility casa por campo — escrever "Yen" faz o dinheiro
    /// chegar zerado e o teste passar (ou falhar) pelo motivo errado.
    /// </summary>
    [TestFixture]
    public class MarketOrderServiceTests
    {
        private EventBus _events;
        private GameClock _clock;
        private Ledger _ledger;
        private PricingService _pricing;
        private MarketOrderService _market;
        private FakeUnlockContext _unlocks;
        private FakeItemCatalog _catalog;

        private ItemDefinition _productA;   // ¥100/un × 8 = ¥800 a caixa
        private ItemDefinition _productB;   // ¥200/un × 4 = ¥800 a caixa
        private ItemDefinition _locked;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _clock = new GameClock(_events, new GameClockSettings
            {
                DayStartHour = 6f, ClosingHour = 22f, EndOfDayHour = 24f,
                GameHoursPerRealSecond = 1f,
            });

            _ledger = new Ledger(_events, _clock, Money.FromYen(5000));
            _pricing = new PricingService();
            _unlocks = new FakeUnlockContext { StoreLevel = 1 };

            _productA = Product(baseCostYen: 100, unitsPerBox: 8);
            _productB = Product(baseCostYen: 200, unitsPerBox: 4);

            _locked = Product(baseCostYen: 100, unitsPerBox: 8);
            var gate = ScriptableObject.CreateInstance<StoreLevelUnlock>();
            JsonUtility.FromJsonOverwrite(@"{""_requiredLevel"":10}", gate);
            SetUnlock(_locked, gate);

            _catalog = new FakeItemCatalog(_productA, _productB, _locked);

            _market = new MarketOrderService(_ledger, _pricing, _clock, _unlocks,
                                             _catalog, _events)
            {
                DeliveryHours = 2f,
                MaxPendingOrders = 3,
            };
        }

        [TearDown]
        public void TearDown()
        {
            _market.Dispose();
            _ledger.Dispose();

            Object.DestroyImmediate(_productA);
            Object.DestroyImmediate(_productB);
            Object.DestroyImmediate(_locked);
        }

        private static ItemDefinition Product(long baseCostYen, int unitsPerBox)
        {
            ItemDefinition product = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(
                $@"{{""_baseCost"":{{""_yen"":{baseCostYen}}},""_unitsPerBox"":{unitsPerBox}}}",
                product);
            return product;
        }

        /// <summary>
        /// `_unlock` é uma referência a outro asset, que JsonUtility não
        /// consegue escrever a partir de um literal — então vai por reflexão.
        /// </summary>
        private static void SetUnlock(ItemDefinition product, UnlockCondition unlock)
        {
            typeof(ItemDefinition)
                .GetField("_unlock", System.Reflection.BindingFlags.NonPublic
                                   | System.Reflection.BindingFlags.Instance)
                .SetValue(product, unlock);
        }

        private MarketCart Cart(ItemDefinition product, int boxes)
        {
            var cart = new MarketCart();
            cart.AddBoxes(product, boxes);
            return cart;
        }

        // ── validação antes de qualquer efeito ───────────────────────────────

        [Test]
        public void Carrinho_vazio_nao_vira_pedido()
        {
            Assert.AreEqual(MarketOrderResult.EmptyCart,
                _market.TryCheckout(new MarketCart(), out _));
            Assert.AreEqual(Money.FromYen(5000), _ledger.Balance);
        }

        [Test]
        public void Sem_saldo_nada_e_debitado()
        {
            // 10 caixas × ¥800 = ¥8000, com ¥5000 em caixa.
            MarketCart cart = Cart(_productA, 10);

            Assert.AreEqual(MarketOrderResult.NotEnoughMoney,
                _market.TryCheckout(cart, out MarketOrder order));

            Assert.IsNull(order);
            Assert.AreEqual(Money.FromYen(5000), _ledger.Balance);
            Assert.AreEqual(0, _market.DeliveryQueue.PendingBoxes);
            Assert.AreEqual(10, cart.TotalBoxes, "O carrinho não é esvaziado quando falha.");
        }

        [Test]
        public void Produto_travado_falha_ANTES_de_debitar()
        {
            // Sem esta checagem, um produto de nível 10 era comprável no nível 1
            // e metade da progressão da fase não tinha efeito nenhum.
            var cart = new MarketCart();
            cart.AddBoxes(_productA, 1);
            cart.AddBoxes(_locked, 1);

            Assert.AreEqual(MarketOrderResult.ProductLocked, _market.TryCheckout(cart, out _));
            Assert.AreEqual(Money.FromYen(5000), _ledger.Balance);
        }

        [Test]
        public void Subir_de_nivel_libera_o_produto()
        {
            _unlocks.StoreLevel = 10;

            Assert.AreEqual(MarketOrderResult.Ok, _market.TryCheckout(Cart(_locked, 1), out _));
        }

        [Test]
        public void Deposito_lotado_recusa_o_pedido_alem_do_limite()
        {
            _market.MaxPendingOrders = 2;

            Assert.AreEqual(MarketOrderResult.Ok, _market.TryCheckout(Cart(_productA, 1), out _));
            Assert.AreEqual(MarketOrderResult.Ok, _market.TryCheckout(Cart(_productA, 1), out _));
            Assert.AreEqual(MarketOrderResult.TooManyPendingOrders,
                _market.TryCheckout(Cart(_productA, 1), out _));
        }

        [Test]
        public void Carrinho_de_graca_vira_pedido_em_vez_de_dizer_que_falta_dinheiro()
        {
            // O livro-razão recusa saque de ¥0, e com razão. Repassar esse
            // "false" como NotEnoughMoney contradiz o CanAfford.
            ItemDefinition brinde = Product(baseCostYen: 0, unitsPerBox: 1);
            MarketCart cart = Cart(brinde, 1);

            Assert.IsTrue(_market.CanAfford(cart));
            Assert.AreEqual(MarketOrderResult.Ok, _market.TryCheckout(cart, out _));
            Assert.AreEqual(Money.FromYen(5000), _ledger.Balance);

            Object.DestroyImmediate(brinde);
        }

        // ── pedido e custo ───────────────────────────────────────────────────

        [Test]
        public void Pedido_debita_e_alimenta_a_tabela_de_custo()
        {
            var cart = new MarketCart();
            cart.AddBoxes(_productA, 2);   // 16 un a ¥100
            cart.AddBoxes(_productB, 1);   //  4 un a ¥200

            // ¥500 no TOTAL por 10 unidades: ¥50 a unidade. O parâmetro é o
            // total pago, não o unitário.
            _pricing.RecordRestock(_productA, Money.FromYen(500), 10);

            bool placed = false;
            _market.OrderPlaced += _ => placed = true;

            Assert.AreEqual(MarketOrderResult.Ok, _market.TryCheckout(cart, out MarketOrder order));

            Assert.AreEqual(Money.FromYen(2600), _ledger.Balance);   // 5000 − 2400
            Assert.AreEqual(3, order.TotalBoxes);
            Assert.IsTrue(placed);
            Assert.AreEqual(3, cart.TotalBoxes, "O serviço não limpa o carrinho — quem chama decide.");

            PricingData a = _pricing.GetPricingData(_productA);
            Assert.AreEqual(26, a.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(50), a.LastCost);
            Assert.AreEqual(Money.FromYen(100), a.CurrentCost);
            // (500 + 1600) / 26 = ¥80,77 → ¥81. Money arredonda para longe do
            // zero, e ¥80 aqui era o teste afirmando o resultado errado.
            Assert.AreEqual(Money.FromYen(81), a.AverageCost);

            PricingData b = _pricing.GetPricingData(_productB);
            Assert.AreEqual(4, b.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(200), b.CurrentCost);
        }

        [Test]
        public void Evento_de_pedido_sai_no_barramento()
        {
            StockOrderPlaced captured = default;
            using (_events.Subscribe<StockOrderPlaced>(e => captured = e))
            {
                _market.TryCheckout(Cart(_productA, 2), out _);
            }

            Assert.AreEqual(Money.FromYen(1600), captured.Total);
            Assert.AreEqual(2, captured.BoxCount);
        }

        // ── entrega ──────────────────────────────────────────────────────────

        [Test]
        public void A_mercadoria_so_chega_quando_o_prazo_vence()
        {
            _market.TryCheckout(Cart(_productA, 3), out MarketOrder order);

            Assert.AreEqual(0, _market.DeliveryQueue.PendingBoxes, "Ainda a caminho.");
            Assert.AreEqual(1, _market.Pending.Count);

            _clock.Tick(1f);
            Assert.AreEqual(0, _market.DeliveryQueue.PendingBoxes, "Uma hora de duas.");

            _clock.Tick(1.5f);

            Assert.IsTrue(order.Delivered);
            Assert.AreEqual(3, _market.DeliveryQueue.PendingBoxes);
            Assert.AreEqual(0, _market.Pending.Count);
            Assert.AreEqual(1, _market.History.Count);
        }

        [Test]
        public void Prazo_zero_entrega_na_hora()
        {
            _market.DeliveryHours = 0f;

            _market.TryCheckout(Cart(_productA, 2), out MarketOrder order);

            Assert.IsTrue(order.Delivered);
            Assert.AreEqual(2, _market.DeliveryQueue.PendingBoxes);
        }

        [Test]
        public void A_fila_entrega_uma_caixa_por_vez_e_esvazia()
        {
            // Sem um consumidor, a fila só enchia: o jogador pagava e a
            // mercadoria nunca chegava. Este teste é o contrato do consumidor.
            _market.DeliveryHours = 0f;
            _market.TryCheckout(Cart(_productA, 3), out _);

            int taken = 0;
            while (_market.DeliveryQueue.TryTakeBox(out ItemDefinition product))
            {
                Assert.AreSame(_productA, product);
                taken++;
            }

            Assert.AreEqual(3, taken);
            Assert.AreEqual(0, _market.DeliveryQueue.PendingBoxes);
        }

        [Test]
        public void Entregar_de_dentro_do_aviso_de_entrega_nao_estoura()
        {
            // Dois pedidos vencidos e um assinante que entrega tudo: a varredura
            // que removia durante a iteração indexava fora da lista.
            _market.TryCheckout(Cart(_productA, 1), out _);
            _market.TryCheckout(Cart(_productB, 1), out _);

            int delivered = 0;
            _market.OrderDelivered += _ => { delivered++; _market.DeliverAllNow(); };

            Assert.DoesNotThrow(() => _clock.Tick(3f));
            Assert.AreEqual(2, delivered, "Cada pedido entrega uma vez só.");
            Assert.AreEqual(0, _market.Pending.Count);
        }

        // ── vitrine ──────────────────────────────────────────────────────────

        [Test]
        public void A_vitrine_nao_oferece_o_que_ainda_esta_travado()
        {
            // Recusar o produto travado só no pagamento é tarde: o jogador monta
            // o carrinho, aperta comprar e leva um "produto bloqueado" sem saber
            // qual. A tela tem que nem mostrar.
            CollectionAssert.AreEquivalent(new[] { _productA, _productB },
                                           _market.AvailableProducts);

            _unlocks.StoreLevel = 10;

            CollectionAssert.AreEquivalent(new[] { _productA, _productB, _locked },
                                           _market.AvailableProducts);
        }

        [Test]
        public void Sem_catalogo_a_vitrine_fica_vazia_em_vez_de_estourar()
        {
            var semCatalogo = new MarketOrderService(_ledger, _pricing, _clock, _unlocks);

            Assert.AreEqual(0, semCatalogo.AvailableProducts.Count);

            semCatalogo.Dispose();
        }

        // ── custo ────────────────────────────────────────────────────────────

        [Test]
        public void O_custo_registrado_bate_com_o_que_saiu_do_caixa()
        {
            // A invariante: a tabela de Preços registra EXATAMENTE o que o
            // livro-razão debitou. Hoje ela se sustenta por acidente — BoxCost é
            // derivado (custo unitário × unidades), então o unitário volta
            // exato na divisão. No dia em que a caixa tiver preço próprio (um
            // desconto por volume é o próximo pedido óbvio), ¥100 por 3 unidades
            // vira ¥33 no unitário, e ¥33 × 3 = ¥99 contra ¥100 debitados — um
            // ien por caixa, acumulando, com a margem que o jogador vê saindo de
            // uma base que não é a que ele pagou. Registrar o TOTAL fecha isso
            // agora, e este teste é o que vai reclamar se alguém voltar atrás.
            _market.DeliveryHours = 0f;

            Money antes = _ledger.Balance;
            _market.TryCheckout(Cart(_productA, 3), out _);

            Money gasto = antes - _ledger.Balance;
            PricingData data = _pricing.GetPricingData(_productA);

            Assert.AreEqual(Money.FromYen(2400), gasto);
            Assert.AreEqual(gasto, data.TotalSpent, "O que a tabela registra é o que saiu.");
            Assert.AreEqual(24, data.UnitsInAverage);
        }

        [Test]
        public void A_primeira_compra_nao_inventa_um_custo_anterior()
        {
            // CurrentCost começa valendo o custo de TABELA do produto, que
            // ninguém pagou. Promovê-lo a "custo anterior" na primeira compra
            // fazia a tela mostrar uma variação de preço que nunca houve.
            _market.DeliveryHours = 0f;
            _market.TryCheckout(Cart(_productA, 1), out _);

            PricingData data = _pricing.GetPricingData(_productA);

            Assert.AreEqual(Money.Zero, data.LastCost, "Não havia compra anterior.");
            Assert.AreEqual(Money.FromYen(100), data.CurrentCost);
        }

        [Test]
        public void Carrinho_tem_teto_por_produto()
        {
            // A quantidade vem de um campo de texto, e cada caixa vira um objeto
            // na cena. Sem teto, um 999999 digitado por engano trava a Unity.
            var cart = new MarketCart();
            cart.SetBoxes(_productA, 999999);

            Assert.AreEqual(MarketCart.MaxBoxesPerProduct, cart.TotalBoxes);
        }
    }
}
