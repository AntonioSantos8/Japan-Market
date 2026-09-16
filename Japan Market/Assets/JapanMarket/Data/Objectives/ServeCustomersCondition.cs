using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
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
}

