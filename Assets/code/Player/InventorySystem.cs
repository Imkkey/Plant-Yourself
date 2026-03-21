using UnityEngine;
using System.Collections.Generic;
using Fusion;

// 4. Сам инвентарь игрока
[RequireComponent(typeof(NetworkObject))]
public class InventorySystem : NetworkBehaviour
{
    [Networked, Capacity(4)]
    public NetworkArray<int> Slots { get; }

    public bool TryPickupItem(int itemId)
    {
        if (!HasStateAuthority) return false;

        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i] == 0) // Empty slot found
            {
                Slots.Set(i, itemId);
                return true;
            }
        }
        return false;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= Slots.Length || indexB < 0 || indexB >= Slots.Length) return;

        int temp = Slots[indexA];
        Slots.Set(indexA, Slots[indexB]);
        Slots.Set(indexB, temp);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_DropItem(int index)
    {
        if (index < 0 || index >= Slots.Length) return;

        int itemId = Slots[index];
        if (itemId == 0) return;
        
        Slots.Set(index, 0); 
        
        var db = Resources.Load<ItemDatabase>("ItemDatabase");
        if (db != null) 
        {
            var item = db.GetItem(itemId);
            if (item != null && item.WorldPrefab.IsValid) 
            {
                Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
                Runner.Spawn(item.WorldPrefab, dropPos, Quaternion.identity, Object.InputAuthority);
            }
        }
    }
}
