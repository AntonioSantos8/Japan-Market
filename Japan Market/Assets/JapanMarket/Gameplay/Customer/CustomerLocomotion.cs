using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// O ÚNICO componente autorizado a escrever no transform do cliente.
    ///
    /// Essa frase é a correção inteira do jitter. Hoje três donos disputam a
    /// mesma posição: o NavMeshAgent (que continua ativo e aplicando desvio de
    /// obstáculo mesmo com isStopped = true, porque isStopped desliga o
    /// seguimento de caminho e não o avoidance), o DOMoveY do bob de fila e o
    /// DORotateQuaternion do fidget. Os três escrevem por frame, e o NPC treme
    /// parado.
    ///
    /// Aqui ninguém mais toca posição nem rotação. A animação lê a INTENÇÃO
    /// (<see cref="DesiredSpeed"/>) em vez de medir a velocidade real do agente,
    /// que é ruidosa por natureza. E o "fidget" de fila sai do transform e vira
    /// variação de idle no Animator, onde não disputa nada.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class CustomerLocomotion : MonoBehaviour
    {
        [Tooltip("Folga somada ao stoppingDistance para considerar que chegou.")]
        [SerializeField] private float _arrivalTolerance = 0.12f;

        [Tooltip("Graus por segundo ao virar para encarar algo estando parado.")]
        [SerializeField] private float _turnSpeedDegrees = 480f;

        [Tooltip("Segundos sem progresso antes de declarar o caminho impossível. " +
                 "Diferente do timeout cego do código antigo: aqui é detecção de " +
                 "travamento, e leva a uma transição de estado, não a uma espera.")]
        [SerializeField] private float _stuckTimeout = 4f;

        [SerializeField] private float _stuckSpeedThreshold = 0.05f;

        private NavMeshAgent _agent;
        private Vector3 _faceDirection;
        private bool _isFacing;
        private float _stuckTimer;
        private float _lastRemainingDistance;

        /// <summary>
        /// Velocidade que este componente PRETENDE ter. É o que a animação lê.
        ///
        /// Ler agent.velocity, como o NpcAnimationManager faz hoje, capta o
        /// ruído das microcorreções e faz a animação piscar entre Idle e Walk.
        /// A intenção não tem ruído: ou estamos indo a algum lugar, ou não.
        /// </summary>
        public float DesiredSpeed { get; private set; }

        public bool IsHalted { get; private set; } = true;
        public bool PathFailed { get; private set; }
        public Vector3 Destination { get; private set; }

        public bool HasPath => !IsHalted && _agent.hasPath;

        /// <summary>
        /// Chegou ao destino. Exige as três condições porque cada uma sozinha
        /// mente: pathPending deixa remainingDistance em Infinity, hasPath
        /// continua true por um frame depois de chegar, e velocity leva alguns
        /// frames para zerar.
        /// </summary>
        public bool HasArrived
        {
            get
            {
                if (IsHalted) return true;
                if (_agent.pathPending) return false;
                if (_agent.remainingDistance > _agent.stoppingDistance + _arrivalTolerance)
                    return false;

                return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.02f;
            }
        }

        // ── ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoBraking = true;

            // A rotação é assumida já no Awake, e não só no Configure: um
            // cliente colocado à mão na cena nunca chama Configure, e ficaria
            // com o agente E o TickTravelFacing escrevendo rotação — o conflito
            // que esta classe existe para eliminar.
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
        }

        private void OnEnable() => Halt();

        public void Configure(float moveSpeed, float turnSpeedDegrees)
        {
            _agent.speed = moveSpeed;
            _turnSpeedDegrees = turnSpeedDegrees;
        }

        // ── comandos ─────────────────────────────────────────────────────────

        /// <summary>Vai até o ponto. Devolve false se o destino é inalcançável.</summary>
        public bool MoveTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh)
            {
                // Sem esta guarda, SetDestination num agente fora da NavMesh
                // apenas emite um warning e não faz nada — e o estado fica
                // esperando por uma chegada que nunca acontece.
                PathFailed = true;
                return false;
            }

            Destination = destination;
            PathFailed = false;
            _isFacing = false;
            _stuckTimer = 0f;
            _lastRemainingDistance = float.PositiveInfinity;

            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;
            _agent.isStopped = false;
            IsHalted = false;
            DesiredSpeed = _agent.speed;

            if (_agent.SetDestination(destination)) return true;

            PathFailed = true;
            return false;
        }

        /// <summary>
        /// Para de verdade.
        ///
        /// Cada linha resolve um jeito diferente de o NPC continuar se mexendo:
        /// ResetPath tira o caminho (sem caminho não há recálculo), velocity
        /// zerada mata o resíduo, isStopped desliga o seguimento, e
        /// NoObstacleAvoidance é o que impede o agente de ser empurrado pelo
        /// próprio cálculo de desvio quando outro cliente passa do lado.
        /// </summary>
        public void Halt()
        {
            if (_agent == null) return;

            // Tudo o que mexe no agente fica dentro da guarda: o setter de
            // velocity exige um agente posicionado na malha e loga erro fora
            // dela — e Halt() roda no OnEnable de todo cliente que nasce.
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            IsHalted = true;
            DesiredSpeed = 0f;
            _stuckTimer = 0f;
        }

        /// <summary>Vira para uma direção. Só tem efeito enquanto parado.</summary>
        public void FaceTowards(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;

            _faceDirection = worldDirection.normalized;
            _isFacing = true;
        }

        public void FacePoint(Vector3 worldPoint) => FaceTowards(worldPoint - transform.position);

        /// <summary>Existe um caminho completo até lá? Não move o cliente.</summary>
        public bool CanReach(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return false;

            var path = new NavMeshPath();
            return _agent.CalculatePath(destination, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        public void WarpTo(Vector3 position)
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.Warp(position);
            else transform.position = position;
        }

        // ── atualização ──────────────────────────────────────────────────────

        private void Update()
        {
            if (IsHalted) { TickFacing(); return; }

            TickPathHealth();
            TickTravelFacing();
        }

        /// <summary>Girar parado. É a única escrita em rotação do projeto inteiro.</summary>
        private void TickFacing()
        {
            if (!_isFacing) return;

            Quaternion target = Quaternion.LookRotation(_faceDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, _turnSpeedDegrees * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, target) < 0.5f) _isFacing = false;
        }

        /// <summary>Em movimento, o corpo aponta para onde o agente está indo.</summary>
        private void TickTravelFacing()
        {
            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, _turnSpeedDegrees * Time.deltaTime);
        }

        /// <summary>
        /// Detecta caminho impossível e travamento.
        ///
        /// O código antigo resolvia isso com dois timeouts de 15 segundos que
        /// simplesmente desistiam de esperar, sem dizer por quê. Aqui o
        /// resultado é um sinal — PathFailed — que a máquina de estados lê como
        /// transição global, e o cliente reage no mesmo frame.
        /// </summary>
        private void TickPathHealth()
        {
            if (_agent.pathPending) return;

            if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                PathFailed = true;
                return;
            }

            if (HasArrived) { _stuckTimer = 0f; return; }

            bool progressing = _agent.remainingDistance < _lastRemainingDistance - 0.01f
                            || _agent.velocity.sqrMagnitude > _stuckSpeedThreshold * _stuckSpeedThreshold;

            _lastRemainingDistance = Mathf.Min(_lastRemainingDistance, _agent.remainingDistance);

            if (progressing) { _stuckTimer = 0f; return; }

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= _stuckTimeout) PathFailed = true;
        }

        private void OnDisable()
        {
            _isFacing = false;
            DesiredSpeed = 0f;
        }
    }
}
