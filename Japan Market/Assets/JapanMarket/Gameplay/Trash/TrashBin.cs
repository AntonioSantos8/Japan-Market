using System;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// A lixeira: um móvel que recebe lixo num saco.
    ///
    /// O que mudou em relação ao <c>TrashBin</c> legado, e por quê:
    ///
    ///  • A categoria não é fixa no Inspector. O saco trava na categoria do
    ///    PRIMEIRO lixo — é isso que transforma "juntar lixo" em "separar lixo".
    ///    Com categoria fixa por lixeira, cada lixo tem exatamente um destino
    ///    possível e não existe decisão a tomar.
    ///
    ///  • Não há mais penalidade em dinheiro por errar. O custo de errar é o
    ///    saco ocupado e a viagem até os fundos — um custo que o jogador entende
    ///    olhando a lixeira, em vez de um débito silencioso no caixa.
    ///
    ///  • O dinheiro não entra aqui. A lixeira não conhece o livro-razão: ela
    ///    devolve o saco, o jogador leva até a doca, e o caminhão paga no
    ///    fechamento do dia. Uma lixeira que depositasse dinheiro seria uma
    ///    lixeira impossível de testar e de rebalancear.
    ///
    /// Monte: um prefab com <c>FurnitureInstance</c> + este componente + um
    /// Collider marcado como Is Trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrashBin : FurnitureCapabilityBehaviour, ITrashReceptacle
    {
        [Min(1)]
        [Tooltip("Quantos lixos cabem num saco antes de ele precisar ser trocado.")]
        [SerializeField] private int _bagCapacity = TrashBag.DefaultCapacity;

        [Header("Saco")]
        [Tooltip("O saco que o jogador tira daqui e leva até os fundos. " +
                 "Precisa ter um TrashBagItem.")]
        [SerializeField] private GameObject _bagPrefab;

        [Tooltip("Onde o saco aparece ao ser retirado. Vazio usa o transform da lixeira.")]
        [SerializeField] private Transform _bagSpawnPoint;

        [Header("Recusa")]
        [Tooltip("Impulso aplicado ao lixo recusado, para ele sair de dentro da lixeira.")]
        [SerializeField] private float _rejectForce = 3f;

        private TrashBag _bag;
        private IEventBus _events;

        public TrashBag Bag => _bag ??= new TrashBag(_bagCapacity);
        public bool IsFull => Bag.IsFull;

        public event Action<ITrashReceptacle> ContentsChanged;

        protected override void Awake()
        {
            base.Awake();
            _bag = new TrashBag(_bagCapacity);
        }

        public TrashSortResult TryDiscard(TrashDefinition trash)
        {
            TrashSortResult result = Bag.TryAdd(trash);
            if (result != TrashSortResult.Ok) return result;

            ContentsChanged?.Invoke(this);

            Events?.Publish(new TrashDiscarded(
                Bag.Category != null ? Bag.Category.Key : string.Empty,
                Bag.Count, Bag.Capacity));

            return result;
        }

        public bool TryTakeBag(out TrashBag bag)
        {
            bag = null;
            if (Bag.IsEmpty) return false;

            bag = _bag;
            _bag = new TrashBag(_bagCapacity);

            ContentsChanged?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Tira o saco e materializa na cena. É o que a interação do jogador
        /// chama — o único caminho pelo qual o lixo sai daqui.
        ///
        /// A lixeira materializa o saco, e não um sistema de fora, porque ela é
        /// quem sabe de onde ele sai. Mas ela para aí: onde o saco é entregue e
        /// quanto ele vale são problema da doca e do serviço.
        /// </summary>
        public bool TryReleaseBag(out GameObject spawned)
        {
            spawned = null;

            if (_bagPrefab == null)
            {
                Debug.LogWarning(
                    $"[TrashBin] '{name}' não tem Bag Prefab. O jogador não consegue " +
                    "tirar o saco, e a lixeira cheia trava para sempre.", this);
                return false;
            }

            if (!TryTakeBag(out TrashBag bag)) return false;

            Transform origin = _bagSpawnPoint != null ? _bagSpawnPoint : transform;
            spawned = Instantiate(_bagPrefab, origin.position, origin.rotation);

            if (spawned.TryGetComponent(out TrashBagItem item))
            {
                item.Initialize(bag);
                return true;
            }

            // Sem o componente o saco viraria um objeto decorativo e o conteúdo
            // sumiria. Melhor desfazer: o lixo volta para a lixeira e o jogador
            // percebe que alguma coisa está errada com o prefab.
            Debug.LogError(
                $"[TrashBin] O Bag Prefab de '{name}' não tem TrashBagItem. O saco " +
                "seria perdido — a retirada foi cancelada.", this);

            Destroy(spawned);
            spawned = null;
            _bag = bag;

            ContentsChanged?.Invoke(this);
            return false;
        }

        /// <summary>
        /// Restaura o saco de um save. O <c>_bagCapacity</c> do prefab vence a
        /// capacidade salva: se o balanceamento mudou entre versões, é a nova
        /// regra que vale, e o excedente é descartado por <c>TrashBag.Restore</c>.
        /// </summary>
        public void RestoreBag(TrashCategory category, TrashDefinition[] items)
        {
            _bag = new TrashBag(_bagCapacity);
            _bag.Restore(category, items);

            ContentsChanged?.Invoke(this);
        }

        // ── cena ─────────────────────────────────────────────────────────────

        private IEventBus Events
        {
            get
            {
                if (_events != null) return _events;

                GameContext game = GameContext.Current;
                return _events = game != null ? game.Events : null;
            }
        }

        /// <summary>
        /// O jogador soltou alguma coisa dentro da lixeira.
        ///
        /// O que NÃO é lixo é simplesmente ignorado, sem penalidade e sem
        /// destruir nada — o legado apagava a caixa de produtos que encostasse
        /// aqui e cobrava ¥100. Uma caixa de mercadoria paga que some por
        /// encostar na lixeira é perda de progresso por acidente de colisão.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out TrashItem item)) return;
            if (item.Definition == null) return;

            TrashSortResult result = TryDiscard(item.Definition);

            if (result == TrashSortResult.Ok)
            {
                Destroy(item.gameObject);
                return;
            }

            Reject(other, result);
        }

        /// <summary>
        /// Empurra o lixo recusado para fora, para ele não ficar preso dentro do
        /// gatilho disparando OnTriggerEnter a cada reentrada.
        /// </summary>
        private void Reject(Collider other, TrashSortResult reason)
        {
            if (other.attachedRigidbody != null && _rejectForce > 0f)
            {
                Vector3 away = (other.transform.position - transform.position);
                away.y = 0f;

                if (away.sqrMagnitude < 0.01f) away = transform.forward;

                other.attachedRigidbody.AddForce(
                    away.normalized * _rejectForce + Vector3.up * (_rejectForce * 0.5f),
                    ForceMode.Impulse);
            }

            RejectionReason = reason;
            Rejected?.Invoke(this, reason);
        }

        /// <summary>Último motivo de recusa. A UI lê para mostrar o aviso certo.</summary>
        public TrashSortResult RejectionReason { get; private set; }

        /// <summary>Um lixo foi recusado. Quem mostra o aviso na tela assina isto.</summary>
        public event Action<TrashBin, TrashSortResult> Rejected;
    }
}
