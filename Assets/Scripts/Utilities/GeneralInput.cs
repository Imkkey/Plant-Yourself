using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Projectiles
{
	/// <summary>
	/// Handles general game input and debug input - cursor locking, peer switching.
	/// </summary>
	public class GeneralInput : MonoBehaviour
	{
		// PUBLIC MEMBERS

		public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

		// PRIVATE MEMBERS

		private static int _lastSingleInputChange;
		private static int _cursorLockRequests;

		// PUBLIC METHODS

		public void RequestCursorLock()
		{
			// Static requests count is used for multi-peer setup
			_cursorLockRequests++;

			if (_cursorLockRequests == 1)
			{
				// First lock request, let's lock
				SetLockedState(true);
			}
		}

		public void RequestCursorRelease()
		{
			_cursorLockRequests--;

			Assert.Check(_cursorLockRequests >= 0, "Cursor lock requests are negative, this should not happen");

			if (_cursorLockRequests == 0)
			{
				SetLockedState(false);
			}
		}

		// MONOBEHAVIOUR

		private void Update()
		{
			// Only one single input change per frame is possible (important for multi-peer multi-input game)
			if (_lastSingleInputChange == Time.frameCount)
				return;

			var keyboard = Keyboard.current;
			if (keyboard == null)
				return;

			// Enter key is used for locking/unlocking cursor in game view
			if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
			{
				SetLockedState(Cursor.lockState != CursorLockMode.Locked);
				_lastSingleInputChange = Time.frameCount;
			}

			// Check switching peer in multi-peer mode
			if (keyboard.numpad0Key.wasPressedThisFrame || keyboard.uKey.wasPressedThisFrame)
			{
				SetActiveRunner(-1);
			}
			else if (keyboard.numpad1Key.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)
			{
				SetActiveRunner(0);
			}
			else if (keyboard.numpad2Key.wasPressedThisFrame || keyboard.oKey.wasPressedThisFrame)
			{
				SetActiveRunner(1);
			}
			else if (keyboard.numpad3Key.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
			{
				SetActiveRunner(2);
			}
		}

		// PRIVATE METHODS

		private void SetLockedState(bool value)
		{
			Cursor.lockState = value == true ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !value;

			//Debug.Log($"Cursor lock state {Cursor.lockState}, visibility {Cursor.visible}");
		}

		private void SetActiveRunner(int index)
		{
			var enumerator = NetworkRunner.GetInstancesEnumerator();

			int currentIndex = -1;
			while (enumerator.MoveNext() == true)
			{
				var runner = enumerator.Current;

				// Skip temporary runner
				if (runner.LocalPlayer.IsRealPlayer == false)
					continue;

				currentIndex++;

				runner.SetVisible(index < 0 || currentIndex == index);
				runner.ProvideInput = index < 0 || currentIndex == index;
			}
		}
	}
}
