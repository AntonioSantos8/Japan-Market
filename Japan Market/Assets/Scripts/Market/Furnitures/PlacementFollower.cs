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

    private Bounds StoreBounds => storeArea.bounds;

    private void Start()
    {
        _manager = ServiceLocator.Get<FurnitureManager>();
    }

    private void Update()
    {
        if (_manager == null || !_manager.IsBuildingMode) return;

        GameObject ghost = _manager.GetActiveGhost();
        if (ghost == null) return;

        HandleScrollInput();
        FollowAnchor(ghost.transform);
    }

    private void HandleScrollInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        viewDistance = Mathf.Clamp(
            viewDistance + scroll * scrollSpeed * Time.deltaTime,
            minViewDistance,
            maxViewDistance);
    }

    private void FollowAnchor(Transform ghostTransform)
    {
        FurnitureData currentSelected = _manager.GetCurrentSelected();
        if (currentSelected == null || anchor == null || storeArea == null) return;

        Vector3 target = ComputeTargetPosition(currentSelected.floorDistance);
        ghostTransform.position = Vector3.Lerp(ghostTransform.position, target, Time.deltaTime * 18f);
    }

    private Vector3 ComputeTargetPosition(float floorDistance)
    {
        Bounds bounds = StoreBounds;
        Vector3 point = anchor.position + anchor.forward * viewDistance;

        point.x = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
        point.z = Mathf.Clamp(point.z, bounds.min.z, bounds.max.z);

        float grid = Mathf.Max(0.01f, gridSize);
        float snappedX = Mathf.Round(point.x / grid) * grid;
        float snappedZ = Mathf.Round(point.z / grid) * grid;
        float hoverY = Mathf.Sin(Time.time * 2.2f) * 0.025f;

        return new Vector3(snappedX, floorDistance + hoverY, snappedZ);
    }
}
