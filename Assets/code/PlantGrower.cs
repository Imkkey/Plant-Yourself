using UnityEngine;
using Fusion;

public class PlantGrower : NetworkBehaviour
{
    [Header("Настройки Роста")]
    [SerializeField] private float growthSpeed = 5f;
    [SerializeField] private float maxGrowthDistance = 20f;
    [SerializeField] private NetworkPrefabRef oakBlockPrefab;
    [SerializeField] private NetworkPrefabRef vineBlockPrefab;
    [SerializeField] private NetworkPrefabRef chamomilePetalPrefab;
    [SerializeField] private float blockSpacing = 0.6f;

    // Ссылки на другие компоненты
    private PlayerController _player;
    private CharacterController _cc;

    [Networked] public PlantType CurrentPlant { get; set; } = PlantType.None;
    [Networked] public NetworkBool IsPlantSelected { get; set; } = false;
    [Networked] public float Fertilizer { get; set; } = 100f;
    [Networked] public int ChamomileCharges { get; set; } = 6;
    [Networked] public float ChamomileRechargeTimer { get; set; }
    [Networked] public NetworkId ActiveChamomilePetalId { get; set; }
    [Networked] public NetworkBool IsGrowing { get; private set; }
    [Networked] public NetworkBool IsRetracting { get; private set; }

    [Networked] public NetworkBool InChamomileForm { get; set; }
    [Networked] public float ChamomileChargePower { get; set; }
    [Networked] public NetworkBool IsChargingChamomile { get; set; }
    [Networked] private NetworkBool WasActionHeld { get; set; }

    [Networked, Capacity(30)] private NetworkArray<NetworkId> _spawnedPetals { get; }
    [Networked] private int _spawnedPetalsCount { get; set; }

    [Networked] private Vector3 _growthHeadPos { get; set; }
    [Networked] public Vector3 LastBlockPos { get; private set; }
    [Networked] private float _currentGrowthDist { get; set; }
    [Networked] private Vector3 _initialGrowthDir { get; set; }
    
    [Networked, Capacity(100)] private NetworkArray<NetworkId> _spawnedBlocks { get; }
    [Networked] private int _spawnedBlocksCount { get; set; }
    [Networked] private float _retractTimer { get; set; }
    
    [Networked] private float _treeIdleTimer { get; set; }
    [Networked] private float _treeIdleDuration { get; set; }

    public override void Spawned()
    {
        _player = GetComponent<PlayerController>();
        _cc = GetComponent<CharacterController>();
        if (HasStateAuthority)
        {
            CurrentPlant = PlantType.None;
            IsPlantSelected = false;
            Fertilizer = 100f;
            ChamomileCharges = 6;
            ChamomileRechargeTimer = 0f;
        }
    }

    public void ProcessGrowth(NetworkInputData input, NetworkButtons pressed)
    {
        // Пассивная регенерация (независимо от выбранного цветка, пока не находимся в процессе роста)
        if (!IsGrowing && !IsRetracting)
        {
            Fertilizer = Mathf.Min(100f, Fertilizer + Runner.DeltaTime * 15f);

            if (ChamomileCharges < 6)
            {
                ChamomileRechargeTimer += Runner.DeltaTime;
                if (ChamomileRechargeTimer >= 5f) // 5 секунд на восстановление ВСЕХ снарядов
                {
                    ChamomileCharges = 6;
                    ChamomileRechargeTimer = 0f;
                }
            }
            else
            {
                ChamomileRechargeTimer = 0f;
            }
        }

        if (CurrentPlant == PlantType.Chamomile)
        {
            ProcessChamomileGrowth(input, pressed);
            return;
        }

        if (pressed.IsSet(NetworkInputButtons.Action))
        {
            if (IsGrowing || IsRetracting)
            {
                IsGrowing = false;
                IsRetracting = true;
                _retractTimer = 0f;
            }
            else if (!IsRetracting)
            {
                if (IsOnSoil())
                {
                    StartGrowth(input);
                }
            }
        }

        if (IsRetracting)
        {
            HandleRetract();
        }
        else if (IsGrowing)
        {
            UpdateGrowth(input);
        }
    }

