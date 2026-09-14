using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Base fina para todo componente de capacidade.
    ///
    /// Sim, isto é herança num refatoramento que prega composição — e é
    /// deliberado. A composição está em QUAIS capacidades um prefab tem; esta
    /// classe não carrega comportamento nenhum, só a ligação "eu pertenço a
    /// este móvel", que seria copiada em cada capacidade se não existisse.
    /// Herança para encanamento transversal, composição para comportamento.
    /// </summary>
    // NÃO coloque [DisallowMultipleComponent] aqui. O atributo é herdado e o
    // Unity o resolve contra a classe base que o carrega — com ele na base, um
    // móvel com ProductStorage não aceitaria mais nem CustomerSlots nem
    // PowerConsumer, e o freezer (armazenamento + energia) seria impossível de
    // montar. Cada capacidade concreta declara o seu.
    public abstract class FurnitureCapabilityBehaviour : MonoBehaviour, IFurnitureCapability
    {
        private FurnitureInstance _owner;

        public IFurniture Owner => _owner;

        /// <summary>Acesso tipado para as subclasses, sem cast.</summary>
        protected FurnitureInstance Furniture => _owner;

        protected virtual void Awake()
        {
            _owner = GetComponentInParent<FurnitureInstance>(true);

            if (_owner == null)
            {
                Debug.LogError(
                    $"[{GetType().Name}] Nenhum FurnitureInstance neste objeto nem nos pais. " +
                    "Toda capacidade precisa pertencer a um móvel — sem isso ela nunca " +
                    "entra no registro e nenhum sistema consegue encontrá-la.", this);
                enabled = false;
            }
        }
    }
}
