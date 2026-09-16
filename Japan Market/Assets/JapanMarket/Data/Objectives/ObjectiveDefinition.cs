using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// O que o jogador ganha ao concluir um objetivo.
    ///
    /// Os três campos são opcionais e somam: dinheiro, XP, e uma flag de
    /// progresso. A flag é o que liga objetivo a desbloqueio sem criar acoplamento
    /// nenhum — o objetivo levanta "primeira_venda", e um <c>UnlockCondition</c>
    /// qualquer pergunta por ela. Nenhum dos dois lados conhece o outro.
    /// </summary>
    [System.Serializable]
    public struct ObjectiveReward
    {
        [Tooltip("Depositado no caixa ao concluir. Zero não movimenta o livro-razão.")]
        public Money Money;

        [Min(0)]
        [Tooltip("XP da loja concedido ao concluir.")]
        public int XP;

        [Tooltip("Flag de progresso levantada ao concluir. Deixe vazio se não usar.")]
        public string Flag;

        public bool HasMoney => Money.IsPositive;
        public bool HasXP => XP > 0;
        public bool HasFlag => !string.IsNullOrWhiteSpace(Flag);
        public bool IsEmpty => !HasMoney && !HasXP && !HasFlag;
    }

    /// <summary>
    /// Um objetivo da loja: um título, uma ou mais condições, e uma recompensa.
    ///
    /// Criar um objetivo é criar este asset e apontar as condições. Não existe
    /// lista para arrastar, número para cadastrar, nem <c>switch</c> para
    /// editar — o catálogo se popula no import e o serviço trata todos igual.
    /// É isto que faz o "objetivo 37" não precisar existir em lugar nenhum do
    /// código.
    ///
    /// Várias condições significam TODAS: "venda 20 itens E sobreviva 3 dias".
    /// Para "ou", crie dois objetivos — a UI fica mais clara e a regra também.
    /// </summary>
    [CreateAssetMenu(fileName = "Objective", menuName = "Japan Market/Objective", order = 50)]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        [Tooltip("Gerado na criação do asset. O save guarda isto, não o nome.")]
        [SerializeField, ReadOnlyField] private ObjectiveId _id;

        [Header("Texto")]
        [SerializeField] private LocalizedText _title;
        [SerializeField] private LocalizedText _description;

        [Header("Regra")]
        [Tooltip("TODAS precisam estar cumpridas. Uma lista vazia torna o objetivo inválido.")]
        [SerializeField] private ObjectiveCondition[] _conditions;

        [Header("Recompensa")]
        [SerializeField] private ObjectiveReward _reward;

        [Header("Progressão")]
        [Tooltip("Quando este objetivo passa a valer. Vazio = disponível desde o começo.")]
        [SerializeField] private UnlockCondition _unlock;

        [Tooltip("Ordem de exibição na lista de objetivos. Menor aparece primeiro.")]
        [SerializeField] private int _sortOrder;

        public ObjectiveId Id => _id;
        public LocalizedText Title => _title;
        public LocalizedText Description => _description;
        public ObjectiveReward Reward => _reward;
        public UnlockCondition Unlock => _unlock;
        public int SortOrder => _sortOrder;

        public IReadOnlyList<ObjectiveCondition> Conditions =>
            (IReadOnlyList<ObjectiveCondition>)_conditions ?? System.Array.Empty<ObjectiveCondition>();

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);

        /// <summary>
        /// Objetivo utilizável? Sem condição nenhuma ele é concluído no instante
        /// em que aparece, e o jogador recebe a recompensa sem ter feito nada —
        /// que é pior que o objetivo não existir, porque ninguém percebe.
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (!_id.IsValid) return false;
                if (_conditions == null || _conditions.Length == 0) return false;

                for (int i = 0; i < _conditions.Length; i++)
                    if (_conditions[i] == null || !_conditions[i].IsValid) return false;

                return true;
            }
        }

        /// <summary>Motivo da invalidez, para o validador de catálogo.</summary>
        public string DescribeProblem()
        {
            if (!_id.IsValid) return "Sem id — recrie o asset para gerar um.";
            if (_conditions == null || _conditions.Length == 0)
                return "Sem condições: concluiria sozinho e pagaria a recompensa de graça.";

            for (int i = 0; i < _conditions.Length; i++)
            {
                if (_conditions[i] == null) return $"Condição {i + 1} está vazia.";
                if (!_conditions[i].IsValid) return $"Condição {i + 1} tem alvo menor que 1.";
            }

            return null;
        }

        public override string ToString() =>
            $"{(_title.IsEmpty ? name : _title.Value)} ({Conditions.Count} condição/ões)";

#if UNITY_EDITOR
        /// <summary>Chamado uma vez pelo import, quando o asset nasce sem id.</summary>
        public bool EditorEnsureId()
        {
            if (_id.IsValid) return false;

            _id = ObjectiveId.Generate();
            return true;
        }

        public void EditorInitialize(ObjectiveId id, ObjectiveCondition[] conditions,
                                     ObjectiveReward reward, UnlockCondition unlock = null,
                                     int sortOrder = 0)
        {
            _id = id;
            _conditions = conditions;
            _reward = reward;
            _unlock = unlock;
            _sortOrder = sortOrder;
        }
#endif
    }
}
