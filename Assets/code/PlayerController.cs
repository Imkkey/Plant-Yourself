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

    [Header("── Камера (Растение) ──────────────")]
    [SerializeField] private float treeCamDistance = 12f;

    public static PlayerController Local { get; private set; }

    // Ссылки на компоненты
    private CharacterController _cc;
    private PlantGrower _grower;
    private Keyboard _kb;
    private Mouse    _mouse;

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

    // ══════════════════════════════════════════════════════════════
    //  ИНТЕРФЕЙС NETWORK BEHAVIOUR
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _grower = GetComponent<PlantGrower>();
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

            if (PlantSelectionUI.Instance != null && _grower != null && !_grower.IsPlantSelected)
            {
                PlantSelectionUI.Instance.Show();
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

    // Собираем локальный ввод. Вызывается из NetworkManager.OnInput
    public NetworkInputData GetLocalInput()
    {
        var data = new NetworkInputData();
        if (_grower != null && !_grower.IsPlantSelected) return data; // Отключаем ввод до выбора

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
        data.Buttons.Set(NetworkInputButtons.PlantChamomile, _kb.digit3Key.isPressed);


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
        if (_kb == null || _mouse == null) return;

        if (_grower != null && !_grower.IsPlantSelected)
        {
            // Даем PlantSelectionUI управлять курсором. Просто не крутим камеру здесь
            return;
        }

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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetInitialPlant(PlantType plant)
    {
        if (_grower != null)
        {
            _grower.CurrentPlant = plant;
            _grower.IsPlantSelected = true;
        }
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

        if (_grower != null && (_grower.IsGrowing || _grower.IsRetracting))
        {
            targetPivot = _grower.LastBlockPos;
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

            if (isG || isR)
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

        int layerMask = ~0 & ~(1 << gameObject.layer);
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
}
