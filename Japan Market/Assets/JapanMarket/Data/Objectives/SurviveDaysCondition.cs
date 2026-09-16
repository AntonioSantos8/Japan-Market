using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "SurviveDays",
        menuName = "Japan Market/Objective/Sobreviver dias", order = 63)]
    public sealed class SurviveDaysCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<DayEnded>(_ => progress.Add(1));

        public override string Describe() => $"Sobreviver {Target} dia(s)";
    }
}

