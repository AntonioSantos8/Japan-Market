using System;
using System.Collections.Generic;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>Por que a ferramenta não agiu. A mira do jogador mostra qual.</summary>
    public enum ToolUseResult
    {
        Ok = 0,

        /// <summary>Nenhuma ferramenta selecionada.</summary>
        NoTool = 1,

        /// <summary>O slot ainda não abriu.</summary>
        SlotLocked = 2,

        /// <summary>A ferramenta não trabalha nessa superfície.</summary>
        WrongSurface = 3,

        /// <summary>Acabou o uso: precisa consertar.</summary>
        Broken = 4,
    }

    /// <summary>
    /// A roda de ferramentas: o que o jogador tem na mão e o que isso alcança.
    ///
    /// O que este serviço deliberadamente NÃO faz: saber o nome de uma
    /// ferramenta. Não existe "se for a esponja, limpe o balcão" — a ferramenta
    /// declara em que superfícies trabalha, a sujeira declara em que superfície
    /// está, e o cinto só cruza os dois. Uma ferramenta nova é um asset.
    ///
    /// Também não sabe desenhar roda nenhuma: ele expõe os slots e avisa quando
    /// algo muda. A roda na tela é casca por cima disto, e o cinto continua
    /// testável sem abrir cena.
    /// </summary>
    public interface IToolBelt
    {
        IReadOnlyList<ToolSlot> Slots { get; }

        /// <summary>Slot selecionado, ou null com a mão vazia.</summary>
        ToolSlot Selected { get; }

        int SelectedIndex { get; }

        /// <summary>
        /// Seleciona um slot. Falha em slot travado ou índice inexistente —
        /// desselecionar é <see cref="Deselect"/>, que é uma intenção diferente.
        /// </summary>
        bool TrySelect(int index);

        void Deselect();

        /// <summary>
        /// Pode agir nesta superfície agora? A mira chama isto por frame para
        /// decidir o que desenhar, então não aloca e não muda nada.
        /// </summary>
        ToolUseResult Check(ToolSurface surface);

        /// <summary>
        /// Usa a ferramenta selecionada. Só GASTA se o resultado for
        /// <see cref="ToolUseResult.Ok"/> — errar a superfície não pode consumir
        /// durabilidade, senão o jogador quebra a esponja limpando vidro.
        /// </summary>
        ToolUseResult TryUse(ToolSurface surface, out float power);

        /// <summary>Conserta o slot. Quem cobra o preço é quem chama.</summary>
        bool TryRepair(int index);

        /// <summary>
        /// Reavalia os destravamentos. Chamado quando o nível ou uma flag muda —
        /// um slot que abre no nível 5 precisa abrir na hora em que o jogador
        /// chega no 5, e não na próxima vez que ele abrir a roda.
        /// </summary>
        void Refresh();

        /// <summary>A ferramenta na mão mudou. Quem troca o modelo assina isto.</summary>
        event Action<ToolSlot> SelectionChanged;

        /// <summary>Uma ferramenta acabou de quebrar neste uso.</summary>
        event Action<ToolSlot> Broke;

        /// <summary>Um slot destravou, ou o desgaste mudou. A roda redesenha.</summary>
        event Action SlotsChanged;
    }
}
