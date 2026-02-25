using UnityEngine;
using Fusion;

/// <summary>
/// Простой скрипт для блока дуба, который спавнится при росте.
/// </summary>
public class OakBlock : NetworkBehaviour
{
    public override void Spawned()
    {
        // Можно добавить анимацию появления (Scale up)
        transform.localScale = Vector3.one * 0.1f;
    }

    public override void FixedUpdateNetwork()
    {
        if (transform.localScale.x < 1f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Runner.DeltaTime * 5f);
        }
    }
}
