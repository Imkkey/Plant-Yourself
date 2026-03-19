using UnityEngine;
using UnityEngine.EventSystems;

// 2. Логика слота как приемника
public class InventorySlotUI : MonoBehaviour, IDropHandler
{
    public int SlotIndex; 
    public DraggableItem ItemUI; 

    public void OnDrop(PointerEventData eventData)
    {
        DraggableItem droppedItem = eventData.pointerDrag?.GetComponent<DraggableItem>();
        if (droppedItem == null || droppedItem.ItemID == 0) return;

        InventorySlotUI fromSlot = droppedItem.currentSlot;
        if (fromSlot == null || fromSlot == this) return; 

        if (PlayerController.Local != null)
        {
            var invSys = PlayerController.Local.GetComponent<InventorySystem>();
            if (invSys != null)
            {
                invSys.RPC_SwapSlots(fromSlot.SlotIndex, this.SlotIndex);
            }
        }
    }
}
