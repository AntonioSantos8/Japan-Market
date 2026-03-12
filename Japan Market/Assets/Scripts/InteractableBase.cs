using UnityEngine;
using UnityEngine.Events;
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    public UnityEvent OnDropEvent;
    public UnityEvent OnPickEvent;
   
    protected Rigidbody rb;
    //protected Outline outline;
    protected Vector3 originalScale;
    protected bool isOutlineable = true;
    protected bool isMarkable = true;   
    protected bool canInteract = true;
    protected bool addOutline = true;
    public string interactionText;
    public Items itemType;
    
    
    public virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
          originalScale = transform.localScale;  
        if(gameObject.GetComponent<InteractableBase>() == this) 
        {
            // var outl = gameObject.AddComponent<Outline>();
            // outline = outl;
            // outline.OutlineMode = Outline.Mode.OutlineAll;
            // outline.OutlineColor = Color.white;
            // outline.OutlineWidth = 4f;
            // outline.enabled = false;
            // cb = GetComponent<ICustomBehaviourOnPick>();

        }
      


    }
    
    public abstract void Interact();


    public virtual void OnLookAt()
    {
                
        if(!canInteract) {          
          //  ServiceLocator.Get<PlayerInteractions>().GetInteractionText().SetActive(false);
          //outline.enabled = false; return; 
        }
        if (!isOutlineable)
        {
            //ServiceLocator.Get<F2FGrabSystem>().CanThrow = true;
            //outline.enabled = false;
            return;


        }
        if (isMarkable)
        {
           // ServiceLocator.Get<PlayerInteractions>().SetInteractionText("");
        }
        //ServiceLocator.Get<PlayerInteractions>().SetInteractionText(SetInteractionText());
       // outline.enabled = true;
        //ServiceLocator.Get<F2FGrabSystem>().CanThrow = false;
    }
    public virtual string SetInteractionText()
    {
        return interactionText;


    }
    public virtual void OnLookAway()
    {

         //  ServiceLocator.Get<PlayerInteractions>().GetInteractionText().SetActive(false);
       // if(outline != null)
         //   ServiceLocator.Get<F2FGrabSystem>().CanThrow = true;
        //outline.enabled = false;
    }
    public void SetCanInteract(bool value) 
    {

        canInteract = value;
    }
    


public void Grab( Items itemType = Items.None)
    {
        // if(!ServiceLocator.Get<F2FGrabSystem>().GrabObj(this, itemType)) 
        // {
        //      return;
        // }

       
            OnPickEvent?.Invoke();
 
            canInteract = false;
     
      
      
        
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    
    }
    [ContextMenu("Can Interact True")]
     void SetCanInteractTrue() 
    {
        SetCanInteract(true);
    
    
    }
    public void Drop(bool passOverride = false)
    {
      //  if(!passOverride)
        //if(ServiceLocator.Get<F2FGrabSystem>().HeldObject != null) return;
     

      

       // ServiceLocator.Get<F2FGrabSystem>().CurrentItem = Items.None;
       
        canInteract = true;
       
        transform.SetParent(null);
      
       
        //ServiceLocator.Get<F2FGrabSystem>().HeldObject = null;
        
        transform.localScale = originalScale;
        
        rb.useGravity = true;
        
        rb.isKinematic = false;
    }

    public void Throw(Vector3 force)
    {
        Drop();
        rb.AddForce(force, ForceMode.Impulse);
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public Rigidbody GetRb()
    {
  
        return rb;
    }
    public Items GetItemType() { return itemType; }
    public GameObject GetGameObject()
    {
        return gameObject;
    }
}
