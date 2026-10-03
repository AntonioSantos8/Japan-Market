using UnityEngine;

public class FurnitureInstance : MonoBehaviour
{
    public FurnitureData Data { get; set; }
    public FurnitureSaveData SaveData = new FurnitureSaveData();
    public Shelf shelf;
    public Transform interactionPoint;
    public Vector3 InteractionPosition
    {
        get
        {
            Vector3 position = interactionPoint != null
                ? interactionPoint.position
                : transform.position + transform.forward;
            // Interaction markers can be at shelf height. Navigation needs the
            // floor below the marker, not its visual height (2.32m on BigShelf).
            position.y = transform.position.y - (Data != null ? Data.floorDistance : 0f);
            return position;
        }
    }
}
