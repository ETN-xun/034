using UnityEngine;
using System.Collections.Generic;

public class BackpackInventoryState : MonoBehaviour
{
    [System.Serializable]
    public class InventoryRecord
    {
        public CircuitElementType type;
        public int count;
        public string prefabResourcePath;
    }

    [SerializeField]
    private List<InventoryRecord> records = new List<InventoryRecord>();

    private void Awake()
    {
        if (records != null && records.Count > 0)
        {
            BackpackItemSpawner.ApplyInventorySnapshot(records);
        }
    }

    public void SetInventory(List<InventoryRecord> newRecords)
    {
        records = newRecords ?? new List<InventoryRecord>();
    }
}
