using UnityEngine;

public class OpenFreezer : MonoBehaviour
{
    [SerializeField] Camera cam;
    Transform selectedFreezer;
    [SerializeField] LayerMask freezerLayer;
    [SerializeField] float interactDistance = 3f;
    [SerializeField] float speed = 2f;
    [SerializeField] float minDistanceX = 0.5f;
    [SerializeField] float maxDistanceX = 1.5f;
    bool isDragging = false;

  void Update()
    {
        RaycastHit hit;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, interactDistance, freezerLayer))
        {
            if (Input.GetMouseButtonDown(0))
            {
                selectedFreezer = hit.transform;
                isDragging = true;
       
            }
        }

        if (Input.GetMouseButton(0) && isDragging && selectedFreezer != null)
        {
            float mouseX = -Input.GetAxis("Mouse X");

            Vector3 pos = selectedFreezer.localPosition;

            pos.x += mouseX * speed;
            pos.x = Mathf.Clamp(pos.x, minDistanceX, maxDistanceX);

            selectedFreezer.localPosition = Vector3.Lerp(selectedFreezer.localPosition, pos, speed*Time.deltaTime);
         
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            selectedFreezer = null;
        }
    }
}