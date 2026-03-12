using UnityEngine;

public class OutlineAppear : MonoBehaviour
{
    
    [SerializeField] private float range = 5f;
    [SerializeField] private LayerMask interactiveLayer;
    private Outline currentobject;

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, interactiveLayer))
        {
            Outline outline = hit.collider.GetComponent<Outline>();

            if (outline != null)
            {
                if (currentobject != outline)
                {
                    DisablePrevious();
                    currentobject = outline;
                    currentobject.enabled = true;
                }
            }
            else
            {
                DisablePrevious();
            }
        }
        else
        {
            DisablePrevious();
        }
    }

    void DisablePrevious()
    {
        if (currentobject != null)
        {
            currentobject.enabled = false;
            currentobject = null;
        }
    }
}
