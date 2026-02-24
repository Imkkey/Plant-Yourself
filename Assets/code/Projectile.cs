using UnityEngine;
using Fusion;

public class Projectile : NetworkBehaviour
{
    private PlayerRef _ownerId;
    private int _damage;
    
    // Вместо направления и скорости теперь храним текущий вектор скорости (Velocity)
    [Networked] private Vector3 _velocity { get; set; }

    [Networked] private TickTimer _lifeTimer { get; set; }

    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float gravityMultiplier = 1.5f; // Насколько сильно снаряд падает вниз

    /// <summary>
    /// Инициализация снаряда. Вызывается сразу после Runner.Spawn на сервере.
    /// </summary>
    public void Init(PlayerRef ownerId, int damage, Vector3 direction, float speed)
    {
        _ownerId = ownerId;
        _damage = damage;
        
        // Задаем начальную скорость полета снаряда
        _velocity = direction * speed;

        // Таймер жизни снаряда
        _lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        // Прокси (другие клиенты) просто считывают позицию с NetworkTransform, 
        // они не должны двигать пулю сами, иначе она будет "дёргаться"
        if (!HasStateAuthority && !Object.HasInputAuthority) return;

        // Проверяем, не истек ли срок жизни снаряда
        if (_lifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        // Вычисляем будущую позицию
        Vector3 moveDelta = _velocity * Runner.DeltaTime;
        
        // Перед тем как сдвинуть, пускаем Raycast чтобы не пролететь сквозь стену
        if (Physics.Raycast(transform.position, _velocity.normalized, out RaycastHit hit, moveDelta.magnitude))
        {
            HitSomething(hit.collider);
            return;
        }

        // Если не врезались, просто двигаемся
        transform.position += moveDelta;

        // Применяем гравитацию (чтобы кинжал падал по параболе)
        Vector3 newVel = _velocity;
        newVel.y += Physics.gravity.y * gravityMultiplier * Runner.DeltaTime;
        _velocity = newVel;

        // Поворачиваем кинжал так, чтобы он летел острием вперед (по направлению полета)
        if (_velocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity.normalized);
        }
    }

    // Для случаев, если снаряд все же задевает кого-то хитбоксом
    private void OnTriggerEnter(Collider other)
    {
        HitSomething(other);
    }

    private void HitSomething(Collider col)
    {
        if (!HasStateAuthority) return;

        PlayerController target = col.GetComponent<PlayerController>();

        // Если врезались не в себя
        if (target != null && target.Object.InputAuthority != _ownerId)
        {
            target.TakeDamage(_damage);
            Runner.Despawn(Object); // Уничтожаемся об игрока
        }
        else if (target == null)
        {
            // Уничтожаемся при касании стены или другого препятствия
            Runner.Despawn(Object);
        }
    }
}
