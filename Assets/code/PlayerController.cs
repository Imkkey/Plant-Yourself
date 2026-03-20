using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

public enum PlantType
{
    None = 0,
    Oak,
    Vine,
    Chamomile
}

/// <summary>
/// 3D PlayerController — камера Fortnite/TPS стиль (New Input System)
/// ─────────────────────────────────────────────────────────────────
/// WASD        — движение (стрейф относительно камеры)
/// Space       — прыжок
/// Left Shift  — рывок (несколько зарядов)
/// Left Ctrl   — приседание
/// Mouse       — поворот камеры и тела
/// Scroll      — приближение / удаление
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  ПАРАМЕТРЫ ИЗ INSPECTOR
    // ══════════════════════════════════════════════════════════════

    [Header("── Движение ─────────────────────────────")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("── Рывок (Left Shift) ──────────────────")]
    [SerializeField] private float dashForce = 14f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 1.0f;
    [SerializeField] private int maxDashCharges = 2;

    [Header("── Приседание (Left Ctrl) ─────────────")]
    [SerializeField] private float crouchHeight = 0.9f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("── Зацеп за выступ (Space у края) ────")]
    [SerializeField] private float mantleReach = 0.7f;
    [SerializeField] private float mantleMinHeight = 0.5f;
    [SerializeField] private float mantleMaxHeight = 2.2f;
    [SerializeField] private float mantleSpeed = 6f;
    [SerializeField] private float mantleForwardStep = 0.4f;

    [Header("── Камера (Fortnite/TPS) ──────────────")]
    [SerializeField] private Camera cam;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float pitchMin = -70f;
    [SerializeField] private float pitchMax = 70f;
    [SerializeField] private float camDistance = 3.5f;
    [SerializeField] private float camMinDistance = 0.5f;
    [SerializeField] private float camMaxDistance = 8f;
    [SerializeField] private float camScrollSpeed = 3f;
    [SerializeField] private float camShoulderOffset = 0.8f;
    [SerializeField] private float camPivotHeight = 1.4f;
    [SerializeField] private float crouchCamPivotHeight = 0.9f;
    [SerializeField] private float camCollisionRadius = 0.3f;
    [SerializeField] private LayerMask camCollisionMask = ~0;
    [SerializeField] private float camFollowSpeed = 12f;

    [Header("── UI Игрока ──────────────────────────")]
    [SerializeField] private TMPro.TMP_Text nameTag;
    [SerializeField] private GameObject voiceIcon; // Иконка, когда игрок говорит

    [Header("── Камера (Растение) ──────────────")]


    public static PlayerController Local { get; private set; }

    // Ссылки на компоненты
    private CharacterController _cc;
    private PlantGrower _grower;
    private InventorySystem _inventory;
    private Keyboard _kb;
    private Mouse    _mouse;

    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public NetworkBool IsHostPlayer { get; set; }

    [Networked] private Vector3 _velocity { get; set; }
    [Networked] private NetworkBool _isGrounded { get; set; }

    // Характеристики CC
    private float   _standHeight;
    private Vector3 _standCenter;
    private float   _targetCCHeight;
    private Vector3 _targetCCCenter;
    [Networked] private NetworkBool _isCrouching { get; set; }

    // Рывок
    [Networked] private int     _dashCharges { get; set; }
    [Networked] private float   _dashCooldownTimer { get; set; }
    [Networked] private NetworkBool _isDashing { get; set; }
    [Networked] private float   _dashTimer { get; set; }
    [Networked] private Vector3 _dashDirection { get; set; }

    // Зацеп (Mantle)
    [Networked] private NetworkBool _isMantling { get; set; }
    [Networked] private Vector3 _mantleStartPos { get; set; }
    [Networked] private Vector3 _mantleTopPos { get; set; }
    [Networked] private Vector3 _mantleEndPos { get; set; }
    [Networked] private float   _mantleProgress { get; set; }

    // Ввод по сети
    [Networked] private NetworkButtons _prevButtons { get; set; }
    private NetworkInputData _currentInput;
    private NetworkButtons   _currentPressed;



    // Камера — локально
    private float   _yaw;
    private float   _pitch;
    private Vector3 _pivotPos;
    private float   _currentCamDist;
    private Renderer[] _renderers;

    // ── Ping System ──
    private class PingMarker
    {
        public Vector3 Position;
        public float SpawnTime;
        public string PlayerName;
    }
    private System.Collections.Generic.List<PingMarker> _activePings = new System.Collections.Generic.List<PingMarker>();
    private Texture2D _pingTexture;
    private AudioClip _proceduralPingSound;

    // ══════════════════════════════════════════════════════════════
    //  ИНТЕРФЕЙС NETWORK BEHAVIOUR
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _grower = GetComponent<PlantGrower>();
        _inventory = GetComponent<InventorySystem>();
        _renderers = GetComponentsInChildren<Renderer>(true);

        _standHeight    = _cc.height;
        _standCenter    = _cc.center;
        _targetCCHeight = _cc.height;
        _targetCCCenter = _cc.center;

        // ── НАСТРОЙКА КОЛЛИЗИЙ ИГРОКОВ (АБСОЛЮТНАЯ ПЛАВНОСТЬ) ──
        // Выделяем игрокам уникальный физический слой (10), чтобы движок Unity аппаратно
        // игнорировал расчеты коллизий ПЕРЕД началом любого движения, а не после.
        gameObject.layer = 10;
        Physics.IgnoreLayerCollision(10, 10, true);

        if (crouchHeight >= _standHeight)
            crouchHeight = _standHeight * 0.5f;

        // Фикс ходьбы по блокам и склонам
        _cc.slopeLimit = 65f;
        _cc.stepOffset = 0.3f; // Не слишком большая, чтобы не "телепортироваться" вверх по блокам
    }

    public override void Spawned()
    {
        bool inMainMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";

        if (cam != null)
        {
            cam.gameObject.SetActive(HasInputAuthority && !inMainMenu);
            AudioListener audioListener = cam.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = HasInputAuthority && !inMainMenu;
        }

        if (inMainMenu)
        {
            if (_cc) _cc.enabled = false;
            if (_renderers != null) foreach (var r in _renderers) if (r) r.enabled = false;
            
            if (HasInputAuthority)
            {
                Local = this;
                RPC_SetPlayerName(NetworkManager.LocalPlayerName);
            }
            return; // Пропускаем остальную инициализацию в меню
        }

        if (HasInputAuthority)
        {
            Local = this;
            if (cam == null) cam = Camera.main;
            _yaw             = transform.eulerAngles.y;
            _pitch           = 15f;
            _currentCamDist  = camDistance;
            _pivotPos        = transform.position + Vector3.up * camPivotHeight;
            if (cam != null) 
            {
                camCollisionMask &= ~(1 << gameObject.layer);
                camCollisionMask &= ~(1 << 10); // Игнорируем всех остальных игроков камерой
            }

            if (PlantSelectionUI.Instance != null && _grower != null && !_grower.IsPlantSelected)
            {
                PlantSelectionUI.Instance.Show();
            }

            RPC_SetPlayerName(NetworkManager.LocalPlayerName);
        }

        // ── Коллизии теперь аппаратно отключены через IgnoreLayerCollision(10, 10) в Awake ──

        // ── Настройка микрофона из PlayerPrefs ──
        if (HasInputAuthority)
        {
            Photon.Voice.Unity.Recorder recorder = GetComponent<Photon.Voice.Unity.Recorder>();
            if (recorder != null)
            {
                // Принудительно выключаем эхо, если оно осталось включенным в префабе
                recorder.DebugEchoMode = false;

                string savedMic = PlayerPrefs.GetString("SelectedMicrophone", "");
                if (string.IsNullOrEmpty(savedMic))
                {
                    recorder.MicrophoneDevice = new Photon.Voice.DeviceInfo("[Default]");
                }
                else
                {
                    recorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(savedMic);
                }
            }

            // Отключаем Speaker у самого себя, чтобы случайно не воспроизводить свой же голос или шум 
            Photon.Voice.Unity.Speaker speaker = GetComponent<Photon.Voice.Unity.Speaker>();
            if (speaker != null)
            {
                speaker.enabled = false;
            }
        }
        else
        {
            // Устанавливаем громкость голоса других игроков из настроек
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                // Для 3D звука важно:
                audioSource.spatialBlend = 1f; // Полное 3D (отдаление зависит от расстояния)
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 30f; // На каком расстоянии перестает быть слышно
                
                audioSource.volume = PlayerPrefs.GetFloat("VoiceVolume", 1f);
            }
        }
        
        if (HasStateAuthority)
        {
            _dashCharges = maxDashCharges;
        }
    }



    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerName(NetworkString<_32> name)
    {
        PlayerName = name;
    }

    // Собираем локальный ввод. Вызывается из NetworkManager.OnInput
    public NetworkInputData GetLocalInput()
    {
        var data = new NetworkInputData();
        if (_grower != null && !_grower.IsPlantSelected) return data; // Отключаем ввод до выбора

        _kb = Keyboard.current;
        
        float h = 0f, v = 0f;
        bool jmp = false, crouch = false, dash = false, act = false, inetract = false;
        bool p1 = false, p2 = false, p3 = false;

        // PC Ввод
        if (_kb != null)
        {
            if (_kb.dKey.isPressed || _kb.rightArrowKey.isPressed)  h += 1f;
            if (_kb.aKey.isPressed || _kb.leftArrowKey.isPressed)   h -= 1f;
            if (_kb.wKey.isPressed || _kb.upArrowKey.isPressed)     v += 1f;
            if (_kb.sKey.isPressed || _kb.downArrowKey.isPressed)   v -= 1f;
            
            jmp = _kb.spaceKey.isPressed;
            crouch = _kb.leftCtrlKey.isPressed;
            dash = _kb.leftShiftKey.isPressed;
            act = _kb.fKey.isPressed;
            inetract = _kb.eKey.isPressed;
            p1 = _kb.digit1Key.isPressed;
            p2 = _kb.digit2Key.isPressed;
            p3 = _kb.digit3Key.isPressed;
        }

        // Мобильный ввод
        if (MobileInputManager.Instance != null)
        {
            h += MobileInputManager.Instance.Move.x;
            v += MobileInputManager.Instance.Move.y;
            
            jmp |= MobileInputManager.Instance.Jump;
            crouch |= MobileInputManager.Instance.Crouch;
            dash |= MobileInputManager.Instance.Dash;
            act |= MobileInputManager.Instance.Action;
            inetract |= MobileInputManager.Instance.Interact;
            p1 |= MobileInputManager.Instance.PlantOak;
            p2 |= MobileInputManager.Instance.PlantVine;
            p3 |= MobileInputManager.Instance.PlantChamomile;
        }

        data.MoveDirection = new Vector2(Mathf.Clamp(h, -1f, 1f), Mathf.Clamp(v, -1f, 1f));

        data.Buttons.Set(NetworkInputButtons.Jump,   jmp);
        data.Buttons.Set(NetworkInputButtons.Crouch, crouch);
        data.Buttons.Set(NetworkInputButtons.Dash,   dash);
        data.Buttons.Set(NetworkInputButtons.Action, act);
        data.Buttons.Set(NetworkInputButtons.Interact, inetract);
        data.Buttons.Set(NetworkInputButtons.PlantOak, p1);
        data.Buttons.Set(NetworkInputButtons.PlantVine, p2);
        data.Buttons.Set(NetworkInputButtons.PlantChamomile, p3);

        data.LookAngles = new Vector2(_pitch, _yaw);
        return data;
    }

    // ══════════════════════════════════════════════════════════════
    //  ГРАФИЧЕСКИЙ UPDATE (АНИМАЦИЯ, КАМЕРА, МЫШЬ)
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;

        ApplyColliderTransition();

        bool isG = _grower != null && _grower.IsGrowing;
        bool isR = _grower != null && _grower.IsRetracting;

        bool shouldBeVisible = !isG && !isR;
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = shouldBeVisible;
            }
        }

        if (!HasInputAuthority) return;

        _kb    = Keyboard.current;
        _mouse = Mouse.current;
        // if (_kb == null || _mouse == null) return; На мобилках их нет, убираем этот жесткий выход

        if (_grower != null && !_grower.IsPlantSelected)
        {
            // Даем PlantSelectionUI управлять курсором. Просто не крутим камеру здесь
            return;
        }

        if (_kb != null && _kb.tabKey.isPressed)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            return;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        HandleLook();

        // ── PING SYSTEM ──
        if (_mouse != null && _mouse.middleButton.wasPressedThisFrame && cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out RaycastHit pingHit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                RPC_SendPing(pingHit.point, PlayerName.ToString());
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetInitialPlant(PlantType plant)
    {
        if (_grower != null)
        {
            _grower.CurrentPlant = plant;
            _grower.IsPlantSelected = true;
        }
    }

    public override void Render()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;

        if (nameTag != null)
        {
            if (HasInputAuthority)
            {
                if (nameTag.gameObject.activeSelf) nameTag.gameObject.SetActive(false);
                if (voiceIcon != null && voiceIcon.activeSelf) voiceIcon.SetActive(false);
                return;
            }

            nameTag.text = PlayerName.ToString();

            if (Camera.main != null && nameTag.gameObject.activeInHierarchy)
            {
                // Поворачиваем имя так, чтобы оно смотрело на камеру (разворачиваем на 180 от камеры, чтобы текст не был зеркальным)
                nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - Camera.main.transform.position);
            }
        }

        // Логика отображения иконки голоса (только для других игроков)
        if (voiceIcon != null && !HasInputAuthority)
        {
            var voiceObj = GetComponent<Photon.Voice.Fusion.VoiceNetworkObject>();
            if (voiceObj != null)
            {
                bool isSpeaking = voiceObj.IsSpeaking;
                if (voiceIcon.activeSelf != isSpeaking)
                {
                    voiceIcon.SetActive(isSpeaking);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (Object == null || !Object.IsValid) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;
        if (!HasInputAuthority) return;
        PositionCamera();
    }

    private void HandleLook()
    {
        Vector2 delta = Vector2.zero;
        if (_mouse != null) delta += _mouse.delta.ReadValue();
        if (MobileInputManager.Instance != null) delta += MobileInputManager.Instance.LookDelta;

        _yaw   += delta.x * mouseSensitivity;
        _pitch -= delta.y * mouseSensitivity;
        _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // Мгновенный поворот тела по yaw
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    private void PositionCamera()
    {
        if (cam == null) return;

        float scroll = _mouse != null ? _mouse.scroll.ReadValue().y : 0f;
        camDistance  = Mathf.Clamp(camDistance - scroll * camScrollSpeed * 0.01f,
                                   camMinDistance, camMaxDistance);

        float heightT = Mathf.Clamp01((_cc.height - crouchHeight) / Mathf.Max(0.01f, _standHeight - crouchHeight));
        float currentPivotHeight = Mathf.Lerp(crouchCamPivotHeight, camPivotHeight, heightT);

        Vector3 targetPivot = transform.position + Vector3.up * currentPivotHeight;
        
        float targetBaseDist = camDistance;

        if (_grower != null && (_grower.IsGrowing || _grower.IsRetracting))
        {
            // Поднимаем точку фокусировки камеры, чтобы она не лежала прямо на земле и не проваливалась под текстуры
            targetPivot = _grower.LastBlockPos + Vector3.up * 1.5f;
            targetBaseDist = 5.5f;
            
            if (_grower.CurrentPlant == PlantType.Oak)
            {
                targetBaseDist = 8.5f;
            }

            if (_grower.InChamomileForm)
            {
                if (_grower.ActiveChamomilePetalId.IsValid)
                {
                    // Следим за лепестком
                    targetBaseDist = 5.5f * 0.6f;
                }
                else
                {
                    // Точка камеры на ромашке (чуть ниже центра или как раз на уровне груди, 0.8f)
                    targetPivot = transform.position + Vector3.up * 0.8f;
                    
                    if (_grower.IsChargingChamomile)
                    {
                        // Приближаемся максимально близко, чтобы перекрыть коллизию с землей (до 0.5 метров)
                        float chargeRatio = _grower.ChamomileChargePower / 20f;
                        targetBaseDist = Mathf.Lerp(5.5f, 0.5f, chargeRatio);
                    }
                }
            }
        }

        _pivotPos = Vector3.Lerp(_pivotPos, targetPivot, camFollowSpeed * Time.deltaTime);

        Quaternion camRot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    backDir = camRot * Vector3.back;

        Quaternion camYaw     = Quaternion.Euler(0f, _yaw, 0f);
        Vector3    rightOffset = camYaw * Vector3.right * camShoulderOffset;

        Vector3 pivotWithOffset = _pivotPos + rightOffset;

        float desiredDist = targetBaseDist;
        
        // Защита камеры от проникновения в стены (учитывая высокий FOV)
        // 1. Увеличиваем радиус SphereCast, чтобы учесть конусообразный фрустум камеры с высоким FOV
        float actualCollisionRadius = Mathf.Max(camCollisionRadius, cam.nearClipPlane * 1.5f);

        bool isGrowingOak = _grower != null && (_grower.IsGrowing || _grower.IsRetracting) && _grower.CurrentPlant == PlantType.Oak;
        bool isGrowingVine = _grower != null && (_grower.IsGrowing || _grower.IsRetracting) && _grower.CurrentPlant == PlantType.Vine;

        RaycastHit[] hits = Physics.SphereCastAll(pivotWithOffset, actualCollisionRadius,
                               backDir, targetBaseDist, camCollisionMask, QueryTriggerInteraction.Ignore);
        
        float closestHitDist = targetBaseDist;
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue; 
            
            // Игнорируем блоки дерева
            if (isGrowingOak && h.collider.GetComponentInParent<OakBlock>() != null) continue;
            if (isGrowingVine && h.collider.GetComponentInParent<VineBlock>() != null) continue;
            
            float hitDist = Mathf.Max(0f, h.distance); // Перестраховка если мы уже внутри (distance 0)
            if (hitDist < closestHitDist)
                closestHitDist = hitDist;
        }

        desiredDist = Mathf.Max(closestHitDist, camMinDistance);

        if (desiredDist < _currentCamDist)
            _currentCamDist = desiredDist;
        else
            _currentCamDist = Mathf.Lerp(_currentCamDist, desiredDist,
                                         camFollowSpeed * Time.deltaTime);

        // Гарантируем, что камера не провалится в саму стену даже при подходе вплотную
        Vector3 finalCamPos = pivotWithOffset + backDir * _currentCamDist;
        
        bool isInsideWall = false;
        Collider[] overlapHits = Physics.OverlapSphere(finalCamPos, cam.nearClipPlane * 1.1f, camCollisionMask, QueryTriggerInteraction.Ignore);
        foreach (var coll in overlapHits)
        {
            if (coll.transform.root == transform.root) continue;

            if (isGrowingOak && coll.GetComponentInParent<OakBlock>() != null) continue;
            if (isGrowingVine && coll.GetComponentInParent<VineBlock>() != null) continue;

            isInsideWall = true;
            break;
        }

        if (isInsideWall)
        {
             _currentCamDist = Mathf.Max(camMinDistance, _currentCamDist - cam.nearClipPlane * 1.5f);
             finalCamPos = pivotWithOffset + backDir * _currentCamDist;
        }

        cam.transform.position = finalCamPos;
        cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    // ══════════════════════════════════════════════════════════════
    //  ФИЗИЧЕСКИЙ СЕТЕВОЙ UPDATE (АВТОРИТАРНОЕ ДВИЖЕНИЕ)
    // ══════════════════════════════════════════════════════════════

    public override void FixedUpdateNetwork()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;

        if (GetInput(out NetworkInputData input))
        {
            _currentInput   = input;
            _currentPressed = input.Buttons.GetPressed(_prevButtons);
            _prevButtons    = input.Buttons;

            if (_grower != null)
            {
                if (!_grower.IsPlantSelected)
                {
                    // Разрешаем только падение (гравитацию), если растение еще не выбрано
                    HandleMovement();
                    return;
                }

                _grower.ProcessGrowth(input, _currentPressed);
            }

            bool isG = _grower != null && _grower.IsGrowing;
            bool isR = _grower != null && _grower.IsRetracting;

            // КЛЮЧЕВОЙ ФИКС ААА: Коллайдер должен быть ВЫКЛЮЧЕН строгим условием до начала всех расчетов, 
            // чтобы Unity не успевала посчитать sweep test при откатах сети (Rollback).
            if (isG || isR || _isMantling)
            {
                if (_cc.enabled) _cc.enabled = false;
            }
            else
            {
                if (!_cc.enabled) _cc.enabled = true;
            }

            if (!isG && !isR)
            {
                HandleCrouch();
                HandleDash();
                if (_isMantling)
                    HandleMantle();
                else if (!_isDashing)
                    HandleMovement();
            }
            HandleDashRecharge();
        }
    }

    private void HandleMovement()
    {
        // ВАЖНО: Используем LookAngles из сети, а не локальный transform.rotation!
        // Это предотвращает сломанную физику при откатах (Rollback) сети.
        Quaternion lookRot = Quaternion.Euler(0f, _currentInput.LookAngles.y, 0f);
        Vector3 netRight   = lookRot * Vector3.right;
        Vector3 netForward = lookRot * Vector3.forward;

        Vector2 input   = _currentInput.MoveDirection;
        Vector3 moveDir = netRight * input.x + netForward * input.y;

        if (moveDir.magnitude > 1f) moveDir.Normalize();

        float speed = _isCrouching ? crouchSpeed : walkSpeed;

        if (!_isCrouching)
        {
            HandleInteraction(); // <- Добавлена логика подбора предметов E

            bool jumpPressed = _currentPressed.IsSet(NetworkInputButtons.Jump);
            bool jumpHeld    = _currentInput.Buttons.IsSet(NetworkInputButtons.Jump);
            bool isMoving    = moveDir.sqrMagnitude > 0.01f;

            // Зацеп срабатывает если просто зажат пробел и мы идём в стену
            // (гораздо надёжнее чем ловить миллисекундный тайминг)
            if ((jumpPressed || (jumpHeld && isMoving)) && TryStartMantle()) return;
        }

        bool isClimbing = false;
        Vector3 vineCentroid = Vector3.zero;

        if (!_isCrouching) 
        {
            // Увеличен радиус капсулы до 0.9f, чтобы надежно цеплять лозу даже если коллайдеры отталкивают игрока
            Collider[] hits = Physics.OverlapCapsule(transform.position, transform.position + Vector3.up * 1.8f, 0.9f);
            foreach (var hit in hits)
            {
                VineBlock vine = hit.GetComponent<VineBlock>();
                if (vine == null) vine = hit.GetComponentInParent<VineBlock>();

                if (vine != null)
                {
                    isClimbing = true;
                    vineCentroid = hit.transform.position;
                    break;
                }
            }
        }

        if (isClimbing)
        {
            Vector3 vel = _velocity;
            float climbSpeed = speed * 1.2f; // Чуть быстрее лезем

            // Ползем вверх/вниз если нажимаем W или S
            vel.y = input.y * climbSpeed;
            // Убираем боковую инерцию при падении
            vel.x = 0;
            vel.z = 0;

            // Разрешаем стрейфить по сторонам лозы (A/D)
            Vector3 sideMove = netRight * input.x * climbSpeed * 0.6f;

            // Легонько притягиваем игрока к лозе, чтобы он не отлипал от стены
            Vector3 pushToVine = (vineCentroid - transform.position);
            pushToVine.y = 0;
            sideMove += pushToVine.normalized * 2.0f;

            if (_currentPressed.IsSet(NetworkInputButtons.Jump))
            {
                // Отскок от лозы
                vel.y = Mathf.Sqrt(jumpHeight * -1.5f * gravity);
                vel += -netForward * speed * 1.5f;
                _velocity = vel;
                _cc.Move(_velocity * Runner.DeltaTime);
                _isGrounded = _cc.isGrounded;
                return;
            }

            _velocity = vel;
            _cc.Move((sideMove + Vector3.up * vel.y) * Runner.DeltaTime);
            _isGrounded = _cc.isGrounded;
            return;
        }

        // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Используем занетворканый результат проверки земли (_isGrounded).
        // Иначе при сетевом откате (Rollback) Unity's _cc.isGrounded дает сбой и прыжок "съедается", вызывая дёргания.
        if (_isGrounded)
        {
            Vector3 vel = _velocity;
            // Легкое притяжение к земле, стандартное для CharacterController
            if (vel.y <= 0f) vel.y = -2f; 
            _velocity = vel;

            if (_currentPressed.IsSet(NetworkInputButtons.Jump) && !_isCrouching)
            {
                vel = _velocity;
                vel.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _velocity = vel;
            }
        }

        if (_isMantling) return;

        // Применяем гравитацию только если НЕ на земле (и не цепляемся за лозу/уступ)
        if (!_isGrounded)
        {
            Vector3 vel = _velocity;
            vel.y += gravity * Runner.DeltaTime;
            _velocity = vel;
        }

        _cc.Move((moveDir * speed + Vector3.up * _velocity.y) * Runner.DeltaTime);
        
        // Запоминаем стейт земли в сеть, чтобы в следующем кадре или при откате он был точен на 100%
        _isGrounded = _cc.isGrounded;
    }

    private bool TryStartMantle()
    {
        float feetY = transform.position.y + _standCenter.y - _standHeight * 0.5f;

        Quaternion lookRot = Quaternion.Euler(0f, _currentInput.LookAngles.y, 0f);
        Vector3 netRight   = lookRot * Vector3.right;
        Vector3 netForward = lookRot * Vector3.forward;

        Vector2 input   = _currentInput.MoveDirection;
        Vector3 moveDir = netRight * input.x + netForward * input.y;

        if (moveDir.sqrMagnitude < 0.01f) moveDir = netForward;
        else                              moveDir.Normalize();

        bool      wallFound = false;
        RaycastHit wallHit  = default;

        for (float t = 0.20f; t <= 0.90f; t += 0.15f)
        {
            Vector3 origin = new Vector3(transform.position.x, feetY + _standHeight * t, transform.position.z);
            if (Physics.SphereCast(origin, _cc.radius * 0.5f, moveDir, out RaycastHit hit, mantleReach))
            {
                // Игнорируем самого себя, других игроков, пули и предметы
                if (hit.collider.transform.root == transform.root) continue;
                if (hit.collider.GetComponentInParent<NetworkBehaviour>() != null && !hit.collider.gameObject.isStatic)
                    continue;

                // Проверяем наклоны. Если нормаль поверхности слишком "смотрит вверх"
                // (например склон горы / пандус), то не воспринимаем это как стену для Mantle (ограничение ~75 градусов).
                if (hit.normal.y > 0.25f)
                    continue;

                wallHit  = hit;
                wallFound = true;
                break;
            }
        }

        if (!wallFound) return false;

        float   aboveY    = feetY + mantleMaxHeight + 0.5f;
        Vector3 castAbove = new Vector3(wallHit.point.x, aboveY, wallHit.point.z)
                          + moveDir * (_cc.radius + 0.1f);

        if (!Physics.Raycast(castAbove, Vector3.down, out RaycastHit ledgeHit, mantleMaxHeight + 1f))
            return false;

        float ledgeH = ledgeHit.point.y - feetY;

        if (ledgeH < mantleMinHeight || ledgeH > mantleMaxHeight)
            return false;

        float feetToPivot = -_standCenter.y + _standHeight * 0.5f;

        float phase1FeetY = ledgeHit.point.y + 0.25f;
        Vector3 topPos = new Vector3(transform.position.x, phase1FeetY + feetToPivot, transform.position.z);

        float phase2FeetY = ledgeHit.point.y;
        Vector3 endPos = new Vector3(
            ledgeHit.point.x + moveDir.x * mantleForwardStep,
            phase2FeetY + feetToPivot,
            ledgeHit.point.z + moveDir.z * mantleForwardStep
        );

        // ── ЗАЩИТА ОТ ПРОХОЖДЕНИЯ СКВОЗЬ СТЕНЫ ──
        int layerMask = ~0 & ~(1 << gameObject.layer) & ~(1 << 10); // Важно не зацепиться за другого игрока!
        
        float safeRadius = _cc.radius * 0.8f;
        
        // 1. Проверяем путь СНИЗУ ВВЕРХ (чтобы не удариться головой о балкон или залезть в текстуру)
        Vector3 startP1 = transform.position + _standCenter - Vector3.up * (_standHeight * 0.5f - safeRadius);
        Vector3 startP2 = transform.position + _standCenter + Vector3.up * (_standHeight * 0.5f - safeRadius);
        Vector3 dirToTop = topPos - transform.position;
        if (Physics.CapsuleCast(startP1, startP2, safeRadius, dirToTop.normalized, out RaycastHit upHit, dirToTop.magnitude, layerMask, QueryTriggerInteraction.Ignore))
        {
            return false; // Заблокировано сверху
        }

        // 2. Проверяем не застрянем ли мы в высшей точке (topPos)
        Vector3 topP1 = topPos + _standCenter - Vector3.up * (_standHeight * 0.5f - safeRadius);
        Vector3 topP2 = topPos + _standCenter + Vector3.up * (_standHeight * 0.5f - safeRadius);
        if (Physics.CheckCapsule(topP1, topP2, safeRadius, layerMask, QueryTriggerInteraction.Ignore))
        {
            return false; // Точка над уступом уже находится в текстуре
        }

        // 3. Плавно пытаемся продвинуться вперед (на сам уступ)
        Vector3 dirToEnd = endPos - topPos;
        float distToEnd = dirToEnd.magnitude;

        if (distToEnd > 0.01f)
        {
            if (Physics.CapsuleCast(topP1, topP2, safeRadius, dirToEnd.normalized, out RaycastHit fwdHit, distToEnd, layerMask, QueryTriggerInteraction.Ignore))
            {
                // Если нету места чтобы перебросить хотя-бы малую часть тела — отменяем прыжок
                if (fwdHit.distance < safeRadius * 0.5f) return false;

                // Спасаем прыжок, но останавливаемся перед стеной
                endPos = topPos + dirToEnd.normalized * Mathf.Max(0f, fwdHit.distance - 0.05f);
            }
        }

        // Если все проверки пройдены — начинаем перемещение
        _isMantling     = true;
        _mantleProgress = 0f;
        _mantleStartPos = transform.position;
        _mantleTopPos   = topPos;
        _mantleEndPos   = endPos;

        _velocity = Vector3.zero;
        return true;
    }

    private void HandleMantle()
    {
        float heightDiff    = Mathf.Max(0.1f, _mantleTopPos.y - _mantleStartPos.y);
        float progressSpeed = mantleSpeed / heightDiff;

        _mantleProgress = Mathf.MoveTowards(_mantleProgress, 1f, progressSpeed * Runner.DeltaTime);

        const float phaseSplit = 0.65f;
        Vector3 newPos;

        if (_mantleProgress < phaseSplit)
        {
            float t1 = Mathf.SmoothStep(0f, 1f, _mantleProgress / phaseSplit);
            newPos = Vector3.Lerp(_mantleStartPos, _mantleTopPos, t1);
        }
        else
        {
            float t2 = Mathf.SmoothStep(0f, 1f, (_mantleProgress - phaseSplit) / (1f - phaseSplit));
            newPos = Vector3.Lerp(_mantleTopPos, _mantleEndPos, t2);
        }

        // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Так как _cc.enabled выключен строго в начале кадра,
        // мы имеем полное право просто двигать transform.position. 
        // Это гарантирует 0% взаимодействия с физикой и идеальную плавность при любых откатах.
        transform.position = newPos;

        if (_mantleProgress >= 1f)
        {
            _isMantling = false;
            _velocity   = Vector3.zero;
            // Коллайдер включится сам в начале следующего кадра
        }
    }

    private void HandleInteraction()
    {
        if (_currentPressed.IsSet(NetworkInputButtons.Interact) && HasStateAuthority && _inventory != null)
        {
            // Используем Сферу впереди игрока (OverlapSphere), чтобы не нужно было идеально целиться лучом
            Vector3 checkPos = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
            float checkRadius = 2f;
            
            // Находим все коллайдеры вокруг
            Collider[] hits = Physics.OverlapSphere(checkPos, checkRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var coll in hits)
            {
                WorldItem item = coll.GetComponentInParent<WorldItem>();
                if (item != null)
                {
                    // Пытаемся взять предмет. Берем только первый успешный.
                    if (_inventory.TryPickupItem(item.ItemID))
                    {
                        Runner.Despawn(item.Object);
                        break;
                    }
                }
            }
        }
    }

    private void HandleDash()
    {
        if (_currentPressed.IsSet(NetworkInputButtons.Dash) && _dashCharges > 0 && !_isDashing && _isGrounded)
        {
            Quaternion lookRot = Quaternion.Euler(0f, _currentInput.LookAngles.y, 0f);
            Vector2 input      = _currentInput.MoveDirection;
            Vector3 inputDir   = (lookRot * Vector3.right) * input.x + (lookRot * Vector3.forward) * input.y;
            
            _dashDirection   = inputDir.magnitude > 0.1f ? inputDir.normalized : (lookRot * Vector3.forward);

            _isDashing   = true;
            _dashTimer   = dashDuration;
            _dashCharges--;
            
            Vector3 vel = _velocity;
            vel.y = 0f;
            _velocity = vel;
        }

        if (_isDashing)
        {
            _dashTimer -= Runner.DeltaTime;
            float t = _dashTimer / dashDuration;
            Vector3 dashMove = _dashDirection * dashForce * t * Runner.DeltaTime;
            
            Vector3 vel = _velocity;
            vel.y += gravity * Runner.DeltaTime * 0.3f;
            _velocity = vel;
            
            dashMove.y  += _velocity.y * Runner.DeltaTime;
            _cc.Move(dashMove);

            if (_dashTimer <= 0f) _isDashing = false;
        }
    }

    private void HandleDashRecharge()
    {
        if (_dashCharges < maxDashCharges)
        {
            _dashCooldownTimer += Runner.DeltaTime;
            if (_dashCooldownTimer >= dashCooldown)
            {
                _dashCharges++;
                _dashCooldownTimer = 0f;
            }
        }
        else _dashCooldownTimer = 0f;
    }

    private void HandleCrouch()
    {
        bool wants = _currentInput.Buttons.IsSet(NetworkInputButtons.Crouch);

        if (wants && !_isCrouching)
        {
            _isCrouching    = true;
            _targetCCHeight = crouchHeight;
            float delta     = _standHeight - crouchHeight;
            _targetCCCenter = new Vector3(_standCenter.x, _standCenter.y - delta * 0.5f, _standCenter.z);
        }
        else if (!wants && _isCrouching && !IsCeilingAbove())
        {
            _isCrouching    = false;
            _targetCCHeight = _standHeight;
            _targetCCCenter = _standCenter;
        }
    }

    private void ApplyColliderTransition()
    {
        if (!_cc.enabled) return;

        float t    = crouchTransitionSpeed * Time.deltaTime;
        _cc.height = Mathf.Lerp(_cc.height, _targetCCHeight, t);
        _cc.center = Vector3.Lerp(_cc.center, _targetCCCenter, t);
    }

    private bool IsCeilingAbove()
    {
        float feetY = transform.position.y + _standCenter.y - _standHeight * 0.5f;
        float r = _cc.radius * 0.9f;

        float bottomY = feetY + crouchHeight + r;
        float topY    = feetY + _standHeight - r;

        if (bottomY > topY) bottomY = topY;

        Vector3 p1 = new Vector3(transform.position.x, bottomY, transform.position.z);
        Vector3 p2 = new Vector3(transform.position.x, topY,    transform.position.z);

        bool wasEnabled = _cc.enabled;
        if (wasEnabled) _cc.enabled = false;

        int layerMask = ~0 & ~(1 << gameObject.layer) & ~(1 << 10);
        bool hit = Physics.CheckCapsule(p1, p2, r, layerMask, QueryTriggerInteraction.Ignore);

        if (wasEnabled) _cc.enabled = true;

        return hit;
    }

    public int   DashCharges          => _dashCharges;
    public float DashRechargeProgress => Mathf.Clamp01(_dashCooldownTimer / dashCooldown);
    public bool  IsDashing            => _isDashing;
    public bool  IsCrouching          => _isCrouching;
    public bool  IsGrounded           => _cc.isGrounded;
    public PlantGrower Grower         => _grower;

    // ══════════════════════════════════════════════════════════════
    //  КУРСОР (UI)
    // ══════════════════════════════════════════════════════════════
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ══════════════════════════════════════════════════════════════
    //  PING SYSTEM
    // ══════════════════════════════════════════════════════════════

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendPing(Vector3 pos, string pName)
    {
        if (Local != null)
        {
            Local.AddPingLocally(pos, pName);
        }
    }

    private void AddPingLocally(Vector3 pos, string pName)
    {
        GeneratePingAssets();
        _activePings.Add(new PingMarker { Position = pos, SpawnTime = Time.time, PlayerName = pName });
        
        // Воспроизводим звук локально, создавая временный AudioSource в месте пинга
        AudioSource.PlayClipAtPoint(_proceduralPingSound, pos, 0.8f);

        // ── Создаем физически видимый 3D-луч (столб света) как в Apex/Fortnite ──
        GameObject beamObj = new GameObject("PingBeam");
        beamObj.transform.position = pos;
        
        LineRenderer lr = beamObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, pos); // Начинается четко в точке пинга
        lr.SetPosition(1, pos + Vector3.up * 50f); // Уходит высоко в небо
        lr.startWidth = 0.4f;
        lr.endWidth = 0.4f;
        lr.material = new Material(Shader.Find("Sprites/Default")); // Светящийся неулиточный материал
        lr.startColor = new Color(1f, 0.7f, 0.1f, 0.7f); // Ярко-желтый снизу
        lr.endColor = new Color(1f, 0.7f, 0.1f, 0f);     // Растворяется вверху
        
        Destroy(beamObj, 5f); // Исчезнет ровно через 5 секунд вместе с меткой в OnGUI
    }

    private void GeneratePingAssets()
    {
        if (_pingTexture != null) return;
        
        // 1. Процедурная текстура "ромбика" (Метка)
        _pingTexture = new Texture2D(32, 32);
        Color clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Abs(x - 16f);
                float dy = Mathf.Abs(y - 16f);
                if (dx + dy < 14f)
                {
                    if (dx + dy < 10f) _pingTexture.SetPixel(x, y, new Color(1f, 0.8f, 0.2f)); 
                    else _pingTexture.SetPixel(x, y, Color.black);
                }
                else _pingTexture.SetPixel(x, y, clear);
            }
        }
        _pingTexture.Apply();

        // 2. Процедурный звук (Короткий "Бип")
        int sampleRate = 44100;
        float duration = 0.3f;
        _proceduralPingSound = AudioClip.Create("Ping", (int)(sampleRate * duration), 1, sampleRate, false);
        float[] samples = new float[_proceduralPingSound.samples];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / sampleRate;
            // Создаем приятный электронный звук
            samples[i] = Mathf.Sin(t * 2f * Mathf.PI * 1000f) * Mathf.Exp(-t * 15f) * 0.5f;
        }
        _proceduralPingSound.SetData(samples, 0);
    }

    private void OnGUI()
    {
        if (!HasInputAuthority || cam == null || _activePings == null || _pingTexture == null) return;
        
        float currentTime = Time.time;
        for (int i = _activePings.Count - 1; i >= 0; i--)
        {
            var ping = _activePings[i];
            float age = currentTime - ping.SpawnTime;
            float lifetime = 5f; // Живет 5 секунд

            if (age >= lifetime)
            {
                _activePings.RemoveAt(i);
                continue;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(ping.Position);
            if (screenPos.z < 0) continue; // Позади камеры

            bool occluded = false;
            Vector3 camPos = cam.transform.position;
            Vector3 dir = ping.Position - camPos;
            float dist = dir.magnitude;
            
            // Проверка преград (чтобы отследить "перекрытие" стенами)
            if (Physics.Raycast(camPos, dir.normalized, dist - 0.2f, camCollisionMask, QueryTriggerInteraction.Ignore))
            {
                occluded = true;
            }

            // Плавное исчезновение
            float alpha = 1f;
            if (lifetime - age < 1f) alpha = lifetime - age; 

            // Затемнение, если за стеной
            Color drawColor = occluded ? new Color(0.3f, 0.3f, 0.3f, alpha * 0.5f) : new Color(1f, 1f, 1f, alpha);
            Color oldColor = GUI.color;
            GUI.color = drawColor;

            // Легкая пульсация по размеру
            float size = 32f;
            float pulse = Mathf.Sin(age * 15f) * 0.15f + 1f; 
            size *= pulse;

            // Рисуем иконку
            Rect rect = new Rect(screenPos.x - size / 2f, Screen.height - screenPos.y - size / 2f, size, size);
            GUI.DrawTexture(rect, _pingTexture);

            // Текст (С именем и дистанцией)
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = drawColor;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            
            Rect labelRect = new Rect(screenPos.x - 75f, Screen.height - screenPos.y + size / 2f, 150f, 40f);
            int distInt = Mathf.RoundToInt(dist);
            GUI.Label(labelRect, $"{ping.PlayerName}\n{distInt}m", style);

            GUI.color = oldColor;
        }
    }
}
