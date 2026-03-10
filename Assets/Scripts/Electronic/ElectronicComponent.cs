using System.Data;
using UnityEngine;


/// <summary>
/// 电子元器件
/// </summary>

public class ElectronicComponent : MonoBehaviour
{
    public ElectronicConfig Config;

    public void SetData(ElectronicConfig config)
    {
        this.Config = config;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = config.Color;
            Texture2D texture = TextureCreator.CreateTexture(config.Color, config.Shape);
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
        }

        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            // 根据形状调整碰撞体大小
            switch (config.Shape)
            {
                case ShapeType.Sine:
                    collider.offset = Vector2.zero;
                    collider.transform.localScale = Vector3.one * 0.8f;
                    break;
                case ShapeType.Square:
                    collider.offset = Vector2.zero;
                    collider.transform.localScale = Vector3.one * 0.9f;
                    break;
                case ShapeType.Triangle:
                    collider.offset = Vector2.zero;
                    collider.transform.localScale = Vector3.one * 0.85f;
                    break;
                default:
                    collider.offset = Vector2.zero;
                    collider.transform.localScale = Vector3.one;
                    break;
            }
        }
    }


    void Start()
    {
        // 自动加上Draggable组件
        if (GetComponent<DraggablePlacedComponent>() == null)        {
            gameObject.AddComponent<DraggablePlacedComponent>();
        }
    }
}






