using UnityEngine;
using Fusion;

/// <summary>
/// Простой скрипт для блока дуба, который спавнится при росте.
/// </summary>
public class OakBlock : NetworkBehaviour
{
    [Networked] public Vector3 TargetScale { get; set; }

    public override void Spawned()
    {
        if (TargetScale == Vector3.zero) TargetScale = Vector3.one;

        // Можно добавить анимацию появления (Scale up)
        transform.localScale = TargetScale * 0.1f;
    }

    public override void FixedUpdateNetwork()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Runner.DeltaTime * 10f);
    }
}
