using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{

    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class CustomerLocomotion : MonoBehaviour
    {
        [Tooltip("Slack added to stoppingDistance to consider that it has arrived.")]
        [SerializeField] private float _arrivalTolerance = 0.12f;

        [Tooltip("Degrees per second when turning to face something while stationary.")]
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

        public float DesiredSpeed { get; private set; }

        public bool IsHalted { get; private set; } = true;
        public bool PathFailed { get; private set; }
        public Vector3 Destination { get; private set; }

        public bool HasPath => !IsHalted && _agent.hasPath;

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

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoBraking = true;

            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
        }

        private void OnEnable() => Halt();

        public void Configure(float moveSpeed, float turnSpeedDegrees)
        {
            _agent.speed = moveSpeed;
            _turnSpeedDegrees = turnSpeedDegrees;
        }

        public bool MoveTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh)
            {

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

        public void Halt()
        {
            if (_agent == null) return;

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

        public void FaceTowards(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;

            _faceDirection = worldDirection.normalized;
            _isFacing = true;
        }

        public void FacePoint(Vector3 worldPoint) => FaceTowards(worldPoint - transform.position);

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

        private void Update()
        {
            if (IsHalted) { TickFacing(); return; }

            TickPathHealth();
            TickTravelFacing();
        }

        private void TickFacing()
        {
            if (!_isFacing) return;

            Quaternion target = Quaternion.LookRotation(_faceDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, _turnSpeedDegrees * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, target) < 0.5f) _isFacing = false;
        }

        private void TickTravelFacing()
        {
            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, _turnSpeedDegrees * Time.deltaTime);
        }

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
