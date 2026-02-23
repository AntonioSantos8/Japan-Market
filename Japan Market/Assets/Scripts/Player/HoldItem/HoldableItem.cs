using UnityEngine;

public class HoldableItem : MonoBehaviour
{
    Rigidbody rb;
    private void Start()
    {
rb = GetComponent<Rigidbody>();
    }
    void OnMouseDown()
    {

        ShelfItem shelfItem = GetComponent<ShelfItem>();
        if (shelfItem != null)
        {
            shelfItem.GetSegment().InactiveCollide();
            shelfItem.RemoveFromShelf();
        }

        rb.isKinematic = false;
        ServiceLocator.Get<DragRigidbody>().HandleInputBegin(Input.mousePosition);
    
    }

    void OnMouseUp()
    {
        ServiceLocator.Get<DragRigidbody>().HandleInputEnd(Input.mousePosition);
    }

    void OnMouseDrag()
    {
        ServiceLocator.Get<DragRigidbody>().HandleInput(Input.mousePosition);
    }
}
