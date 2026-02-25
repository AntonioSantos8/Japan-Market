using UnityEngine;

public class HoldableItem : MonoBehaviour
{
    public static HoldableItem Current;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void BeginHold()
    {
        if (TryGetComponent(out ShelfItem shelfItem))
        {
            shelfItem.RemoveFromShelf();
        }

        rb.isKinematic = false;
        Current = this;
    }

    public void EndHold()
    {
        Current = null;
    }
}