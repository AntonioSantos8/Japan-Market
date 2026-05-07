using UnityEngine;
using UnityEngine.AI;

public enum NpcAnimationState
{
    Idle,
    Walk
}

/// <summary>
/// Gerencia as animações do NPC de forma autônoma, lendo a velocidade real
/// do NavMeshAgent em vez de depender de chamadas externas pontuais.
/// Isso resolve o problema de NPCs andando sem tocar a animação de Walk.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NpcAnimationManager : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Velocidade mínima para considerar o NPC em movimento.")]
    [SerializeField] private float _movementThreshold = 0.1f;

    private static readonly int _walkHash = Animator.StringToHash("Walk");
    private static readonly int _idleHash = Animator.StringToHash("Idle");

    private Animator _animator;
    private NavMeshAgent _agent;
    private NpcAnimationState _currentState;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        SetState(NpcAnimationState.Idle);
    }

    /// <summary>
    /// Atualiza a animação a cada frame com base na velocidade real do agente,
    /// eliminando a dependência de quem está chamando SetAnimationState na hora certa.
    /// </summary>
    private void Update()
    {
        bool isMoving = _agent.velocity.magnitude > _movementThreshold;
        NpcAnimationState targetState = isMoving ? NpcAnimationState.Walk : NpcAnimationState.Idle;

        if (_currentState != targetState)
            SetState(targetState);
    }

    /// <summary>
    /// Permite forçar um estado manualmente caso necessário (ex: animação de interação futura).
    /// </summary>
    public void SetAnimationState(NpcAnimationState newState)
    {
        SetState(newState);
    }

    private void SetState(NpcAnimationState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;

        _animator.SetBool(_walkHash, false);
        _animator.SetBool(_idleHash, false);

        switch (_currentState)
        {
            case NpcAnimationState.Idle:
                _animator.SetBool(_idleHash, true);
                break;
            case NpcAnimationState.Walk:
                _animator.SetBool(_walkHash, true);
                break;
        }
    }
}