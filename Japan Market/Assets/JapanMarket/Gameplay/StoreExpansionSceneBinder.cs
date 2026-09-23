using System;
using System.Collections.Generic;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Liga uma expansão comprada aos objetos concretos da cena. Cada entrada é
    /// independente: ao comprar, os objetos antigos (por exemplo a parede que
    /// fecha a loja) são desativados e os objetos da área expandida são ativados.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreExpansionSceneBinder : MonoBehaviour
    {
        [Serializable]
        public sealed class VisualSwap
        {
            [Min(2)] public int section = 2;
            [Tooltip("Nome apenas para facilitar a identificação no Inspector.")]
            public string label = "Expansão";

            [Tooltip("Objetos visíveis antes da compra e desativados depois dela.")]
            public GameObject[] disableWhenPurchased = Array.Empty<GameObject>();

            [Tooltip("Objetos ocultos antes da compra e ativados depois dela.")]
            public GameObject[] enableWhenPurchased = Array.Empty<GameObject>();
        }

        [SerializeField] private VisualSwap[] expansions = Array.Empty<VisualSwap>();

        private IStoreExpansionService _service;

        private void OnEnable()
        {
            TryBind();
        }

        private void Start()
        {
            // GameContext carrega o save no Start com ordem -10000. Este Start
            // acontece depois e já enxerga todas as compras restauradas.
            TryBind();
            ApplyCurrentState();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void TryBind()
        {
            IStoreExpansionService next = GameContext.Current?.Expansions;
            if (ReferenceEquals(next, _service)) return;

            Unbind();
            _service = next;

            if (_service != null)
                _service.SectionPurchased += OnSectionPurchased;
        }

        private void Unbind()
        {
            if (_service != null)
                _service.SectionPurchased -= OnSectionPurchased;
            _service = null;
        }

        private void OnSectionPurchased(int section)
        {
            VisualSwap swap = Find(section);
            if (swap == null)
            {
                Debug.LogWarning(
                    $"[Expansão] A seção {section} foi comprada, mas não possui troca " +
                    "de objetos configurada nesta cena.", this);
                return;
            }

            Apply(swap, purchased: true);
        }

        /// <summary>
        /// Reaplica todas as paredes conforme o estado atual. Também pode ser
        /// chamado depois de um carregamento manual de save.
        /// </summary>
        [ContextMenu("Expansões/Atualizar objetos pela partida")]
        public void ApplyCurrentState()
        {
            TryBind();
            if (_service == null) return;

            for (int i = 0; i < expansions.Length; i++)
            {
                VisualSwap swap = expansions[i];
                if (swap == null) continue;
                Apply(swap, _service.IsOwned(swap.section));
            }
        }

        private VisualSwap Find(int section)
        {
            for (int i = 0; i < expansions.Length; i++)
                if (expansions[i] != null && expansions[i].section == section)
                    return expansions[i];
            return null;
        }

        private static void Apply(VisualSwap swap, bool purchased)
        {
            SetActive(swap.disableWhenPurchased, !purchased);
            SetActive(swap.enableWhenPurchased, purchased);
        }

        private static void SetActive(IReadOnlyList<GameObject> objects, bool active)
        {
            if (objects == null) return;

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject target = objects[i];
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < expansions.Length; i++)
            {
                VisualSwap swap = expansions[i];
                if (swap == null) continue;

                if (swap.section < 2)
                    Debug.LogWarning("[Expansão] O número da seção precisa ser 2 ou maior.", this);
                else if (!seen.Add(swap.section))
                    Debug.LogWarning(
                        $"[Expansão] A seção {swap.section} aparece mais de uma vez.", this);
            }
        }
#endif
    }
}
