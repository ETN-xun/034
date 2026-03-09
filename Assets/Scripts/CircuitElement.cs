using UnityEngine;

public class CircuitElement : MonoBehaviour
{
    [field: SerializeField]
    public CircuitElementType ElementType { get; private set; }

    [field: SerializeField]
    public bool IsLocked { get; private set; }

    public void SetType(CircuitElementType type)
    {
        ElementType = type;
    }

    public void ToggleLock()
    {
        IsLocked = !IsLocked;
    }
}
