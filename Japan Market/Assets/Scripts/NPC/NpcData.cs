using UnityEngine;

public enum NpcHumor { Happy, Neutral, Angry }

/// <summary>Eventos especiais que fazem o NPC falar algo específico.</summary>
public enum NpcEvent { DirtyStore, EmptyShelves, CashierTooSlow, ReturningItems, Impatient, PriceTooHigh }

[System.Serializable]
public class HumorDialogue
{
    public NpcHumor humor;
    [TextArea(1, 4)]
    public string[] phrases;
}

[System.Serializable]
public class EventDialogue
{
    public NpcEvent eventType;
    [TextArea(1, 4)]
    public string[] phrases;
}

/// <summary>
/// ScriptableObject de dados puros do NPC.
/// Não guarda estado de runtime (como humor atual) — isso é papel do NpcInstance.
/// </summary>
[CreateAssetMenu(fileName = "NpcData", menuName = "Scriptable Objects/NpcData")]
public class NpcData : ScriptableObject
{
    [Header("Config")]
    public int maxItemsToBuy = 5;

    [Header("Dialogues")]
    public HumorDialogue[] dialogues;

    [Header("Event Dialogues")]
    public EventDialogue[] eventDialogues;

    /// <summary>Retorna uma frase aleatória para o humor informado.</summary>
    public string GetRandomPhrase(NpcHumor humor)
    {
        foreach (var d in dialogues)
        {
            if (d.humor != humor) continue;
            if (d.phrases == null || d.phrases.Length == 0) return "...";

            return d.phrases[Random.Range(0, d.phrases.Length)];
        }
        return "...";
    }

    /// <summary>Retorna uma frase aleatória para o evento informado (vazio se não houver).</summary>
    public string GetRandomEventPhrase(NpcEvent evt)
    {
        if (eventDialogues == null) return string.Empty;

        foreach (var d in eventDialogues)
        {
            if (d.eventType != evt) continue;
            if (d.phrases == null || d.phrases.Length == 0) return string.Empty;

            return d.phrases[Random.Range(0, d.phrases.Length)];
        }
        return string.Empty;
    }
}
