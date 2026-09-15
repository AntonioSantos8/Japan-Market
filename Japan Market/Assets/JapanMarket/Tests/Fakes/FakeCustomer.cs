using System.Collections.Generic;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Um cliente de mentira que só anota os sinais que recebeu.
    ///
    /// É o suficiente para verificar a parte do NPC que mais quebra hoje: quem
    /// foi avisado, com o quê, e quantas vezes. Nada disso precisa de NavMesh.
    /// </summary>
    public sealed class FakeCustomer : ICustomer
    {
        private static int _nextId = 1;

        private readonly List<CustomerSignal> _signals = new();

        public FakeCustomer(string name = null)
        {
            Id = _nextId++;
            Name = name ?? $"cliente {Id}";
        }

        public int Id { get; }
        public string Name { get; }
        public Vector3 Position { get; set; }
        public bool IsAlive { get; set; } = true;

        public IReadOnlyList<CustomerSignal> Signals => _signals;

        public int CountOf(CustomerSignal signal)
        {
            int count = 0;
            for (int i = 0; i < _signals.Count; i++)
                if (_signals[i] == signal) count++;

            return count;
        }

        public bool Received(CustomerSignal signal) => CountOf(signal) > 0;

        public void Notify(CustomerSignal signal) => _signals.Add(signal);

        public void ClearSignals() => _signals.Clear();

        public override string ToString() => Name;
    }
}
