using UnityEngine;

public class PhysicsHoldableItem : InteractableBase
{
  
    public float followForce = 150f;
    public float followDrag = 2f;

  
    public bool canBreak = true;
    public float maxResistance = 100f;
    public float breakThreshold = 15f;

    float currentResistance;
    bool isHeld;
    Transform holdPoint;

    bool isBroken;

    public override void Awake()
    {
        base.Awake();
        currentResistance = maxResistance;
    }

    public override void Interact()
    {
        var controller = ServiceLocator.Get<ItemRaycastController>();
        controller.PickItem(rb);
    }

    void FixedUpdate()
    {
        if (!isHeld || holdPoint == null) return;

        Vector3 dir = (holdPoint.position - rb.position);
        Vector3 vel = rb.linearVelocity;

        Vector3 force = dir * followForce - vel * 5f;
        rb.AddForce(force, ForceMode.Acceleration);

        rb.linearDamping = followDrag;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!canBreak || isBroken) return;

        float impact = collision.relativeVelocity.magnitude;

        if (impact >= breakThreshold)
        {
            currentResistance -= impact;

            if (currentResistance <= 0f)
            {
                BreakObject();
            }
        }
    }

    void BreakObject()
    {
        if (isBroken) return;
        isBroken = true;

        Destroy(gameObject);
    }

    public void StartHolding(Transform point)
    {
        holdPoint = point;
        isHeld = true;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = followDrag;
    }

    public void StopHolding()
    {
        isHeld = false;
        holdPoint = null;

        rb.linearDamping = 0f;
    }
}