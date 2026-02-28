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

        transform.localScale = TargetScale * 0.1f;
    }

    public override void Render()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Time.deltaTime * 10f);
    }
}

