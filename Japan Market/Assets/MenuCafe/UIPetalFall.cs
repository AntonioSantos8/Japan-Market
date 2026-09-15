using UnityEngine;
using UnityEngine.UI;

public class UIPetalFall : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform petalPrefab;
    [SerializeField] private RectTransform container;

    [Header("Amount")]
    [SerializeField] private int petalCount = 20;

    [Header("Fall")]
    [SerializeField] private float fallSpeedMin = 25f;
    [SerializeField] private float fallSpeedMax = 50f;

    [Header("Movement")]
    [SerializeField] private float swayAmount = 40f;
    [SerializeField] private float swaySpeedMin = 0.5f;
    [SerializeField] private float swaySpeedMax = 1.5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeedMin = -60f;
    [SerializeField] private float rotationSpeedMax = 60f;

    [Header("Fade")]
    [SerializeField] private float fadeDistance = 150f;

    private RectTransform[] petals;
    private Image[] images;

    private float[] speeds;
    private float[] swaySpeeds;
    private float[] swayOffsets;
    private float[] rotationSpeeds;

    private float[] startX;

    void Start()
    {
        petals = new RectTransform[petalCount];
        images = new Image[petalCount];

        speeds = new float[petalCount];
        swaySpeeds = new float[petalCount];
        swayOffsets = new float[petalCount];
        rotationSpeeds = new float[petalCount];

        startX = new float[petalCount];

        for (int i = 0; i < petalCount; i++)
        {
            RectTransform petal = Instantiate(petalPrefab, container);

            petals[i] = petal;
            images[i] = petal.GetComponent<Image>();

            speeds[i] = Random.Range(fallSpeedMin, fallSpeedMax);
            swaySpeeds[i] = Random.Range(swaySpeedMin, swaySpeedMax);
            swayOffsets[i] = Random.Range(0f, 100f);
            rotationSpeeds[i] = Random.Range(rotationSpeedMin, rotationSpeedMax);

            petal.localScale = Vector3.one * Random.Range(0.5f, 1f);

            ResetPetal(i, true);
        }
    }

    void Update()
    {
        float height = container.rect.height;
        float bottom = -height / 2f;
        float top = height / 2f;

        for (int i = 0; i < petals.Length; i++)
        {
            RectTransform petal = petals[i];

            Vector2 pos = petal.anchoredPosition;

            // ↓ Movimento para baixo
            pos.y -= speeds[i] * Time.deltaTime;

            // ↔️ Movimento suave para os lados
            pos.x = startX[i] +
                     Mathf.Sin(Time.time * swaySpeeds[i] + swayOffsets[i])
                     * swayAmount;

            petal.anchoredPosition = pos;

            // 🔄 Rotação
            petal.Rotate(
                0f,
                0f,
                rotationSpeeds[i] * Time.deltaTime
            );

            // 🌫️ Fade quando estiver chegando ao final
            float fadeStart = bottom + fadeDistance;

            float alpha = 1f;

            if (pos.y < fadeStart)
            {
                alpha = Mathf.InverseLerp(
                    bottom,
                    fadeStart,
                    pos.y
                );
            }

            Color color = images[i].color;
            color.a = alpha;
            images[i].color = color;

            // Só reseta quando estiver COMPLETAMENTE fora
            if (pos.y < bottom - 100f)
            {
                ResetPetal(i, false);
            }
        }
    }

    void ResetPetal(int i, bool randomHeight)
    {
        float width = container.rect.width;
        float height = container.rect.height;

        startX[i] = Random.Range(
            -width / 2f,
            width / 2f
        );

        float y;

        if (randomHeight)
        {
            y = Random.Range(
                -height / 2f,
                height / 2f
            );
        }
        else
        {
            // Nasce acima da tela
            y = height / 2f + 100f;
        }

        petals[i].anchoredPosition = new Vector2(
            startX[i],
            y
        );

        petals[i].localRotation = Quaternion.Euler(
            0f,
            0f,
            Random.Range(0f, 360f)
        );

        Color color = images[i].color;
        color.a = 1f;
        images[i].color = color;
    }
}