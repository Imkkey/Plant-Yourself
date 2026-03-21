using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerItem : MonoBehaviour
{
    [Tooltip("Сюда перетащите текст, в котором будет написано имя игрока")]
    public TMP_Text textName;
    
    [Tooltip("Сюда перетащите кнопку кика (крестик)")]
    public Button btnKick;

    [Tooltip("Сюда перетащите иконку короны для хоста")]
    public Image hostCrownIcon;
}
