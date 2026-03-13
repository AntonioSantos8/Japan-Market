using UnityEngine;

public enum NpcHumor
{
    Happy,
    Angry,
    Neutral
}

[System.Serializable]
public class HumorDialogue
{
    public NpcHumor humor;
    public string[] phrases;
}

[CreateAssetMenu(fileName = "NPC", menuName = "Scriptable Objects/NpcData")]
public class NpcData : ScriptableObject
{
    [Header("NPC Config")]
    public int itemCount;
    public NpcHumor npcHumor;

    public HumorDialogue[] dialogues;

    public string GetRandomPhrase()
    {
        foreach (var d in dialogues)
        {
            if (d.humor == npcHumor)
            {
                int index = Random.Range(0, d.phrases.Length);
                return d.phrases[index];
            }
        }

        return "";
    }

    public void SwitchHumor(NpcHumor newHumor)
    {
        npcHumor = newHumor;
    }
}