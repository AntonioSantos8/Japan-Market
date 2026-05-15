using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI fpsText;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 0.5f) 
        {
            int fps = Mathf.RoundToInt(1f / Time.unscaledDeltaTime);
            fpsText.text = $"{fps} FPS";
            timer = 0f;
        }
    }
}
