using UnityEngine;

public class RotateToCamera : MonoBehaviour
{
    Transform cameraToRotate;
    [SerializeField]Vector3 offset;
    void Start()
    {
        cameraToRotate = Camera.main.transform;
    }

   
   
    void Update()
    {
        transform.LookAt(cameraToRotate);
        transform.eulerAngles += offset;
    }
    private void OnValidate()
    {
        if (cameraToRotate == null) return;
        
        cameraToRotate = Camera.main.transform;
        Update();
    }
}
