using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O registro é C# puro justamente para poder ser testado assim: sem cena,
    /// sem Play Mode, sem prefab. Os casos abaixo são os da sua lista de
    /// situações extremas — caixa removida com fila, móvel destruído durante uma
    /// interação, consulta a uma capacidade que ninguém tem.
    /// </summary>
    public sealed class FurnitureRegistryTests
    {
        // ── dublês ───────────────────────────────────────────────────────────

        private interface IDummyCapability : IFurnitureCapability { }

        private sealed class DummyCheckout : ICheckoutStation
        {
            public IFurniture Owner { get; set; }
            public CheckoutStationState State { get; set; } = CheckoutStationState.Idle;
            public bool IsOperational => State != CheckoutStationState.Unavailable;
            public int QueueLength { get; set; }
            public Vector3 QueueAnchor => Vector3.zero;
            public Vector3 QueueDirection => Vector3.back;
            public float QueueSpacing => 1f;
            public Vector3 CounterPosition => Vector3.zero;
            public event Action<ICheckoutStation> StateChanged;
            public void RaiseStateChanged() => StateChanged?.Invoke(this);
        }

        private sealed class FakeFurniture : IFurniture, ICapabilityProvider
        {
            private readonly Dictionary<Type, IFurnitureCapability> _capabilities = new();

            public FurnitureId Id { get; } = FurnitureId.Generate();
            public FurnitureDefinition Definition => null;
            public Vector3 Position { get; set; }
            public Quaternion Rotation => Quaternion.identity;
            public bool IsAlive { get; set; } = true;

            public FakeFurniture With<T>(T capability) where T : class, IFurnitureCapability
            {
                _capabilities[typeof(T)] = capability;
                if (capability is DummyCheckout checkout) checkout.Owner = this;
                return this;
            }

            public bool TryGetCapability<T>(out T capability) where T : class, IFurnitureCapability
            {
                if (_capabilities.TryGetValue(typeof(T), out IFurnitureCapability found))
                { capability = (T)found; return true; }
                capability = null; return false;
            }

            public bool HasCapability<T>() where T : class, IFurnitureCapability =>
                _capabilities.ContainsKey(typeof(T));

            public IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities() =>
                _capabilities;
        }

        // ── testes ───────────────────────────────────────────────────────────

        [Test]
        public void Movel_registrado_aparece_em_All_e_por_id()
        {
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture();

            registry.Register(furniture);

            Assert.AreEqual(1, registry.All.Count);
            Assert.IsTrue(registry.TryGetById(furniture.Id, out IFurniture found));
            Assert.AreSame(furniture, found);
        }

        [Test]
        public void Registrar_duas_vezes_nao_duplica()
        {
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture();

            registry.Register(furniture);
            registry.Register(furniture);

            Assert.AreEqual(1, registry.All.Count);
        }

        [Test]
        public void WithCapability_indexa_por_contrato()
        {
            var registry = new FurnitureRegistry();
            registry.Register(new FakeFurniture().With<ICheckoutStation>(new DummyCheckout()));
            registry.Register(new FakeFurniture());   // sem capacidade nenhuma

            Assert.AreEqual(1, registry.WithCapability<ICheckoutStation>().Count);
        }

        [Test]
        public void WithCapability_sem_ninguem_devolve_lista_vazia_e_nao_null()
        {
            var registry = new FurnitureRegistry();

            IReadOnlyList<IDummyCapability> none = registry.WithCapability<IDummyCapability>();

            Assert.IsNotNull(none, "Nunca devolver null — quem consome faria foreach em null.");
            Assert.AreEqual(0, none.Count);
        }

        [Test]
        public void Removing_dispara_ANTES_de_a_capacidade_sair_do_indice()
        {
            // Este é o coração do caso "caixa registradora sendo removida":
            // quem reage ao aviso ainda precisa conseguir ler a estação para se
            // desligar dela em ordem.
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture().With<ICheckoutStation>(new DummyCheckout());
            registry.Register(furniture);

            int visibleDuringEvent = -1;
            registry.Removing += _ =>
                visibleDuringEvent = registry.WithCapability<ICheckoutStation>().Count;

            registry.Unregister(furniture);

            Assert.AreEqual(1, visibleDuringEvent, "A estação ainda tinha que estar visível.");
            Assert.AreEqual(0, registry.WithCapability<ICheckoutStation>().Count);
        }

        [Test]
        public void Placed_dispara_com_o_movel_ja_consultavel()
        {
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture().With<ICheckoutStation>(new DummyCheckout());

            int visibleDuringEvent = -1;
            registry.Placed += _ =>
                visibleDuringEvent = registry.WithCapability<ICheckoutStation>().Count;

            registry.Register(furniture);

            Assert.AreEqual(1, visibleDuringEvent);
        }

        [Test]
        public void Unregister_de_quem_nunca_entrou_nao_dispara_nada()
        {
            var registry = new FurnitureRegistry();
            bool fired = false;
            registry.Removing += _ => fired = true;

            registry.Unregister(new FakeFurniture());

            Assert.IsFalse(fired);
        }

        [Test]
        public void TryFind_ignora_movel_morto()
        {
            // O NPC pergunta "tem caixa disponível?" no mesmo frame em que o
            // jogador arrancou a última. Não pode receber a morta.
            var registry = new FurnitureRegistry();
            var dead = new FakeFurniture().With<ICheckoutStation>(new DummyCheckout());
            registry.Register(dead);
            dead.IsAlive = false;

            Assert.IsFalse(registry.TryFind<ICheckoutStation>(null, out _));
        }

        [Test]
        public void TryFind_respeita_o_filtro()
        {
            var registry = new FurnitureRegistry();

            var busy = new DummyCheckout { QueueLength = 4 };
            var free = new DummyCheckout { QueueLength = 0 };
            registry.Register(new FakeFurniture().With<ICheckoutStation>(busy));
            registry.Register(new FakeFurniture().With<ICheckoutStation>(free));

            Assert.IsTrue(registry.TryFind<ICheckoutStation>(
                station => station.QueueLength == 0, out ICheckoutStation found));
            Assert.AreSame(free, found);
        }

        [Test]
        public void TryGetById_ignora_movel_morto()
        {
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture();
            registry.Register(furniture);

            furniture.IsAlive = false;

            Assert.IsFalse(registry.TryGetById(furniture.Id, out _));
        }

        [Test]
        public void Clear_avisa_cada_remocao()
        {
            var registry = new FurnitureRegistry();
            registry.Register(new FakeFurniture());
            registry.Register(new FakeFurniture());

            int removed = 0;
            registry.Removing += _ => removed++;

            registry.Clear();

            Assert.AreEqual(2, removed);
            Assert.AreEqual(0, registry.All.Count);
        }
    }
}
