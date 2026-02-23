using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// A very simple turret that holds a single weapon that is constantly firing.
	/// </summary>
	public class SimpleTurret : NetworkBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Transform _fireTransform;

		private Weapon _weapon;
		private WeaponContext _weaponContext = new();

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			// Fire constantly
			_weaponContext.Buttons.SetDown(EInputButton.Fire);

			_weapon.ProcessFireInput();
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_weaponContext.FireTransform = _fireTransform;

			_weapon = GetComponentInChildren<Weapon>(true);
			_weapon.SetWeaponContext(_weaponContext);
		}
	}
}
