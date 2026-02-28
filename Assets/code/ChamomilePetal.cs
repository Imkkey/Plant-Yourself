using UnityEngine;
using Fusion;

public class ChamomilePetal : NetworkBehaviour
{
    [Header("Petal Falling Mechanics")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float fallGravity = 3f;
    [SerializeField] private float flutterSpeed = 2f;
    [SerializeField] private float flutterAmplitude = 1.5f;

    [Networked] public NetworkBool IsFrozen { get; set; }
    [Networked] private Vector3 Velocity { get; set; }
    [Networked] private float TimeAlive { get; set; }
    [Networked] private Vector3 InitialForward { get; set; }
    [Networked] private Vector3 InitialRight { get; set; }

    private NetworkId _ownerId;
    public PlantGrower OwnerGrower { get; set; }
    
    private Collider _col;

    public override void Spawned()
    {
        _col = GetComponent<Collider>();
    }

    public override void Render()
    {
        if (_col != null)
        {
            _col.isTrigger = !IsFrozen;
        }
    }

    public void Init(Vector3 lookDirection, PlantGrower owner, float shootPower)
    {
        InitialForward = lookDirection;
        InitialRight = Vector3.Cross(Vector3.up, lookDirection).normalized;
        if (InitialRight.sqrMagnitude < 0.01f) InitialRight = Vector3.right;
        
        // Ослабляем вертикальный угол при броске вверх, чтобы лепесток не улетал в космос
        Vector3 flattenedDir = new Vector3(lookDirection.x, lookDirection.y * 0.3f, lookDirection.z).normalized;
        Velocity = flattenedDir * (shootPower * 1.5f);
        IsFrozen = false;
        TimeAlive = 0f;
        OwnerGrower = owner;
        _ownerId = owner.Object.Id;
    }

    public override void FixedUpdateNetwork()
    {
        if (IsFrozen)
        {
            // Лепесток остается навсегда, пока игрок не выйдет из режима цветка
            return;
        }

        Vector3 vel = Velocity;
        
        // Гравитация (падаем чуть быстрее)
        vel.y -= (fallGravity + 1.5f) * Runner.DeltaTime;
        if (vel.y < -5f) vel.y = -5f; // Увеличенная Terminal velocity

        // Сопротивление воздуха по горизонтали (торможение)
        Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
        float currentSpeed = horizontalVel.magnitude;
        if (currentSpeed > 0f) {
            float drag = 30f; // Сильно увеличили drag, чтобы лепесток останавливался быстрее
            currentSpeed -= drag * Runner.DeltaTime;
            if (currentSpeed < 0f) currentSpeed = 0f;
            horizontalVel = horizontalVel.normalized * currentSpeed;
            vel.x = horizontalVel.x;
            vel.z = horizontalVel.z;
        }

        Velocity = vel;

        TimeAlive += Runner.DeltaTime;

        // Имитация колебаний
        float flutterX = Mathf.Sin(TimeAlive * flutterSpeed * 1.3f) * flutterAmplitude;
        float flutterZ = Mathf.Cos(TimeAlive * flutterSpeed * 0.9f) * flutterAmplitude;

        Vector3 moveOffset = Velocity * Runner.DeltaTime;
        moveOffset += InitialRight * flutterX * Runner.DeltaTime;
        moveOffset += InitialForward * flutterZ * Runner.DeltaTime;

        transform.position += moveOffset;
        
        // Поворот по направлению колебаний для красоты
        Quaternion targetRot = Quaternion.Euler(
            Mathf.Clamp(Velocity.y * 5f, -30f, 30f) + flutterZ * 5f,
            transform.rotation.eulerAngles.y + flutterX * 10f * Runner.DeltaTime,
            flutterX * 10f
        );

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Runner.DeltaTime * 10f);
        
        // Столкновение с препятствиями (чтобы не проходил сквозь стены и пол)
        if (HasStateAuthority)
        {
            var hits = Physics.OverlapSphere(transform.position, 0.3f);
            foreach (var hit in hits)
            {
                if (hit.transform.root != transform.root && !hit.isTrigger)
                {
                    // Игнорируем игроков (проходим сквозь них)
                    if (hit.GetComponentInParent<PlayerController>() != null) continue;

                    // Игнорируем другие лепестки (проходим сквозь них)
                    if (hit.GetComponentInParent<ChamomilePetal>() != null) continue;

                    Freeze();
                    return;
                }
            }
        }
    }

    public void Freeze()
    {
        IsFrozen = true;
        TimeAlive = 0f; // сброс таймера времени жизни

        // Если это petal игрока, сообщаем игроку (вызывается на клиенте или сервере, но StateAuthority у сервера)
        if (OwnerGrower != null && OwnerGrower.HasStateAuthority)
        {
            OwnerGrower.OnPetalFrozen();
        }
    }
}
