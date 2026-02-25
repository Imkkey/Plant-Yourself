using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class PlantSelectionUI : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject selectionPanel;

    [Header("Buttons")]
    [SerializeField] private Button selectOakButton;
    [SerializeField] private Button selectVineButton;

    private static PlantSelectionUI _instance;
    public static PlantSelectionUI Instance => _instance;

    private void Awake()
    {
        _instance = this;
        // По умолчанию панель скрыта, пока игрок не заспавнится
        if (selectionPanel != null) selectionPanel.SetActive(false);

        if (selectOakButton != null) selectOakButton.onClick.AddListener(() => SelectPlant(PlantType.Oak));
        if (selectVineButton != null) selectVineButton.onClick.AddListener(() => SelectPlant(PlantType.Vine));
    }

    public void Show()
    {
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Hide()
    {
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
            if (PlayerController.Local != null)
            {
                PlayerController.Local.LockCursor();
            }
        }
    }

    private void SelectPlant(PlantType type)
    {
        if (PlayerController.Local != null)
        {
            PlayerController.Local.RPC_SetInitialPlant(type);
            Hide();
        }
    }
}
