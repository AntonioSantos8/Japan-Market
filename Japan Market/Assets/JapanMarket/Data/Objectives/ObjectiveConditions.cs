using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>"Venda 50 itens." Conta ITENS, não vendas.</summary>
    [CreateAssetMenu(fileName = "SellItems",
        menuName = "Japan Market/Objective/Vender itens", order = 60)]
    public sealed class SellItemsCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<SaleCompleted>(e => progress.Add(e.ItemCount));

        public override string Describe() => $"Vender {Target} {(Target == 1 ? "item" : "itens")}";
    }

    /// <summary>
    /// "Fature ¥20.000." Soma a RECEITA das vendas, e não o saldo.
    ///
    /// Receita, de propósito: um objetivo amarrado ao saldo é cumprido pegando
    /// empréstimo, e desfeito pagando o aluguel. O que o jogador fez continua
    /// feito mesmo que o dinheiro já tenha saído.
    /// </summary>
    [CreateAssetMenu(fileName = "EarnRevenue",
        menuName = "Japan Market/Objective/Faturar em vendas", order = 61)]
    public sealed class EarnRevenueCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<SaleCompleted>(e => progress.Add(ToInt(e.Revenue)));

        public override string Describe() => $"Faturar ¥{Target:N0} em vendas";

        /// <summary>
        /// Money é long e o progresso é int. Uma venda acima de ~¥2 bilhões não
        /// existe neste jogo, mas o clamp evita que um dia ela vire progresso
        /// NEGATIVO por estouro — e um objetivo que anda para trás é o tipo de
        /// bug que ninguém encontra.
        /// </summary>
        private static int ToInt(Money money) =>
            money.Yen <= 0 ? 0 : money.Yen > int.MaxValue ? int.MaxValue : (int)money.Yen;
    }

    /// <summary>"Atenda 30 clientes." Só conta quem saiu tendo comprado.</summary>
    [CreateAssetMenu(fileName = "ServeCustomers",
        menuName = "Japan Market/Objective/Atender clientes", order = 62)]
    public sealed class ServeCustomersCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<CustomerLeft>(e =>
            {
                if (e.Reason == CustomerLeaveReason.Purchased) progress.Add(1);
            });

        public override string Describe() => $"Atender {Target} cliente(s)";
    }

    /// <summary>"Sobreviva 7 dias." Conta fechamentos de dia, não aberturas.</summary>
    [CreateAssetMenu(fileName = "SurviveDays",
        menuName = "Japan Market/Objective/Sobreviver dias", order = 63)]
    public sealed class SurviveDaysCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<DayEnded>(_ => progress.Add(1));

        public override string Describe() => $"Sobreviver {Target} dia(s)";
    }

    /// <summary>"Receba 20 caixas de estoque."</summary>
    [CreateAssetMenu(fileName = "ReceiveStock",
        menuName = "Japan Market/Objective/Receber caixas", order = 64)]
    public sealed class ReceiveStockCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<StockOrderDelivered>(e => progress.Add(e.BoxCount));

        public override string Describe() => $"Receber {Target} caixa(s) de estoque";
    }

    /// <summary>
    /// "Recicle 20 itens." Opcionalmente de uma categoria só.
    ///
    /// O filtro é pela CHAVE da categoria, em texto, e não por referência ao
    /// asset: Data pode referenciar Data, mas o evento vem de Core, que não
    /// enxerga o <c>TrashCategory</c>. Deixar o filtro vazio conta tudo.
    /// </summary>
    [CreateAssetMenu(fileName = "RecycleTrash",
        menuName = "Japan Market/Objective/Reciclar lixo", order = 66)]
    public sealed class RecycleTrashCondition : ObjectiveCondition
    {
        [Tooltip("Chave da categoria (plastico, metal...). Vazio conta qualquer lixo.")]
        [SerializeField] private string _categoryKey;

        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<TrashDiscarded>(e =>
            {
                if (!Matches(e.CategoryKey)) return;

                progress.Add(1);
            });

        private bool Matches(string key) =>
            string.IsNullOrWhiteSpace(_categoryKey)
            || string.Equals(key, _categoryKey.Trim(), StringComparison.OrdinalIgnoreCase);

        public override string Describe() =>
            string.IsNullOrWhiteSpace(_categoryKey)
                ? $"Reciclar {Target} item(ns) de lixo"
                : $"Reciclar {Target} item(ns) de {_categoryKey}";

#if UNITY_EDITOR
        public void EditorSetCategoryKey(string key) => _categoryKey = key;
#endif
    }

    /// <summary>
    /// "Chegue ao nível 5."
    ///
    /// Usa <c>Set</c>, e não <c>Add</c>: o nível é um estado, não um acumulado.
    /// Somar cada aviso de mudança faria subir do 1 ao 3 valer três, e o objetivo
    /// "chegue ao nível 3" seria cumprido no nível 3 por coincidência — e o
    /// "chegue ao nível 10" seria cumprido no nível 4.
    /// </summary>
    [CreateAssetMenu(fileName = "ReachStoreLevel",
        menuName = "Japan Market/Objective/Chegar ao nível", order = 65)]
    public sealed class ReachStoreLevelCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<StoreLevelChanged>(e => progress.Set(e.Level));

        public override string Describe() => $"Chegar ao nível {Target} da loja";
    }
}
