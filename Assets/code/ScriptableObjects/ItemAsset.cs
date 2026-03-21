using UnityEngine;
using Fusion;

[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item Asset")]
public class ItemAsset : ScriptableObject
{
    public int ItemID; // Unique ID > 0. 0 means empty.
    public string ItemName;
    public Sprite Icon;
    public PlantType PlantAssigned; 
    public NetworkPrefabRef WorldPrefab; 
}
