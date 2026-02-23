using UnityEngine;

public class OpenDoors : MonoBehaviour
{
    [SerializeField] Camera cam;
    Transform selectedDoor;
    int leftDoor = 1;
    [SerializeField] LayerMask doorLayer;
    [SerializeField] float interactDistance = 3f;
    [SerializeField] float motorSpeed = 300f;

    void Update()
    {
        RaycastHit hit;

        
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, interactDistance, doorLayer))
        {
            if (Input.GetMouseButtonDown(0))
            {
                selectedDoor = hit.collider.transform;

                HingeJoint joint = selectedDoor.GetComponent<HingeJoint>();
                if (joint != null)
                {
                    joint.useMotor = true;

                   
                    if (selectedDoor.localPosition.x > 0)
                        leftDoor = 1;
                    else
                        leftDoor = -1;
                }
            }
        }

        
        if (Input.GetMouseButton(0) && selectedDoor != null)
        {
            HingeJoint joint = selectedDoor.GetComponent<HingeJoint>();
            JointMotor motor = joint.motor;

            float mouseDelta = -Input.GetAxis("Mouse X");

            motor.targetVelocity = mouseDelta * motorSpeed * leftDoor;
           

            joint.motor = motor;
        }

      
        if (Input.GetMouseButtonUp(0) && selectedDoor != null)
        {
            HingeJoint joint = selectedDoor.GetComponent<HingeJoint>();
            JointMotor motor = joint.motor;

            motor.targetVelocity = 0;
            joint.motor = motor;

            selectedDoor = null;
        }
    }
}