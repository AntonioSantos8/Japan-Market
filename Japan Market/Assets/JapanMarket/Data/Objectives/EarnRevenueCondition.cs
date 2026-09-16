using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
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
}

