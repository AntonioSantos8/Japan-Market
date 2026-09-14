using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Traduz a intenção da locomoção em parâmetros de Animator. Não toca o
    /// transform, não lê o NavMeshAgent.
    ///
    /// O componente atual lê <c>agent.velocity.magnitude</c> contra um limiar
    /// de 0,1 — e como o agente parado ainda recebe microcorreções, e como a
    /// fila reinicia o destino de quem já chegou toda vez que alguém entra ou
    /// sai, a animação fica alternando Idle/Walk. Ler a intenção com damping
    /// elimina as duas causas de uma vez: intenção não tem ruído, e a suavização
    /// absorve o transiente de partida e parada.
    /// </summary>
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

        [Header("Idle de fila")]
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

            // Um Animator sem o parâmetro esperado gera um warning por frame no
            // Unity. Conferir uma vez é mais barato que poluir o console.
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

        /// <summary>Chamado pela cesta quando o cliente pega ou larga produtos.</summary>
        public void SetCarrying(bool carrying)
        {
            if (_hasCarryingParameter) _animator.SetBool(CarryingHash, carrying);
        }
    }
}
