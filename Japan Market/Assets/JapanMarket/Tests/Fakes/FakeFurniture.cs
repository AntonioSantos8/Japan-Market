using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Um móvel de mentira, com as capacidades que o teste quiser pendurar.
    ///
    /// Está aqui, e não aninhado num arquivo de teste, porque checkout, registro
    /// e — nas próximas fases — economia e objetivos precisam do mesmo dublê. Duas
    /// cópias de um fake divergem exatamente como duas cópias de código de
    /// produção.
    /// </summary>
    public sealed class FakeFurniture : IFurniture, ICapabilityProvider
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

            // Todo dublê de capacidade precisa saber de quem é. Testar por uma
            // interface, e não por cada classe concreta, evita que a próxima
            // capacidade de teste nasça sem dono e o teste falhe por um motivo
            // que não é o testado.
            if (capability is IOwnedTestCapability owned) owned.Owner = this;

            return this;
        }

        public bool TryGetCapability<T>(out T capability) where T : class, IFurnitureCapability
        {
            if (_capabilities.TryGetValue(typeof(T), out IFurnitureCapability found))
            {
                capability = (T)found;
                return true;
            }

            capability = null;
            return false;
        }

        public bool HasCapability<T>() where T : class, IFurnitureCapability =>
            _capabilities.ContainsKey(typeof(T));

        public IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities() =>
            _capabilities;
    }
}
