using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// O saco de lixo como objeto na cena: o jogador pega, carrega e larga na
    /// doca dos fundos.
    ///
    /// Ele carrega o <see cref="TrashBag"/> de verdade — o mesmo objeto que
    /// estava dentro da lixeira, não uma cópia. É o que garante que o valor que
    /// o caminhão paga é exatamente o do lixo que foi separado, sem nenhuma
    /// conversão no caminho que pudesse divergir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrashBagItem : MonoBehaviour
    {
        [Header("Depuração")]
        [Tooltip("Só leitura: o que tem dentro deste saco agora.")]
        [SerializeField] private string _contents = "(vazio)";

        public TrashBag Bag { get; private set; }

        /// <summary>Já foi entregue: não pode ser entregue de novo.</summary>
        public bool IsDelivered { get; private set; }

        public void Initialize(TrashBag bag)
        {
            Bag = bag;
            _contents = bag != null ? bag.ToString() : "(vazio)";
        }

        /// <summary>
        /// Marcado pela doca no instante em que o saco entra, ANTES de qualquer
        /// coisa poder acontecer. Dois gatilhos de doca sobrepostos disparam no
        /// mesmo frame, e sem esta trava o mesmo lixo seria pago duas vezes.
        /// </summary>
        public bool TryMarkDelivered()
        {
            if (IsDelivered || Bag == null || Bag.IsEmpty) return false;

            IsDelivered = true;
            return true;
        }

        /// <summary>
        /// Desfaz a marca quando a entrega não se concretizou — cena sem
        /// GameContext, doca que recusou. Sem isto o saco ficaria marcado como
        /// entregue sem ter sido, e o jogador carregaria para sempre um saco que
        /// nenhuma doca aceita mais.
        /// </summary>
        public void Undeliver() => IsDelivered = false;
    }
}
