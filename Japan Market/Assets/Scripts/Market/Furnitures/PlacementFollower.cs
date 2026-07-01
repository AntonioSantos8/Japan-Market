using UnityEngine;

public class PlacementFollower : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private float viewDistance = 4f;
    [SerializeField] private float gridSize = 2f;
    [SerializeField] private BoxCollider storeArea;

    [Header("Scroll Config")]
    [SerializeField] private float scrollSpeed = 15f;
    [SerializeField] private float minViewDistance = 1.5f;
    [SerializeField] private float maxViewDistance = 10f;

    private FurnitureManager _manager;
    private Bounds storeBounds => storeArea.bounds;

    private void Start()
    {
        _manager = ServiceLocator.Get<FurnitureManager>();
    }

    private void Update()
    {
        if (!_manager.IsBuildingMode) return;

        GameObject ghost = _manager.GetActiveGhost();
        if (ghost == null) return;

        HandleScrollInput();
        UpdateGhostPosition(ghost.transform);
    }

    private void HandleScrollInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        viewDistance += scroll * scrollSpeed * Time.deltaTime;
        viewDistance = Mathf.Clamp(viewDistance, minViewDistance, maxViewDistance);
    }

    private void UpdateGhostPosition(Transform ghostTransform)
    {
        FurnitureData currentSelected = _manager.GetCurrentSelected();
        Vector3 targetPoint = anchor.position + anchor.forward * viewDistance;

        targetPoint.x = Mathf.Clamp(targetPoint.x, storeBounds.min.x, storeBounds.max.x);
        targetPoint.z = Mathf.Clamp(targetPoint.z, storeBounds.min.z, storeBounds.max.z);

        float snappedX = Mathf.Round(targetPoint.x / gridSize) * gridSize;
        float snappedZ = Mathf.Round(targetPoint.z / gridSize) * gridSize;

        ghostTransform.position = new Vector3(snappedX, currentSelected.floorDistance, snappedZ);
    }
}