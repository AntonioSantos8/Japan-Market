using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class PowerConsumer : FurnitureCapabilityBehaviour, IPowerConsumer
    {
        [Tooltip("Starts turned on? A freezer yes; a sign maybe not.")]
        [SerializeField] private bool _startsPoweredOn = true;

        [Tooltip("Overrides the definition cost. Leave zero to use the definition.")]
        [SerializeField] private Money _costOverride;

        private bool _poweredOn;

        public bool IsPoweredOn => _poweredOn;

        public Money DailyCost
        {
            get
            {
                if (!_poweredOn) return Money.Zero;
                if (_costOverride > Money.Zero) return _costOverride;

                FurnitureInstance owner = Furniture;
                if (owner == null || owner.Definition == null) return Money.Zero;

                return owner.Definition.DailyPowerCost;
            }
        }

        public void SetPowered(bool on) => _poweredOn = on;

        protected override void Awake()
        {
            base.Awake();
            _poweredOn = _startsPoweredOn;
        }
    }
}
