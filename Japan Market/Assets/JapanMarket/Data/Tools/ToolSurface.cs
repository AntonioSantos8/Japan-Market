using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Uma superfície em que uma ferramenta trabalha: chão, vidro, balcão,
    /// prateleira.
    ///
    /// É o mesmo truque do <see cref="StorageTrait"/>, e resolve o mesmo
    /// problema. A alternativa é a ferramenta dizer "eu limpo sujeira do tipo
    /// Poeira" — e aí criar um tipo de sujeira novo obriga a revisitar toda
    /// ferramenta, e criar uma ferramenta nova obriga a revisitar toda sujeira.
    ///
    /// Com a superfície no meio: a sujeira diz em que superfície ela está, a
    /// ferramenta diz em que superfícies ela trabalha, e ninguém pergunta "que
    /// ferramenta é essa?". Um rodo novo é um asset; a esponja não é tocada.
    /// </summary>
    [CreateAssetMenu(fileName = "ToolSurface",
        menuName = "Japan Market/Tool/Surface", order = 80)]
    public sealed class ToolSurface : ScriptableObject
    {
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;

        public override string ToString() => _displayName.IsEmpty ? name : _displayName.Value;
    }
}
