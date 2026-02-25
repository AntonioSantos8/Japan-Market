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
        if(rb == null) { rb = gameObject.AddComponent<Rigidbody>(); }
        rb.isKinematic = false;
        Current = this;
    }

    public void EndHold()
    {
        Current = null;
    }
}