using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Empurra o <see cref="IObjectiveService.Flush"/> uma vez por frame.
    ///
    /// Por que isto existe em vez de o serviço concluir na hora: o progresso é
    /// anotado DENTRO do despacho de um evento do barramento. Pagar a recompensa
    /// ali dentro publicaria um evento de dinheiro de dentro de um handler de
    /// dinheiro, e o EventBus tem guarda de recursão por tipo — ele LANÇA. Não é
    /// teoria: o objetivo "fature ¥20.000" ouve a venda, e a venda que o conclui
    /// é exatamente o despacho em que a recompensa entraria.
    ///
    /// Então o serviço anota durante o evento e paga depois, fora do despacho.
    /// Alguém precisa ser esse "depois", e num jogo Unity esse alguém é um frame.
    ///
    /// LateUpdate, e não Update: tudo o que gera progresso no frame já aconteceu,
    /// então a recompensa sai no mesmo frame da ação que a mereceu, e não no
    /// seguinte.
    ///
    /// Não precisa ser colocado na cena — o <see cref="GameContext"/> adiciona
    /// este componente a si mesmo. É infraestrutura do serviço, não uma peça que
    /// o designer monta.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]   // interno: não aparece no menu Add Component
    public sealed class ObjectiveRunner : MonoBehaviour
    {
        private IObjectiveService _objectives;

        internal void Bind(IObjectiveService objectives) => _objectives = objectives;

        private void LateUpdate() => _objectives?.Flush();
    }
}
