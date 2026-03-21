using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class WorldItem : NetworkBehaviour
{
    [Tooltip("ID предмета из базы данных ItemDatabase. ID = 0 значит пусто.")]
    public int ItemID = 1;
}
