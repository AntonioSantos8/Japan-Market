using UnityEngine;

public class FurnitureInstance : MonoBehaviour
{
    public FurnitureData Data { get; set; }
    public FurnitureSaveData SaveData = new FurnitureSaveData();

    [SerializeField] private Transform interactionPoint;
    public Vector3 InteractionPosition => interactionPoint != null
        ? interactionPoint.position
        : transform.position + transform.forward;
}