using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 1. Логика перетаскивания иконки
[RequireComponent(typeof(Image))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Transform parentAfterDrag;
    [HideInInspector] public InventorySlotUI currentSlot;
    
    private Image _image;
    public int ItemID; 
    
    private GameObject _draggingIcon; 
    private RectTransform _dragRect;
    
    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    public void Setup(ItemAsset item)
    {
        if (item == null || item.ItemID == 0)
        {
            ItemID = 0;
            _image.sprite = null;
            _image.color = new Color(0, 0, 0, 0); 
            _image.raycastTarget = false;
        }
        else
        {
            ItemID = item.ItemID;
            _image.sprite = item.Icon;
            _image.color = Color.white;
            _image.raycastTarget = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ItemID == 0) return;

        _draggingIcon = new GameObject("DraggingIcon");
        Canvas canvas = GetComponentInParent<Canvas>();
        _draggingIcon.transform.SetParent(canvas.transform, false);
        _draggingIcon.transform.SetAsLastSibling();

        Image dragImage = _draggingIcon.AddComponent<Image>();
        dragImage.sprite = _image.sprite;
        dragImage.raycastTarget = false; 
        
        _dragRect = _draggingIcon.GetComponent<RectTransform>();
        _dragRect.sizeDelta = GetComponent<RectTransform>().rect.size;

        _image.color = new Color(1, 1, 1, 0.3f); 
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_draggingIcon != null)
        {
            _dragRect.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_draggingIcon != null)
        {
            Destroy(_draggingIcon);
        }

        _image.color = Color.white;
        
        GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
        if (dropTarget == null || dropTarget.GetComponentInParent<InventorySlotUI>() == null)
        {
            if (PlayerController.Local != null)
            {
                var invSys = PlayerController.Local.GetComponent<InventorySystem>();
                if (invSys != null && currentSlot != null)
                {
                    invSys.RPC_DropItem(currentSlot.SlotIndex);
                }
            }
        }
    }
}
