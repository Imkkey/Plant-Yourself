using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Зона для вращения камеры (обычно невидимая панель на половину или весь экран).
/// Настраивается в UI Canvas, перехватывает свайпы/перетаскивания мыши и отправляет в PlayerController.
/// </summary>
public class TouchZone : MonoBehaviour, IDragHandler
{
    // Коэффициент чувствительности прямо на экране, если нужно подкрутить
    [Tooltip("Чувствительность горизонтального-вертикального свайпа. 1 = 1 пиксель")]
    public float sensitivityMultiplier = 1f;

    public void OnDrag(PointerEventData eventData)
    {
        if (MobileInputManager.Instance != null)
        {
            // Передаем точное смещение пальца (в пикселях)
            MobileInputManager.Instance.AddLookDelta(eventData.delta * sensitivityMultiplier);
        }
    }
}
