using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class NpcPhrases : MonoBehaviour
{
    [SerializeField] private TMP_Text phraseText;
    private NpcInstance _npcInstance;

    private void Awake()
    {
        _npcInstance = GetComponent<NpcInstance>();
    }

    private void Start()
    {
        StartCoroutine(NpcSpeak());
    }

    IEnumerator NpcSpeak()
    {
        while (true)
        {
            phraseText.DOFade(1, 1f);

            phraseText.text = GetPhraseForCurrentHumor();

            yield return new WaitForSeconds(5f);
            phraseText.DOFade(0, 1f).OnComplete(() => phraseText.text = "");
            yield return new WaitForSeconds(1f);
        }
    }

    private string GetPhraseForCurrentHumor()
    {
        if (_npcInstance._data == null) return "";

        foreach (var d in _npcInstance._data.dialogues)
        {
            if (d.humor == _npcInstance.currentHumor)
            {
                int index = Random.Range(0, d.phrases.Length);
                return d.phrases[index];
            }
        }
        return "...";
    }
}