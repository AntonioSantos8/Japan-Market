using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Os clientes sujam a loja.
    ///
    /// Reescrito do <c>TrashSystem</c> legado. O que mudou, e por quê:
    ///
    ///  • A lista de clientes vem do barramento (<c>CustomerEntered</c> /
    ///    <c>CustomerLeft</c>), não de <c>ServiceLocator.Get&lt;MarketManager&gt;()</c>
    ///    dentro de uma corrotina. O antigo estourava se o MarketManager ainda
    ///    não tivesse se registrado, e a corrotina morria junto — a loja nunca
    ///    mais sujava naquela sessão, sem nada no console.
    ///
    ///  • O catálogo sorteia o lixo, com peso. O antigo usava um array no
    ///    Inspector e <c>Random.Range</c> uniforme: um lixo criado e esquecido
    ///    fora daquela lista simplesmente não existia no jogo.
    ///
    ///  • A posição é validada contra a NavMesh, e não só contra o chão. Lixo
    ///    que cai atrás de uma prateleira é lixo que o jogador vê, não alcança,
    ///    e fica sujando a loja para sempre.
    ///
    /// Monte: um GameObject com este componente em qualquer lugar da cena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrashSpawner : MonoBehaviour
    {
        [Header("Catálogo")]
        [Tooltip("De onde sai o sorteio. Se vazio, resolve pelo GameContext.")]
        [SerializeField] private TrashCatalog _catalog;

        [Header("Ritmo")]
        [Min(1f)]
        [Tooltip("Segundos entre uma tentativa e a seguinte.")]
        [SerializeField] private float _secondsBetweenTries = 20f;

        [Range(0f, 1f)]
        [Tooltip("Chance de sujar a cada tentativa. Rolada UMA vez por tentativa, " +
                 "não uma por cliente — com a chance por cliente, loja cheia virava " +
                 "muito mais lixo do que o número configurado sugere.")]
        [SerializeField] private float _chancePerTry = 0.35f;

        [Min(0)]
        [Tooltip("Teto de lixo no chão ao mesmo tempo. 0 = sem limite.")]
        [SerializeField] private int _maxOnFloor = 12;

        [Header("Posição")]
        [Min(0.2f)]
        [Tooltip("Raio ao redor do cliente onde o lixo pode cair.")]
        [SerializeField] private float _radius = 1.5f;

        [Tooltip("Um cliente só suja uma vez por visita.")]
        [SerializeField] private bool _oncePerCustomer = true;

        [Header("Depuração")]
        [SerializeField] private bool _logSpawns;

        private readonly Dictionary<int, Transform> _customers = new();
        private readonly HashSet<int> _alreadyLittered = new();
        private readonly List<GameObject> _onFloor = new();
        private readonly List<Transform> _eligible = new();

        private readonly List<System.IDisposable> _subscriptions = new();

        private IEventBus _events;
        private float _timer;

        private void OnEnable() => _timer = _secondsBetweenTries;

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (!TryResolve()) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = _secondsBetweenTries;
            TrySpawn();
        }

        // ── barramento ───────────────────────────────────────────────────────

        private bool TryResolve()
        {
            if (_events != null) return true;

            GameContext game = GameContext.Current;
            if (game == null) return false;

            _events = game.Events;
            if (_events == null) return false;

            _subscriptions.Add(_events.Subscribe<CustomerEntered>(OnCustomerEntered));
            _subscriptions.Add(_events.Subscribe<CustomerLeft>(OnCustomerLeft));

            return true;
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < _subscriptions.Count; i++) _subscriptions[i]?.Dispose();

            _subscriptions.Clear();
            _customers.Clear();
            _alreadyLittered.Clear();
            _events = null;
        }

        private void OnCustomerEntered(CustomerEntered e)
        {
            if (e.Transform == null) return;

            _customers[e.CustomerId] = e.Transform;
        }

        private void OnCustomerLeft(CustomerLeft e)
        {
            _customers.Remove(e.CustomerId);
            _alreadyLittered.Remove(e.CustomerId);
        }

        // ── spawn ────────────────────────────────────────────────────────────

        private void TrySpawn()
        {
            if (_maxOnFloor > 0 && CountOnFloor() >= _maxOnFloor) return;

            Transform chosen = PickCustomer();
            if (chosen == null) return;

            // A chance é rolada DEPOIS de haver um candidato: rolar antes faz a
            // loja vazia "gastar" tentativas, e o ritmo efetivo fica diferente do
            // configurado sem ninguém entender por quê.
            if (Random.value > _chancePerTry) return;

            TrashDefinition trash = Catalog?.PickRandom();
            if (trash == null || trash.Prefab == null) return;

            if (!TryFindSpot(chosen.position, out Vector3 spot)) return;

            GameObject spawned = Instantiate(trash.Prefab, spot, Quaternion.Euler(
                0f, Random.Range(0f, 360f), 0f));

            if (spawned.TryGetComponent(out TrashItem item)) item.Initialize(trash);
            else
            {
                Debug.LogWarning(
                    $"[TrashSpawner] O prefab de '{trash.name}' não tem TrashItem. " +
                    "A lixeira não vai conseguir classificar este lixo.", this);
            }

            _onFloor.Add(spawned);

            if (_logSpawns) Debug.Log($"[Lixo] {trash} apareceu perto de {chosen.name}.", this);
        }

        private Transform PickCustomer()
        {
            _eligible.Clear();

            foreach (KeyValuePair<int, Transform> entry in _customers)
            {
                if (entry.Value == null) continue;
                if (_oncePerCustomer && _alreadyLittered.Contains(entry.Key)) continue;

                _eligible.Add(entry.Value);
            }

            if (_eligible.Count == 0) return null;

            Transform chosen = _eligible[Random.Range(0, _eligible.Count)];

            if (_oncePerCustomer)
            {
                foreach (KeyValuePair<int, Transform> entry in _customers)
                    if (entry.Value == chosen) { _alreadyLittered.Add(entry.Key); break; }
            }

            return chosen;
        }

        /// <summary>
        /// Um ponto alcançável a pé, perto do cliente.
        ///
        /// NavMesh, e não raycast no chão: o chão inclui o vão atrás da
        /// prateleira e o lado de dentro do balcão, e lixo lá é lixo que o
        /// jogador enxerga, não alcança, e que fica baixando a limpeza da loja
        /// para sempre.
        /// </summary>
        private bool TryFindSpot(Vector3 origin, out Vector3 result)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _radius;
                var candidate = new Vector3(origin.x + offset.x, origin.y, origin.z + offset.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = origin;
            return false;
        }

        /// <summary>
        /// Conta o que ainda existe, limpando de passagem o que foi destruído.
        /// A comparação com null é a da Unity de propósito — um GameObject
        /// destruído continua sendo referência C# viva, e `is null` ou `?.`
        /// passariam por cima da sobrecarga e a lista nunca esvaziaria.
        /// </summary>
        private int CountOnFloor()
        {
            for (int i = _onFloor.Count - 1; i >= 0; i--)
                if (_onFloor[i] == null) _onFloor.RemoveAt(i);

            return _onFloor.Count;
        }

        private ITrashCatalog Catalog
        {
            get
            {
                if (_catalog != null) return _catalog;

                GameContext game = GameContext.Current;
                return game != null && game.Services.TryResolve(out ITrashCatalog resolved)
                    ? resolved
                    : null;
            }
        }
    }
}
