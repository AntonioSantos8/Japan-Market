using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    public abstract class FurnitureCapabilityBehaviour : MonoBehaviour, IFurnitureCapability
    {
        private FurnitureInstance _owner;

        public IFurniture Owner => _owner;

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
