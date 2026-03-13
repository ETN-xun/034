using UnityEngine;

public class CircuitElement : MonoBehaviour
{
    public const int DefaultLength = 2;
    public const int DefaultWidth = 2;

    [field: SerializeField]
    public CircuitElementType ElementType { get; private set; }

    [field: SerializeField]
    public bool IsLocked { get; private set; }

    [field: SerializeField]
    public int Length { get; private set; } = DefaultLength;

    [field: SerializeField]
    public int Width { get; private set; } = DefaultWidth;

    public Vector2 OccupiedSize => new Vector2(Length, Width);
    public float LengthScaleMultiplier => Length / (float)DefaultLength;
    public float WidthScaleMultiplier => Width / (float)DefaultWidth;

    private void OnValidate()
    {
        Length = NormalizeDimension(Length);
        Width = NormalizeDimension(Width);
    }

    public void SetType(CircuitElementType type)
    {
        ElementType = type;
    }

    public void ToggleLock()
    {
        IsLocked = !IsLocked;
    }

    public void SetSize(int length, int width)
    {
        Length = NormalizeDimension(length);
        Width = NormalizeDimension(width);
    }

    public Rect GetOccupiedRect()
    {
        var size = OccupiedSize;
        var half = size * 0.5f;
        var center = transform.position;
        return Rect.MinMaxRect(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y);
    }

    private static int NormalizeDimension(int value)
    {
        return Mathf.Max(1, value);
    }
}
