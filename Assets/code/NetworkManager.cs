using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance;
    public static string LocalPlayerName = "Player";

    [Header("── Сеть ─────────────────────────────────")]
    [Tooltip("Prefab игрока с компонентом NetworkObject")]
    public NetworkPrefabRef playerPrefab;

    private NetworkRunner _runner;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Применяем глобальную громкость сразу при старте игры
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
    }

    public async System.Threading.Tasks.Task<bool> StartHost(string roomName)
    {
        return await StartGame(GameMode.Host, roomName);
    }

    public async System.Threading.Tasks.Task<bool> StartClient(string roomName)
    {
        return await StartGame(GameMode.Client, roomName);
    }

    public void Disconnect()
    {
        if (_runner != null)
        {
            _runner.Shutdown();
            _runner = null;
        }
        _spawnedPlayers.Clear();
    }

    private async System.Threading.Tasks.Task<bool> StartGame(GameMode mode, string roomName)
    {
        if (_runner != null)
        {
            _ = _runner.Shutdown();
            _runner = null;
        }

        var runnerGo = new GameObject("Session");
        runnerGo.transform.SetParent(this.transform);

        _runner = runnerGo.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        var sceneManager = runnerGo.AddComponent<NetworkSceneManagerDefault>();

        // ── Добавляем VoiceChat клиент прямо на Сессию (чтобы Игроки его могли найти) ──
        var voiceClient = runnerGo.AddComponent<Photon.Voice.Fusion.FusionVoiceClient>();
        voiceClient.UseFusionAppSettings = true;

        var appSettings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
        appSettings.FixedRegion = "eu";

        var args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomName,
            SceneManager = sceneManager,
            CustomPhotonAppSettings = appSettings
        };

        _runner.AddCallbacks(this);
        _runner.AddCallbacks(voiceClient); // <- Подключаем войс чат к событиям бекенда Fusion
        
        var result = await _runner.StartGame(args);

        if (!result.Ok)
        {
            Debug.LogWarning($"Failed to start/join game: {result.ShutdownReason}");
            // Если комната не найдена или другая ошибка сети
            if (_runner != null)
            {
                _runner.Shutdown();
                _runner = null;
            }
            return false;
        }

        return true;
    }

    // ── Helper API для кастомного UI Лобби ────────────────────────────

    public bool IsSessionActive()
    {
        return _runner != null;
    }

    public string GetRoomCode()
    {
        return _runner != null && _runner.IsRunning ? _runner.SessionInfo.Name : "";
    }

    public bool IsServer()
    {
        return _runner != null && _runner.IsServer;
    }

    public void StartGameScene(string sceneName = "GameScene")
    {
        if (_runner != null && _runner.IsServer)
        {
            // Переход на игровую сцену
            _runner.LoadScene(sceneName, new LoadSceneParameters(LoadSceneMode.Single));
        }
    }

    public void KickPlayer(PlayerRef player)
    {
        if (_runner != null && _runner.IsServer && player != _runner.LocalPlayer)
        {
            _runner.Disconnect(player);
        }
    }

    // ── INetworkRunnerCallbacks ──────────────────────────────────
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // Спавним игрока. Если мы в MainMenu, PlayerController сам спрячет физику и модель.
            Vector3 spawnPos = new Vector3(UnityEngine.Random.Range(-5f, 5f), 1f, UnityEngine.Random.Range(-5f, 5f));
            NetworkObject no = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player, (r, o) => {
                o.GetComponent<PlayerController>().IsHostPlayer = (player == r.LocalPlayer);
            });
            _spawnedPlayers[player] = no;
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObj))
        {
            if (playerObj != null) runner.Despawn(playerObj);
            _spawnedPlayers.Remove(player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (PlayerController.Local != null)
        {
            input.Set(PlayerController.Local.GetLocalInput());
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        // Когда запускается игровая сцена через _runner.LoadScene,
        // старые PlayerController'ы (которые были спрятаны в MainMenu) удаляются Юнити.
        // Поэтому здесь Сервер перепроверяет ВСЕХ игроков и переспавнивает их для игры.
        if (runner.IsServer)
        {
            if (SceneManager.GetActiveScene().name == "GameScene")
            {
                foreach (var ply in runner.ActivePlayers)
                {
                    // Игрок мог существовать, но его NetworkObject был уничтожен сменой сцены
                    if (!_spawnedPlayers.ContainsKey(ply) || _spawnedPlayers[ply] == null || !_spawnedPlayers[ply].IsValid)
                    {
                        Vector3 spawnPos = new Vector3(UnityEngine.Random.Range(-5f, 5f), 1f, UnityEngine.Random.Range(-5f, 5f));
                        NetworkObject no = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, ply, (r, o) => {
                            o.GetComponent<PlayerController>().IsHostPlayer = (ply == r.LocalPlayer);
                        });
                        _spawnedPlayers[ply] = no;
                    }
                }
            }
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        _spawnedPlayers.Clear();
        if (_runner == runner) _runner = null;
        if (runner != null && runner.gameObject != null && runner.gameObject.name == "Session")
        {
            Destroy(runner.gameObject);
        }

        // Принудительно освобождаем курсор при завершении сессии
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Если хост кикает нас или выходим, мы возвращаемся в MainMenu
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    // Boilerplate (Пустые)
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i) { }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr msg) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> list) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken t) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey k, ArraySegment<byte> d) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
}
