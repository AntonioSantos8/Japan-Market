using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// A doca dos fundos: onde os sacos ficam esperando o caminhão.
    ///
    /// O caminhão não existe como objeto — ele é o fechamento do dia. O
    /// <c>TrashService</c> é uma fonte de receita registrada no
    /// <c>DayCycle</c>, então o que está aqui vira dinheiro no relatório do dia
    /// em que foi entregue.
    ///
    /// Monte: um GameObject com este componente e um Collider marcado como Is
    /// Trigger, na porta dos fundos. Nada mais.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrashDock : MonoBehaviour
    {
        [Tooltip("Destrói o saco ao entregar. Desligue se você tiver uma pilha " +
                 "de sacos visível esperando o caminhão.")]
        [SerializeField] private bool _destroyOnDeliver = true;

        [Header("Depuração")]
        [SerializeField] private bool _logDeliveries;

        private ITrashService _trash;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out TrashBagItem item)) return;

            // Marca ANTES de qualquer outra coisa. Dois gatilhos sobrepostos
            // disparam no mesmo frame para o mesmo saco, e o serviço só recusa a
            // repetição se o objeto for o mesmo — o que é verdade hoje e é
            // exatamente o tipo de coisa que deixa de ser verdade em silêncio.
            if (!item.TryMarkDelivered()) return;

            if (!TryResolve())
            {
                Debug.LogWarning(
                    "[TrashDock] Nenhum serviço de lixo disponível — a cena não tem " +
                    "GameContext. O saco foi devolvido.", this);

                // Devolvido, e não engolido: perder o trabalho do jogador por
                // falta de configuração de cena é o pior desfecho possível.
                item.Undeliver();
                return;
            }

            if (!_trash.TryDeposit(item.Bag))
            {
                item.Undeliver();
                return;
            }

            if (_logDeliveries)
                Debug.Log($"[Reciclagem] {item.Bag} entregue. Doca: " +
                          $"{_trash.PendingBags} saco(s), {_trash.PendingValue}.", this);

            if (_destroyOnDeliver) Destroy(item.gameObject);
        }

        /// <summary>
        /// Resolvido por tentativa, e não uma vez no Start: o GameContext pode
        /// entrar depois numa cena aditiva, e uma doca que desistiu na primeira
        /// tentativa nunca mais aceitaria um saco.
        /// </summary>
        private bool TryResolve()
        {
            if (_trash != null) return true;

            GameContext game = GameContext.Current;
            if (game == null) return false;

            return game.Services.TryResolve(out _trash);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 1.5f);
        }
    }
}
