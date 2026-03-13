using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI Instance { get; private set; }
    public static bool IsBackpackOpen => Instance == null || Instance.isShowing;
    public static bool ShouldRenderBackpackUI => Instance == null || Instance.backpackOpenProgress > 0.001f;
    public static float BackpackOpenProgress => Instance == null ? 1f : Instance.backpackOpenProgress;

    [SerializeField] private Button backPackBtn;
    [SerializeField] private bool isShowing = true;
    [SerializeField] private float toggleDuration = 0.5f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    private Tween backpackToggleTween;
    private float backpackOpenProgress = 1f;
    private Canvas backpackButtonCanvas;
    private BackpackButtonDragHandler backpackButtonDragHandler;

    private void Awake()
    {
        Instance = this;
        backpackOpenProgress = isShowing ? 1f : 0f;
    }

    private void Start()
    {
        if (backPackBtn != null)
        {
            backPackBtn.onClick.AddListener(ToggleBackpackUI);
            ConfigureBackpackButton();
        }
    }

    private void LateUpdate()
    {
        EnsureBackpackButtonOnTop();
    }

    private void OnDestroy()
    {
        if (backpackToggleTween != null && backpackToggleTween.IsActive())
        {
            backpackToggleTween.Kill();
        }

        if (backPackBtn != null)
        {
            backPackBtn.onClick.RemoveListener(ToggleBackpackUI);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void ToggleBackpackUI()
    {
        if (backpackButtonDragHandler != null && backpackButtonDragHandler.ConsumeClickSuppression())
        {
            return;
        }

        if (backpackToggleTween != null && backpackToggleTween.IsActive())
        {
            backpackToggleTween.Kill();
        }

        isShowing = !isShowing;
        var targetProgress = isShowing ? 1f : 0f;
        var easing = isShowing ? openEase : closeEase;
        backpackToggleTween = DOTween
            .To(() => backpackOpenProgress, value => backpackOpenProgress = value, targetProgress, toggleDuration)
            .SetEase(easing);
    }

    private void ConfigureBackpackButton()
    {
        if (backPackBtn == null)
        {
            return;
        }

        EnsureBackpackButtonOnTop();
        backpackButtonDragHandler = backPackBtn.GetComponent<BackpackButtonDragHandler>();
        if (backpackButtonDragHandler == null)
        {
            backpackButtonDragHandler = backPackBtn.gameObject.AddComponent<BackpackButtonDragHandler>();
        }

        backpackButtonDragHandler.Initialize(backPackBtn.GetComponent<RectTransform>());
    }

    private void EnsureBackpackButtonOnTop()
    {
        if (backPackBtn == null)
        {
            return;
        }

        var buttonTransform = backPackBtn.transform;
        buttonTransform.SetAsLastSibling();
        if (backpackButtonCanvas == null)
        {
            backpackButtonCanvas = backPackBtn.GetComponent<Canvas>();
            if (backpackButtonCanvas == null)
            {
                backpackButtonCanvas = backPackBtn.gameObject.AddComponent<Canvas>();
            }
        }

        backpackButtonCanvas.overrideSorting = true;
        backpackButtonCanvas.sortingOrder = 999;
        if (backPackBtn.GetComponent<GraphicRaycaster>() == null)
        {
            backPackBtn.gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}

public class BackpackButtonDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform buttonRect;
    private RectTransform parentRect;
    private Vector2 pointerOffset;
    private bool suppressNextClick;

    public void Initialize(RectTransform rectTransform)
    {
        buttonRect = rectTransform;
        parentRect = buttonRect == null ? null : buttonRect.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        suppressNextClick = true;
        if (buttonRect == null)
        {
            buttonRect = transform as RectTransform;
            parentRect = buttonRect == null ? null : buttonRect.parent as RectTransform;
        }

        if (buttonRect == null || parentRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out var localPoint);
        pointerOffset = buttonRect.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (buttonRect == null || parentRect == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            return;
        }

        buttonRect.anchoredPosition = localPoint + pointerOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public bool ConsumeClickSuppression()
    {
        if (!suppressNextClick)
        {
            return false;
        }

        suppressNextClick = false;
        return true;
    }
}
