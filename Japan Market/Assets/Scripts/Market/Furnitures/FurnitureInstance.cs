using UnityEngine;

public class FurnitureInstance : MonoBehaviour
{
    [SerializeField] private Transform interactionPoint;

    public Vector3 InteractionPosition => interactionPoint != null
        ? interactionPoint.position
        : transform.position + transform.forward;
}