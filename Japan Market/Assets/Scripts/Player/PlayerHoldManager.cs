using UnityEngine;

public class PlayerHoldManager : MonoBehaviour
{
    public Transform handPivot;

    Rigidbody heldItemRb;
    Transform heldItem;

    void Start()
    {
        ServiceLocator.Register(this);
    }

    void Update()
    {
        if (heldItem == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            DropItem();
        }
    }

    public void PickItem(Rigidbody itemRb)
    {
        heldItemRb = itemRb;
        heldItem = itemRb.transform;

        heldItemRb.isKinematic = true;
        heldItemRb.useGravity = false;

   
        heldItem.SetParent(handPivot);

     
        heldItem.localPosition = Vector3.zero;
        heldItem.localRotation = Quaternion.identity;
                   
    }

    public void DropItem()
    {
   
        heldItem.SetParent(null);

   
        heldItemRb.isKinematic = false;
        heldItemRb.useGravity = true;

        heldItemRb = null;
        heldItem = null;
    }
}