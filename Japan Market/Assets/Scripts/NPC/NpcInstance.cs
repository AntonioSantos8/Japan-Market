using UnityEngine;

[RequireComponent(typeof(NpcPhrases))]
public class NpcInstance : MonoBehaviour
{
    public NpcData _data;

    [Header("Humor System")]
    public int humorCoins = 100;
    public NpcHumor currentHumor = NpcHumor.Happy;

    public void ReceiveWrongChange(int penalty)
    {
        humorCoins -= penalty;
        UpdateHumorState();
        Debug.Log($"NPC recebeu troco errado! Moedas atuais: {humorCoins}. Humor: {currentHumor}");
    }

    private void UpdateHumorState()
    {
        if (humorCoins >= 70)
            currentHumor = NpcHumor.Happy;
        else if (humorCoins >= 30)
            currentHumor = NpcHumor.Neutral;
        else
            currentHumor = NpcHumor.Angry;
    }
}