    private bool IsOnSoil()
    {
        // Не используем маску слоев (используем ~0), так как игрок и пол могут быть на одном слое (Default).
        // Вместо этого просто игнорируем коллайдеры с тем же root'ом, что и у игрока.
        var hits = Physics.SphereCastAll(transform.position + Vector3.up * 0.5f, 0.3f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        
        foreach (var hit in hits)
        {
            // Игнорируем себя
            if (hit.collider.transform.root == transform.root) continue;
            if (hit.collider.isTrigger) continue;

            // Проверка по тегам
            bool isSoilTag = false;
            try
            {
                if (hit.collider.CompareTag("Soil") || hit.collider.CompareTag("Ground")) isSoilTag = true;
            }
            catch { }

            // Проверка по имени объекта
            string objName = hit.collider.gameObject.name.ToLower();
            if (isSoilTag || objName.Contains("soil") ||
                objName.Contains("почва") || objName.Contains("земля") || 
                objName.Contains("dirt") || objName.Contains("grass") || objName.Contains("terrain"))
            {
                return true;
            }
        }
        return false;
    }

    private void StartGrowth(NetworkInputData input)
    {
        IsGrowing = true;
        _currentGrowthDist = 0f;
        _treeIdleTimer = 0f;
        _treeIdleDuration = 15f + ((float)(Runner.Tick % 500) / 100f);

        float feetY = transform.position.y + _cc.center.y - _cc.height * 0.5f;

        if (CurrentPlant == PlantType.Vine)
        {
            Quaternion lookRot = Quaternion.Euler(0f, input.LookAngles.y, 0f);
            Vector3 forward = lookRot * Vector3.forward;
            _initialGrowthDir = forward;
            
            Vector3 scanStart = new Vector3(transform.position.x, feetY + 0.2f, transform.position.z);
            Vector3 targetStartPos = scanStart + forward * 0.5f; 
            
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
                if (!foundSurface) { targetStartPos = scanStart + forward * (dist - 0.25f); break; }
            }

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
        }
        else
        {
            _growthHeadPos = new Vector3(transform.position.x, feetY + 0.2f, transform.position.z);
        }
        LastBlockPos = _growthHeadPos;
    }

    private void HandleRetract()
    {
        _retractTimer += Runner.DeltaTime;
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
                    if (obj != null) Runner.Despawn(obj);
                }

