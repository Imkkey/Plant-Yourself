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

    public void Init(Vector3 lookDirection, PlantGrower owner)
    {
        InitialForward = lookDirection;
        InitialRight = Vector3.Cross(Vector3.up, lookDirection).normalized;
        if (InitialRight.sqrMagnitude < 0.01f) InitialRight = Vector3.right;
        
        // Петля взлетает вверх и чуть-чуть вперед по направлению взгляда
        Velocity = Vector3.up * jumpForce + lookDirection * 2f;
        IsFrozen = false;
        TimeAlive = 0f;
        OwnerGrower = owner;
        _ownerId = owner.Object.Id;
    }

    public override void FixedUpdateNetwork()
    {
        if (IsFrozen)
        {
            TimeAlive += Runner.DeltaTime;
            if (TimeAlive >= 15f && HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
            return;
        }

        Vector3 vel = Velocity;
        vel.y -= fallGravity * Runner.DeltaTime;

        // Падает медленнее, сопротивление воздуха (Terminal Velocity)
        if (vel.y < -3f) vel.y = -3f;

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
        
        // Столкновение с землей
        if (HasStateAuthority && Velocity.y < 0f)
        {
            var hits = Physics.OverlapSphere(transform.position, 0.3f);
            foreach (var hit in hits)
            {
                if (hit.transform.root != transform.root && !hit.isTrigger && hit.GetComponent<Collider>().GetComponentInParent<PlayerController>() == null)
                {
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
