using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PriceDisplayUI : MonoBehaviour
{
    [Header("Conteúdo do prefab")]
    [SerializeField] private Image productImage;
    [SerializeField] private TMP_Text productNameText;
    [SerializeField] private TMP_InputField priceInput;
    [SerializeField] private Slider discountSlider;
    [SerializeField] private TMP_Text averageCostValue;
    [SerializeField] private TMP_Text discountValue;
    [SerializeField] private TMP_Text discountedPriceValue;
    [SerializeField] private TMP_Text marketPriceValue;
    [SerializeField] private TMP_Text profitValue;

    [Header("Animação")]
    [SerializeField] private Vector2 visiblePosition = Vector2.zero;
    [SerializeField] private Vector2 hiddenPosition = new(0f, -1000f);
    [SerializeField] private float transitionTime = 0.4f;
    [SerializeField] private Ease ease = Ease.OutBack;
    [SerializeField] private UnityEvent onFinishReturn;

    [Header("Desconto")]
    [SerializeField, Range(1f, 25f)] private float discountStep = 5f;
    [SerializeField, Range(0f, 100f)] private float maximumDiscount = 90f;

    private RectTransform rectTransform;
    private Tween positionTween;
    private Tween scaleTween;
    private Vector3 initialScale;
    private float basePrice;
    private float averageCost;
    private float marketPrice;
    private float discountPercent;
    private bool isOpen;

    public float EnteredPrice
    {
        get
        {
            if (priceInput != null && float.TryParse(priceInput.text, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out float value))
                return Mathf.Max(0f, Mathf.Round(value));
            return basePrice;
        }
    }

    public float DiscountPercent => discountPercent;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        initialScale = transform.localScale;
        if (Mathf.Approximately(initialScale.x, 0f)) initialScale.x = 1f;
        if (priceInput != null) priceInput.onValueChanged.AddListener(OnPriceChanged);
        if (discountSlider != null)
        {
            discountSlider.minValue = 0f;
            discountSlider.maxValue = maximumDiscount;
            discountSlider.wholeNumbers = true;
            discountSlider.onValueChanged.AddListener(SetDiscount);
        }
        if (rectTransform != null) rectTransform.anchoredPosition = hiddenPosition;
        transform.localScale = new Vector3(0f, initialScale.y, initialScale.z);
    }

    private void OnDestroy()
    {
        if (priceInput != null) priceInput.onValueChanged.RemoveListener(OnPriceChanged);
        if (discountSlider != null) discountSlider.onValueChanged.RemoveListener(SetDiscount);
    }

    private void Update()
    {
        if (!isOpen) return;
        float direction = Input.mouseScrollDelta.y;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) direction = -1f;
        if (Input.GetKeyDown(KeyCode.RightArrow)) direction = 1f;
        if (direction > 0f) SetDiscount(discountPercent + discountStep);
        else if (direction < 0f) SetDiscount(discountPercent - discountStep);
    }

    public void ShowDisplay(AllIThingsData data, float currentBasePrice, float currentDiscount)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        basePrice = Mathf.Max(0f, Mathf.Round(currentBasePrice));
        marketPrice = data != null ? data.marketPrice : 0f;
        averageCost = CalculateAverageCost(data);

        if (productImage != null)
        {
            productImage.sprite = data != null ? data.itemSprite : null;
            productImage.enabled = productImage.sprite != null;
            productImage.preserveAspect = true;
        }
        if (productNameText != null)
            productNameText.text = data != null && !string.IsNullOrWhiteSpace(data.itemName)
                ? data.itemName : "Produto";
        if (priceInput != null)
            priceInput.SetTextWithoutNotify(Mathf.RoundToInt(basePrice).ToString(CultureInfo.InvariantCulture));

        discountPercent = Mathf.Clamp(currentDiscount, 0f, maximumDiscount);
        if (discountSlider != null) discountSlider.SetValueWithoutNotify(discountPercent);
        RefreshPricingPreview();

        positionTween?.Kill();
        scaleTween?.Kill();
        if (rectTransform != null)
            positionTween = rectTransform.DOAnchorPos(visiblePosition, transitionTime).SetEase(ease);
        transform.localScale = new Vector3(0f, initialScale.y, initialScale.z);
        scaleTween = transform.DOScale(initialScale, transitionTime).SetEase(ease);

        isOpen = true;
        ServiceLocator.Get<ItemRaycastController>()?.SetGeneralCanInteract(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerLook look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = false;
        ServiceLocator.Get<PlayerMotor>()?.SetCanMove(false);
        if (priceInput != null) priceInput.ActivateInputField();
    }

    public void ShowDisplay(float currentPrice, float referenceMarketPrice)
    {
        ShowDisplay(null, currentPrice, 0f);
        marketPrice = referenceMarketPrice;
        RefreshPricingPreview();
    }

    public void SetDiscount(float value)
    {
        discountPercent = Mathf.Clamp(Mathf.Round(value), 0f, maximumDiscount);
        if (discountSlider != null && !Mathf.Approximately(discountSlider.value, discountPercent))
            discountSlider.SetValueWithoutNotify(discountPercent);
        RefreshPricingPreview();
    }

    public void IncreaseDiscount() => SetDiscount(discountPercent + discountStep);
    public void DecreaseDiscount() => SetDiscount(discountPercent - discountStep);

    public void ApplyAndClose()
    {
        ServiceLocator.Get<GlobalPrices>()?.Apply();
        CloseAndRestorePlayer();
    }

    public void CancelAndClose()
    {
        ServiceLocator.Get<GlobalPrices>()?.SetCurrentDisplayNone();
        CloseAndRestorePlayer();
    }

    private void CloseAndRestorePlayer()
    {
        LockMouse();
        CloseDisplay();
        SetCanInteractTrue();
    }

    private void OnPriceChanged(string _) => RefreshPricingPreview();

    private void RefreshPricingPreview()
    {
        float typedPrice = EnteredPrice;
        float finalPrice = Mathf.Round(typedPrice * (1f - discountPercent / 100f));
        float profit = finalPrice - averageCost;
        if (averageCostValue != null) averageCostValue.text = Yen(averageCost);
        if (discountValue != null) discountValue.text = Mathf.RoundToInt(discountPercent) + "%";
        if (discountedPriceValue != null) discountedPriceValue.text = Yen(finalPrice);
        if (marketPriceValue != null) marketPriceValue.text = Yen(marketPrice);
        if (profitValue != null)
        {
            profitValue.text = SignedYen(profit);
            profitValue.color = profit >= 0f
                ? new Color(0.3f, 1f, 0.3f, 1f)
                : new Color(1f, 0.35f, 0.3f, 1f);
        }
    }

    private static float CalculateAverageCost(AllIThingsData data)
    {
        if (data == null) return 0f;
        int unitsPerBox = data.boxGrid != null ? data.boxGrid.TotalCapacity : 1;
        return data.singleItemPrice / Mathf.Max(1, unitsPerBox);
    }

    private static string Yen(float value) =>
        "¥" + Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture);

    private static string SignedYen(float value) =>
        (value < 0f ? "-" : string.Empty) + Yen(Mathf.Abs(value));

    public void CloseDisplay()
    {
        isOpen = false;
        positionTween?.Kill();
        scaleTween?.Kill();
        if (rectTransform != null)
            positionTween = rectTransform.DOAnchorPos(hiddenPosition, transitionTime)
                .SetEase(ease).OnComplete(() => onFinishReturn?.Invoke());
        else
            onFinishReturn?.Invoke();
        scaleTween = transform.DOScale(new Vector3(0f, initialScale.y, initialScale.z),
            transitionTime + 0.1f).SetEase(ease);
    }

    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerLook look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = true;
        ServiceLocator.Get<PlayerMotor>()?.SetCanMove(true);
    }

    public void SetCanInteractTrue() =>
        ServiceLocator.Get<ItemRaycastController>()?.SetGeneralCanInteract(true);

    private void OnDisable()
    {
        isOpen = false;
        positionTween?.Kill();
        scaleTween?.Kill();
    }
}