                if (_spawnedBlocksCount > 0)
                {
                    NetworkObject prevObj = Runner.FindObject(_spawnedBlocks[_spawnedBlocksCount - 1]);
                    if (prevObj != null) LastBlockPos = prevObj.transform.position;
                }
                else
                {
                    IsRetracting = false;
                    LastBlockPos = transform.position;
                    break;
                }
            }
            else { IsRetracting = false; break; }
        }
    }

    private void UpdateGrowth(NetworkInputData input)
    {
        _treeIdleTimer += Runner.DeltaTime;
        if (_treeIdleTimer >= _treeIdleDuration)
        {
            IsGrowing = false; IsRetracting = true; _retractTimer = 0f; return;
        }

        bool isInputActive = (CurrentPlant == PlantType.Vine) ? input.Buttons.IsSet(NetworkInputButtons.Action) : input.MoveDirection.y > 0.1f;

        if (isInputActive && _currentGrowthDist < maxGrowthDistance)
        {
            if (Fertilizer > 0)
            {
                Fertilizer -= growthSpeed * Runner.DeltaTime * 10f;
                
                Vector3 growDir;
                Quaternion spawnRot;
                CalculateGrowthDirection(input, out growDir, out spawnRot);

                float moveDist = growthSpeed * Runner.DeltaTime;

                // --- КОЛЛИЗИИ СО СТЕНАМИ ---
                if (CurrentPlant == PlantType.Oak)
                {
                    // Для дуба - скользим по стенам, чтобы не застревать намертво
                    if (Physics.SphereCast(_growthHeadPos, 0.4f, growDir, out RaycastHit hit, moveDist * 2f + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.root != transform.root && 
                            (hit.collider.GetComponentInParent<NetworkBehaviour>() == null || hit.collider.gameObject.isStatic))
                        {
                            Vector3 slideDir = Vector3.ProjectOnPlane(growDir, hit.normal).normalized;
                            
                            // Проверка на внутренний угол / вторую стену
                            if (Physics.SphereCast(_growthHeadPos, 0.4f, slideDir, out RaycastHit hit2, moveDist * 2f + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                if (hit2.collider.transform.root != transform.root && 
                                    (hit2.collider.GetComponentInParent<NetworkBehaviour>() == null || hit2.collider.gameObject.isStatic))
                                {
                                    slideDir = Vector3.ProjectOnPlane(slideDir, hit2.normal).normalized;
                                }
                            }
                            
                            if (slideDir.sqrMagnitude > 0.01f)
                            {
                                growDir = slideDir;
                            }
                        }
                    }
                }
                else if (CurrentPlant == PlantType.Vine)
                {
                    // Для лозы - если упирается прямо в стену, меняет направление на свободное
                    if (Physics.SphereCast(_growthHeadPos, 0.25f, growDir, out RaycastHit hit, moveDist * 1.5f + 0.15f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.root != transform.root && 
                            (hit.collider.GetComponentInParent<NetworkBehaviour>() == null || hit.collider.gameObject.isStatic))
                        {
                            Vector3 right = spawnRot * Vector3.right;
                            Vector3 left = -right;
                            Vector3 back = -growDir;

                            float rightOpen = GetOpenDist(_growthHeadPos, right);
                            float leftOpen = GetOpenDist(_growthHeadPos, left);

                            if (rightOpen > 0.5f || leftOpen > 0.5f)
                            {
                                growDir = (rightOpen > leftOpen) ? right : left;
                            }
                            else
                            {
                                growDir = back;
                            }
                            
                            // Запоминаем новое направление для следующих кадров
                            _initialGrowthDir = growDir.normalized; 
                            // Немного отодвигаем назад, чтобы лоза не застряла в следующем кадре
                            _growthHeadPos += hit.normal * 0.15f;
                        }
                    }
                }
                // -----------------------------

                _growthHeadPos += growDir * moveDist;
                _currentGrowthDist += moveDist;

                if (CurrentPlant == PlantType.Vine)
                {
                    Vector3 tempHeadPos = _growthHeadPos;
                    SnapVineToSurface(growDir, ref tempHeadPos, ref spawnRot);
                    _growthHeadPos = tempHeadPos;
                }

                if (Vector3.Distance(_growthHeadPos, LastBlockPos) >= blockSpacing)
                {
                    SpawnBlock(spawnRot);
                    LastBlockPos = _growthHeadPos;
                    _treeIdleTimer = 0f;
                    _treeIdleDuration = 15f + ((float)(Runner.Tick % 500) / 100f);
                }
            }
        }
        else if (!isInputActive)
        {
            Fertilizer = Mathf.Min(100f, Fertilizer + Runner.DeltaTime * 5f);
        }
    }

    private float GetOpenDist(Vector3 pos, Vector3 dir)
    {
        if (Physics.SphereCast(pos, 0.3f, dir, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root != transform.root && 
                (hit.collider.GetComponentInParent<NetworkBehaviour>() == null || hit.collider.gameObject.isStatic))
            {
                return hit.distance;
            }
        }
        return 3f; // Место открыто
    }

    private void CalculateGrowthDirection(NetworkInputData input, out Vector3 growDir, out Quaternion spawnRot)
    {
        if (CurrentPlant == PlantType.Vine)
        {
            Vector3 forward = _initialGrowthDir;
            bool hasFloor = false;
            foreach (var hit in Physics.RaycastAll(_growthHeadPos + Vector3.up * 0.5f, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root == transform.root) continue;
                if (hit.collider.GetComponentInParent<NetworkBehaviour>() != null && !hit.collider.gameObject.isStatic) continue;
                hasFloor = true; break;
            }

            if (hasFloor) { growDir = forward; spawnRot = Quaternion.LookRotation(forward, Vector3.up); }
            else { growDir = Vector3.down; spawnRot = Quaternion.LookRotation(Vector3.down, -forward); }
        }
        else
        {
            spawnRot = Quaternion.Euler(input.LookAngles.x, input.LookAngles.y, 0f);
            growDir = spawnRot * Vector3.forward;
        }
    }

    private void SnapVineToSurface(Vector3 growDir, ref Vector3 headPos, ref Quaternion spawnRot)
    {
        Vector3 scanDir = (growDir == Vector3.down) ? -(spawnRot * Vector3.forward) : Vector3.down;
        Vector3 scanOrigin = headPos - scanDir * 0.5f;

        if (Physics.Raycast(scanOrigin, scanDir, out RaycastHit wallHit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (wallHit.collider.transform.root != transform.root && (wallHit.collider.GetComponentInParent<NetworkBehaviour>() == null || wallHit.collider.gameObject.isStatic))
            {
                if (growDir == Vector3.down && wallHit.normal.y > 0.8f) return;
                
                Vector3 targetPos = wallHit.point + wallHit.normal * 0.15f;
                if (growDir != Vector3.down) { targetPos.x = headPos.x; targetPos.z = headPos.z; }
                headPos = Vector3.Lerp(headPos, targetPos, 15f * Runner.DeltaTime);
                
                Vector3 toLast = (LastBlockPos - headPos).normalized;
                if (toLast.sqrMagnitude < 0.001f) toLast = (growDir == Vector3.down) ? Vector3.up : -growDir.normalized;
                spawnRot = Quaternion.LookRotation(toLast, wallHit.normal);
            }
        }
    }

    private void SpawnBlock(Quaternion spawnRot)
    {
        if (!HasStateAuthority) return;

        NetworkPrefabRef prefab = CurrentPlant == PlantType.Vine ? vineBlockPrefab : oakBlockPrefab;
        if (prefab == NetworkPrefabRef.Empty) return;

        Vector3 spawnScale = Vector3.one;
        if (CurrentPlant == PlantType.Vine)
        {
            float dist = Vector3.Distance(LastBlockPos, _growthHeadPos);
            spawnScale = new Vector3(1f, 1f, Mathf.Max(1.0f, dist * 2.5f));
        }

        NetworkObject obj = Runner.Spawn(prefab, _growthHeadPos, spawnRot, Object.InputAuthority, (runner, no) => {
            if (CurrentPlant == PlantType.Vine) {
                var v = no.GetComponent<VineBlock>(); if (v) v.TargetScale = spawnScale;
            } else {
                var o = no.GetComponent<OakBlock>(); if (o) o.TargetScale = spawnScale;
            }
        });

        if (obj && _spawnedBlocksCount < _spawnedBlocks.Length)
        {
            _spawnedBlocks.Set(_spawnedBlocksCount, obj.Id);
            _spawnedBlocksCount++;
        }
    }

    public void ResetGrowth()
    {
        IsGrowing = false;
        IsRetracting = false;
        InChamomileForm = false;
        LastBlockPos = transform.position;
    }

    private void ProcessChamomileGrowth(NetworkInputData input, NetworkButtons pressed)
    {
        bool actionHeld = input.Buttons.IsSet(NetworkInputButtons.Action);
        bool jumpPressed = pressed.IsSet(NetworkInputButtons.Jump);

        if (!InChamomileForm)
        {
            if (pressed.IsSet(NetworkInputButtons.Action))
            {
                if (!IsOnSoil()) return; // Можно расти ТОЛЬКО на почве/земле

                // Enter Chamomile form
                InChamomileForm = true;
                IsGrowing = true; 
                LastBlockPos = transform.position;
                WasActionHeld = actionHeld; 
                ActiveChamomilePetalId = default;
            }
        }
        else
        {
            // Exit form
            if (jumpPressed)
            {
                ExitChamomileForm();
                return;
            }

            // Did we just press Action?
            if (actionHeld && !WasActionHeld)
            {
                bool frozeExisting = false;
                if (ActiveChamomilePetalId.IsValid)
                {
                    NetworkObject petalObj = Runner.FindObject(ActiveChamomilePetalId);
                    if (petalObj != null)
                    {
                        var petal = petalObj.GetComponent<ChamomilePetal>();
                        if (petal != null && !petal.IsFrozen)
                        {
                            petal.Freeze();
                            frozeExisting = true;
                        }
                    }
                    ActiveChamomilePetalId = default;
                }

                // Start charging the next petal if we have charges AND we didn't just freeze one
                if (!frozeExisting && ChamomileCharges > 0)
                {
                    IsChargingChamomile = true;
                    ChamomileChargePower = 0f;
                }
            }

            // Handle charging and throwing
            if (IsChargingChamomile)
            {
                if (actionHeld && ChamomileCharges > 0)
                {
                    // Charge!
                    ChamomileChargePower += Runner.DeltaTime * 15f; 
                    if (ChamomileChargePower > 20f) ChamomileChargePower = 20f;
                }
                else
                {
                    // Action Released -> Throw!
                    ShootChamomilePetal(input, ChamomileChargePower);
                    IsChargingChamomile = false;
                    ChamomileChargePower = 0f;
                }
            }
            
            // Camera follow logic
            if (ActiveChamomilePetalId.IsValid)
            {
                NetworkObject petalObj = Runner.FindObject(ActiveChamomilePetalId);
                if (petalObj != null)
                {
                    LastBlockPos = petalObj.transform.position;
                }
                else
                {
                    ActiveChamomilePetalId = default;
                }
            }
            else
            {
                LastBlockPos = transform.position;
            }
        }

        WasActionHeld = actionHeld;
    }

    private void ExitChamomileForm()
    {
        InChamomileForm = false;
        IsGrowing = false;
        IsChargingChamomile = false;
        ChamomileChargePower = 0f;
        ActiveChamomilePetalId = default;
        
        // Мгновенный откат всех зарядов при возвращении в боба
        if (HasStateAuthority)
        {
            ChamomileCharges = 6;
            ChamomileRechargeTimer = 0f;
        }

        // Destroy all petals spawned by this player
        if (HasStateAuthority)
        {
            for (int i = 0; i < _spawnedPetalsCount; i++)
            {
                NetworkId petalId = _spawnedPetals[i];
                if (petalId.IsValid)
                {
                    NetworkObject petalObj = Runner.FindObject(petalId);
                    if (petalObj != null) Runner.Despawn(petalObj);
                }
            }
            _spawnedPetalsCount = 0;
        }
    }

    private void ShootChamomilePetal(NetworkInputData input, float power)
    {
        if (HasStateAuthority && ChamomileCharges > 0)
        {
            ChamomileCharges--;
            Quaternion lookRot = Quaternion.Euler(input.LookAngles.x, input.LookAngles.y, 0f);
            Vector3 spawnPos = transform.position + Vector3.up * 1f + lookRot * Vector3.forward * 0.5f;

            float shootPower = Mathf.Max(power, 5f); // min power

            NetworkObject petalObj = Runner.Spawn(chamomilePetalPrefab, spawnPos, lookRot, Object.InputAuthority, (runner, no) =>
            {
                var petal = no.GetComponent<ChamomilePetal>();
                if (petal != null) petal.Init(lookRot * Vector3.forward, this, shootPower);
            });
            
            if (petalObj != null)
            {
                ActiveChamomilePetalId = petalObj.Id;
                if (_spawnedPetalsCount < _spawnedPetals.Length)
                {
                    _spawnedPetals.Set(_spawnedPetalsCount, petalObj.Id);
                    _spawnedPetalsCount++;
                }
            }
        }
        LastBlockPos = transform.position;
    }

    public void RestoreChamomileCharge()
    {
        if (HasStateAuthority && ChamomileCharges < 6)
        {
            ChamomileCharges++;
        }
    }

    public void OnPetalFrozen()
    {
        // Не возвращаемся в оболочку при заморозке, игрок решает сам (через прыжок)
    }

    private void OnGUI()
    {
        if (HasInputAuthority && InChamomileForm && IsChargingChamomile)
        {
            float powerRatio = ChamomileChargePower / 20f;
            float w = 200f;
            float h = 15f;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f + 40f;
            
            // Background
            Color oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            
            // Foreground
            GUI.color = new Color(1f, 0.8f, 0.2f, 1f); 
            GUI.DrawTexture(new Rect(x, y, w * powerRatio, h), Texture2D.whiteTexture);
            
            GUI.color = oldColor;
        }
    }
}
