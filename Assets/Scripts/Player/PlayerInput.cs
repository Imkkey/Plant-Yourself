using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Addons.SimpleKCC;

namespace Projectiles
{
	public enum EInputButton
	{
		Fire     = 0,
		AltFire  = 1,
		Jump     = 2,
		Reload   = 3,
	}

	public struct GameplayInput : INetworkInput
	{
		public int            WeaponSlot => WeaponButton - 1;

		public Vector2        MoveDirection;
		public Vector2        LookRotationDelta;
		public byte           WeaponButton;
		public NetworkButtons Buttons;
	}

	/// <summary>
	/// PlayerInput handles accumulating player input from Unity and passes the accumulated input to Fusion.
	/// </summary>
	public sealed class PlayerInput : ContextBehaviour, IBeforeUpdate, IAfterTick
	{
		// PUBLIC METHODS

		public NetworkButtons PreviousButtons => _previousButtons;
		public Vector2        AccumulatedLook => _lookRotationAccumulator.AccumulatedValue;

		// PRIVATE MEMBERS

		[SerializeField]
		private float _lookSensitivity = 3;

		[Networked]
		private NetworkButtons _previousButtons { get; set; }

		private GameplayInput _accumulatedInput;
		private Vector2Accumulator _lookRotationAccumulator = new(0.02f, true);

		private PlayerAgent _agent;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			// Only local player needs networked properties (previous input buttons).
			// This saves network traffic by not synchronizing networked properties to other clients except local player.
			ReplicateToAll(false);
			ReplicateTo(Object.InputAuthority, true);

			if (HasInputAuthority == false)
				return;

			// Register to Fusion input poll callback
			var networkEvents = Runner.GetComponent<NetworkEvents>();
			networkEvents.OnInput.AddListener(OnInput);

			Context.GeneralInput.RequestCursorLock();
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (runner == null)
				return;

			var networkEvents = runner.GetComponent<NetworkEvents>();
			if (networkEvents != null)
			{
				networkEvents.OnInput.RemoveListener(OnInput);
			}
		}

		// IBeforeUpdate INTERFACE

		void IBeforeUpdate.BeforeUpdate()
		{
			// This method is called BEFORE ANY FixedUpdateNetwork() and is used to accumulate input from Keyboard/Mouse.
			// Input accumulation is mandatory - this method is called multiple times before new forward FixedUpdateNetwork() - common if rendering speed is faster than Fusion simulation.

			if (HasInputAuthority == false)
				return;

			// Input is tracked only if the cursor is locked and runner should provide input
			if (Runner.ProvideInput == false || Context.GeneralInput.IsLocked == false || _agent.InputBlocked == true)
			{
				_accumulatedInput = default;
				return;
			}

			var mouse = Mouse.current;
			if (mouse != null)
			{
				var mouseDelta = mouse.delta.ReadValue();

				var lookRotationDelta = new Vector2(-mouseDelta.y, mouseDelta.x);
				lookRotationDelta *= _lookSensitivity / 60f;
				_lookRotationAccumulator.Accumulate(lookRotationDelta);

				_accumulatedInput.Buttons.Set(EInputButton.Fire, mouse.leftButton.isPressed);
				_accumulatedInput.Buttons.Set(EInputButton.AltFire, mouse.rightButton.isPressed);
			}

			var keyboard = Keyboard.current;
			if (keyboard != null)
			{
				var moveDirection = Vector2.zero;

				if (keyboard.wKey.isPressed) { moveDirection += Vector2.up;    }
				if (keyboard.sKey.isPressed) { moveDirection += Vector2.down;  }
				if (keyboard.aKey.isPressed) { moveDirection += Vector2.left;  }
				if (keyboard.dKey.isPressed) { moveDirection += Vector2.right; }

				_accumulatedInput.MoveDirection = moveDirection.normalized;

				_accumulatedInput.Buttons.Set(EInputButton.Jump, keyboard.spaceKey.isPressed);
				_accumulatedInput.Buttons.Set(EInputButton.Reload, keyboard.rKey.isPressed);

				_accumulatedInput.WeaponButton = 0;
				for (int i = (int)Key.Digit1; i <= (int)Key.Digit9; i++)
				{
					if (keyboard[(Key)i].isPressed == true)
					{
						_accumulatedInput.WeaponButton = (byte)(i - (int)Key.Digit1 + 1);
						break;
					}
				}
			}
		}

		// IAfterTick INTERFACE

		void IAfterTick.AfterTick()
		{
			_previousButtons = GetInput<GameplayInput>().GetValueOrDefault().Buttons;
		}

		// MONOBEHAVIOUR

		private void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// PRIVATE METHODS

		private void OnInput(NetworkRunner runner, NetworkInput networkInput)
		{
			// Mouse movement (delta values) is aligned to engine update.
			// To get perfectly smooth interpolated look, we need to align the mouse input with Fusion ticks.
			_accumulatedInput.LookRotationDelta = _lookRotationAccumulator.ConsumeTickAligned(runner);

			if (_agent.InputBlocked == true)
				return;

			// Fusion polls accumulated input. This callback can be executed multiple times in a row if there is a performance spike.
			networkInput.Set(_accumulatedInput);
		}
	}
}
