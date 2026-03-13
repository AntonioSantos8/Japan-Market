
using UnityEngine;
public class OutlineAppear : MonoBehaviour
{
    [SerializeField] float range = 5f;
    [SerializeField] LayerMask interactiveLayer;
    Outline objectOutline;
    ObjectEmission objectEmission;
    void Update()
    {
        bool hitSomething = false;

        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, range, interactiveLayer))
        {
            ActivateObject(hit);
            hitSomething = true;
        }
        Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit mouseHit;

        if (Physics.Raycast(mouseRay, out mouseHit, 100f, interactiveLayer))
        {
            ActivateObject(mouseHit);
            hitSomething = true;
        }

        if (!hitSomething)
        {
            DisablePrevious();
        }
    }
    void ActivateObject(RaycastHit hit)
    {
        Outline outline = hit.collider.GetComponent<Outline>();
        ObjectEmission highlight = hit.collider.GetComponent<ObjectEmission>();

        if (outline != null)
        {
            if (objectOutline != outline)
            {
                DisablePrevious();

                objectOutline = outline;
                objectEmission = highlight;

                objectOutline.enabled = true;

                if (objectEmission != null)
                    objectEmission.ActiveEmission();
            }
        }
    }
    void DisablePrevious()
    {
        if (objectOutline != null)
        {
            objectOutline.enabled = false;

            if (objectEmission != null)
                objectEmission.DisableEmission();

            objectOutline = null;
            objectEmission = null;
        }
    }
}