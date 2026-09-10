using System;

namespace JapanMarket.Core
{
    /// <summary>
    /// Canal de comunicação entre sistemas que não se conhecem.
    ///
    /// Contrato importante: <see cref="Subscribe{T}"/> devolve um
    /// <see cref="IDisposable"/>. Guardá-lo e descartá-lo no <c>OnDestroy</c> é o
    /// que torna impossível a categoria "esqueci de remover o listener" — não há
    /// um método <c>Unsubscribe</c> justamente para não existir a tentação de
    /// tentar remover pelo delegate e errar a instância.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Dispara o evento para todos os inscritos, na ordem de inscrição.
        ///
        /// Publicar um evento do MESMO tipo de dentro de um handler dele é sempre
        /// um erro de projeto (recursão) e lança <see cref="InvalidOperationException"/>.
        /// Publicar um evento de tipo diferente é normal e permitido — é assim que
        /// uma venda gera progresso de objetivo.
        /// </summary>
        void Publish<T>(in T gameEvent) where T : struct, IGameEvent;

        /// <summary>
        /// Inscreve um handler. Descarte o retorno para cancelar a inscrição.
        /// Inscrever e cancelar durante um despacho em andamento é seguro.
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent;

        /// <summary>Número de inscritos ativos — só para diagnóstico e testes.</summary>
        int SubscriberCount<T>() where T : struct, IGameEvent;
    }
}
