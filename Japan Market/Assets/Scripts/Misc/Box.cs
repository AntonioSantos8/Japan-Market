using System;
using UnityEngine;

public class Box : InteractableBase
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

    [SerializeField] float rbAngularDamping = 13;

    public override void Interact()
    {
        var controller = ServiceLocator.Get<ItemRaycastController>();
        controller.PickItem(rb, true);
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

        //if (ServiceLocator.Get<ItemRaycastController>().PickItem(rb))
            anim.SetTrigger("Open");
        ServiceLocator.Get<SoundManager>().Play(SFX.AbrirCaixa);
    }

    public void StopHolding()
    {
        isHeld = false;
        holdPoint = null;


        rb.linearDamping = 0f;
    }

    Animator anim;
 
    public override void Awake()
    {
        base.Awake();
        anim = GetComponentInChildren<Animator>();
        currentResistance = maxResistance;
    }
    void Start()
    {
        rb.angularDamping = rbAngularDamping;
        OnPickEvent?.AddListener(()=>{  ServiceLocator.Get<ItemRaycastController>().isWithBox = true ; } );
         OnDropEvent?.AddListener(()=>{  ServiceLocator.Get<ItemRaycastController>().isWithBox = false ;    } );
    }
   
    public override bool  OnLookAt()
    {
        if(ServiceLocator.Get<ItemRaycastController>().HeldItem != null) return false;
        base.OnLookAt();
      return true;
    }
 
}
