using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.RawImage backgroundImage;
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.04f, 0.02f);

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<UnityEngine.UI.RawImage>();
    }

    private void Update()
    {
        if (backgroundImage == null) return;

        Rect uv = backgroundImage.uvRect;
        Vector2 delta = scrollSpeed * Time.unscaledDeltaTime;
        uv.x = Mathf.Repeat(uv.x + delta.x, 1f);
        uv.y = Mathf.Repeat(uv.y + delta.y, 1f);
        backgroundImage.uvRect = uv;
    }
}
