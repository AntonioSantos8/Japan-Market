using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Composition root. Constrói e registra os serviços do jogo numa ordem
    /// explícita, e derruba tudo quando a cena morre.
    ///
    /// Substitui o problema de ordem de inicialização do ServiceLocator atual,
    /// onde treze classes se registram no Awake e quatro no Start, e alguém que
    /// consome no Start pode perder a corrida. O caso concreto é o StoreSign:
    /// ele busca MarketManager e NpcManager no Start(), e os dois se registram
    /// no Start() também. Perdendo o sorteio, os dois campos ficam null para
    /// sempre e a primeira abertura da loja lança NullReferenceException.
    ///
    /// [DefaultExecutionOrder(-10000)] garante que este Awake roda antes de
    /// qualquer outro — inclusive antes dos MonoBehaviours legados.
    ///
    /// Coloque UM destes na cena, num GameObject vazio chamado "— Game Context —".
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameContext : MonoBehaviour
    {
        [Header("Catálogos")]
        [Tooltip("O único catálogo de produtos do projeto. Populado automaticamente " +
                 "pelo import — não arraste produtos à mão.")]
        [SerializeField] private ItemCatalog _itemCatalog;

        [Header("Diagnóstico")]
        [Tooltip("Valida o catálogo ao entrar em Play e lista os problemas no console.")]
        [SerializeField] private bool _validateCatalogOnPlay = true;

        private ServiceContainer _container;
        private EventBus _events;
        private StoreProgress _progress;

        public static GameContext Current { get; private set; }

        public IServiceContainer Services => _container;
        public IEventBus Events => _events;
        public StoreProgress Progress => _progress;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[GameContext] Já existe um GameContext nesta cena. " +
                               "Este objeto será destruído.", this);
                Destroy(gameObject);
                return;
            }

            Current = this;

            _container = new ServiceContainer();
            _events    = new EventBus();
            _progress  = new StoreProgress();

            // A ponte com o ServiceLocator legado. Enquanto ela existir, os 87
            // scripts atuais continuam funcionando sem uma linha de mudança.
            ServiceContainer.SetCurrent(_container);

            RegisterCoreServices();
        }

        private void RegisterCoreServices()
        {
            _container.Register<IEventBus>(_events);
            _container.Register<IUnlockContext>(_progress);

            if (_itemCatalog != null)
            {
                _itemCatalog.Rebuild();
                _container.Register<IItemCatalog>(_itemCatalog);

                if (_validateCatalogOnPlay) ReportCatalogProblems();
            }
            else
            {
                Debug.LogWarning("[GameContext] Nenhum ItemCatalog atribuído. " +
                                 "Os sistemas novos vão rodar sem catálogo até a Fase 2 " +
                                 "estar ligada na cena.", this);
            }
        }

        private void ReportCatalogProblems()
        {
            var problems = _itemCatalog.Validate();
            if (problems.Count == 0)
            {
                Debug.Log($"[GameContext] Catálogo OK — {_itemCatalog.All.Count} produtos.", this);
                return;
            }

            foreach (ItemCatalog.Problem problem in problems)
            {
                Object context = problem.Item != null ? problem.Item : (Object)this;
                string where = problem.Item != null ? problem.Item.name : "catálogo";
                Debug.LogWarning($"[Catálogo] {where}: {problem.Message}", context);
            }
        }

        /// <summary>
        /// Cuidado com a ordem aqui: [DefaultExecutionOrder(-10000)] adianta o
        /// Awake — e adianta o OnDestroy junto. Ou seja, este OnDestroy roda
        /// ANTES do de todos os MonoBehaviours legados.
        ///
        /// Vários deles usam serviços no próprio OnDestroy (o NpcInstance chama
        /// MarketManager.UnregisterClient, por exemplo). Se limpássemos o
        /// container aqui, cada um desses receberia null ao sair do Play Mode e
        /// lançaria NullReferenceException — um comportamento que hoje NÃO existe,
        /// porque o dicionário estático do ServiceLocator antigo sobrevive à
        /// destruição da cena.
        ///
        /// Então a derrubada é deliberadamente passiva: o próximo GameContext
        /// substitui o container e o bus no Awake dele, e os antigos viram lixo
        /// coletável. Nada vaza entre cenas, e nada quebra no caminho.
        ///
        /// Para derrubar de fato (teste de integração, troca de perfil), chame
        /// <see cref="Teardown"/> explicitamente.
        /// </summary>
        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// Descarta bus e container agora. Só chame quando tiver certeza de que
        /// nenhum MonoBehaviour ainda vai consultar serviços — na prática, em
        /// testes de integração e ao trocar de save.
        /// </summary>
        public void Teardown()
        {
            _events?.Clear();
            _container?.Clear();
            if (_container != null) ServiceContainer.ClearCurrent(_container);
            if (Current == this) Current = null;
        }
    }
}
