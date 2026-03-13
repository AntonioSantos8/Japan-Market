using System.Collections;
using DG.Tweening;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class NpcPhrases : MonoBehaviour
{
    [SerializeField] private TMP_Text phraseText;
    [SerializeField] private NpcData _data;
    private void Start()
    {
        StartCoroutine(NpcSpeak());
    }
    IEnumerator NpcSpeak()
    {
        while(true){
            phraseText.DOFade(1, 1f);
            phraseText.text = _data.GetRandomPhrase();
            yield return new WaitForSeconds(5f);
            phraseText.DOFade(0, 1f).OnComplete(() => phraseText.text = "");
            yield return new WaitForSeconds(1f);
        }
       
    }
}
