using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Автоматически управляет интерфейсом игры в зависимости от платформы.
/// </summary>
public class PlatformManager : MonoBehaviour
{
    [Header("Мобильный интерфейс (Кнопки, джойстики)")]
    public List<GameObject> mobileUIElements = new List<GameObject>();

    [Header("ПК интерфейс (Специфичный HUD, подсказки)")]
    public List<GameObject> pcUIElements = new List<GameObject>();

    void Start()
    {
        UpdatePlatformUI();
    }

    private void UpdatePlatformUI()
    {
        // Проверяем, на каком устройстве мы запущены (или под какую платформу билд)
#if UNITY_ANDROID || UNITY_IOS
        SetUIActive(mobileUIElements, true);
        SetUIActive(pcUIElements, false);
#else
        // На ПК (Standalone), в редакторе (Editor) или WebGL
        SetUIActive(mobileUIElements, false);
        SetUIActive(pcUIElements, true);
#endif
    }

    private void SetUIActive(List<GameObject> uiList, bool isActive)
    {
        if (uiList == null) return;
        
        foreach (var ui in uiList)
        {
            if (ui != null)
                ui.SetActive(isActive);
        }
    }
}
