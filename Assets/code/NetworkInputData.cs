using UnityEngine;
using Fusion;

/// <summary>
/// Структура сетевого ввода — передаётся от клиента к серверу каждый тик.
/// Заполняется в NetworkManager.OnInput().
/// Читается в PlayerController.FixedUpdateNetwork() на сервере.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    // ── Движение ─────────────────────────────────────────────────

    /// Горизонтальное и вертикальное движение (WASD / Стрелки)
    public Vector2 MoveDirection;

    /// Угол взгляда: X = pitch (вверх/вниз), Y = yaw (влево/вправо)
    public Vector2 LookAngles;

    // ── Кнопки ───────────────────────────────────────────────────

    public NetworkButtons Buttons;
}

/// <summary>
/// Константы кнопок для NetworkButtons (битовые флаги)
/// Использование: data.Buttons.IsSet(NetworkInputButtons.Jump)
/// </summary>
public static class NetworkInputButtons
{
    public const int Jump   = 0;   // Space
    public const int Action = 1;   // F key
    public const int Dash   = 2;   // Left Shift
    public const int Crouch = 3;   // Left Ctrl
    public const int Interact = 4; // 'E' key
    public const int PlantOak = 5; // '1' key
    public const int PlantVine = 6; // '2' key
    public const int PlantChamomile = 7; // '3' key
}
