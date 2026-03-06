using UnityEngine;

public class RaycastSegment : MonoBehaviour
{
    public bool isWithBox;
    public float distance = 3f;
    public Camera cam;

    GameObject lastObject;
    void Start()
    {
        ServiceLocator.Register(this);
    }
    void Update()
    {
        if (!isWithBox) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, distance))
        {
            GameObject hitObject = hit.collider.gameObject;

         
            if (hitObject != lastObject)
            {
                lastObject = hitObject;

             
            
                OnLookObject(hitObject);
            }
        }
        else
        {
            lastObject = null;
        }
    }

    void OnLookObject(GameObject obj)
    {
       
    }
}