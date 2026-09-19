using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O que o save guarda de um objetivo. Id e números — nenhuma referência a
    /// asset, porque um asset apagado entre duas versões do jogo não pode
    /// derrubar o carregamento.
    /// </summary>
    public readonly struct ObjectiveSnapshot
    {
        public readonly ObjectiveId Id;
        public readonly int[] Progress;
        public readonly bool Completed;

        public ObjectiveSnapshot(ObjectiveId id, int[] progress, bool completed)
        {
            Id = id;
            Progress = progress ?? Array.Empty<int>();
            Completed = completed;
        }
    }

    /// <summary>
    /// A lista de objetivos da loja: quais estão valendo, quanto falta, e o que
    /// acontece quando um conclui.
    ///
    /// O que este serviço deliberadamente NÃO tem: conhecimento de nenhum
    /// objetivo específico. Não existe "se for o objetivo 3, conte vendas" em
    /// lugar nenhum — cada <c>ObjectiveCondition</c> se inscreve sozinha no
    /// barramento e reporta. Adicionar o objetivo número 40 é criar um asset.
    /// </summary>
    public interface IObjectiveService
    {
        /// <summary>Objetivos valendo agora, incluindo os já concluídos ainda em tela.</summary>
        IReadOnlyList<ActiveObjective> Active { get; }

        /// <summary>Quantos podem estar valendo ao mesmo tempo. 0 = sem limite.</summary>
        int MaxActive { get; set; }

        /// <summary>
        /// Falso durante fluxos exclusivos, como o tutorial inicial. Objetivos
        /// da loja não acumulam progresso nem pagam recompensas nesse período.
        /// </summary>
        bool TrackingEnabled { get; }

        void SetTrackingEnabled(bool enabled);

        /// <summary>
        /// Paga o que venceu e ativa o que desbloqueou.
        ///
        /// Existe como passo separado por um motivo específico, e não por gosto:
        /// o progresso é anotado DENTRO do despacho de um evento, e pagar a
        /// recompensa ali publicaria um evento de dinheiro de dentro de um
        /// handler de dinheiro — o barramento tem guarda de recursão e LANÇA.
        /// Anotar e pagar depois é o que mantém as duas coisas fora do caminho
        /// uma da outra. Chamado a cada frame pelo <c>ObjectiveRunner</c>.
        /// </summary>
        void Flush();

        IReadOnlyList<ObjectiveSnapshot> Snapshot();

        event Action<ActiveObjective> ObjectiveStarted;
        event Action<ActiveObjective> ObjectiveCompleted;

        /// <summary>Qualquer progresso mudou. Para a UI redesenhar a lista.</summary>
        event Action ProgressChanged;
    }
}
