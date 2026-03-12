using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{

    [SerializeField] private Button backPackBtn;
    [SerializeField] private GameObject backPackPanel;

    void Start()
    {
        backPackBtn.onClick.AddListener(backPackAnimationPlay);
    }


    private Tween backPackAnimationTween;
    private bool isShowing = true;
    public float scaleY = 10f;

    private void backPackAnimationPlay()
    {
        if (backPackAnimationTween.IsActive())
        {
            backPackAnimationTween.Kill();
        }

        if (isShowing)
        {
            backPackAnimationTween = backPackPanel.transform.DOScaleY(0f, 0.5f).SetEase(Ease.InBack);
        }
        else
        {
            backPackAnimationTween = backPackPanel.transform.DOScaleY(scaleY, 0.5f).SetEase(Ease.OutBack);
        }
        isShowing = !isShowing;
    }







}
