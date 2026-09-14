using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
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

        [Tooltip("O único catálogo de móveis. Mesma regra: populado pelo import.")]
        [SerializeField] private FurnitureCatalog _furnitureCatalog;

        [Header("Diagnóstico")]
        [Tooltip("Valida o catálogo ao entrar em Play e lista os problemas no console.")]
        [SerializeField] private bool _validateCatalogOnPlay = true;

        private ServiceContainer _container;
        private EventBus _events;
        private StoreProgress _progress;
        private FurnitureRegistry _furniture;
        private PricingService _pricing;

        public static GameContext Current { get; private set; }

        public IServiceContainer Services => _container;
        public IEventBus Events => _events;
        public StoreProgress Progress => _progress;
        public IFurnitureRegistry Furniture => _furniture;
        public IPricingService Pricing => _pricing;

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
            _furniture = new FurnitureRegistry();
            _pricing   = new PricingService();

            // A ponte com o ServiceLocator legado. Enquanto ela existir, os 87
            // scripts atuais continuam funcionando sem uma linha de mudança.
            ServiceContainer.SetCurrent(_container);

            RegisterCoreServices();
        }

        private void RegisterCoreServices()
        {
            _container.Register<IEventBus>(_events);
            _container.Register<IUnlockContext>(_progress);
            _container.Register<IFurnitureRegistry>(_furniture);

            // Até a Fase 6, o PricingService guarda o que o jogador definir e
            // cai no preço de mercado para o resto. Não é placeholder: é o
            // comportamento correto de uma loja que ainda não remarcou nada.
            _container.Register<IPricingService>(_pricing);

            if (_itemCatalog != null)
            {
                _itemCatalog.Rebuild();
                _container.Register<IItemCatalog>(_itemCatalog);
            }
            else WarnMissingCatalog("ItemCatalog", "produtos");

            if (_furnitureCatalog != null)
            {
                _furnitureCatalog.Rebuild();
                _container.Register<IFurnitureCatalog>(_furnitureCatalog);
            }
            else WarnMissingCatalog("FurnitureCatalog", "móveis");

            if (!_validateCatalogOnPlay) return;

            // A checagem de null tem que acontecer AQUI, com a referência ainda
            // tipada como objeto Unity. Dentro de Validate o parâmetro é uma
            // interface, e aí o operador == sobrecarregado sai de cena: um asset
            // deletado passaria pelo guard e lançaria MissingReferenceException.
            if (_itemCatalog != null) Validate(_itemCatalog);
            if (_furnitureCatalog != null) Validate(_furnitureCatalog);
        }

        private void WarnMissingCatalog(string assetType, string what) =>
            Debug.LogWarning($"[GameContext] Nenhum {assetType} atribuído — os sistemas " +
                             $"novos rodam sem catálogo de {what}. Crie um em " +
                             "Assets → Create → Japan Market e arraste aqui.", this);

        private void Validate(IValidatableCatalog catalog)
        {
            List<CatalogProblem> problems = catalog.Validate();
            if (problems.Count == 0)
            {
                Debug.Log($"[Catálogo] {catalog.CatalogName}: {catalog.EntryCount} " +
                          "entradas, nenhum problema.", this);
                return;
            }

            foreach (CatalogProblem problem in problems)
            {
                Object context = problem.Asset != null ? problem.Asset : (Object)this;
                Debug.LogWarning($"[Catálogo] {catalog.CatalogName} · " +
                                 $"{problem.AssetName}: {problem.Message}", context);
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
