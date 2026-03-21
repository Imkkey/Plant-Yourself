using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Кастомный экранный джойстик (Virtual Joystick).
/// Подключается напрямую к нашему MobileInputManager!
/// </summary>
public class CustomJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Визуальные элементы")]
    [Tooltip("Задний фон (база) джойстика")]
    public RectTransform background;
    
    [Tooltip("Сама кнопочка (ручка), которую мы тянем")]
    public RectTransform handle;

    [Header("Параметры")]
    [Tooltip("На сколько пикселей можно оттягивать ручку от центра")]
    public float handleRange = 100f;

    private Vector2 inputVector = Vector2.zero;

    private void Start()
    {
        // Если забыли указать ссылки, скрипт попытается найти всё сам
        if (background == null) 
            background = GetComponent<RectTransform>();
            
        if (handle == null && transform.childCount > 0) 
            handle = transform.GetChild(0).GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData); // Сразу реагируем на касание
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position;
        
        // Переводим координаты экрана в координаты нашего UI-объекта
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position))
        {
            // Ограничиваем сдвиг радиусом джойстика (чтобы ручка не вылетала за края)
            position = Vector2.ClampMagnitude(position, handleRange);

            // Визуально сдвигаем ручку
            if (handle != null)
            {
                handle.anchoredPosition = position;
            }

            // Высчитываем нормализованное направление (значения от -1 до 1)
            inputVector = position / handleRange;

            // Передаем значения WASD скрипту MobileInputManager
            if (MobileInputManager.Instance != null)
            {
                MobileInputManager.Instance.SetMove(inputVector);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Когда отпускаем палец — возвращаем ручку в центр
        inputVector = Vector2.zero;
        
        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        // Останавливаем персонажа
        if (MobileInputManager.Instance != null)
        {
            MobileInputManager.Instance.SetMove(Vector2.zero);
        }
    }
}
