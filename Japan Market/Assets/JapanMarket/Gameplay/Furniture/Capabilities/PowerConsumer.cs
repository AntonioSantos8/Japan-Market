using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Este móvel gasta energia enquanto ligado.
    ///
    /// O custo vem do <see cref="FurnitureDefinition"/>, não de um campo no
    /// prefab: mudar quanto um freezer consome é editar um asset, e todos os
    /// freezers da loja mudam junto.
    ///
    /// A conta de luz do fim do dia (Fase 6) é literalmente a soma dos
    /// <see cref="IPowerConsumer"/> registrados que estiverem ligados — o aviso
    /// da referência sobre dispositivos elétricos encarecerem a despesa vira
    /// consequência do modelo, não uma regra escrita à parte.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerConsumer : FurnitureCapabilityBehaviour, IPowerConsumer
    {
        [Tooltip("Começa ligado? Um freezer sim; um letreiro talvez não.")]
        [SerializeField] private bool _startsPoweredOn = true;

        [Tooltip("Sobrepõe o custo da definição. Deixe zero para usar a definição.")]
        [SerializeField] private Money _costOverride;

        private bool _poweredOn;

        public bool IsPoweredOn => _poweredOn;

        public Money DailyCost
        {
            get
            {
                if (!_poweredOn) return Money.Zero;
                if (_costOverride > Money.Zero) return _costOverride;

                // Sem ?. aqui de propósito: Furniture é um objeto Unity, e o
                // operador de coalescência nula ignora a sobrecarga de == que
                // detecta objeto destruído.
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
