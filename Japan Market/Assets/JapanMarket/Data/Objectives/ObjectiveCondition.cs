using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Onde uma condição anota o que observou.
    ///
    /// A condição NÃO sabe a qual objetivo pertence, não guarda contagem e não
    /// decide se concluiu. Ela é o asset — uma regra compartilhada, possivelmente
    /// por vários objetivos ao mesmo tempo — e um asset que guardasse progresso
    /// seria progresso compartilhado entre partidas, salvo dentro do projeto.
    /// É a mesma separação de <c>LoanDefinition</c> (asset) e <c>ActiveLoan</c>
    /// (instância em jogo).
    /// </summary>
    public interface IObjectiveProgress
    {
        int Current { get; }

        /// <summary>Soma ao acumulado: "venda 50 itens".</summary>
        void Add(int amount);

        /// <summary>Substitui o acumulado: "chegue ao nível 5".</summary>
        void Set(int value);
    }

    /// <summary>
    /// Uma coisa que o jogador precisa fazer. O asset diz O QUE contar e QUANTO.
    ///
    /// Adicionar um tipo novo de objetivo é herdar desta classe e criar o asset.
    /// Nenhum sistema existente muda, porque ninguém em lugar nenhum pergunta
    /// "que tipo de objetivo é esse?" — o serviço só chama <see cref="Watch"/> e
    /// espera o progresso chegar. É deliberadamente o mesmo formato do
    /// <see cref="UnlockCondition"/>, que já resolveu este problema para
    /// desbloqueios: a alternativa era um <c>switch (tipoDeObjetivo)</c> no
    /// gerente, e aí cada objetivo novo edita o gerente.
    ///
    /// <see cref="Watch"/> recebe o barramento e devolve a inscrição. Quem assina
    /// descarta — a condição não guarda o IDisposable, porque o mesmo asset pode
    /// estar sendo observado por dois objetivos.
    ///
    /// A consequência de projeto, e ela é intencional: uma condição só consegue
    /// observar o que passa pelo barramento. Um objetivo sobre algo que não é
    /// publicado começa publicando o evento. Isso mantém Data sem enxergar
    /// Domain, e força que todo estado interessante do jogo seja anunciado —
    /// que é exatamente o que a UI e o relatório diário também precisam.
    /// </summary>
    public abstract class ObjectiveCondition : ScriptableObject
    {
        [Min(1)]
        [Tooltip("Quanto é preciso acumular para esta condição estar cumprida.")]
        [SerializeField] private int _target = 1;

        public int Target => _target < 1 ? 1 : _target;

        /// <summary>
        /// Liga a condição ao barramento. Tudo o que ela observar vai para
        /// <paramref name="progress"/>.
        /// </summary>
        public abstract IDisposable Watch(IEventBus events, IObjectiveProgress progress);

        /// <summary>Texto para a UI: "Vender 50 itens".</summary>
        public abstract string Describe();

        /// <summary>
        /// Alvo zero ou negativo conclui o objetivo no instante em que ele
        /// começa — prêmio de graça por um asset mal preenchido.
        /// </summary>
        public virtual bool IsValid => _target >= 1;

#if UNITY_EDITOR
        /// <summary>Só editor e testes escrevem: em runtime a condição é dado.</summary>
        public void EditorSetTarget(int target) => _target = target < 1 ? 1 : target;
#endif
    }
}
