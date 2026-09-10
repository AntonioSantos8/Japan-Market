using System;

namespace JapanMarket.Core
{
    /// <summary>
    /// Um handler publicou o mesmo tipo de evento que estava tratando.
    ///
    /// Isso é sempre recursão, e sempre erro de projeto: sem o guard, a pilha
    /// estoura. Herda de InvalidOperationException para quem só quer capturar a
    /// categoria genérica, mas tem tipo próprio para que o bus consiga
    /// distinguir a exceção do guard de uma exceção qualquer de handler — a
    /// primeira precisa chegar ao chamador, a segunda não pode derrubar os
    /// outros inscritos.
    /// </summary>
    public sealed class EventBusRecursionException : InvalidOperationException
    {
        public Type EventType { get; }

        public EventBusRecursionException(Type eventType)
            : base($"[EventBus] '{eventType.Name}' foi publicado de dentro de um handler " +
                   $"do próprio '{eventType.Name}'. Isso é recursão e sempre indica erro " +
                   "de projeto. Publique um evento de outro tipo, ou adie a publicação " +
                   "para depois do despacho corrente.")
        {
            EventType = eventType;
        }
    }
}
