using UnityEngine;
public enum NpcAnimationState
{
    Idle,
    Walk
}
public class NpcAnimationManager : MonoBehaviour
{
    private Animator animator;
    private NpcAnimationState currentState;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        currentState = NpcAnimationState.Idle;
    }

    public void SetAnimationState(NpcAnimationState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        switch (currentState)
        {
            case NpcAnimationState.Idle:
                animator.SetTrigger("Idle");
                break;
            case NpcAnimationState.Walk:
                animator.SetTrigger("Walk");
                break;
        }
    }
}
