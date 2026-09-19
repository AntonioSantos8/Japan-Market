using System;
using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Um slot da roda: o que ele traz de fábrica e quando ele abre.
    /// </summary>
    [Serializable]
    public struct ToolSlotLayout
    {
        [Tooltip("Ferramenta que ocupa este slot. Pode ficar vazio num slot que " +
                 "o jogador preenche comprando.")]
        public ToolDefinition Tool;

        [Tooltip("Quando o slot destrava. Vazio = aberto desde o começo.")]
        public UnlockCondition Unlock;
    }

    /// <summary>
    /// A roda de ferramentas: quantos slots, em que ordem, e quais são travados.
    ///
    /// É um asset, e não um array no Inspector de um MonoBehaviour, por um
    /// motivo prático: a roda precisa existir igual em toda cena — a principal,
    /// a Sandbox, a de teste — e uma lista por cena é uma lista que diverge.
    ///
    /// O destravamento é o mesmo <see cref="UnlockCondition"/> de produto, móvel
    /// e empréstimo. Um slot que abre "no nível 5" e um que abre "ao concluir o
    /// objetivo X" são o mesmo campo com assets diferentes — nada no código da
    /// roda sabe que existe nível ou objetivo.
    /// </summary>
    [CreateAssetMenu(fileName = "ToolBeltLayout",
        menuName = "Japan Market/Tool/Belt Layout", order = 82)]
    public sealed class ToolBeltLayout : ScriptableObject
    {
        [Tooltip("Os slots, na ordem em que aparecem na roda.")]
        [SerializeField] private ToolSlotLayout[] _slots;

        public IReadOnlyList<ToolSlotLayout> Slots =>
            (IReadOnlyList<ToolSlotLayout>)_slots ?? Array.Empty<ToolSlotLayout>();

        public int SlotCount => _slots?.Length ?? 0;

        /// <summary>
        /// Problemas que o validador reporta. O pior deles é o slot repetido: a
        /// mesma ferramenta em dois slots teria DOIS desgastes independentes, e o
        /// jogador consertaria um e continuaria com o outro quebrado.
        /// </summary>
        public string DescribeProblem()
        {
            if (_slots == null || _slots.Length == 0)
                return "Nenhum slot: o jogador não teria ferramenta nenhuma.";

            for (int i = 0; i < _slots.Length; i++)
            {
                ToolDefinition tool = _slots[i].Tool;
                if (tool == null) continue;

                for (int j = i + 1; j < _slots.Length; j++)
                {
                    if (_slots[j].Tool != tool) continue;

                    return $"'{tool.name}' aparece nos slots {i + 1} e {j + 1}. " +
                           "Cada slot tem desgaste próprio — o jogador consertaria um " +
                           "e continuaria com o outro quebrado.";
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorSetSlots(ToolSlotLayout[] slots) => _slots = slots;
#endif
    }
}
