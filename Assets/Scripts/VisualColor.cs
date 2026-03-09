using UnityEngine;

public class VisualColor : MonoBehaviour
{
    public enum Preset
    {
        Generator,
        Receiver,
        Terminal,
        Backpack
    }

    [SerializeField]
    private Preset preset;

    private void Awake()
    {
        var rendererComponent = GetComponent<Renderer>();
        if (rendererComponent == null)
        {
            return;
        }

        rendererComponent.material.color = GetColor();
    }

    private Color GetColor()
    {
        switch (preset)
        {
            case Preset.Generator:
                return new Color(0.2f, 0.45f, 1f, 1f);
            case Preset.Receiver:
                return new Color(1f, 0.2f, 0.2f, 1f);
            case Preset.Terminal:
                return new Color(1f, 0.92f, 0.1f, 1f);
            case Preset.Backpack:
                return new Color(0.15f, 0.15f, 0.15f, 1f);
            default:
                return Color.white;
        }
    }
}
