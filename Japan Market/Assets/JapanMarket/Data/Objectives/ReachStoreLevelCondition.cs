using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "ReachStoreLevel",
        menuName = "Japan Market/Objective/Chegar ao nível", order = 65)]
    public sealed class ReachStoreLevelCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<StoreLevelChanged>(e => progress.Set(e.Level));

        public override string Describe() => $"Chegar ao nível {Target} da loja";
    }
}

