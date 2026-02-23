using UnityEngine;
using UnityEngine.Events;

public class OnCollisionExit : MonoBehaviour
{
    public UnityEvent OnExit;
    private void OnTriggerExit(Collider other)
    {
        if(other.gameObject.layer == LayerMask.NameToLayer("Interactive"))
        OnExit?.Invoke();
    }
}
