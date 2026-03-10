using UnityEngine;

public class CircuitElement : MonoBehaviour
{
    public const float OccupiedWidth = 2f;
    public const float OccupiedHeight = 2f;

    [field: SerializeField]
    public CircuitElementType ElementType { get; private set; }

    [field: SerializeField]
    public bool IsLocked { get; private set; }

    public Vector2 OccupiedSize => new Vector2(OccupiedWidth, OccupiedHeight);

    public void SetType(CircuitElementType type)
    {
        ElementType = type;
    }

    public void ToggleLock()
    {
        IsLocked = !IsLocked;
    }

    public Rect GetOccupiedRect()
    {
        var size = OccupiedSize;
        var half = size * 0.5f;
        var center = transform.position;
        return Rect.MinMaxRect(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y);
    }
}
