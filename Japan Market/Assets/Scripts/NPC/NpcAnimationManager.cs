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


        animator.SetBool("Walk", false);
        animator.SetBool("Idle", false);

        switch (currentState)
        {
            case NpcAnimationState.Idle:
                animator.SetBool("Idle", true);
                break;
            case NpcAnimationState.Walk:
                animator.SetBool("Walk", true);
                break;
        }
    }
}
