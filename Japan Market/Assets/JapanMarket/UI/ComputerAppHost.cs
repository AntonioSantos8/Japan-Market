using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Controla a navegação entre os apps que já existem no prefab.
    /// Este componente não cria, posiciona ou estiliza nenhum elemento de UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComputerAppHost : MonoBehaviour
    {
        [Tooltip("Apps presentes na hierarquia do prefab. Se vazio, são encontrados nos filhos.")]
        [SerializeField] private List<ComputerAppView> _apps = new();

        private void Awake()
        {
            if (_apps.Count == 0)
                _apps.AddRange(GetComponentsInChildren<ComputerAppView>(true));

            for (int i = 0; i < _apps.Count; i++)
            {
                ComputerAppView app = _apps[i];
                if (app != null)
                    app.Bind(Open, ShowDesktop);
            }

            ShowDesktop();
        }

        private void OnEnable() => ShowDesktop();

        private void OnDestroy()
        {
            for (int i = 0; i < _apps.Count; i++)
                _apps[i]?.Unbind();
        }

        public void Open(ComputerAppView selectedApp)
        {
            if (selectedApp == null) return;

            for (int i = 0; i < _apps.Count; i++)
            {
                ComputerAppView app = _apps[i];
                if (app == null) continue;

                app.SetIconVisible(false);
                app.SetWindowVisible(app == selectedApp);
            }
        }

        public void ShowDesktop()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                ComputerAppView app = _apps[i];
                if (app == null) continue;

                app.SetWindowVisible(false);
                app.SetIconVisible(true);
            }
        }
    }
}
