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
    public const int Dash   = 1;   // Left Shift
    public const int Crouch = 2;   // Left Ctrl
    public const int Fire   = 3;   // Left Mouse
    public const int Aim    = 4;   // Right Mouse
    public const int Reload = 5;   // R
    public const int Mantle = 6;   // Space + W/A/D (особый флаг)
}
