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


        if (gameObject.TryGetComponent(out ShelfItem shelfItem))
        {
            shelfItem.GetSegment().SetCanPut(false);
            shelfItem.GetSegment().ActiveCollider();
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
