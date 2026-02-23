using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Weapon component that processes player input and is responsible for controlling weapon cadence.
	/// </summary>
	public class WeaponTrigger : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private int _cadence = 600;
		[SerializeField]
		private EInputButton _fireButton = EInputButton.Fire;
		[SerializeField]
		private bool _fireOnKeyDownOnly;

		[Networked]
		private TickTimer _fireCooldown { get; set; }

		private int _fireTicks;

		// WeaponComponent INTERFACE

		public override bool IsBusy => _fireCooldown.ExpiredOrNotRunning(Runner) == false;

		public override bool CanFire()
		{
			if (Weapon.IsBusy() == true)
				return false;

			if (_fireCooldown.ExpiredOrNotRunning(Runner) == false)
				return false;

			return _fireOnKeyDownOnly == true ? PressedButtons.IsSet(_fireButton) : Buttons.IsSet(_fireButton);
		}

		public override void Fire()
		{
			_fireCooldown = TickTimer.CreateFromTicks(Runner, _fireTicks);
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			base.Spawned();

			float fireTime = 60f / _cadence;
			_fireTicks = (int)System.Math.Ceiling(fireTime / (double)Runner.DeltaTime);
		}
	}
}
