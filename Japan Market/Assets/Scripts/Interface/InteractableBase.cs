using UnityEngine;
using UnityEngine.Events;
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    public UnityEvent OnDropEvent;
    public UnityEvent OnPickEvent;
   
    protected Rigidbody rb;
    protected Outline outline;
    protected Vector3 originalScale;
    protected bool isOutlineable = true;
    protected bool isMarkable = true;   
    protected bool canInteract = true;
    protected bool addOutline = true;
    public string interactionText ;
    
    
    public virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
          originalScale = transform.localScale;  
        if(gameObject.GetComponent<InteractableBase>() == this) 
        {
             var outl = gameObject.AddComponent<Outline>();
             outline = outl;
            outline.OutlineMode = Outline.Mode.OutlineAll;
             outline.OutlineColor = Color.white;
             outline.OutlineWidth = 4f;
            outline.enabled = false;
            //cb = GetComponent<ICustomBehaviourOnPick>();

        }
      


    }
    
    public abstract void Interact();


    public virtual void OnLookAt()
    {
                
        if(!canInteract) {          
          //  ServiceLocator.Get<PlayerInteractions>().GetInteractionText().SetActive(false);
          outline.enabled = false; return; 
        }
        if (!isOutlineable)
        {
            //ServiceLocator.Get<F2FGrabSystem>().CanThrow = true;
            outline.enabled = false;
            return;


        }
        if (isMarkable)
        {
           // ServiceLocator.Get<PlayerInteractions>().SetInteractionText("");
        }
        //ServiceLocator.Get<PlayerInteractions>().SetInteractionText(SetInteractionText());
        outline.enabled = true;
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
     if(outline!= null )
        outline.enabled = false;
    }
    public void SetCanInteract(bool value) 
    {

        canInteract = value;
    }
    



    [ContextMenu("Can Interact True")]
     void SetCanInteractTrue() 
    {
        SetCanInteract(true);
    
    
    }
   
    

    public Transform GetTransform()
    {
        return transform;
    }

    public Rigidbody GetRb()
    {
  
        return rb;
    }
   
    public GameObject GetGameObject()
    {
        return gameObject;
    }
}
