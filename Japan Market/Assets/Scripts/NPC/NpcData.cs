using UnityEngine;
public enum NpcHumor
{
    Happy,
    Angry,
    Neutral
}
[CreateAssetMenu(fileName = "NPC", menuName = "Scriptable Objects/NpcData")]
public class NpcData : ScriptableObject
{
    [Header("NPC Config")]
    public int itemCount;
    public NpcHumor npcHumor;   
    public void SwitchHumor(NpcHumor newHumor)
    {
        npcHumor = newHumor;
    }
}
