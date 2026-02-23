using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Represents weapon object. Can hold one or more weapon actions.
	/// </summary>
	public class Weapon : NetworkBehaviour
	{
		// PUBLIC MEMBERS

		public int         WeaponSlot                 => _weaponSlot;
		public string      DisplayName                => _displayName;
		public Sprite      Icon                       => _icon;
		public string      PrimaryActionDescription   => _weaponActions.Length > 0 ? _weaponActions[0].Description : null;
		public string      SecondaryActionDescription => _weaponActions.Length > 1 ? _weaponActions[1].Description : null;

		public bool        IsArmed                    { get; private set; }
		public Transform[] BarrelTransforms           { get; private set; }

		// PRIVATE MEMBERS

		[SerializeField]
		private int _weaponSlot;

		[Header("UI")]
		[SerializeField]
		private string _displayName;
		[SerializeField]
		private Sprite _icon;

		private WeaponAction[] _weaponActions;
		private WeaponContext _context;

		// PUBLIC METHODS

		public void ArmWeapon()
		{
			if (IsArmed == true)
				return;

			IsArmed = true;
			OnArmed();
		}

		public void DisarmWeapon()
		{
			if (IsArmed == false)
				return;

			IsArmed = false;
			OnDisarmed();
		}

		public bool ProcessFireInput()
		{
			Assert.Check(Runner.Stage != default, "Process input should be called from FixedUpdateNetwork");

			bool fired = false;
			for (int i = 0; i < _weaponActions.Length; i++)
			{
				fired |= _weaponActions[i].TryFire();
			}

			return fired;
		}

		public bool IsBusy()
		{
			for (int i = 0; i < _weaponActions.Length; i++)
			{
				if (_weaponActions[i].IsBusy() == true)
					return true;
			}

			return false;
		}

		public void SetWeaponContext(WeaponContext context)
		{
			if (_context == context)
				return;

			_context = context;

			for (int i = 0; i < _weaponActions.Length; i++)
			{
				_weaponActions[i].SetWeaponContext(context);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			SetWeaponContext(null);
			IsArmed = false;
		}

		// MONOBEHAVIOUR

		protected virtual void Awake()
		{
			_weaponActions = GetComponentsInChildren<WeaponAction>();
			BarrelTransforms = new Transform[_weaponActions.Length];

			for (int i = 0; i < _weaponActions.Length; i++)
			{
				var weaponAction = _weaponActions[i];

				weaponAction.Initialize(this, (byte)i);
				BarrelTransforms[i] = weaponAction.BarrelTransform;
			}
		}

		// PROTECTED METHODS

		protected virtual void OnArmed()
		{
			// Do visual effects, sounds here
			// OnArmed is executed in render only
		}

		protected virtual void OnDisarmed()
		{
			// OnDisarmed is executed in render only
		}
	}
}
