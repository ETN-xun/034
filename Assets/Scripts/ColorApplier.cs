using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ColorApplier : MonoBehaviour
{
    [SerializeField]
    private Color color = Color.white;

    private void Awake()
    {
        var rendererComponent = GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.material.color = color;
        }
    }
}
