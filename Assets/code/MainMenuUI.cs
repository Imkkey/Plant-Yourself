using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Кнопки Меню (Слева)")]
    public Button btnPlay;
    public Button btnNews;
    public Button btnSettings;
    public Button btnExit;

    [Header("Панели (Справа)")]
    public GameObject panelPlay;
    public GameObject panelNews;
    public GameObject panelSettings;

    [Header("Подключение (Внутри Panel Play)")]
    public TMP_InputField inputNickname;
    [Tooltip("Поле для ввода кода (4 цифры)")]
    public TMP_InputField inputRoomName;
    [Tooltip("Создать свою комнату")]
    public Button btnHost;
    [Tooltip("Войти по коду")]
    public Button btnJoin;

    [Header("Кастомное Лобби")]
    [Tooltip("Сюда закинь панель лобби, которая будет показываться вместо Play")]
    public GameObject panelLobby;
    [Tooltip("Текст, куда скрипт впишет код из 4 цифр")]
    public TMP_Text textRoomCode;
    [Tooltip("Кнопка 'Начать Игру' (Только для хоста)")]
    public Button btnStartGame;
    [Tooltip("Кнопка выхода из лобби обратно в меню")]
    public Button btnLeaveLobby;

    [Header("Список игроков (Scroll View)")]
    [Tooltip("Объект Content внутри твоего Scroll View")]
    public Transform playerListContent;
    [Tooltip("Префаб плашки игрока (должен иметь скрипт LobbyPlayerItem)")]
    public GameObject playerLobbyItemPrefab;

    [Header("Настройки Камеры")]
    [Tooltip("Перетащи сюда главную камеру меню")]
    public Camera menuCamera;
    [Tooltip("Скорость движения камеры")]
    public float cameraMoveSpeed = 0.5f;
    [Tooltip("Амплитуда движения камеры (на сколько далеко она отходит от центра)")]
    public float cameraAmplitude = 1.5f;

    private Vector3 _cameraInitialPos;
    private int _lastPlayerCount = -1;
    // Кэш текущих имён, чтобы не перерисовывать список просто так
    private string _lastPlayersCache = "";

    private void Start()
    {
        // Привязываем кнопки бокового меню
        if (btnPlay != null) btnPlay.onClick.AddListener(() => SwitchPanel(panelPlay));
        if (btnNews != null) btnNews.onClick.AddListener(() => SwitchPanel(panelNews));
        if (btnSettings != null) btnSettings.onClick.AddListener(() => SwitchPanel(panelSettings));
        if (btnExit != null) btnExit.onClick.AddListener(ExitGame);

        // Привязываем кнопки хоста и джоина (Внутри панели Play)
        if (btnHost != null) btnHost.onClick.AddListener(() => StartGame("Host"));
        if (btnJoin != null) btnJoin.onClick.AddListener(() => StartGame("Client"));

        // Восстанавливаем만 никнейм
        if (PlayerPrefs.HasKey("SavedNickname") && inputNickname != null)
            inputNickname.text = PlayerPrefs.GetString("SavedNickname");

        // Привязываем кнопки лобби
        if (btnStartGame != null) btnStartGame.onClick.AddListener(() => {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsServer())
                NetworkManager.Instance.StartGameScene("GameScene");
        });
        
        if (btnLeaveLobby != null) btnLeaveLobby.onClick.AddListener(() => {
            if (NetworkManager.Instance != null) NetworkManager.Instance.Disconnect();
            if (panelLobby != null) panelLobby.SetActive(false);
            _lastPlayerCount = -1;
            _lastPlayersCache = "";
        });

        // При старте скрываем лобби и закрываем все панели
        if (panelLobby != null) panelLobby.SetActive(false);
        SwitchPanel(null);

        // Сохраняем начальную позицию камеры
        if (menuCamera != null)
        {
            _cameraInitialPos = menuCamera.transform.position;
        }
        else
        {
            menuCamera = Camera.main;
            if (menuCamera != null) _cameraInitialPos = menuCamera.transform.position;
        }
    }

    private void Update()
    {
        // Постоянно обновляем окно кастомного лобби, если мы в нём
        if (panelLobby != null && panelLobby.activeSelf && NetworkManager.Instance != null)
        {
            // Если нас отключили или кикнули — закрываем лобби и возвращаемся в меню
            if (!NetworkManager.Instance.IsSessionActive())
            {
                panelLobby.SetActive(false);
                _lastPlayerCount = -1;
                _lastPlayersCache = "";
                return;
            }

            string roomCode = NetworkManager.Instance.GetRoomCode();

            // Обновление кода комнаты
            if (textRoomCode != null)
            {
                textRoomCode.text = string.IsNullOrEmpty(roomCode) ? "" : roomCode;
            }

            // Обновление кнопки старта
            bool isHost = NetworkManager.Instance.IsServer();
            if (btnStartGame != null) btnStartGame.interactable = isHost;

            // Обновление списка игроков и кнопок кика
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

            // Сортируем игроков: Хост всегда первый (сверху), остальные по времени захода (по их сетевому ID)
            System.Array.Sort(players, (a, b) => {
                if (a.IsHostPlayer && !b.IsHostPlayer) return -1;
                if (!a.IsHostPlayer && b.IsHostPlayer) return 1;
                if (a.Object != null && b.Object != null && a.Object.IsValid && b.Object.IsValid)
                    return a.Object.Id.Raw.CompareTo(b.Object.Id.Raw);
                return 0;
            });
            
            // Генерируем "хэш" из всех имён, чтобы понять, изменился ли состав
            string currentCache = "";
            foreach (var p in players)
            {
                currentCache += p.PlayerName.ToString() + p.IsHostPlayer.ToString();
            }

            // Перерисовываем список, только если кто-то зашел/вышел/изменил имя
            if (players.Length != _lastPlayerCount || currentCache != _lastPlayersCache)
            {
                _lastPlayerCount = players.Length;
                _lastPlayersCache = currentCache;

                // Удаляем старые плашки игроков из списка
                if (playerListContent != null)
                {
                    foreach (Transform child in playerListContent)
                    {
                        Destroy(child.gameObject);
                    }
                }

                // Создаем новые плашки
                if (playerListContent != null && playerLobbyItemPrefab != null)
                {
                    foreach (var p in players)
                    {
                        GameObject newPlayerItem = Instantiate(playerLobbyItemPrefab, playerListContent);
                        LobbyPlayerItem itemScript = newPlayerItem.GetComponent<LobbyPlayerItem>();

                        if (itemScript != null)
                        {
                            // Устанавливаем имя
                            if (itemScript.textName != null)
                            {
                                itemScript.textName.text = p.PlayerName.ToString() + (p.IsHostPlayer ? " [HOST]" : "");
                            }

                            // Настраиваем кнопку кика (крестик)
                            if (itemScript.btnKick != null)
                            {
                                // Кнопку показываем только если МЫ хост, и игрок на этой плашке НЕ хост
                                bool canKick = isHost && !p.IsHostPlayer;
                                itemScript.btnKick.gameObject.SetActive(canKick);

                                // Назначаем действие на кнопку
                                // Нужно захватить ссылку для лямбды, чтобы она работала корректно
                                Fusion.PlayerRef playerRefToKick = p.Object.InputAuthority;
                                itemScript.btnKick.onClick.AddListener(() => {
                                    if (NetworkManager.Instance != null && NetworkManager.Instance.IsServer())
                                    {
                                        NetworkManager.Instance.KickPlayer(playerRefToKick);
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }

        // Движение камеры влево-вправо
        if (menuCamera != null)
        {
            float offset = Mathf.Sin(Time.time * cameraMoveSpeed) * cameraAmplitude;
            menuCamera.transform.position = _cameraInitialPos + menuCamera.transform.right * offset;
        }
    }

    private void SwitchPanel(GameObject targetPanel)
    {
        // Проверяем, открыта ли уже эта панель
        bool isAlreadyActive = targetPanel != null && targetPanel.activeSelf;

        // Выключаем вообще все панели
        if (panelPlay != null) panelPlay.SetActive(false);
        if (panelNews != null) panelNews.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(false);

        // Если панель не была открыта, то включаем её (работает как переключатель)
        if (!isAlreadyActive && targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
    }

    private async void StartGame(string mode)
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("На сцене нет NetworkManager! Перетащите его в эту сцену из игры.");
            return;
        }

        // 1. Сохраняем имя
        if (inputNickname != null && !string.IsNullOrWhiteSpace(inputNickname.text))
        {
            NetworkManager.LocalPlayerName = inputNickname.text.Trim();
        }
        else
        {
            NetworkManager.LocalPlayerName = "Игрок " + Random.Range(100, 999);
        }

        PlayerPrefs.SetString("SavedNickname", NetworkManager.LocalPlayerName);
        PlayerPrefs.Save();

        // 2. Пробуем запустить сервер или подключиться к игре
        bool success = false;
        
        if (mode == "Host")
        {
            // Случайный код из 4 цифр для друга
            string randomCode = Random.Range(1000, 10000).ToString();
            success = await NetworkManager.Instance.StartHost(randomCode);
        }
        else if (mode == "Client")
        {
            // Подключаемся по коду из инпута
            string roomCode = "GameRoom";
            if (inputRoomName != null && !string.IsNullOrWhiteSpace(inputRoomName.text))
            {
                roomCode = inputRoomName.text.Trim();
            }
            success = await NetworkManager.Instance.StartClient(roomCode);
        }

        // 3. Если подключение успешно - открываем наш UI Лобби
        if (success)
        {
            if (panelLobby != null) panelLobby.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Не удалось войти в игру (комната не найдена или ошибка сети).");
            // Если была открыта, на всякий случай закрываем (должно быть закрыто)
            if (panelLobby != null) panelLobby.SetActive(false);
        }
    }

    private void ExitGame()
    {
        Debug.Log("Выход из игры...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
