using System.Collections.Generic;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Materializa no depósito as caixas que o jogador já pagou.
    ///
    /// Sem este componente o ciclo de compra não fecha: o dinheiro sai, o pedido
    /// entra na <see cref="DeliveryQueue"/>, e a fila só enche — não existia um
    /// único <c>TryTakeBox</c> no projeto. O jogador pagava e a mercadoria nunca
    /// chegava.
    ///
    /// Ele é só isso, de propósito: o Domain decide O QUE entregar e QUANDO, e
    /// este MonoBehaviour decide ONDE aparece. Trocar o depósito de lugar, ou
    /// empilhar as caixas de outro jeito, não toca em regra nenhuma.
    ///
    /// Coloque um na cena e aponte <c>_dropPoint</c> para a porta dos fundos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeliverySpawner : MonoBehaviour
    {
        [Tooltip("Onde as caixas aparecem. Se vazio, usa o transform deste objeto.")]
        [SerializeField] private Transform _dropPoint;

        [Tooltip("Área em que as caixas são espalhadas, em metros.")]
        [SerializeField] private Vector2 _scatter = new(1.2f, 1.2f);

        [Header("Posicionamento seguro")]
        [Min(1)]
        [Tooltip("Quantos pontos são testados antes de usar o ponto central.")]
        [SerializeField] private int _spawnSearchAttempts = 32;

        [Min(0.1f)]
        [Tooltip("Espaço horizontal reservado para cada caixa.")]
        [SerializeField] private float _boxClearanceRadius = 0.55f;

        [Min(0.1f)]
        [Tooltip("Altura livre exigida acima do chão.")]
        [SerializeField] private float _boxClearanceHeight = 1.1f;

        [Min(0.01f)]
        [Tooltip("Distância que deixa a caixa acima do chão antes da física acomodá-la.")]
        [SerializeField] private float _heightAboveGround = 0.12f;

        [Min(1f)]
        [Tooltip("Altura do início do raycast usado para encontrar o chão.")]
        [SerializeField] private float _groundProbeHeight = 5f;

        [Tooltip("Camadas consideradas chão ou obstáculo. Vazio significa todas.")]
        [SerializeField] private LayerMask _blockingLayers = ~0;

        [Min(0f)]
        [Tooltip("Segundos entre uma caixa e a seguinte.")]
        [SerializeField] private float _secondsBetweenBoxes = 0.25f;

        [Min(0)]
        [Tooltip("Quantas caixas podem estar no chão ao mesmo tempo. 0 = sem limite. " +
                 "O resto espera na fila até o jogador guardar as que já chegaram.")]
        [SerializeField] private int _maxBoxesOnFloor = 30;

        [Header("Depuração")]
        [SerializeField] private bool _logDeliveries;

        private IMarketOrderService _market;
        private float _cooldown;

        /// <summary>
        /// As caixas que ESTE spawner pôs no chão e ainda existem.
        ///
        /// A lista, e não um contador. Um contador só sobe: ele depende de
        /// alguém avisar que a caixa foi guardada, e enquanto esse alguém não
        /// existe (a prateleira é da fase seguinte) o teto vira um travamento
        /// definitivo — trinta caixas entregues e o resto do pedido pago some na
        /// fila para sempre. Aqui a caixa destruída sai da lista sozinha: quem
        /// consome não precisa saber que este componente existe.
        /// </summary>
        private readonly List<GameObject> _onFloor = new();

        private Transform Drop => _dropPoint != null ? _dropPoint : transform;

        private void Update()
        {
            if (_market == null) { TryResolve(); return; }

            // O computador pausa o tempo do jogo. Usar deltaTime fazia a
            // primeira caixa aparecer e congelava todas as seguintes no
            // cooldown enquanto o jogador continuava na tela de compras.
            if (_cooldown > 0f) { _cooldown -= Time.unscaledDeltaTime; return; }
            if (_maxBoxesOnFloor > 0 && CountOnFloor() >= _maxBoxesOnFloor) return;
            if (_market.DeliveryQueue.PendingBoxes == 0) return;

            // Uma por vez: trezentas caixas instanciadas no mesmo frame é um
            // engasgo visível, e o jogador não consegue guardar mais que uma de
            // cada vez mesmo.
            if (!_market.DeliveryQueue.TryTakeBox(out ItemDefinition product)) return;

            _cooldown = _secondsBetweenBoxes;
            Spawn(product);
        }

        /// <summary>
        /// Resolvido por tentativa a cada frame, e não uma vez no Start: o
        /// GameContext pode entrar depois numa cena aditiva, e um spawner que
        /// desistiu na primeira tentativa nunca mais entrega nada.
        /// </summary>
        private void TryResolve()
        {
            GameContext game = GameContext.Current;
            if (game == null) return;

            game.Services.TryResolve(out _market);
        }

        private void Spawn(ItemDefinition product)
        {
            if (product == null) return;

            if (product.BoxPrefab == null)
            {
                Debug.LogWarning($"[DeliverySpawner] '{product.name}' não tem Box Prefab. " +
                                 "A caixa foi paga e não pode ser materializada — preencha " +
                                 "o campo no asset do produto.", this);
                return;
            }

            Vector3 position = FindSafeSpawnPosition();

            var box = Object.Instantiate(product.BoxPrefab, position, Drop.rotation);
            foreach (var component in box.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IStockDeliveryReceiver receiver) receiver.InitializeDelivery(product);
            _onFloor.Add(box);

            if (_logDeliveries)
                Debug.Log($"[Entrega] {product.name} — {_market.DeliveryQueue.PendingBoxes} " +
                          "caixa(s) ainda na fila.", this);
        }

        private Vector3 FindSafeSpawnPosition()
        {
            int mask = _blockingLayers.value == 0 ? Physics.AllLayers : _blockingLayers.value;
            int attempts = Mathf.Max(1, _spawnSearchAttempts);

            // O primeiro teste é exatamente no Drop Point. Os seguintes formam
            // anéis progressivos: se o ponto estiver numa parede, a busca sai
            // dela em vez de continuar sorteando dentro dos mesmos 1,2 metros.
            for (int i = 0; i < attempts; i++)
            {
                Vector2 offset = i == 0 ? Vector2.zero : SpiralOffset(i);
                Vector3 horizontal = Drop.position + new Vector3(offset.x, 0f, offset.y);
                Vector3 rayOrigin = horizontal + Vector3.up * _groundProbeHeight;

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit ground,
                        _groundProbeHeight * 2f, mask, QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 clearanceCenter = ground.point +
                    Vector3.up * (_boxClearanceHeight * 0.5f + 0.03f);
                Vector3 halfExtents = new(
                    _boxClearanceRadius,
                    _boxClearanceHeight * 0.5f,
                    _boxClearanceRadius);

                if (Physics.CheckBox(clearanceCenter, halfExtents, Drop.rotation,
                        mask, QueryTriggerInteraction.Ignore))
                    continue;

                return ground.point + Vector3.up * _heightAboveGround;
            }

            Debug.LogWarning(
                "[DeliverySpawner] Nenhum espaço totalmente livre foi encontrado perto " +
                $"de '{Drop.name}'. Usando o centro do ponto de entrega.", this);

            return GroundedFallback(mask);
        }

        private Vector2 SpiralOffset(int index)
        {
            const float GoldenAngle = 2.39996323f;

            float baseRadius = Mathf.Max(_boxClearanceRadius * 2.2f,
                Mathf.Max(_scatter.x, _scatter.y) * 0.5f);
            float radius = baseRadius * Mathf.Sqrt(index);
            float angle = index * GoldenAngle;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private Vector3 GroundedFallback(int mask)
        {
            Vector3 origin = Drop.position + Vector3.up * _groundProbeHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground,
                    _groundProbeHeight * 2f, mask, QueryTriggerInteraction.Ignore))
                return ground.point + Vector3.up * _heightAboveGround;

            return Drop.position + Vector3.up * _heightAboveGround;
        }

        /// <summary>
        /// Quantas caixas deste spawner ainda existem, limpando de passagem as
        /// que já foram destruídas.
        ///
        /// A comparação com <c>null</c> é o operador da Unity: um GameObject
        /// destruído continua sendo uma referência C# viva e só a sobrecarga de
        /// <c>==</c> sabe disso. Nada de <c>?.</c> nem de <c>is null</c> aqui —
        /// os dois passam por cima dela e a lista nunca esvaziaria.
        /// </summary>
        private int CountOnFloor()
        {
            for (int i = _onFloor.Count - 1; i >= 0; i--)
                if (_onFloor[i] == null) _onFloor.RemoveAt(i);

            return _onFloor.Count;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f);
            Gizmos.DrawWireCube(Drop.position, new Vector3(_scatter.x, 0.1f, _scatter.y));

            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.7f);
            float searchRadius = Mathf.Max(_boxClearanceRadius * 2.2f,
                Mathf.Max(_scatter.x, _scatter.y) * 0.5f) *
                Mathf.Sqrt(Mathf.Max(1, _spawnSearchAttempts - 1));
            Gizmos.DrawWireSphere(Drop.position, searchRadius);
        }
    }
}
