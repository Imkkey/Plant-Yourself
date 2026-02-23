using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Fusion.Sockets;
using System;

/// <summary>
/// NetworkManager — Photon Fusion 2
/// ──────────────────────────────────────────────────────────────
/// Настройка:
///   1. Создай пустой GameObject "NetworkManager" на сцене
///   2. Повесь этот скрипт на него
///   3. Создай Player Prefab:
///      - Добавь компонент NetworkObject на объект игрока
///      - Убедись что PlayerController использует [Networked] свойства
///      - Перетащи prefab в поле "Player Prefab" в Inspector
///   4. Заполни UI ссылки в Inspector
/// </summary>
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("── Сеть ─────────────────────────────────")]

    [Tooltip("Prefab игрока с компонентом NetworkObject")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Tooltip("Название комнаты по умолчанию")]
    [SerializeField] private string defaultRoomName = "GameRoom";

    [Header("── UI ──────────────────────────────────")]

    [SerializeField] private GameObject  lobbyPanel;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button      hostButton;
    [SerializeField] private Button      joinButton;
    [SerializeField] private Button      disconnectButton;
    [SerializeField] private TMP_Text    statusText;

    // ── Внутренние переменные ────────────────────────────────────

    private NetworkRunner _runner;

    // Хранит спавненных игроков: PlayerRef → объект
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers
        = new Dictionary<PlayerRef, NetworkObject>();

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Awake()
    {
        // Привязываем кнопки
        if (hostButton)       hostButton.onClick.AddListener(OnClickHost);
        if (joinButton)       joinButton.onClick.AddListener(OnClickJoin);
        if (disconnectButton) disconnectButton.onClick.AddListener(OnClickDisconnect);

        ShowLobby(true);
        SetStatus("Введи название комнаты и нажми Host или Join");
    }

    // ── UI Handlers ──────────────────────────────────────────────

    private void OnClickHost()
    {
        string room = GetRoomName();
        SetStatus($"Создаем сервер (Host Mode): {room}...");
        StartGame(GameMode.Host, room);
    }

    private void OnClickJoin()
    {
        string room = GetRoomName();
        SetStatus($"Подключаемся к серверу (Client Mode): {room}...");
        StartGame(GameMode.Client, room);
    }

    private void OnClickDisconnect()
    {
        if (_runner != null)
        {
            _runner.Shutdown();
            _runner = null;
        }
        _spawnedPlayers.Clear();
        ShowLobby(true);
        SetStatus("Отключён. Введи название комнаты.");
    }

    private string GetRoomName()
    {
        if (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text))
            return roomNameInput.text.Trim();
        return defaultRoomName;
    }

    // ── Запуск сессии ────────────────────────────────────────────

    private async void StartGame(GameMode mode, string roomName)
    {
        // Check if there is already a runner, if so dont add another, just use it or clean it up
        _runner = gameObject.GetComponent<NetworkRunner>();
        if (_runner == null) {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }
        
        _runner.ProvideInput = true;   // этот клиент отправляет Input

        // Same for SceneManager, avoid adding duplicate components
        var sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null) {
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        var args = new StartGameArgs
        {
            GameMode       = mode,
            SessionName    = roomName,
            // ── КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: СВЕТ И SKYBOX ────────
            // Убрали загрузку сцены через (Scene = ...).
            // Запуск сети произойдёт прямо в текущей сцене без её перезагрузки.
            // Это спасает запечённый свет (тени) и скайбокс от "синего экрана".
            SceneManager   = sceneManager
        };

        var result = await _runner.StartGame(args);

        if (result.Ok)
        {
            ShowLobby(false);
            SetStatus(mode == GameMode.Host
                ? $"Хост: {roomName}  |  Ожидаем игроков..."
                : $"Подключён к: {roomName}");
        }
        else
        {
            SetStatus($"Ошибка подключения: {result.ShutdownReason}");
            // Destroy only if we actually added it
            if (_runner != null) {
                Destroy(_runner);
                _runner = null;
            }
            sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager != null) {
                Destroy(sceneManager);
            }
        }
    }

    // ── INetworkRunnerCallbacks ──────────────────────────────────

    /// Вызывается когда игрок подключается — спавним его объект
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnPos = new Vector3(UnityEngine.Random.Range(-5f, 5f), 1f, UnityEngine.Random.Range(-5f, 5f));
            NetworkObject no = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            _spawnedPlayers[player] = no;
        }

        SetStatus($"Игрок {player.PlayerId} присоединился. Всего: {_spawnedPlayers.Count}");
    }

    /// Вызывается когда игрок отключается — удаляем его объект
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObj))
        {
            runner.Despawn(playerObj);
            _spawnedPlayers.Remove(player);
        }

        SetStatus($"Игрок {player.PlayerId} отключился. Осталось: {_spawnedPlayers.Count}");
    }

    /// Сбор Input от локального игрока
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (PlayerController.Local != null)
        {
            input.Set(PlayerController.Local.GetLocalInput());
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        SetStatus($"Сессия завершена: {reason}");
        ShowLobby(true);
        _spawnedPlayers.Clear();

        if (_runner != null)
        {
            Destroy(_runner);
            _runner = null;
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
        => SetStatus("Подключён к серверу!");

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        => SetStatus($"Отключён от сервера: {reason}");

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        => SetStatus($"Не удалось подключиться: {reason}");

    // Остальные callbacks (оставляем пустыми, добавим по мере надобности)
    public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i)      { }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr msg) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> list)     { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken t)            { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey k, ArraySegment<byte> d) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress) { }
    public void OnSceneLoadDone(NetworkRunner r)   { }
    public void OnSceneLoadStart(NetworkRunner r)  { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject o, PlayerRef p)    { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject o, PlayerRef p)   { }

    // ── Helpers ──────────────────────────────────────────────────

    private void ShowLobby(bool show)
    {
        if (lobbyPanel) lobbyPanel.SetActive(show);
        if (disconnectButton) disconnectButton.gameObject.SetActive(!show);
    }

    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[Network] {msg}");
    }
}
