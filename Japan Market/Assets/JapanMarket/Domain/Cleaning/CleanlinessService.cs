using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Uma sujeira registrada. O serviço só precisa saber que ela existe.
    /// </summary>
    public interface IDirtSource
    {
        /// <summary>Quanto ainda falta limpar. Zero significa limpa.</summary>
        float Remaining { get; }
    }

    /// <summary>
    /// O lado de ESCRITA da limpeza: onde a sujeira se anuncia.
    ///
    /// Separado do <see cref="IStoreCleanliness"/>, que só lê, pela mesma razão
    /// que <c>IProgressFlags</c> é separado de <c>IUnlockContext</c>: o cliente
    /// consulta a limpeza para decidir se vai embora, e um cliente capaz de
    /// registrar sujeira é um cliente capaz de mentir sobre a loja.
    /// </summary>
    public interface IDirtRegistry
    {
        void Register(IDirtSource dirt);
        void Unregister(IDirtSource dirt);
    }

    /// <summary>
    /// Quanta sujeira existe na loja, e quem avisa quando isso muda.
    ///
    /// Implementa o <see cref="IStoreCleanliness"/> que a IA do cliente já
    /// consumia desde a Fase 4 e que ninguém fornecia — o
    /// <c>CustomerContext.WantsToLeaveDirty</c> comparava contra um serviço nulo
    /// e nunca era verdade. De hoje em diante a loja suja de fato espanta
    /// cliente.
    ///
    /// Substitui o <c>Clean.ActiveDustCount</c>, um contador ESTÁTICO
    /// incrementado no OnEnable de cada sujeira. Três problemas concretos que
    /// isso tinha: não zerava entre partidas no editor (entrar em Play duas
    /// vezes deixava a loja permanentemente suja), não era testável sem
    /// instanciar sujeira de verdade, e um objeto desativado em vez de destruído
    /// contava como limpo enquanto continuava visível na tela.
    /// </summary>
    public sealed class CleanlinessService : IStoreCleanliness, IDirtRegistry
    {
        private readonly IEventBus _events;
        private readonly HashSet<IDirtSource> _dirt = new();

        private int _lastReported = -1;

        /// <summary>
        /// Quantas sujeiras equivalem a "no limite do tolerável" (Normalized 1).
        /// Não é um teto: a loja pode passar disso, e o valor satura em 1.
        /// </summary>
        public int ToleranceReference { get; set; } = 12;

        public CleanlinessService(IEventBus events = null)
        {
            _events = events;
        }

        public int ActiveDirtCount => _dirt.Count;

        public float Normalized
        {
            get
            {
                if (ToleranceReference <= 0) return _dirt.Count > 0 ? 1f : 0f;

                float value = _dirt.Count / (float)ToleranceReference;
                return value > 1f ? 1f : value;
            }
        }

        /// <summary>
        /// Registra uma sujeira. HashSet de propósito: a mesma sujeira
        /// registrada duas vezes (um OnEnable depois de um reparent, por
        /// exemplo) contaria dobrado e a loja pareceria o dobro de suja.
        /// </summary>
        public void Register(IDirtSource dirt)
        {
            if (dirt == null || !_dirt.Add(dirt)) return;

            Announce();
        }

        public void Unregister(IDirtSource dirt)
        {
            if (dirt == null || !_dirt.Remove(dirt)) return;

            Announce();
        }

        /// <summary>
        /// Esvazia tudo. Chamado ao carregar um save e ao recomeçar — sem isto o
        /// contador atravessaria a troca de partida, que é exatamente o defeito
        /// do contador estático que este serviço substitui.
        /// </summary>
        public void Clear()
        {
            if (_dirt.Count == 0) return;

            _dirt.Clear();
            Announce();
        }

        /// <summary>
        /// Só publica quando o número muda de verdade. Uma loja com trinta
        /// sujeiras sendo esfregadas publicaria trinta eventos idênticos por
        /// segundo, e quem assina redesenha a cada um.
        /// </summary>
        private void Announce()
        {
            if (_dirt.Count == _lastReported) return;

            _lastReported = _dirt.Count;
            _events?.Publish(new CleanlinessChanged(_dirt.Count, Normalized));
        }

        public override string ToString() =>
            $"{_dirt.Count} sujeira(s), {Normalized:P0} do tolerável";
    }
}
