using UnityEngine;
using Fusion;

public class WeaponPickup : NetworkBehaviour
{
    [Tooltip("Какой класс получит игрок, поднявший это оружие")]
    public PlayerClass ClassToGive = PlayerClass.Swordsman;

    // Ничего особенного здесь нет, так как сам плеер сканирует область перед собой 
    // с помощью OverlapSphere и ищет WeaponPickup при нажатии кнопки E.
    
    // В редакторе нужно просто повесить этот скрипт на 3D-модельку (например, меч),
    // добавить коллайдер (BoxCollider / SphereCollider) и ОБЯЗАТЕЛЬНО NetworkObject.
}
