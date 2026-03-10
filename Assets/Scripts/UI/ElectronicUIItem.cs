using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ElectronicUIItem : MonoBehaviour, IPointerClickHandler
{
    public ElectronicConfig Config;
    public event System.Action<ElectronicConfig> OnItemClicked;

    public void SetData(ElectronicConfig config)
    {
        this.Config = config;
        var image = GetComponent<Image>();
        image.color = config.Color;
        Texture2D texture = TextureCreator.CreateTexture(config.Color, config.Shape);
        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 右键点击时显示上下文菜单
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 显示上下文菜单的逻辑



        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 左键点击时触发事件
            OnItemClicked?.Invoke(Config);

            Destroy(gameObject); // 点击后销毁UI项
        }
    }

    public void OnDestroy()
    {

        OnItemClicked = null;
    }
}
