using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class GameStatsUI : MonoBehaviour
{
    [Header("UI Элемент")]
    [Tooltip("Перетащи сюда TextMeshPro - Text для вывода статистики")]
    [SerializeField] private TMP_Text statsText;
    
    [Header("Шкала Удобрения")]
    [Tooltip("Перетащи сюда Slider, который будет служить шкалой удобрения")]
    [SerializeField] private Slider fertilizerSlider;
    
    [Header("Настройки")]
    [SerializeField] private string gameVersion = "1.0 Alpha";
    [SerializeField] private float updateInterval = 0.5f; // Как часто обновлять текст (раз в 0.5с)

    private float _timer;
    private int _frameCount;
    private float _deltaTime;
    private float _fps;

    private NetworkRunner _runner;

    private void Update()
    {
        // Плавное обновление шкалы удобрения (каждый кадр)
        if (fertilizerSlider != null && PlayerController.Local != null && PlayerController.Local.Grower != null)
        {
            // Плавно меняем значение слайдера для красоты (от 0 до 1, так как Fertilizer от 0 до 100)
            float targetValue = PlayerController.Local.Grower.Fertilizer / 100f;
            fertilizerSlider.value = Mathf.Lerp(fertilizerSlider.value, targetValue, Time.deltaTime * 10f);
        }

        // Подсчет FPS
        _frameCount++;
        _deltaTime += Time.unscaledDeltaTime;
        
        // Обновляем текст каждые полсекунды, чтобы он не так сильно мелькал
        if (_deltaTime > updateInterval)
        {
            _fps = _frameCount / _deltaTime;
            _frameCount = 0;
            _deltaTime -= updateInterval;
            
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (statsText == null) return;

        // Ищем раннер (если он запущен или мы переподключились)
        if (_runner == null || !_runner.IsRunning)
        {
            _runner = FindAnyObjectByType<NetworkRunner>();
        }

        string pingStr = "Offline";
        string regionStr = "N/A";
        string roomStr = "N/A";
        string playersStr = "0";
        string modeStr = "Offline";
        string plantStr = "None";

        if (PlayerController.Local != null && PlayerController.Local.Grower != null)
        {
            plantStr = PlayerController.Local.Grower.CurrentPlant.ToString();
        }

        if (_runner != null && _runner.IsRunning)
        {
            // Пинг (RTT = Round Trip Time)
            double rtt = _runner.GetPlayerRtt(_runner.LocalPlayer) * 1000.0;
            pingStr = $"{rtt:0} ms";

            // Информация о режиме
            modeStr = _runner.GameMode.ToString();

            // Инфа по текущей комнате
            if (_runner.SessionInfo != null && _runner.SessionInfo.IsValid)
            {
                roomStr = _runner.SessionInfo.Name;
                regionStr = string.IsNullOrWhiteSpace(_runner.SessionInfo.Region) ? "Auto" : _runner.SessionInfo.Region;
                playersStr = $"{_runner.SessionInfo.PlayerCount} / {_runner.SessionInfo.MaxPlayers}";
            }
        }

        // Формируем красивый текст с поддержкой цветов HTML
        string text = $"<color=#00FF00><b>FPS:</b></color> {_fps:0}\n";
        text += $"<color=#00FFFF><b>Ping:</b></color> {pingStr}\n";
        text += $"<color=#FFA500><b>Room:</b></color> {roomStr}\n";
        text += $"<color=#FFFF00><b>Region:</b></color> {regionStr}\n";
        text += $"<color=#FF69B4><b>Players:</b></color> {playersStr}\n";
        text += $"<color=#ADD8E6><b>Mode:</b></color> {modeStr}\n";
        text += $"<color=#00FA9A><b>Plant:</b></color> {plantStr}\n";
        text += $"<color=#AAAAAA><b>Version:</b></color> {gameVersion}";

        statsText.text = text;
    }
    public void Disconnect()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Disconnect();
        }
        else if (_runner != null)
        {
            _runner.Shutdown();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    [Header("Панель выбора растения (для кнопок)")]
    [Tooltip("Необязательно. Если указать панель, кнопки выбора её закроют и вернут курсор в игру")]
    [SerializeField] private GameObject plantSelectionPanel;

    public void SelectOak()
    {
        SelectPlant(PlantType.Oak);
    }

    public void SelectVine()
    {
        SelectPlant(PlantType.Vine);
    }

    public void SelectChamomile()
    {
        SelectPlant(PlantType.Chamomile);
    }

    private void SelectPlant(PlantType type)
    {
        if (PlayerController.Local != null)
        {
            PlayerController.Local.RPC_SetInitialPlant(type);

            // Закрываем панель выбора растения
            if (PlantSelectionUI.Instance != null)
            {
                PlantSelectionUI.Instance.Hide();
            }
            else if (plantSelectionPanel != null)
            {
                plantSelectionPanel.SetActive(false);
                PlayerController.Local.LockCursor();
            }
        }
    }
}
