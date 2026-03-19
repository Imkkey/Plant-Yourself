using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// 3. Главный менеджер UI инвентаря
public class InventoryUI : MonoBehaviour
{
    [Header("UI Панель (со слотами)")]
    public GameObject InventoryPanel; 
    
    [Header("Слоты - Вручную добавьте сюда 4 слота по порядку")]
    public List<InventorySlotUI> Slots;

    [Header("Анимация (выдвижение вверх)")]
    public float YOffset = 50f;     // На сколько пикселей вверх поднимется при TAB
    public float AnimSpeed = 15f;   // Скорость выезжания

    private ItemDatabase _db;
    private bool _isInventoryOpen = false;

    private RectTransform _panelRect;
    private Vector2 _basePos;
    private Vector2 _targetPos;

    void Start()
    {
        _db = Resources.Load<ItemDatabase>("ItemDatabase");
        
        if (InventoryPanel != null) 
        {
            InventoryPanel.SetActive(true); // Теперь панель активна всегда (чтобы видеть вещи всё время)
            _panelRect = InventoryPanel.GetComponent<RectTransform>();
            if (_panelRect != null)
            {
                _basePos = _panelRect.anchoredPosition;
                _targetPos = _basePos; // Изначально опущена
            }
        }
    }
    
    void Update()
    {
        if (PlayerController.Local == null) return;
        
        var kb = Keyboard.current;
        if (kb != null && InventoryPanel != null && _panelRect != null) 
        {
            if (kb.tabKey.isPressed)
            {
                _isInventoryOpen = true;
                _targetPos = _basePos + new Vector2(0, YOffset); // Поднимаем
            }
            else 
            {
                _isInventoryOpen = false;
                _targetPos = _basePos; // Опускаем обратно
            }

            // Плавное движение
            _panelRect.anchoredPosition = Vector2.Lerp(_panelRect.anchoredPosition, _targetPos, Time.deltaTime * AnimSpeed);
        }

        var invSys = PlayerController.Local.GetComponent<InventorySystem>();
        if (invSys != null && _db != null && Slots != null)
        {
            for (int i = 0; i < Slots.Count && i < invSys.Slots.Length; i++)
            {
                if (Slots[i] == null || Slots[i].ItemUI == null) continue;

                int networkItemId = invSys.Slots[i];
                
                Slots[i].SlotIndex = i;
                Slots[i].ItemUI.currentSlot = Slots[i]; 

                if (Slots[i].ItemUI.ItemID != networkItemId)
                {
                    var asset = _db.GetItem(networkItemId);
                    Slots[i].ItemUI.Setup(asset);
                }
            }
        }
    }
}
