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
    [SerializeField] private float camCollisionRadius = 0.1f;
    [SerializeField] private LayerMask camCollisionMask = ~0;
    [SerializeField] private float camFollowSpeed = 12f;

    [Header("── Камера (Дерево) ──────────────")]
    [SerializeField] private float treeCamDistance = 12f;

    [Header("── Параметры Растения ────────────────────")]
    [SerializeField] private float growthSpeed = 5f;
    [SerializeField] private float maxGrowthDistance = 20f;
    [SerializeField] private NetworkPrefabRef oakBlockPrefab; 
    [SerializeField] private NetworkPrefabRef vineBlockPrefab; 
    [SerializeField] private float blockSpacing = 0.6f;



    // ══════════════════════════════════════════════════════════════
    //  ПРИВАТНОЕ СОСТОЯНИЕ И СЕТЬ
    // ══════════════════════════════════════════════════════════════

    public static PlayerController Local { get; private set; }

    private CharacterController _cc;
    private Keyboard _kb;
    private Mouse    _mouse;

    [Networked] private Vector3 _velocity { get; set; }
    [Networked] private NetworkBool _isGrounded { get; set; }

    // Характеристики класса
    [Networked] public PlantType CurrentPlant { get; private set; }
    [Networked] public float Fertilizer { get; private set; } = 100f;
    [Networked] public NetworkBool IsGrowing { get; private set; }


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

    // Состояние роста
    [Networked] private Vector3 _growthHeadPos { get; set; }
    [Networked] private Vector3 _lastBlockPos { get; set; }
    [Networked] private float _currentGrowthDist { get; set; }
    [Networked] private Vector3 _initialGrowthDir { get; set; }
    
    [Networked] public NetworkBool IsRetracting { get; private set; }
    [Networked, Capacity(100)] private NetworkArray<NetworkId> _spawnedBlocks { get; }
    [Networked] private int _spawnedBlocksCount { get; set; }
    [Networked] private float _retractTimer { get; set; }
    
    [Networked] private float _treeIdleTimer { get; set; }
    [Networked] private float _treeIdleDuration { get; set; }



    // Камера — локально
    private float   _yaw;
    private float   _pitch;
    private Vector3 _pivotPos;
    private float   _currentCamDist;
    private Renderer[] _renderers;

    // ══════════════════════════════════════════════════════════════
    //  ИНТЕРФЕЙС NETWORK BEHAVIOUR
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _renderers = GetComponentsInChildren<Renderer>(true);

        _standHeight    = _cc.height;
        _standCenter    = _cc.center;
        _targetCCHeight = _cc.height;
        _targetCCCenter = _cc.center;

        if (crouchHeight >= _standHeight)
            crouchHeight = _standHeight * 0.5f;
    }

    public override void Spawned()
    {
        if (cam != null)
        {
            cam.gameObject.SetActive(HasInputAuthority);
            AudioListener audioListener = cam.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = HasInputAuthority;
        }

        if (HasInputAuthority)
        {
            Local = this;
            if (cam == null) cam = Camera.main;
            _yaw             = transform.eulerAngles.y;
            _pitch           = 15f;
            _currentCamDist  = camDistance;
            _pivotPos        = transform.position + Vector3.up * camPivotHeight;
            if (cam != null) camCollisionMask &= ~(1 << gameObject.layer);
        }

        if (HasStateAuthority)
        {
            _dashCharges = maxDashCharges;
            CurrentPlant = PlantType.Oak; // Для теста ставим Дуб
        }
    }



    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
    }

    // Собираем локальный ввод. Вызывается из NetworkManager.OnInput
    public NetworkInputData GetLocalInput()
    {
        var data = new NetworkInputData();
        _kb = Keyboard.current;
        if (_kb == null) return data;

        float h = 0f, v = 0f;
        if (_kb.dKey.isPressed || _kb.rightArrowKey.isPressed)  h += 1f;
        if (_kb.aKey.isPressed || _kb.leftArrowKey.isPressed)   h -= 1f;
        if (_kb.wKey.isPressed || _kb.upArrowKey.isPressed)     v += 1f;
        if (_kb.sKey.isPressed || _kb.downArrowKey.isPressed)   v -= 1f;
        data.MoveDirection = new Vector2(h, v);

        data.Buttons.Set(NetworkInputButtons.Jump,   _kb.spaceKey.isPressed);
        data.Buttons.Set(NetworkInputButtons.Crouch, _kb.leftCtrlKey.isPressed);
        data.Buttons.Set(NetworkInputButtons.Dash,   _kb.leftShiftKey.isPressed);
        data.Buttons.Set(NetworkInputButtons.Action, _kb.fKey.isPressed);
        data.Buttons.Set(NetworkInputButtons.PlantOak, _kb.digit1Key.isPressed);
        data.Buttons.Set(NetworkInputButtons.PlantVine, _kb.digit2Key.isPressed);


        data.LookAngles = new Vector2(_pitch, _yaw);
        return data;
    }

    // ══════════════════════════════════════════════════════════════
    //  ГРАФИЧЕСКИЙ UPDATE (АНИМАЦИЯ, КАМЕРА, МЫШЬ)
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;

        ApplyColliderTransition();

        bool shouldBeVisible = !IsGrowing && !IsRetracting;
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
        if (_kb == null || _mouse == null) return;

        if (_kb.tabKey.isPressed)
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
    }

    private void LateUpdate()
    {
        if (Object == null || !Object.IsValid) return;
        if (!HasInputAuthority || _mouse == null) return;
        PositionCamera();
    }

    private void HandleLook()
    {
        Vector2 delta = _mouse.delta.ReadValue();
        _yaw   += delta.x * mouseSensitivity;
        _pitch -= delta.y * mouseSensitivity;
        _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // Мгновенный поворот тела по yaw
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    private void PositionCamera()
    {
        if (cam == null) return;

        float scroll = _mouse.scroll.ReadValue().y;
        camDistance  = Mathf.Clamp(camDistance - scroll * camScrollSpeed * 0.01f,
                                   camMinDistance, camMaxDistance);

        float heightT = Mathf.Clamp01((_cc.height - crouchHeight) / Mathf.Max(0.01f, _standHeight - crouchHeight));
        float currentPivotHeight = Mathf.Lerp(crouchCamPivotHeight, camPivotHeight, heightT);

        Vector3 targetPivot = transform.position + Vector3.up * currentPivotHeight;
        
        float targetBaseDist = camDistance;

        if (IsGrowing || IsRetracting)
        {
            targetPivot = _lastBlockPos;
            targetBaseDist = treeCamDistance;
        }

        _pivotPos = Vector3.Lerp(_pivotPos, targetPivot, camFollowSpeed * Time.deltaTime);

        Quaternion camRot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    backDir = camRot * Vector3.back;

        Quaternion camYaw     = Quaternion.Euler(0f, _yaw, 0f);
        Vector3    rightOffset = camYaw * Vector3.right * camShoulderOffset;

        Vector3 pivotWithOffset = _pivotPos + rightOffset;

        float desiredDist = targetBaseDist;
        if (Physics.SphereCast(pivotWithOffset, camCollisionRadius,
                               backDir, out RaycastHit hit,
                               targetBaseDist, camCollisionMask,
                               QueryTriggerInteraction.Ignore))
        {
            desiredDist = Mathf.Max(hit.distance, camMinDistance);
        }

        if (desiredDist < _currentCamDist)
            _currentCamDist = desiredDist;
        else
            _currentCamDist = Mathf.Lerp(_currentCamDist, desiredDist,
                                         camFollowSpeed * Time.deltaTime);

        cam.transform.position = pivotWithOffset + backDir * _currentCamDist;
        cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    // ══════════════════════════════════════════════════════════════
    //  ФИЗИЧЕСКИЙ СЕТЕВОЙ UPDATE (АВТОРИТАРНОЕ ДВИЖЕНИЕ)
    // ══════════════════════════════════════════════════════════════

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input))
        {
            _currentInput   = input;
            _currentPressed = input.Buttons.GetPressed(_prevButtons);
            _prevButtons    = input.Buttons;

            if (!IsGrowing && !IsRetracting)
            {
                if (_currentPressed.IsSet(NetworkInputButtons.PlantOak)) CurrentPlant = PlantType.Oak;
                if (_currentPressed.IsSet(NetworkInputButtons.PlantVine)) CurrentPlant = PlantType.Vine;
            }

            HandleGrowth();

            if (IsGrowing || IsRetracting)
            {
                if (_cc.enabled) _cc.enabled = false;
            }
            else
            {
                if (!_cc.enabled) _cc.enabled = true;
            }

            if (!IsGrowing && !IsRetracting)
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
            bool jumpPressed = _currentPressed.IsSet(NetworkInputButtons.Jump);
            bool jumpHeld    = _currentInput.Buttons.IsSet(NetworkInputButtons.Jump);
            bool isMoving    = moveDir.sqrMagnitude > 0.01f;

            // Зацеп срабатывает если просто зажат пробел и мы идём в стену
            // (гораздо надёжнее чем ловить миллисекундный тайминг)
            if ((jumpPressed || (jumpHeld && isMoving)) && TryStartMantle()) return;
        }

        // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Используем занетворканый результат проверки земли (_isGrounded).
        // Иначе при сетевом откате (Rollback) Unity's _cc.isGrounded дает сбой и прыжок "съедается", вызывая дёргания.
        if (_isGrounded)
        {
            Vector3 vel = _velocity;
            if (vel.y < 0f) vel.y = -2f;
            _velocity = vel;

            if (_currentPressed.IsSet(NetworkInputButtons.Jump) && !_isCrouching)
            {
                vel = _velocity;
                vel.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _velocity = vel;
            }
        }

        if (_isMantling) return;

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

        _isMantling     = true;
        _mantleProgress = 0f;
        _mantleStartPos = transform.position;

        float feetToPivot = -_standCenter.y + _standHeight * 0.5f;

        float phase1FeetY = ledgeHit.point.y + 0.25f;
        _mantleTopPos = new Vector3(transform.position.x, phase1FeetY + feetToPivot, transform.position.z);

        float phase2FeetY = ledgeHit.point.y;
        _mantleEndPos = new Vector3(
            ledgeHit.point.x + moveDir.x * mantleForwardStep,
            phase2FeetY + feetToPivot,
            ledgeHit.point.z + moveDir.z * mantleForwardStep
        );

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

        // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Отключаем CC при жестком изменении позиции.
        // Иначе физика борется с сетевым транспортом, вызывая судороги.
        _cc.enabled = false;
        transform.position = newPos;
        _cc.enabled = true;

        if (_mantleProgress >= 1f)
        {
            _isMantling = false;
            _velocity   = Vector3.zero;
        }
    }

    private void HandleDash()
    {
        if (_currentPressed.IsSet(NetworkInputButtons.Dash) && _dashCharges > 0 && !_isDashing)
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

    private void HandleGrowth()
    {
        if (_currentPressed.IsSet(NetworkInputButtons.Action))
        {
            if (IsGrowing || IsRetracting)
            {
                IsGrowing = false;
                IsRetracting = true;
                _retractTimer = 0f;
            }
            else if (!IsRetracting)
            {
                if (CurrentPlant == PlantType.Vine)
                {
                    IsGrowing = true;
                    float feetY = transform.position.y + _standCenter.y - _standHeight * 0.5f;
                    Quaternion lookRot = Quaternion.Euler(0f, _currentInput.LookAngles.y, 0f);
                    Vector3 forward = lookRot * Vector3.forward;
                    _initialGrowthDir = forward; // Запоминаем изначальное направление взгляда
                    
                    Vector3 scanStart = new Vector3(transform.position.x, feetY + 0.2f, transform.position.z);
                    
                    Vector3 targetStartPos = scanStart + forward * 0.5f; 
                    
                    // Ищем ближайший край обрыва в пределах 3 метров
                    for (float dist = 0.5f; dist <= 3.0f; dist += 0.25f)
                    {
                        Vector3 p = scanStart + forward * dist + Vector3.up * 0.5f;
                        bool foundSurface = false;
                        
                        foreach (var hit in Physics.RaycastAll(p, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (hit.collider.transform.root == transform.root) continue;
                            if (hit.collider.GetComponentInParent<NetworkBehaviour>() != null && !hit.collider.gameObject.isStatic) continue;
                            foundSurface = true;
                            break;
                        }

                        if (!foundSurface)
                        {
                            // Край найден! Начинаем от края
                            targetStartPos = scanStart + forward * (dist - 0.25f);
                            break;
                        }
                    }

                    // Ищем высоту пола у стартовой точки лозы, чтобы прилепить её
                    bool surfaceForStart = false;
                    foreach (var hit in Physics.RaycastAll(targetStartPos + Vector3.up * 0.5f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.root == transform.root) continue;
                        if (hit.collider.GetComponentInParent<NetworkBehaviour>() != null && !hit.collider.gameObject.isStatic) continue;
                        
                        _growthHeadPos = hit.point + Vector3.up * 0.1f;
                        surfaceForStart = true;
                        break;
                    }

                    if (!surfaceForStart) _growthHeadPos = targetStartPos;

                    _lastBlockPos = _growthHeadPos;
                    _currentGrowthDist = 0f;
                    _treeIdleTimer = 0f;
                    _treeIdleDuration = 15f + ((float)(Runner.Tick % 500) / 100f);
                }
                else
                {
                    IsGrowing = true;
                    float feetY = transform.position.y + _standCenter.y - _standHeight * 0.5f;
                    // Начинаем рост чуть выше земли (чтобы было видно корень)
                    _growthHeadPos = new Vector3(transform.position.x, feetY + 0.2f, transform.position.z);
                    _lastBlockPos = _growthHeadPos;
                    _currentGrowthDist = 0f;
                    
                    _treeIdleTimer = 0f;
                    _treeIdleDuration = 15f + ((float)(Runner.Tick % 500) / 100f);
                }
            }
        }

        if (IsRetracting)
        {
            _retractTimer += Runner.DeltaTime;
            // Скорость разрушения дерева (1 куб за 0.05 сек)
            float retractDelay = 0.05f; 
            
            while (_retractTimer >= retractDelay)
            {
                _retractTimer -= retractDelay;
                if (_spawnedBlocksCount > 0)
                {
                    _spawnedBlocksCount--;
                    NetworkId idToDespawn = _spawnedBlocks[_spawnedBlocksCount];
                    
                    if (HasStateAuthority)
                    {
                        NetworkObject obj = Runner.FindObject(idToDespawn);
                        if (obj != null)
                        {
                            Runner.Despawn(obj);
                        }
                    }

                    if (_spawnedBlocksCount > 0)
                    {
                        NetworkId prevId = _spawnedBlocks[_spawnedBlocksCount - 1];
                        NetworkObject prevObj = Runner.FindObject(prevId);
                        if (prevObj != null) 
                            _lastBlockPos = prevObj.transform.position;
                    }
                    else
                    {
                        IsRetracting = false;
                        _lastBlockPos = transform.position;
                        break;
                    }
                }
                else
                {
                    IsRetracting = false;
                    break;
                }
            }
        }
        else if (IsGrowing)
        {
            _treeIdleTimer += Runner.DeltaTime;
            if (_treeIdleTimer >= _treeIdleDuration)
            {
                IsGrowing = false;
                IsRetracting = true;
                _retractTimer = 0f;
                return;
            }

            bool isGrowingInputActive = false;
            Vector3 growDir = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (CurrentPlant == PlantType.Vine)
            {
                // Зажатая кнопка F означает активный рост для лозы вниз (или вперед до края)
                isGrowingInputActive = _currentInput.Buttons.IsSet(NetworkInputButtons.Action);
                
                // ВАЖНО: Мы используем направление, сохраненное в МОМЕНТ НАЧАЛА РОСТА, а не крутим за мышкой каждый кадр!
                Vector3 forward = _initialGrowthDir;

                // Проверяем, есть ли пол (проверяем из центра вперед-вниз)
                Vector3 checkStart = _growthHeadPos + Vector3.up * 0.5f;
                bool hasFloor = false;

                foreach(var hit in Physics.RaycastAll(checkStart, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.root == transform.root) continue;
                    if (hit.collider.GetComponentInParent<NetworkBehaviour>() != null && !hit.collider.gameObject.isStatic) continue;
                    
                    hasFloor = true;
                    break;
                }

                if (hasFloor)
                {
                    // Пол есть, ползем вперед (вдоль пола до края)
                    growDir = forward;
                    spawnRot = Quaternion.LookRotation(Vector3.up, forward); 
                }
                else
                {
                    // Обрыв! Ползем вниз
                    growDir = Vector3.down;
                    spawnRot = Quaternion.LookRotation(-forward, Vector3.up); 
                }
            }
            else // Oak
            {
                // Зажатая кнопка W (вперед) означает рост для дуба
                isGrowingInputActive = _currentInput.MoveDirection.y > 0.1f;
                spawnRot = Quaternion.Euler(_currentInput.LookAngles.x, _currentInput.LookAngles.y, 0f);
                growDir = spawnRot * Vector3.forward;
            }

            if (isGrowingInputActive && _currentGrowthDist < maxGrowthDistance)
            {
                // Двигаем голову (если есть удобрения)
                if (Fertilizer > 0)
                {
                    float consumed = growthSpeed * Runner.DeltaTime * 10f; // 10 единиц в секунду
                    Fertilizer -= consumed;

                    _growthHeadPos += growDir * growthSpeed * Runner.DeltaTime;
                    _currentGrowthDist += growthSpeed * Runner.DeltaTime;

                    if (CurrentPlant == PlantType.Vine)
                    {
                        // Вместо широкой сферы, которая цепляет полки со всех сторон, 
                        // лоза ищет стену ТОЛЬКО за своей "спиной" (противоположно её forward-у)
                        // либо ровно под собой (если ползет по полу)
                        Vector3 scanDir = (growDir == Vector3.down) ? -(spawnRot * Vector3.forward) : Vector3.down;
                        Vector3 scanOrigin = _growthHeadPos - scanDir * 0.5f;

                        if (Physics.Raycast(scanOrigin, scanDir, out RaycastHit wallHit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (wallHit.collider.transform.root != transform.root &&
                                (wallHit.collider.GetComponentInParent<NetworkBehaviour>() == null || wallHit.collider.gameObject.isStatic))
                            {
                                Vector3 surfaceNormal = wallHit.normal;

                                // Если лоза ползет ВНИЗ, запрещаем ей "ложиться" на верхние грани полок
                                bool shouldSnap = true;
                                if (growDir == Vector3.down && surfaceNormal.y > 0.8f)
                                {
                                    shouldSnap = false;
                                }

                                if (shouldSnap)
                                {
                                    Vector3 targetPos = wallHit.point + surfaceNormal * 0.15f;
                                    
                                    // Если ползем по полу (вперед), магнитим только по высоте Y
                                    if (growDir != Vector3.down)
                                    {
                                        targetPos.x = _growthHeadPos.x;
                                        targetPos.z = _growthHeadPos.z;
                                    }
                                    
                                    _growthHeadPos = Vector3.Lerp(_growthHeadPos, targetPos, 15f * Runner.DeltaTime);

                                    // Направляем ось Y блока на предыдущий спавн, чтобы при растяжении они точно соединялись
                                    Vector3 toLast = (_lastBlockPos - _growthHeadPos).normalized;
                                    if (toLast.sqrMagnitude < 0.001f)
                                    {
                                        toLast = (growDir == Vector3.down) ? Vector3.up : -growDir.normalized;
                                    }

                                    // Поворачиваем лозу: Z (самая плоская сторона) смотрит ОТ стены, а Y (длина) тянется назад к прошлому блоку для соединения!
                                    spawnRot = Quaternion.LookRotation(surfaceNormal, toLast);
                                }
                            }
                        }
                    }

                    // Спавним блок, если отошли достаточно далеко
                    if (Vector3.Distance(_growthHeadPos, _lastBlockPos) >= blockSpacing)
                    {
                        if (HasStateAuthority)
                        {
                            NetworkPrefabRef prefabToSpawn = CurrentPlant == PlantType.Vine ? vineBlockPrefab : oakBlockPrefab;
                            if (prefabToSpawn != NetworkPrefabRef.Empty)
                            {
                                // Задаем TargetScale, чтобы блоки лозы растягивались по своей оси Y и перекрывали пустоты
                                Vector3 spawnScale = Vector3.one;
                                if (CurrentPlant == PlantType.Vine)
                                {
                                    float distToLast = Vector3.Distance(_lastBlockPos, _growthHeadPos);
                                    // Делаем длину блока в 1.5 раза больше расстояния между ними, чтобы они входили друг в друга (без просечек)
                                    spawnScale = new Vector3(1f, Mathf.Max(1.0f, distToLast * 2.5f), 1f);
                                }

                                NetworkObject obj = Runner.Spawn(prefabToSpawn, _growthHeadPos, spawnRot, Object.InputAuthority, (runner, no) => {
                                    OakBlock blockScript = no.GetComponent<OakBlock>();
                                    if (blockScript != null)
                                    {
                                        blockScript.TargetScale = spawnScale;
                                    }
                                });
                                
                                if (obj != null && _spawnedBlocksCount < _spawnedBlocks.Length)
                                {
                                    _spawnedBlocks.Set(_spawnedBlocksCount, obj.Id);
                                    _spawnedBlocksCount++;
                                }
                            }
                        }
                        _lastBlockPos = _growthHeadPos;

                        _treeIdleTimer = 0f;
                        _treeIdleDuration = 15f + ((float)(Runner.Tick % 500) / 100f);
                    }
                }
                else
                {
                    // Нет удобрений - рост временно прекращен, идет таймер простоя
                }
            }
            else if (!isGrowingInputActive)
            {
                // Регенерация удобрений когда не растем (но в режиме роста)
                Fertilizer = Mathf.Min(100f, Fertilizer + Runner.DeltaTime * 5f);
            }
        }
        else
        {
             // Регенерация удобрений в обычном режиме
             Fertilizer = Mathf.Min(100f, Fertilizer + Runner.DeltaTime * 15f);
        }
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

        // Используем Time.deltaTime потому что это визуальное сглаживание коллайдера (работает и на клиентах)
        float t    = crouchTransitionSpeed * Time.deltaTime;
        _cc.height = Mathf.Lerp(_cc.height, _targetCCHeight, t);
        _cc.center = Vector3.Lerp(_cc.center, _targetCCCenter, t);
    }

    private bool IsCeilingAbove()
    {
        float feetY = transform.position.y + _standCenter.y - _standHeight * 0.5f;
        float r = _cc.radius * 0.9f;

        // Мы хотим проверить только пустое пространство ВЫШЕ текущей головы:
        // Нижняя точка проверочной капсулы будет лежать ровно на уровне crouchHeight
        float bottomY = feetY + crouchHeight + r;
        float topY    = feetY + _standHeight - r;

        if (bottomY > topY) 
            bottomY = topY;

        Vector3 p1 = new Vector3(transform.position.x, bottomY, transform.position.z);
        Vector3 p2 = new Vector3(transform.position.x, topY,    transform.position.z);

        bool wasEnabled = _cc.enabled;
        if (wasEnabled) _cc.enabled = false;

        // Исключаем слой самого игрока, чтобы капсула не задевала хитбоксы и триггеры игрока
        int layerMask = ~0 & ~(1 << gameObject.layer);

        bool hit = Physics.CheckCapsule(p1, p2, r, layerMask, QueryTriggerInteraction.Ignore);

        if (wasEnabled) _cc.enabled = true;

        return hit;
    }

    // ══════════════════════════════════════════════════════════════
    //  ПУБЛИЧНЫЕ СВОЙСТВА (HUD)
    // ══════════════════════════════════════════════════════════════
    public int   DashCharges          => _dashCharges;
    public float DashRechargeProgress => Mathf.Clamp01(_dashCooldownTimer / dashCooldown);
    public bool  IsDashing            => _isDashing;
    public bool  IsCrouching          => _isCrouching;
    public bool  IsGrounded           => _cc.isGrounded;

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
}
