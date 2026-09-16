using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "SellItems",
        menuName = "Japan Market/Objective/Vender itens", order = 60)]
    public sealed class SellItemsCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<SaleCompleted>(e => progress.Add(e.ItemCount));

        public override string Describe() => $"Vender {Target} {(Target == 1 ? "item" : "itens")}";
    }
}

