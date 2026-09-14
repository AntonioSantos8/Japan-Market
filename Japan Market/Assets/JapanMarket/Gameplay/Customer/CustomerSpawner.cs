using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Traz clientes para a loja enquanto ela está aberta.
    ///
    /// Sobre o que mudou em relação ao NpcManager: o spawn não é mais uma
    /// corrotina infinita com <c>while(true)</c> que consulta o MarketManager a
    /// cada volta. Ele assina o evento de abrir/fechar e liga ou desliga um
    /// temporizador — nada roda enquanto a loja está fechada.
    ///
    /// O ponto de saída também deixa de ser descoberto por tag dentro do NPC
    /// (<c>GameObject.FindGameObjectWithTag("Exit")</c>, que lança se o objeto
    /// não existir) e passa a ser entregue no spawn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerSpawner : MonoBehaviour
    {
        [Header("Prefabs e perfis")]
        [SerializeField] private CustomerAgent[] _customerPrefabs;

        [Tooltip("Sorteado por cliente. Vazio = usa o perfil do próprio prefab.")]
        [SerializeField] private CustomerProfileData[] _profiles;

        [Header("Pontos")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _entryPoint;
        [SerializeField] private Transform _exitPoint;

        [Tooltip("Raio de dispersão no spawn, para não nascerem sobrepostos.")]
        [SerializeField] private float _spawnRadius = 0.6f;

        [Header("Ritmo")]
        [SerializeField] private Vector2 _intervalSeconds = new(8f, 25f);

        [Tooltip("Máximo de clientes vivos ao mesmo tempo. 0 = sem limite.")]
        [Min(0)] [SerializeField] private int _maxAlive = 6;

        [Header("Início")]
        [Tooltip("Começa a spawnar sem esperar a loja abrir. Use na Sandbox.")]
        [SerializeField] private bool _autoStart;

        private readonly List<CustomerAgent> _alive = new();
        private System.IDisposable _openSubscription;
        private float _nextSpawnAt;
        private bool _running;

        // ── ciclo de vida ────────────────────────────────────────────────────

        private void OnEnable()
        {
            TrySubscribe();
            if (_autoStart) StartSpawning();
        }

        /// <summary>
        /// Assina o evento de abrir/fechar, se ainda não assinou.
        ///
        /// É chamado no OnEnable e de novo a cada Update porque o GameContext
        /// pode não existir ainda: cena carregada de forma aditiva, spawner
        /// instanciado antes do contexto, ou um contexto recriado depois de um
        /// Teardown. Tentar uma vez só deixaria o spawner mudo para sempre,
        /// sem nenhum erro no console.
        ///
        /// Guardar o IDisposable e descartá-lo no OnDisable é a regra que
        /// elimina a categoria "esqueci de remover o listener".
        /// </summary>
        private void TrySubscribe()
        {
            if (_openSubscription != null) return;

            GameContext game = GameContext.Current;
            if (game == null) return;

            _openSubscription = game.Events.Subscribe<StoreOpenStateChanged>(OnStoreOpenChanged);
        }

        private void OnDisable()
        {
            _openSubscription?.Dispose();
            _openSubscription = null;
            StopSpawning();
        }

        private void OnStoreOpenChanged(StoreOpenStateChanged evt)
        {
            if (evt.IsOpen) StartSpawning();
            else
            {
                StopSpawning();
                NotifyAll(Domain.CustomerSignal.StoreClosed);
            }
        }

        public void StartSpawning()
        {
            _running = true;
            ScheduleNext();
        }

        public void StopSpawning() => _running = false;

        // ── spawn ────────────────────────────────────────────────────────────

        private void Update()
        {
            TrySubscribe();

            if (!_running) return;

            PruneDead();

            if (Time.time < _nextSpawnAt) return;
            if (_maxAlive > 0 && _alive.Count >= _maxAlive) { ScheduleNext(); return; }

            Spawn();
            ScheduleNext();
        }

        private void ScheduleNext() =>
            _nextSpawnAt = Time.time + Random.Range(_intervalSeconds.x, _intervalSeconds.y);

        private void Spawn()
        {
            if (!ValidateSetup()) { StopSpawning(); return; }

            CustomerAgent prefab = _customerPrefabs[Random.Range(0, _customerPrefabs.Length)];
            if (prefab == null) return;   // slot vazio no Inspector

            if (!TryFindSpawnPosition(out Vector3 position)) return;

            CustomerAgent customer = Instantiate(prefab, position, _spawnPoint.rotation);

            CustomerProfileData profile = _profiles != null && _profiles.Length > 0
                ? _profiles[Random.Range(0, _profiles.Length)]
                : null;

            customer.Initialize(_entryPoint.position, _exitPoint.position, profile);
            _alive.Add(customer);
        }

        /// <summary>
        /// Espalha o ponto de nascimento e o projeta na NavMesh. Nascer fora da
        /// malha deixa o agente inerte, e o cliente ficaria parado na porta sem
        /// nenhum erro visível.
        /// </summary>
        private bool TryFindSpawnPosition(out Vector3 position)
        {
            Vector2 offset = Random.insideUnitCircle * _spawnRadius;
            Vector3 candidate = _spawnPoint.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            Debug.LogWarning("[CustomerSpawner] Ponto de spawn fora da NavMesh. " +
                             "Verifique se a malha foi assada.", this);
            position = candidate;
            return false;
        }

        private bool ValidateSetup()
        {
            if (_customerPrefabs == null || _customerPrefabs.Length == 0)
            {
                Debug.LogError("[CustomerSpawner] Nenhum prefab de cliente atribuído.", this);
                return false;
            }

            if (_spawnPoint == null || _entryPoint == null || _exitPoint == null)
            {
                Debug.LogError("[CustomerSpawner] Faltam pontos de spawn, entrada ou saída.", this);
                return false;
            }

            return true;
        }

        private void PruneDead() => _alive.RemoveAll(c => c == null);

        private void NotifyAll(Domain.CustomerSignal signal)
        {
            PruneDead();
            for (int i = 0; i < _alive.Count; i++) _alive[i].Notify(signal);
        }

        /// <summary>Atalho para testar na Sandbox sem esperar o temporizador.</summary>
        [ContextMenu("Spawnar um cliente agora")]
        public void SpawnOneNow()
        {
            if (!Application.isPlaying) return;
            PruneDead();
            Spawn();
        }
    }
}
