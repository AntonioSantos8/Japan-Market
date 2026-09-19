using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Um lixo no chão da loja. Carrega qual lixo ele é, e nada mais.
    ///
    /// Substitui o <c>TrashInstance</c> legado, que tinha um campo público
    /// escrito de fora pelo spawner. Aqui a definição é atribuída uma vez, na
    /// criação, e o componente recusa viver sem ela: um lixo sem definição é um
    /// objeto que a lixeira não sabe classificar e que o jogador carrega por
    /// toda a loja sem conseguir jogar fora.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrashItem : MonoBehaviour
    {
        [Tooltip("Preenchido pelo spawner. Só preencha à mão para lixo posto na cena.")]
        [SerializeField] private TrashDefinition _definition;

        public TrashDefinition Definition => _definition;

        /// <summary>Chamado pelo spawner logo depois do Instantiate.</summary>
        public void Initialize(TrashDefinition definition) => _definition = definition;

        private void Start()
        {
            // Start, e não Awake: o spawner instancia e chama Initialize no
            // mesmo frame, mas o Awake do prefab roda DENTRO do Instantiate,
            // antes de o spawner conseguir atribuir qualquer coisa.
            if (_definition != null) return;

            Debug.LogWarning(
                $"[TrashItem] '{name}' não tem TrashDefinition. Ele não pode ser " +
                "reciclado — preencha o campo no prefab, ou deixe o TrashSpawner criá-lo.",
                this);
        }
    }
}
