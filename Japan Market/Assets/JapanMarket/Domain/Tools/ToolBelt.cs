using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão da roda de ferramentas. C# puro.
    /// </summary>
    public sealed class ToolBelt : IToolBelt, IDisposable
    {
        private readonly IUnlockContext _unlocks;
        private readonly IEventBus _events;
        private readonly List<ToolSlot> _slots = new();
        private readonly List<IDisposable> _subscriptions = new();

        private bool[] _wasUnlocked;
        private int _selected = -1;
        private bool _disposed;

        public ToolBelt(ToolBeltLayout layout, IUnlockContext unlocks = null,
                        IEventBus events = null)
        {
            _unlocks = unlocks;
            _events = events;

            Build(layout);

            // Os dois momentos em que um slot pode ter destravado sem ninguém
            // tocar na roda. Sem isto, o slot que abre no nível 5 só apareceria
            // na próxima vez que o jogador abrisse a roda — e ele não tem como
            // saber que deveria abrir.
            if (_events != null)
            {
                _subscriptions.Add(_events.Subscribe<StoreLevelChanged>(_ => Refresh()));
                _subscriptions.Add(_events.Subscribe<DayStarted>(_ => Refresh()));
                _subscriptions.Add(_events.Subscribe<ObjectiveCompleted>(_ => Refresh()));
            }
        }

        public IReadOnlyList<ToolSlot> Slots => _slots;

        public ToolSlot Selected =>
            _selected >= 0 && _selected < _slots.Count ? _slots[_selected] : null;

        public int SelectedIndex => _selected;

        public event Action<ToolSlot> SelectionChanged;
        public event Action<ToolSlot> Broke;
        public event Action SlotsChanged;

        private void Build(ToolBeltLayout layout)
        {
            _slots.Clear();

            if (layout != null)
            {
                IReadOnlyList<ToolSlotLayout> slots = layout.Slots;
                for (int i = 0; i < slots.Count; i++)
                    _slots.Add(new ToolSlot(i, slots[i].Tool, slots[i].Unlock));
            }

            _wasUnlocked = new bool[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
                _wasUnlocked[i] = _slots[i].IsUnlocked(_unlocks);
        }

        // ── seleção ──────────────────────────────────────────────────────────

        public bool TrySelect(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;

            ToolSlot slot = _slots[index];
            if (!slot.IsUnlocked(_unlocks)) return false;

            // Slot vazio ou com ferramenta QUEBRADA continua selecionável de
            // propósito: é assim que o jogador vê o que está errado e chega à
            // opção de consertar. Recusar a seleção esconderia o problema atrás
            // de um clique que não faz nada.
            if (_selected == index) return true;

            _selected = index;
            SelectionChanged?.Invoke(slot);

            return true;
        }

        public void Deselect()
        {
            if (_selected < 0) return;

            _selected = -1;
            SelectionChanged?.Invoke(null);
        }

        // ── uso ──────────────────────────────────────────────────────────────

        public ToolUseResult Check(ToolSurface surface)
        {
            ToolSlot slot = Selected;

            if (slot == null || slot.IsEmpty) return ToolUseResult.NoTool;
            if (!slot.IsUnlocked(_unlocks)) return ToolUseResult.SlotLocked;
            if (slot.IsBroken) return ToolUseResult.Broken;
            if (!slot.Tool.WorksOn(surface)) return ToolUseResult.WrongSurface;

            return ToolUseResult.Ok;
        }

        public ToolUseResult TryUse(ToolSurface surface, out float power)
        {
            power = 0f;

            ToolUseResult result = Check(surface);
            if (result != ToolUseResult.Ok) return result;

            ToolSlot slot = Selected;
            power = slot.Tool.Power;

            // Gasta DEPOIS de decidir que o uso vale. Gastar antes faria errar a
            // superfície cobrar durabilidade, e o jogador quebraria a esponja
            // esfregando vidro.
            bool justBroke = slot.Wear();

            SlotsChanged?.Invoke();

            if (justBroke)
            {
                Broke?.Invoke(slot);
                _events?.Publish(new ToolBroke(slot.Index, slot.Tool.name,
                                               slot.Tool.RepairCost));
            }

            return ToolUseResult.Ok;
        }

        public bool TryRepair(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;

            ToolSlot slot = _slots[index];
            if (slot.IsEmpty || !slot.Tool.Wears) return false;
            if (slot.UsesLeft >= slot.Tool.MaxUses) return false;

            slot.Repair();
            SlotsChanged?.Invoke();

            return true;
        }

        // ── destravamento ────────────────────────────────────────────────────

        /// <summary>
        /// Só avisa quando alguma coisa REALMENTE mudou. Este método roda a cada
        /// virada de dia, subida de nível e objetivo concluído, e um
        /// <c>SlotsChanged</c> por chamada faria a roda se redesenhar por nada.
        /// </summary>
        public void Refresh()
        {
            bool changed = false;

            for (int i = 0; i < _slots.Count; i++)
            {
                bool unlocked = _slots[i].IsUnlocked(_unlocks);
                if (unlocked == _wasUnlocked[i]) continue;

                _wasUnlocked[i] = unlocked;
                changed = true;
            }

            // O slot selecionado pode ter TRAVADO de novo — é raro, mas acontece
            // com flag revogada ou save carregado por cima. Manter na mão uma
            // ferramenta de slot travado é um estado que a roda não consegue
            // mostrar.
            if (_selected >= 0 && !_slots[_selected].IsUnlocked(_unlocks))
            {
                _selected = -1;
                SelectionChanged?.Invoke(null);
            }

            if (changed) SlotsChanged?.Invoke();
        }

        // ── save ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Restaura o desgaste de um save. O LAYOUT vem sempre do asset — um
        /// save não pode ressuscitar um slot que o designer removeu, nem manter
        /// uma ferramenta que saiu do jogo.
        /// </summary>
        public void Restore(IReadOnlyList<int> usesPerSlot, int selectedIndex)
        {
            if (usesPerSlot != null)
                for (int i = 0; i < _slots.Count && i < usesPerSlot.Count; i++)
                    _slots[i].RestoreUses(usesPerSlot[i]);

            _selected = -1;
            if (selectedIndex >= 0 && selectedIndex < _slots.Count
                && _slots[selectedIndex].IsUnlocked(_unlocks))
            {
                _selected = selectedIndex;
            }

            for (int i = 0; i < _slots.Count; i++)
                _wasUnlocked[i] = _slots[i].IsUnlocked(_unlocks);

            SelectionChanged?.Invoke(Selected);
            SlotsChanged?.Invoke();
        }

        public int[] Snapshot()
        {
            var uses = new int[_slots.Count];
            for (int i = 0; i < _slots.Count; i++) uses[i] = _slots[i].UsesLeft;

            return uses;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _subscriptions.Count; i++) _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }
    }
}
