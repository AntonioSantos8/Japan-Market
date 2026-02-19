using UnityEngine;

public class HoldableItem : MonoBehaviour
{
    void OnMouseDown()
    {
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
