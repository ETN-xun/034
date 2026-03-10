using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BackpackView : MonoBehaviour
{
    // UI元素的预制体和父对象
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentParent;
    private List<ElectronicUIItem> electronicUIItems;
    // 
    [SerializeField] private GameObject componentPrefab;
    // 
    [SerializeField] private Button showHideButton;
    [SerializeField] private RectTransform panel;


    public void Start()
    {
        // 生成一些测试数据，实际上应该从背包系统甚至关卡数据中获取
        var testInventory = new List<ElectronicConfig>
        {
            new ElectronicConfig { Name = "半波发生器", Color = Color.red, Shape = ShapeType.Sine, Rule = RuleType.None },
            new ElectronicConfig { Name = "半波发生器", Color = Color.blue, Shape = ShapeType.Square, Rule = RuleType.None },
            new ElectronicConfig { Name = "半波发生器", Color = Color.green, Shape = ShapeType.Triangle, Rule = RuleType.None },
            new ElectronicConfig { Name = "半波发生器", Color = Color.yellow, Shape = ShapeType.Sine, Rule = RuleType.None },
        };

        Refresh(testInventory);

        // 设置按钮事件
        showHideButton.onClick.AddListener(ShowHide);
    }



    public void Refresh(List<ElectronicConfig> inventoryRecords)
    {
        // 销毁现有的UI项
        if (electronicUIItems != null)
        {
            foreach (var item in electronicUIItems)
            {
                Destroy(item.gameObject);
            }
        }

        electronicUIItems = new List<ElectronicUIItem>();

        // 创建新的UI项
        foreach (var record in inventoryRecords)
        {
            var itemGO = Instantiate(itemPrefab, contentParent);
            var item = itemGO.GetComponent<ElectronicUIItem>();
            item.SetData(record);
            item.OnItemClicked += OnItemClicked;
            electronicUIItems.Add(item);
        }
    }

    public void OnItemClicked(ElectronicConfig config)
    {
        var componentGO = Instantiate(componentPrefab);
        var component = componentGO.GetComponent<ElectronicComponent>();
        component.SetData(config);
        componentGO.transform.position = Vector3.zero; // 可以根据需要设置初始位置
    }


    private bool isVisible = true;
    private Sequence showHideSequence;
    private void ShowHide()
    {
        if (isVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
        isVisible = !isVisible;
    }
    private void Show()
    {
        if (showHideSequence != null)
        {
            showHideSequence.Kill(true);
        }
        showHideSequence = DOTween.Sequence();
        // 右滑弹跳显示
        showHideSequence.Append(panel.DOAnchorPosX(panel.rect.width/2, 0.5f).SetEase(Ease.OutBack));
    }

    private void Hide()
    {
        if (showHideSequence != null)
        {
            showHideSequence.Kill(true);
        }
        showHideSequence = DOTween.Sequence();
        // 左滑隐藏
        showHideSequence.Append(panel.DOAnchorPosX(-panel.rect.width/2, 0.5f).SetEase(Ease.OutBack));
    }
}
