using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{

    public sealed class FurnitureRegistryTests
    {

        private interface IDummyCapability : IFurnitureCapability { }

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
            registry.Register(new FakeFurniture().With<ICheckoutStation>(new FakeCheckoutStation()));
            registry.Register(new FakeFurniture());   

            Assert.AreEqual(1, registry.WithCapability<ICheckoutStation>().Count);
        }

        [Test]
        public void WithCapability_sem_ninguem_devolve_lista_vazia_e_nao_null()
        {
            var registry = new FurnitureRegistry();

            IReadOnlyList<IDummyCapability> none = registry.WithCapability<IDummyCapability>();

            Assert.IsNotNull(none, "Never return null — whoever consumes would foreach on null.");
            Assert.AreEqual(0, none.Count);
        }

        [Test]
        public void Removing_dispara_ANTES_de_a_capacidade_sair_do_indice()
        {

            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture().With<ICheckoutStation>(new FakeCheckoutStation());
            registry.Register(furniture);

            int visibleDuringEvent = -1;
            registry.Removing += _ =>
                visibleDuringEvent = registry.WithCapability<ICheckoutStation>().Count;

            registry.Unregister(furniture);

            Assert.AreEqual(1, visibleDuringEvent, "The station still had to be visible.");
            Assert.AreEqual(0, registry.WithCapability<ICheckoutStation>().Count);
        }

        [Test]
        public void Placed_dispara_com_o_movel_ja_consultavel()
        {
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture().With<ICheckoutStation>(new FakeCheckoutStation());

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

            var registry = new FurnitureRegistry();
            var dead = new FakeFurniture().With<ICheckoutStation>(new FakeCheckoutStation());
            registry.Register(dead);
            dead.IsAlive = false;

            Assert.IsFalse(registry.TryFind<ICheckoutStation>(null, out _));
        }

        [Test]
        public void TryFind_respeita_o_filtro()
        {
            var registry = new FurnitureRegistry();

            var busy = new FakeCheckoutStation();
            var free = new FakeCheckoutStation();
            registry.Register(new FakeFurniture().With<ICheckoutStation>(busy));
            registry.Register(new FakeFurniture().With<ICheckoutStation>(free));

            for (int i = 0; i < 4; i++) busy.TryJoinQueue(new FakeCustomer(), out _);

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
