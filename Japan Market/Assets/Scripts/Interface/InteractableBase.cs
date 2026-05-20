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
    public string interactionText;


    // InteractableBase
    public virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale;
        InitOutline();
    }

    private void InitOutline()
    {
        outline = GetComponent<Outline>();

        if (outline == null)
            outline = GetComponentInChildren<Outline>(true);

        if (outline == null)
        {
            isOutlineable = false;
            return;
        }

        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 4f;
        //outline.enabled = false;
    }

    public abstract void Interact();


    public virtual bool OnLookAt()
    {
        if (outline == null)
            outline = GetComponentInChildren<Outline>(true);
        if (!canInteract)
        {
            if (outline != null) outline.enabled = false;
            return false;
        }

        if (!isOutlineable)
        {
            if (outline != null) outline.enabled = false;
            return false;
        }

        if (outline != null) outline.enabled = true;
        return true;
    }
    public virtual string SetInteractionText()
    {
        return interactionText;


    }
    public virtual void OnLookAway()
    {
        if (outline != null)
            outline = GetComponentInChildren<Outline>(true);

        if (outline != null)
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
