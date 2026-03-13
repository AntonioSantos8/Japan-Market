using UnityEngine;

public class NpcCanvasLookAt : MonoBehaviour
{
    [Header("Billboard")]
    [SerializeField] private Transform target;
    [SerializeField] private bool facePlayerEveryFrame = true;

    private void Start()
    {
        if (target == null && Camera.main != null)
            target = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (facePlayerEveryFrame)
            FaceTarget();
    }

    public void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = transform.position - target.position;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}