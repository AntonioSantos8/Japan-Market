using System;
using UnityEngine;

public class Box : InteractableBase
{
  
    
    Animator anim;
 
    public override void Awake()
    {
        base.Awake();
        anim = GetComponentInChildren<Animator>();
    }
    void Start()
    {
        OnPickEvent?.AddListener(()=>{  ServiceLocator.Get<ItemRaycastController>().isWithBox = true ; } );
         OnDropEvent?.AddListener(()=>{  ServiceLocator.Get<ItemRaycastController>().isWithBox = false ;    } );
    }
    public override void Interact()
    {
       // if(!canInteract) return;
     
        ServiceLocator.Get<ItemRaycastController>().PickItem(rb);
        
        anim.SetTrigger("Open");
    }
    public override void OnLookAt()
    {
        base.OnLookAt();
      
    }
}
