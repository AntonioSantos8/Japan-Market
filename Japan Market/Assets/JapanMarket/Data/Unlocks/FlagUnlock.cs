using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// "Só depois que a flag X for levantada."
    ///
    /// É a outra ponta do <see cref="ObjectiveReward.Flag"/>, e o que torna uma
    /// cadeia de objetivos possível sem que nada conheça nada: o objetivo A
    /// levanta "primeira_venda" ao concluir, e o objetivo B tem um destes
    /// apontando para "primeira_venda". Nenhum dos dois assets referencia o
    /// outro, e o serviço de objetivos não sabe que existe cadeia.
    ///
    /// Serve igualmente para produto, móvel e faixa de empréstimo — qualquer
    /// coisa que já use <see cref="UnlockCondition"/> ganhou "liberado por
    /// objetivo" de graça.
    /// </summary>
    [CreateAssetMenu(fileName = "UnlockByFlag",
        menuName = "Japan Market/Unlock/Flag", order = 42)]
    public sealed class FlagUnlock : UnlockCondition
    {
        [Tooltip("Nome exato da flag. Combine com o campo Flag da recompensa do objetivo.")]
        [SerializeField] private string _flag;

        [Tooltip("Texto mostrado enquanto está travado. Vazio usa um texto genérico.")]
        [SerializeField] private string _description;

        public string Flag => _flag;

        /// <summary>
        /// Flag em branco NUNCA libera, em vez de sempre liberar.
        ///
        /// Os dois são defensáveis, e é por isso que a escolha precisa estar
        /// escrita: um asset preenchido pela metade travando conteúdo é um bug
        /// que alguém percebe e reporta; liberando conteúdo, é um bug que
        /// entrega o jogo inteiro no primeiro dia e ninguém reclama.
        /// </summary>
        public override bool IsSatisfied(IUnlockContext context) =>
            context != null && !string.IsNullOrWhiteSpace(_flag) && context.HasFlag(_flag);

        public override string Describe() =>
            string.IsNullOrWhiteSpace(_description)
                ? "Conclua o objetivo anterior"
                : _description;

#if UNITY_EDITOR
        public void EditorSetFlag(string flag) => _flag = flag;
#endif
    }
}
