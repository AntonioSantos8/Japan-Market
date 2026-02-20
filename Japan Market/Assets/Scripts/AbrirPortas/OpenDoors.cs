using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OpenDoors : MonoBehaviour
{

    [SerializeField] private Camera playerCam;

 
    [SerializeField] private string grabButton = "Grab";
    [SerializeField] private float pickupRange = 2.5f;
    [SerializeField] private float holdDistance = 2f;
    [SerializeField] private float maxDistance = 3f;
    [SerializeField] private float moveForce = 10f;

    private GameObject doorHeld;
    private bool isHolding;

    void FixedUpdate()
    {
        if (Input.GetButton(grabButton))
        {
            if (!isHolding)
                TryGrabDoor();
            else
                HoldDoor();
        }
        else if (isHolding)
        {
            DropDoor();
        }
    }

    void TryGrabDoor()
    {
        Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupRange))
        {
            if (hit.collider.CompareTag("Door"))
            {
                doorHeld = hit.collider.gameObject;

                Rigidbody rb = doorHeld.GetComponent<Rigidbody>();
                if (rb == null) return;

                rb.useGravity = true;
                rb.freezeRotation = false;

                isHolding = true;
            }
        }
    }

    void HoldDoor()
    {
        if (doorHeld == null) return;

        Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        Vector3 targetPos = playerCam.transform.position + ray.direction * holdDistance;
        Vector3 currentPos = doorHeld.transform.position;

        Rigidbody rb = doorHeld.GetComponent<Rigidbody>();
        rb.linearVelocity = (targetPos - currentPos) * moveForce;

        if (Vector3.Distance(currentPos, playerCam.transform.position) > maxDistance)
        {
            DropDoor();
        }
    }

    void DropDoor()
    {
        if (doorHeld == null) return;

        isHolding = false;
        doorHeld = null;
    }
}