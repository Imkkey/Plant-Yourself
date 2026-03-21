using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemAsset> Items = new List<ItemAsset>();

    public ItemAsset GetItem(int id)
    {
        if (id <= 0) return null;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] != null && Items[i].ItemID == id)
                return Items[i];
        }
        return null;
    }
}
