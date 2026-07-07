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

    /// <summary>Retorna uma frase aleatória para o evento informado. Usa frases do ScriptableObject
    /// se configuradas; caso contrário cai nos defaults hardcoded.</summary>
    public string GetRandomEventPhrase(NpcEvent evt)
    {
        if (eventDialogues != null)
        {
            foreach (var d in eventDialogues)
            {
                if (d.eventType != evt) continue;
                if (d.phrases != null && d.phrases.Length > 0)
                    return d.phrases[Random.Range(0, d.phrases.Length)];
            }
        }

        return GetDefaultEventPhrase(evt);
    }

    private static readonly string[][] _defaultEventPhrases = new string[][]
    {
        // DirtyStore
        new[] { "Que loja suja!", "Isso aqui é uma pocilga...", "Dá pra limpar isso aqui não?" },
        // EmptyShelves
        new[] { "Não tem nada que eu quero aqui.", "Prateleiras vazias de novo?", "Preciso ir em outro lugar." },
        // CashierTooSlow
        new[] { "Tô esperando há séculos!", "Isso é um absurdo, vou embora!", "Sem tempo pra isso." },
        // ReturningItems
        new[] { "Vou devolver isso aqui.", "Não vou levar mais nada.", "Melhor colocar de volta." },
        // Impatient
        new[] { "Alguém me atende?", "Quanto tempo ainda?", "Tô aqui esperando..." },
        // PriceTooHigh
        new[] { "Que preço absurdo!", "Tá muito caro isso!", "Nem pensar a esse preço." },
    };

    private static string GetDefaultEventPhrase(NpcEvent evt)
    {
        int index = (int)evt;
        if (index < 0 || index >= _defaultEventPhrases.Length) return string.Empty;
        var phrases = _defaultEventPhrases[index];
        return phrases[Random.Range(0, phrases.Length)];
    }
}
