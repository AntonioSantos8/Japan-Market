using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "ReceiveStock",
        menuName = "Japan Market/Objective/Receber caixas", order = 64)]
    public sealed class ReceiveStockCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<StockOrderDelivered>(e => progress.Add(e.BoxCount));

        public override string Describe() => $"Receber {Target} caixa(s) de estoque";
    }
}

