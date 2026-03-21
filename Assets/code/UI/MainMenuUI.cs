using System.Collections;
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

    [Header("Анимация Стебля (Круглое меню)")]
    public RectTransform stemTransform;
    [Tooltip("Целевой угол для меню Play (против часовой = положительный)")]
    public float stemPlayAngle = 90f;
    [Tooltip("Жесткость пружины")]
    public float springStiffness = 150f;
    [Tooltip("Затухание пружины")]
    public float springDamping = 8f;
    [Tooltip("Кнопка 'Назад', чтобы повернуть стебель обратно к начальным кнопкам")]
    public Button btnBackFromStem;

    private float _currentStemAngle = 0f;
    private float _targetStemAngle = 0f;
    private float _stemVelocity = 0f;

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

    [Header("Пин-код и Раздельные Панели")]
    [Tooltip("Панель ввода кода для клиента (Join)")]
    public GameObject panelJoinCode;
    [Tooltip("Кнопка 'Отмена', чтобы закрыть панель Join без входа")]
    public Button btnCloseJoinPanel;
    [Tooltip("Массив из 6 полей ввода (Input Fields)")]
    public TMP_InputField[] joinCodeInputs = new TMP_InputField[6];
    [Tooltip("Массив из 6 текстов для отображения кода хостом (вместо одного текста)")]
    public TMP_Text[] hostCodeDigits = new TMP_Text[6];
    [Tooltip("Кнопка подтверждения входа после набора 6 цифр")]
    public Button btnConfirmJoin;

    [Header("Кастомное Лобби")]
    [Tooltip("Сюда закинь панель лобби, которая будет показываться вместо Play")]
    public GameObject panelLobby;
    [Tooltip("Текст, куда скрипт впишет код из 4 цифр")]
    public TMP_Text textRoomCode;
    [Tooltip("Кнопка 'Начать Игру' (Только для хоста)")]
    public Button btnStartGame;
    [Tooltip("Кнопка выхода из лобби обратно в меню")]
    public Button btnLeaveLobby;

    [Header("Анимации панели Join")]
    [Tooltip("Сама табличка-фон Join (падает сверху)")]
    public RectTransform joinPanelWindow;
    [Tooltip("Длительность анимации Join (в секундах)")]
    public float joinAnimDuration = 0.4f;

    [Header("Анимации появления Лобби")]
    [Tooltip("Сама табличка-фон лобби (падает сверху)")]
    public RectTransform lobbySignWindow;
    [Tooltip("Группа с цифрами кода комнаты (для плавного появления), на неё нужно повесить CanvasGroup")]
    public CanvasGroup lobbyCodePanelGroup;
    [Tooltip("Панель со списком игроков (выезжает снизу)")]
    public RectTransform lobbyPlayersPanel;
    [Tooltip("Длительность анимации (в секундах)")]
    public float lobbyAnimDuration = 0.5f;

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

    // Позиции для анимации лобби
    private Vector2 _lobbySignOriginalPos;
    private Vector2 _lobbyPlayersOriginalPos;
    private Vector2 _joinPanelOriginalPos;
    private bool _isLobbyClosing = false;
    private bool _isJoinClosing = false;
    private string[] _lastPinValues = new string[6];
    private int _lastRawPlayerCount = -1; // Для дебага
    private int _currentFocusedJoinInput = -1;


    private void Start()
    {
        // Снимаем ограничение ФПС на мобилках (по умолчанию там 30)
        Application.targetFrameRate = 120;
        
        // ── Обязательно Разблокируем курсор для Меню ──
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Привязываем кнопки бокового меню
        if (btnPlay != null) btnPlay.onClick.AddListener(() => {
            SwitchPanel(null); // скрываем панели справа
            SetStemAngle(stemPlayAngle);
        });
        if (btnNews != null) btnNews.onClick.AddListener(() => {
            SwitchPanel(panelNews);
            SetStemAngle(0f);
        });
        if (btnSettings != null) btnSettings.onClick.AddListener(() => {
            SwitchPanel(panelSettings);
            SetStemAngle(0f);
        });
        if (btnExit != null) btnExit.onClick.AddListener(ExitGame);

        // Кнопка назад на стебле (если понадобится)
        if (btnBackFromStem != null) btnBackFromStem.onClick.AddListener(() => {
            SetStemAngle(0f);
        });

        // Привязываем кнопки хоста и джоина
        if (btnHost != null) btnHost.onClick.AddListener(() => StartGame("Host"));
        if (btnJoin != null) btnJoin.onClick.AddListener(() => {
            // Если нажимаем Join, то закрываем активную сессию хоста (и лобби), если она есть
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsSessionActive())
            {
                NetworkManager.Instance.Disconnect();
            }
            
            if (panelLobby != null && panelLobby.activeSelf && !_isLobbyClosing) 
            {
                _isLobbyClosing = true;
                StartCoroutine(UIAnimHelper.AnimateLobbyDisappearance(
                    panelLobby, lobbySignWindow, lobbyPlayersPanel, lobbyCodePanelGroup,
                    lobbyAnimDuration, _lobbySignOriginalPos, _lobbyPlayersOriginalPos,
                    () => { _isLobbyClosing = false; }
                ));
            }

            // Показываем панель Join
            if (panelJoinCode != null)
            {
                if (!panelJoinCode.activeSelf) 
                    StartCoroutine(UIAnimHelper.AnimateJoinAppearance(
                        panelJoinCode, joinPanelWindow, joinCodeInputs,
                        joinAnimDuration, _joinPanelOriginalPos, this
                    ));
            }
            else StartGame("Client"); // Фоллбэк, если панели нет
        });

        // Кнопка подтверждения пин-кода (в панели Join)
        if (btnConfirmJoin != null) btnConfirmJoin.onClick.AddListener(() => StartGame("Client"));
        
        // Кнопка отмены/закрытия панели Join
        if (btnCloseJoinPanel != null) btnCloseJoinPanel.onClick.AddListener(() => {
            if (panelJoinCode != null && panelJoinCode.activeSelf && !_isJoinClosing)
            {
                _isJoinClosing = true;
                StartCoroutine(UIAnimHelper.AnimateJoinDisappearance(
                    panelJoinCode, joinPanelWindow, joinAnimDuration, _joinPanelOriginalPos,
                    () => { _isJoinClosing = false; }
                ));
            }
        });

        // Автоматический переход курсора между 6 полями ввода (пин-код)
        if (joinCodeInputs != null)
        {
            for (int i = 0; i < joinCodeInputs.Length; i++)
            {
                int index = i; // Локальная переменная для лямбды
                if (joinCodeInputs[index] != null)
                {
                    // Поле принимает только 1 символ и только цифры
                    joinCodeInputs[index].characterLimit = 1;
                    joinCodeInputs[index].contentType = TMP_InputField.ContentType.IntegerNumber;
                    
                    // При вводе символа перескакиваем на следующее поле
                    joinCodeInputs[index].onValueChanged.AddListener((val) => {
                        ValidateJoinCodeInputs();

                        if (val.Length == 1 && index < joinCodeInputs.Length - 1)
                        {
                            if (joinCodeInputs[index + 1] != null)
                            {
                                joinCodeInputs[index + 1].Select();
                            }
                        }
                    });
                }
            }
        }
        
        ValidateJoinCodeInputs();

        // Восстанавливаем никнейм
        if (PlayerPrefs.HasKey("SavedNickname") && inputNickname != null)
        {
            string savedName = PlayerPrefs.GetString("SavedNickname");
            inputNickname.text = savedName;
            NetworkManager.LocalPlayerName = savedName;
        }

        // Автоматически сохраняем никнейм при каждом вводе символа
        if (inputNickname != null)
        {
            inputNickname.onValueChanged.AddListener((val) => {
                PlayerPrefs.SetString("SavedNickname", val);
                NetworkManager.LocalPlayerName = val; // Сразу обновляем в менеджере
                PlayerPrefs.Save();
            });
        }

        // Привязываем кнопки лобби
        if (btnStartGame != null) btnStartGame.onClick.AddListener(() => {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsServer())
                NetworkManager.Instance.StartGameScene("GameScene");
        });
        
        if (btnLeaveLobby != null) btnLeaveLobby.onClick.AddListener(() => {
            if (NetworkManager.Instance != null) NetworkManager.Instance.Disconnect();
            if (panelLobby != null && panelLobby.activeSelf && !_isLobbyClosing) 
            {
                _isLobbyClosing = true;
                StartCoroutine(UIAnimHelper.AnimateLobbyDisappearance(
                    panelLobby, lobbySignWindow, lobbyPlayersPanel, lobbyCodePanelGroup,
                    lobbyAnimDuration, _lobbySignOriginalPos, _lobbyPlayersOriginalPos,
                    () => { _isLobbyClosing = false; }
                ));
            }
            _lastPlayerCount = -1;
            _lastPlayersCache = "";
        });

        // Фоллбэки: если пользователь забыл назначить joinPanelWindow, берем RectTransform самой панели
        if (joinPanelWindow == null && panelJoinCode != null) joinPanelWindow = panelJoinCode.GetComponent<RectTransform>();

        // При старте скрываем лобби и закрываем все панели
        // ОЧЕНЬ ВАЖНО: Мы закрываем панели ДО считывания anchoredPosition,
        // чтобы обойти баги Unity Canvas/Layout System с калькуляцией позиций на первом кадре.
        if (panelLobby != null) panelLobby.SetActive(false);
        if (panelJoinCode != null) panelJoinCode.SetActive(false);
        SwitchPanel(null);

        // Теперь безопасно сохраняем начальные позиции для анимаций
        if (lobbySignWindow != null) _lobbySignOriginalPos = lobbySignWindow.anchoredPosition;
        if (lobbyPlayersPanel != null) _lobbyPlayersOriginalPos = lobbyPlayersPanel.anchoredPosition;
        if (joinPanelWindow != null) _joinPanelOriginalPos = joinPanelWindow.anchoredPosition;

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
        // Анимация стебля (пружинистое круговое меню)
        if (stemTransform != null)
        {
            float displacement = _targetStemAngle - _currentStemAngle;
            float springForce = displacement * springStiffness;
            _stemVelocity += springForce * Time.deltaTime;
            _stemVelocity *= (1f - springDamping * Time.deltaTime);
            _currentStemAngle += _stemVelocity * Time.deltaTime;

            if (Mathf.Abs(displacement) < 0.01f && Mathf.Abs(_stemVelocity) < 0.01f)
            {
                _currentStemAngle = _targetStemAngle;
                _stemVelocity = 0f;
            }

            stemTransform.localRotation = Quaternion.Euler(0, 0, _currentStemAngle);
        }

        // Обработка Backspace для панели Join
        if (panelJoinCode != null && panelJoinCode.activeSelf && joinCodeInputs != null)
        {
            // Находим какой инпут сейчас активен
            _currentFocusedJoinInput = -1;
            for (int i = 0; i < joinCodeInputs.Length; i++)
            {
                if (joinCodeInputs[i] != null && joinCodeInputs[i].isFocused)
                {
                    _currentFocusedJoinInput = i;
                    break;
                }
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame)
            {
                if (_currentFocusedJoinInput > 0)
                {
                    // Если в ПРОШЛОМ КАДРЕ это поле УЖЕ было пустым, значит мы пытаемся стереть ПРЕДЫДУЩЕЕ поле
                    if (string.IsNullOrEmpty(_lastPinValues[_currentFocusedJoinInput]))
                    {
                        joinCodeInputs[_currentFocusedJoinInput - 1].Select();
                        joinCodeInputs[_currentFocusedJoinInput - 1].text = "";
                    }
                }
            }

            // Обновляем сохраненные значения для следующего кадра
            for (int i = 0; i < joinCodeInputs.Length; i++)
            {
                if (joinCodeInputs[i] != null) _lastPinValues[i] = joinCodeInputs[i].text;
            }
        }

        // Постоянно обновляем окно кастомного лобби, если мы в нём
        if (panelLobby != null && panelLobby.activeSelf && NetworkManager.Instance != null)
        {
            // Если нас отключили или кикнули — закрываем лобби и возвращаемся в меню
            if (!NetworkManager.Instance.IsSessionActive())
            {
                if (panelLobby.activeSelf && !_isLobbyClosing) 
                {
                    _isLobbyClosing = true;
                    StartCoroutine(UIAnimHelper.AnimateLobbyDisappearance(
                        panelLobby, lobbySignWindow, lobbyPlayersPanel, lobbyCodePanelGroup,
                        lobbyAnimDuration, _lobbySignOriginalPos, _lobbyPlayersOriginalPos,
                        () => { _isLobbyClosing = false; }
                    ));
                }
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

            // Новый вариант (по 1 цифре в 6 Text UI элементах)
            if (hostCodeDigits != null && hostCodeDigits.Length >= 6 && !string.IsNullOrEmpty(roomCode))
            {
                for (int i = 0; i < 6; i++)
                {
                    if (hostCodeDigits[i] != null && i < roomCode.Length)
                    {
                        hostCodeDigits[i].text = roomCode[i].ToString();
                    }
                }
            }

            // Обновление кнопки старта
            bool isHost = NetworkManager.Instance.IsServer();
            if (btnStartGame != null) btnStartGame.interactable = isHost;

            // Обновление списка игроков и кнопок кика
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (players.Length != _lastRawPlayerCount)
            {
                _lastRawPlayerCount = players.Length;
                Debug.Log($"[Lobby UI Debug] Обнаружено {players.Length} объектов PlayerController в сцене.");
                for (int debug_i = 0; debug_i < players.Length; debug_i++)
                {
                    var debugP = players[debug_i];
                    bool isObjNull = debugP == null || debugP.Object == null;
                    bool isValid = !isObjNull && debugP.Object.IsValid;
                    Debug.Log($"  -> PlayerController [{debug_i}]: GameObject={debugP?.gameObject.name}, Object=null? {isObjNull}, IsValid? {isValid}, Name='{debugP?.PlayerName}'");
                }
            }

            // Исключаем невалидных игроков (если они еще не заспавнились по сети)
            System.Collections.Generic.List<PlayerController> validPlayers = new System.Collections.Generic.List<PlayerController>();
            foreach (var p in players)
            {
                if (p == null) continue;
                try 
                {
                    if (p.Object != null && p.Object.IsValid)
                    {
                        validPlayers.Add(p);
                    }
                }
                catch { /* Object property could throw if not attached */ }
            }

            // Сортируем игроков: Хост всегда первый (сверху), остальные по времени захода (по их сетевому ID)
            validPlayers.Sort((a, b) => {
                try 
                {
                    if (a.IsHostPlayer && !b.IsHostPlayer) return -1;
                    if (!a.IsHostPlayer && b.IsHostPlayer) return 1;
                    return a.Object.Id.Raw.CompareTo(b.Object.Id.Raw);
                }
                catch { return 0; }
            });
            
            // Генерируем "хэш" из всех имён, чтобы понять, изменился ли состав
            string currentCache = "";
            foreach (var p in validPlayers)
            {
                try { currentCache += p.PlayerName.ToString() + p.IsHostPlayer.ToString(); }
                catch { currentCache += "Unknown"; }
            }

            // Перерисовываем список, только если кто-то зашел/вышел/изменил имя
            if (validPlayers.Count != _lastPlayerCount || currentCache != _lastPlayersCache)
            {
                Debug.Log($"[Lobby UI Debug] Отрисовка интерфейса. Валидных игроков: {validPlayers.Count} (было {_lastPlayerCount}). Хэш кэша: '{currentCache}'");
                _lastPlayerCount = validPlayers.Count;
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
                    foreach (var p in validPlayers)
                    {
                        GameObject newPlayerItem = Instantiate(playerLobbyItemPrefab, playerListContent);
                        newPlayerItem.transform.localScale = Vector3.one; // Жестко фиксируем скейл
                        
                        LobbyPlayerItem itemScript = newPlayerItem.GetComponent<LobbyPlayerItem>();

                        if (itemScript != null)
                        {
                            // Устанавливаем имя (без [HOST])
                            if (itemScript.textName != null)
                            {
                                string pName = "Загрузка...";
                                try 
                                { 
                                    pName = p.PlayerName.ToString(); 
                                    if (string.IsNullOrWhiteSpace(pName)) pName = "Игрок (Подключается...)";
                                } catch {}
                                itemScript.textName.text = pName;
                            }

                            // Включаем или выключаем иконку короны для хоста
                            if (itemScript.hostCrownIcon != null)
                            {
                                try { itemScript.hostCrownIcon.gameObject.SetActive(p.IsHostPlayer); } catch {}
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

    public void SetStemAngle(float angle)
    {
        _targetStemAngle = angle;
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

        // 1. Убеждаемся, что захватили актуальный ник из поля
        if (inputNickname != null && !string.IsNullOrWhiteSpace(inputNickname.text))
        {
            NetworkManager.LocalPlayerName = inputNickname.text.Trim();
        }

        // Если ник совсем пустой, генерируем случайный
        if (string.IsNullOrWhiteSpace(NetworkManager.LocalPlayerName) || NetworkManager.LocalPlayerName == "Player")
        {
            NetworkManager.LocalPlayerName = "Игрок " + Random.Range(100, 999);
            PlayerPrefs.SetString("SavedNickname", NetworkManager.LocalPlayerName);
            PlayerPrefs.Save();
        }

        // 2. Логика переключения между Хостом и Клиентом (закрытие окон)
        if (mode == "Host")
        {
            // Закрываем панель присоединения, если была открыта (наоборот)
            if (panelJoinCode != null && panelJoinCode.activeSelf && !_isJoinClosing) 
            {
                _isJoinClosing = true;
                StartCoroutine(UIAnimHelper.AnimateJoinDisappearance(
                    panelJoinCode, joinPanelWindow, joinAnimDuration, _joinPanelOriginalPos,
                    () => { _isJoinClosing = false; }
                ));
            }

            // Если мы уже хост, то просто показываем меню лобби заново и прерываем код (не создавая новый сервер)
            if (NetworkManager.Instance.IsSessionActive() && NetworkManager.Instance.IsServer())
            {
                ShowLobbyAnimate(true);
                return;
            }
            // Если мы вдруг были подключены как клиент, но решили стать хостом - отключаемся
            else if (NetworkManager.Instance.IsSessionActive())
            {
                NetworkManager.Instance.Disconnect();
            }
        }
        else if (mode == "Client")
        {
            // Перед входом как клиент убеждаемся, что старые сессии (например недобитый хост) отключены
            if (NetworkManager.Instance.IsSessionActive())
            {
                NetworkManager.Instance.Disconnect();
            }

            // И СРАЗУ ЗАКРЫВАЕМ ПАНЕЛЬ JOIN, чтобы она не зависала, пока мы ждём ответа от сервера
            if (panelJoinCode != null && panelJoinCode.activeSelf && !_isJoinClosing)
            {
                _isJoinClosing = true;
                StartCoroutine(UIAnimHelper.AnimateJoinDisappearance(
                    panelJoinCode, joinPanelWindow, joinAnimDuration, _joinPanelOriginalPos,
                    () => { _isJoinClosing = false; }
                ));
            }
        }

        // 3. Сразу открываем UI Лобби с анимацией, чтобы не было ощущения "зависания"
        // Но `isHost` передаем тру только если мы Host, чтобы клиент не видел табличку Host Code
        ShowLobbyAnimate(mode == "Host");

        // 4. Пробуем запустить сервер или подключиться к игре
        bool success = false;
        
        if (mode == "Host")
        {
            // Случайный код из 6 цифр для друга
            string randomCode = Random.Range(100000, 1000000).ToString();
            success = await NetworkManager.Instance.StartHost(randomCode);
        }
        else if (mode == "Client")
        {
            // Подключаемся по коду из инпута
            string roomCode = "GameRoom";
            
            // Считываем 6 инпутов, если они используются
            bool used6Digits = false;
            if (joinCodeInputs != null && joinCodeInputs.Length == 6 && joinCodeInputs[0] != null)
            {
                roomCode = "";
                foreach (var inp in joinCodeInputs) 
                { 
                    if (inp != null) roomCode += inp.text; 
                }
                used6Digits = true;
            }

            // Фоллбэк на старое одиночное поле, если мы не используем пин-коды
            if (!used6Digits || string.IsNullOrEmpty(roomCode))
            {
                if (inputRoomName != null && !string.IsNullOrWhiteSpace(inputRoomName.text))
                {
                    roomCode = inputRoomName.text.Trim();
                }
            }

            success = await NetworkManager.Instance.StartClient(roomCode);
        }

        // 5. После попытки подключения решаем, отменить анимацию или запустить цифры
        if (!success)
        {
            Debug.LogWarning("Не удалось войти в игру (комната не найдена или ошибка сети).");
            if (panelLobby != null && panelLobby.activeSelf && !_isLobbyClosing) 
            {
                _isLobbyClosing = true;
                StartCoroutine(UIAnimHelper.AnimateLobbyDisappearance(
                    panelLobby, lobbySignWindow, lobbyPlayersPanel, lobbyCodePanelGroup,
                    lobbyAnimDuration, _lobbySignOriginalPos, _lobbyPlayersOriginalPos,
                    () => { _isLobbyClosing = false; }
                ));
            }
        }
        else
        {
            // Успешный вход - если мы заходили как клиент через панель Join, скрываем её (на всякий случай, дубль)
            if (mode == "Client" && panelJoinCode != null && panelJoinCode.activeSelf && !_isJoinClosing)
            {
                _isJoinClosing = true;
                StartCoroutine(UIAnimHelper.AnimateJoinDisappearance(
                    panelJoinCode, joinPanelWindow, joinAnimDuration, _joinPanelOriginalPos,
                    () => { _isJoinClosing = false; }
                ));
            }

            // Если мы успешно создали или подключились к серверу, ждем 1 секунду и анимируем цифры только для хоста
            if (mode == "Host")
            {
                StartCoroutine(UIAnimHelper.AnimateDigitsWithDelay(hostCodeDigits, 1.0f, this));
            }
        }
    }

    private void ShowLobbyAnimate(bool isHost)
    {
        if (panelLobby != null && !panelLobby.activeSelf)
        {
            if (lobbySignWindow != null || lobbyPlayersPanel != null || lobbyCodePanelGroup != null)
            {
                StartCoroutine(UIAnimHelper.AnimateLobbyAppearance(
                    panelLobby, lobbySignWindow, lobbyPlayersPanel, lobbyCodePanelGroup,
                    hostCodeDigits, lobbyAnimDuration, _lobbySignOriginalPos, _lobbyPlayersOriginalPos,
                    isHost
                ));
            }
            else
            {
                panelLobby.SetActive(true);
            }
        }
    }

    private void ValidateJoinCodeInputs()
    {
        if (btnConfirmJoin == null || joinCodeInputs == null) return;
        bool ready = true;
        for (int i = 0; i < joinCodeInputs.Length; i++)
        {
            if (joinCodeInputs[i] != null && string.IsNullOrEmpty(joinCodeInputs[i].text))
            {
                ready = false;
                break;
            }
        }
        btnConfirmJoin.interactable = ready;
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
