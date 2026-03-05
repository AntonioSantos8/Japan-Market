using System;
using UnityEngine;

public class Box : MonoBehaviour, IInteractable
{
    Rigidbody rb;
    bool canInteract = true;
    Animator anim;
    public void Interact()
    {
        if(!canInteract) return;
        canInteract = false;
        ServiceLocator.Get<PlayerHoldManager>().PickItem(rb);
        anim.SetTrigger("Open");
    }
    public void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
    }

}
