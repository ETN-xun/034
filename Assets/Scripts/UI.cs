using DG.Tweening;
using UnityEngine;
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
        }
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
}
