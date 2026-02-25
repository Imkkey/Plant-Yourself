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
                if (ChamomileRechargeTimer >= 5f) // 5 секунд на восстановление 1 заряда
                {
                    ChamomileCharges++;
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
                StartGrowth(input);
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

                _growthHeadPos += growDir * growthSpeed * Runner.DeltaTime;
                _currentGrowthDist += growthSpeed * Runner.DeltaTime;

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
        LastBlockPos = transform.position;
    }

    private void ProcessChamomileGrowth(NetworkInputData input, NetworkButtons pressed)
    {
        if (pressed.IsSet(NetworkInputButtons.Action))
        {
            if (!IsGrowing)
            {
                if (ChamomileCharges > 0)
                {
                    StartChamomile(input);
                }
            }
            else
            {
                if (ActiveChamomilePetalId.IsValid)
                {
                    NetworkObject petalObj = Runner.FindObject(ActiveChamomilePetalId);
                    if (petalObj != null)
                    {
                        var petal = petalObj.GetComponent<ChamomilePetal>();
                        if (petal != null && !petal.IsFrozen)
                        {
                            petal.Freeze();
                            EndChamomile();
                        }
                    }
                }
            }
        }

        if (IsGrowing)
        {
            if (ActiveChamomilePetalId.IsValid)
            {
                NetworkObject petalObj = Runner.FindObject(ActiveChamomilePetalId);
                if (petalObj != null)
                {
                    LastBlockPos = petalObj.transform.position;
                    
                    var petal = petalObj.GetComponent<ChamomilePetal>();
                    if (petal != null && petal.IsFrozen)
                    {
                        EndChamomile();
                    }
                }
                else
                {
                    EndChamomile();
                }
            }
        }
    }

    private void StartChamomile(NetworkInputData input)
    {
        IsGrowing = true;
        
        if (HasStateAuthority)
        {
            ChamomileCharges--;
            Quaternion lookRot = Quaternion.Euler(input.LookAngles.x, input.LookAngles.y, 0f);
            Vector3 spawnPos = transform.position + Vector3.up * 1f + lookRot * Vector3.forward * 0.5f;

            NetworkObject petalObj = Runner.Spawn(chamomilePetalPrefab, spawnPos, lookRot, Object.InputAuthority, (runner, no) =>
            {
                no.GetComponent<ChamomilePetal>().Init(lookRot * Vector3.forward, this);
            });
            
            if (petalObj != null)
            {
                ActiveChamomilePetalId = petalObj.Id;
            }
        }
        LastBlockPos = transform.position;
    }

    private void EndChamomile()
    {
        IsGrowing = false;
        LastBlockPos = transform.position;
        ActiveChamomilePetalId = default;
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
        if (HasStateAuthority && IsGrowing)
        {
            EndChamomile();
        }
    }
}
