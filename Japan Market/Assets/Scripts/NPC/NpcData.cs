using UnityEngine;

/// <summary>
/// ScriptableObject de dados puros do NPC.
/// </summary>
[CreateAssetMenu(fileName = "NpcData", menuName = "Scriptable Objects/NpcData")]
public class NpcData : ScriptableObject
{
    [Header("Config")]
    public int maxItemsToBuy = 5;
}
