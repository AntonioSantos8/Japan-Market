using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um objetivo EM ANDAMENTO: a definição mais o progresso desta partida.
    ///
    /// A definição é o asset, compartilhado e imutável; isto aqui é a instância,
    /// uma por partida. Mesma divisão de <see cref="ActiveLoan"/> e
    /// <c>LoanDefinition</c>. Guardar contagem dentro do ScriptableObject
    /// funcionaria no editor e gravaria o progresso do jogador dentro do
    /// projeto — visível para todos os saves, e perdido no build.
    /// </summary>
    public sealed class ActiveObjective
    {
        private readonly int[] _progress;
        private readonly List<IDisposable> _subscriptions = new();

        public ActiveObjective(ObjectiveDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _progress = new int[definition.Conditions.Count];
        }

        public ObjectiveDefinition Definition { get; }
        public bool IsCompleted { get; private set; }

        public int ConditionCount => _progress.Length;

        /// <summary>Progresso da condição, já limitado ao alvo — é o que a barra mostra.</summary>
        public int ProgressOf(int index)
        {
            if (index < 0 || index >= _progress.Length) return 0;

            int target = Definition.Conditions[index].Target;
            return _progress[index] > target ? target : _progress[index];
        }

        /// <summary>Progresso bruto, sem limite. É o que o save guarda.</summary>
        public int RawProgressOf(int index) =>
            index < 0 || index >= _progress.Length ? 0 : _progress[index];

        public int TargetOf(int index) =>
            index < 0 || index >= _progress.Length ? 0 : Definition.Conditions[index].Target;

        public bool IsConditionMet(int index) =>
            index >= 0 && index < _progress.Length
            && _progress[index] >= Definition.Conditions[index].Target;

        /// <summary>Todas as condições cumpridas. Não diz nada sobre já ter pago.</summary>
        public bool AllConditionsMet
        {
            get
            {
                for (int i = 0; i < _progress.Length; i++)
                    if (!IsConditionMet(i)) return false;

                return _progress.Length > 0;
            }
        }

        /// <summary>Fração de 0 a 1 somando todas as condições. Para a barra geral.</summary>
        public float NormalizedProgress
        {
            get
            {
                if (_progress.Length == 0) return 0f;

                float sum = 0f;
                for (int i = 0; i < _progress.Length; i++)
                {
                    int target = TargetOf(i);
                    if (target > 0) sum += ProgressOf(i) / (float)target;
                }

                return sum / _progress.Length;
            }
        }

        // ── escrita, só de dentro do serviço ─────────────────────────────────

        internal void Track(IDisposable subscription)
        {
            if (subscription != null) _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Solta as inscrições. Chamado quando o objetivo conclui e quando o
        /// serviço morre: um objetivo concluído que continua ouvindo o
        /// barramento é trabalho por venda, para sempre, sem efeito nenhum.
        /// </summary>
        internal void Unsubscribe()
        {
            for (int i = 0; i < _subscriptions.Count; i++) _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }

        internal void MarkCompleted() => IsCompleted = true;

        internal IObjectiveProgress Slot(int index, Action onChanged) =>
            new ProgressSlot(this, index, onChanged);

        internal void RestoreProgress(IReadOnlyList<int> values, bool completed)
        {
            if (values != null)
                for (int i = 0; i < _progress.Length && i < values.Count; i++)
                    _progress[i] = values[i] < 0 ? 0 : values[i];

            IsCompleted = completed;
        }

        public override string ToString() =>
            $"{Definition} — {(IsCompleted ? "concluído" : $"{NormalizedProgress:P0}")}";

        /// <summary>
        /// A ponta que a condição enxerga: uma casa do vetor de progresso.
        ///
        /// A condição recebe isto e não o objetivo inteiro, de propósito. Ela não
        /// consegue concluir nada, não consegue ler as outras condições e não
        /// consegue mexer na definição — o único poder que tem é dizer quanto
        /// observou. O <paramref name="onChanged"/> avisa o serviço de que há o
        /// que reavaliar, sem que a condição saiba que o serviço existe.
        /// </summary>
        private sealed class ProgressSlot : IObjectiveProgress
        {
            private readonly ActiveObjective _owner;
            private readonly int _index;
            private readonly Action _onChanged;

            public ProgressSlot(ActiveObjective owner, int index, Action onChanged)
            {
                _owner = owner;
                _index = index;
                _onChanged = onChanged;
            }

            public int Current => _owner._progress[_index];

            public void Add(int amount)
            {
                // Uma condição que reporta valor negativo faria a barra do
                // jogador ANDAR PARA TRÁS. Nenhum objetivo deste jogo tira
                // progresso, e se um dia tirar isso vira uma decisão explícita e
                // não um efeito colateral de um evento com sinal trocado.
                if (amount <= 0 || _owner.IsCompleted) return;

                _owner._progress[_index] += amount;
                _onChanged?.Invoke();
            }

            public void Set(int value)
            {
                if (_owner.IsCompleted) return;

                int clamped = value < 0 ? 0 : value;
                if (_owner._progress[_index] == clamped) return;

                _owner._progress[_index] = clamped;
                _onChanged?.Invoke();
            }
        }
    }
}
