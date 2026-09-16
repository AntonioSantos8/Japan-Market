using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um slot da roda em jogo: a ferramenta que está nele e o quanto ela aguenta.
    ///
    /// O desgaste mora AQUI e não no <see cref="ToolDefinition"/>. O asset é
    /// compartilhado e persiste dentro do projeto: uma esponja que perdesse
    /// durabilidade no asset voltaria gasta na sessão seguinte, e no build
    /// chegaria gasta de fábrica para todo mundo. Mesma divisão de
    /// <c>LoanDefinition</c>/<c>ActiveLoan</c> e de objetivo/objetivo ativo.
    /// </summary>
    public sealed class ToolSlot
    {
        private readonly UnlockCondition _unlock;

        public ToolSlot(int index, ToolDefinition tool, UnlockCondition unlock)
        {
            Index = index;
            Tool = tool;
            _unlock = unlock;

            UsesLeft = tool != null && tool.Wears ? tool.MaxUses : 0;
        }

        public int Index { get; }
        public ToolDefinition Tool { get; private set; }

        /// <summary>Usos restantes. Sempre 0 numa ferramenta que não desgasta.</summary>
        public int UsesLeft { get; private set; }

        public bool IsEmpty => Tool == null;

        /// <summary>
        /// Quebrada: só vale para ferramenta que desgasta. Uma ferramenta sem
        /// teto de usos tem <see cref="UsesLeft"/> 0 a vida inteira, e confundir
        /// os dois deixaria o tablet permanentemente quebrado.
        /// </summary>
        public bool IsBroken => Tool != null && Tool.Wears && UsesLeft <= 0;

        public bool IsUsable => Tool != null && !IsBroken;

        /// <summary>Fração de vida restante, para a barrinha na roda. 1 se não desgasta.</summary>
        public float Condition =>
            Tool == null || !Tool.Wears ? 1f
            : UsesLeft <= 0 ? 0f
            : UsesLeft / (float)Tool.MaxUses;

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);

        public string DescribeLock() => _unlock != null ? _unlock.Describe() : string.Empty;

        // ── escrita, só de dentro do cinto ───────────────────────────────────

        /// <summary>Gasta um uso. Devolve true se ESTE uso foi o que quebrou.</summary>
        internal bool Wear()
        {
            if (Tool == null || !Tool.Wears || UsesLeft <= 0) return false;

            UsesLeft--;
            return UsesLeft == 0;
        }

        internal void Repair()
        {
            if (Tool == null || !Tool.Wears) return;

            UsesLeft = Tool.MaxUses;
        }

        internal void SetTool(ToolDefinition tool)
        {
            Tool = tool;
            UsesLeft = tool != null && tool.Wears ? tool.MaxUses : 0;
        }

        internal void RestoreUses(int usesLeft)
        {
            if (Tool == null || !Tool.Wears) { UsesLeft = 0; return; }

            // Preso ao teto ATUAL do asset: um save de quando a esponja durava
            // 50 usos não pode devolver 50 numa esponja que agora dura 20.
            UsesLeft = usesLeft < 0 ? 0
                     : usesLeft > Tool.MaxUses ? Tool.MaxUses
                     : usesLeft;
        }

        public override string ToString() =>
            Tool == null
                ? $"Slot {Index}: vazio"
                : $"Slot {Index}: {Tool}{(Tool.Wears ? $" ({UsesLeft}/{Tool.MaxUses})" : "")}";
    }
}
