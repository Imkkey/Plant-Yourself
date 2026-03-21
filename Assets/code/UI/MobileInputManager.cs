using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Простой менеджер мобильного ввода для управления с экрана.
/// Поместите этот скрипт на пустой объект на сцене (или Canvas).
/// Для джойстика можете использовать стандартный OnScreenStick от Unity или вызывать SetMove().
/// Для поворота камеры создайте полноэкранную панель (Image с alpha=0) 
/// и добавьте компонент EventTrigger (Drag) -> вызывать OnCameraDrag.
/// </summary>
public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance;

    [Header("Текущие значения")]
    public Vector2 Move;
    public Vector2 LookDelta;

    // Состояния кнопок
    public bool Jump;
    public bool Dash;
    public bool Crouch;
    public bool Action;
    public bool Interact;
    public bool PlantOak;
    public bool PlantVine;
    public bool PlantChamomile;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        // Сбрасываем свайп камеры каждый кадр после его прочтения в PlayerController
        LookDelta = Vector2.zero;
        
        // (Свайпы теперь обрабатываются через скрипт TouchZone на UI панели)
    }

    // --- МЕТОДЫ ДЛЯ СВАЙПОВ КАМЕРЫ ---
    public void AddLookDelta(Vector2 delta)
    {
        LookDelta += delta;
    }

    // --- МЕТОДЫ ДЛЯ UI КНОПОК (EventTrigger: PointerDown / PointerUp) ---
    // На каждую UI кнопку добавьте EventTrigger (PointerDown -> SetJump(true), PointerUp -> SetJump(false))

    public void SetJump(bool val) { Jump = val; }
    public void SetDash(bool val) { Dash = val; }
    public void SetCrouch(bool val) { Crouch = val; }
    public void SetAction(bool val) { Action = val; }
    public void SetInteract(bool val) { Interact = val; }
    
    public void SetPlantOak(bool val) { PlantOak = val; }
    public void SetPlantVine(bool val) { PlantVine = val; }
    public void SetPlantChamomile(bool val) { PlantChamomile = val; }

    // Метод для выхода из растения по кнопке на экране
    public void ExitPlantForm()
    {
        if (PlayerController.Local != null && PlayerController.Local.Grower != null)
        {
            PlayerController.Local.Grower.ExitPlantForm();
        }
    }

    // Метод для джойстика
    public void SetMove(Vector2 moveInput)
    {
        Move = moveInput;
    }
}
