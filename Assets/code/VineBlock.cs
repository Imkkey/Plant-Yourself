using UnityEngine;
using Fusion;

/// <summary>
/// Скрипт для блока лозы, который спавнится при росте.
/// </summary>
public class VineBlock : NetworkBehaviour
{
    [Networked] public Vector3 TargetScale { get; set; }

    public override void Spawned()
    {
        if (TargetScale == Vector3.zero) TargetScale = Vector3.one;

        // Начальный масштаб для анимации появления
        transform.localScale = new Vector3(TargetScale.x, TargetScale.y, 0.1f);
    }

    public override void FixedUpdateNetwork()
    {
        // Плавное увеличение масштаба
        transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Runner.DeltaTime * 10f);
    }
}
