using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Controla a navegação entre os apps do prefab, liga cada janela ao seu
    /// controlador e cria a aba Gestão como compatibilidade para prefabs antigos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComputerAppHost : MonoBehaviour
    {
        [Tooltip("Apps presentes na hierarquia do prefab. Se vazio, são encontrados nos filhos.")]
        [SerializeField] private List<ComputerAppView> _apps = new();

        private void Awake()
        {
            ComputerAppView[] discovered = GetComponentsInChildren<ComputerAppView>(true);
            for (int i = 0; i < discovered.Length; i++)
                if (!_apps.Contains(discovered[i])) _apps.Add(discovered[i]);

            EnsureManagementApp();

            for (int i = 0; i < _apps.Count; i++)
            {
                ComputerAppView app = _apps[i];
                if (app == null) continue;

                app.SetIconLayout(i);
                AttachController(app);
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

                app.CloseController();
                app.SetIconVisible(false);
                app.SetWindowVisible(app == selectedApp);
            }

            selectedApp.OpenController();
        }

        public void ShowDesktop()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                ComputerAppView app = _apps[i];
                if (app == null) continue;

                app.CloseController();
                app.SetWindowVisible(false);
                app.SetIconVisible(true);
            }
        }

        private void EnsureManagementApp()
        {
            for (int i = 0; i < _apps.Count; i++)
                if (_apps[i] != null && _apps[i].AppName == "Gestão") return;

            ComputerAppView template = null;
            for (int i = 0; i < _apps.Count; i++)
            {
                if (_apps[i] == null) continue;
                template = _apps[i];
                break;
            }

            if (template == null) return;

            GameObject copy = Instantiate(template.gameObject, template.transform.parent, false);
            ComputerAppView management = copy.GetComponent<ComputerAppView>();
            if (management == null)
            {
                Destroy(copy);
                return;
            }

            management.ConfigureDisplayName("Gestão", "G");
            _apps.Add(management);
        }

        private static void AttachController(ComputerAppView view)
        {
            RectTransform content = view.ContentRoot;
            if (content == null) return;

            // O prefab atual do Mercado já contém sua própria loja, com abas de
            // produtos, móveis e carrinho. Criar MarketApp no mesmo Content
            // desenha uma segunda interface por cima e bloqueia os cliques.
            if (view.AppName == "Mercado" && content.Find("Market Store") != null)
                return;

            ComputerApp controller = view.AppName switch
            {
                "Mercado" => GetOrAdd<MarketApp>(content.gameObject),
                "Preços" => GetOrAdd<PricingApp>(content.gameObject),
                "Objetivos" => GetOrAdd<ObjectivesApp>(content.gameObject),
                "Banco" => GetOrAdd<BankApp>(content.gameObject),
                "Relatório" => GetOrAdd<ReportApp>(content.gameObject),
                "Estatísticas" => GetOrAdd<ReportApp>(content.gameObject),
                "Gestão" => GetOrAdd<ManagementApp>(content.gameObject),
                _ => null,
            };

            view.AttachController(controller);
        }

        private static T GetOrAdd<T>(GameObject target) where T : ComputerApp =>
            target.GetComponent<T>() ?? target.AddComponent<T>();
    }
}
