using UnityEngine;

public class ItemsExample : MonoBehaviour
{
    [SerializeField] AllIThingsData allIThingsData;
    public AllIThingsData GetAllThingsData() => allIThingsData;
    public void SetAllThingsData(AllIThingsData data) => allIThingsData = data;
}