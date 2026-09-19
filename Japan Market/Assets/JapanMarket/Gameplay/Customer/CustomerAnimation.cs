using UnityEngine;

namespace JapanMarket.Gameplay
{

    [RequireComponent(typeof(CustomerLocomotion))]
    [DisallowMultipleComponent]
    public sealed class CustomerAnimation : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IdleVariantHash = Animator.StringToHash("IdleVariant");
        private static readonly int CarryingHash = Animator.StringToHash("Carrying");

        [SerializeField] private Animator _animator;

        [Tooltip("Suavização do parâmetro Speed. Absorve o transiente de partida " +
                 "e parada sem deixar a animação lenta para reagir.")]
        [SerializeField] private float _speedDamping = 0.12f;

        [Header("Queue idle")]
        [Tooltip("Quantas variações de idle o Animator tem. O 'fidget' que hoje " +
                 "é feito com DOTween no transform vive aqui — onde não disputa " +
                 "a posição com o NavMeshAgent.")]
        [Min(1)] [SerializeField] private int _idleVariantCount = 1;

        [SerializeField] private Vector2 _idleVariantInterval = new(4f, 9f);

        private CustomerLocomotion _locomotion;
        private float _nextVariantAt;
        private bool _hasSpeedParameter;
        private bool _hasIdleVariantParameter;
        private bool _hasCarryingParameter;

        private void Awake()
        {
            _locomotion = GetComponent<CustomerLocomotion>();

            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null)
            {
                enabled = false;
                return;
            }

            CacheParameters();
        }

        private void CacheParameters()
        {
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == SpeedHash) _hasSpeedParameter = true;
                else if (parameter.nameHash == IdleVariantHash) _hasIdleVariantParameter = true;
                else if (parameter.nameHash == CarryingHash) _hasCarryingParameter = true;
            }
        }

        private void Update()
        {
            if (_hasSpeedParameter)
                _animator.SetFloat(SpeedHash, _locomotion.DesiredSpeed, _speedDamping, Time.deltaTime);

            TickIdleVariant();
        }

        private void TickIdleVariant()
        {
            if (!_hasIdleVariantParameter || _idleVariantCount <= 1) return;
            if (!_locomotion.IsHalted) return;
            if (Time.time < _nextVariantAt) return;

            _animator.SetInteger(IdleVariantHash, Random.Range(0, _idleVariantCount));
            _nextVariantAt = Time.time + Random.Range(_idleVariantInterval.x, _idleVariantInterval.y);
        }

        public void SetCarrying(bool carrying)
        {
            if (_hasCarryingParameter) _animator.SetBool(CarryingHash, carrying);
        }
    }
}
