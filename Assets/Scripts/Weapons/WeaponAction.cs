using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Weapon action is a set of components that represent one weapon action.
	/// E.g. one weapon action will be a standard fire, second will be an alternative fire.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class WeaponAction : NetworkBehaviour
	{
		// PUBLIC MEMBERS

		public Transform BarrelTransform => _barrelTransform;
		public string    Description     => _description;

		// PRIVATE MEMBERS

		[SerializeField]
		private Transform _barrelTransform;
		[SerializeField]
		private string _description;

		[Networked]
		private int _lastFireTick { get; set; }

		private WeaponComponent[] _components;
		private int _lastVisibleFireTick;

		// PUBLIC METHODS

		public void Initialize(Weapon weapon, byte weaponActionId)
		{
			_components = GetComponentsInChildren<WeaponComponent>();

			for (int i = 0; i < _components.Length; i++)
			{
				var component = _components[i];

				component.WeaponActionId = weaponActionId;
				component.Weapon = weapon;
				component.BarrelTransform = _barrelTransform;
			}
		}

		public bool TryFire()
		{
			for (int i = 0; i < _components.Length; i++)
			{
				if (_components[i].CanFire() == false)
					return false;
			}

			for (int i = 0; i < _components.Length; i++)
			{
				_components[i].Fire();
			}

			_lastFireTick = Runner.Tick;
			return true;
		}

		public void SetWeaponContext(WeaponContext weaponContext)
		{
			for (int i = 0; i < _components.Length; i++)
			{
				_components[i].WeaponContext = weaponContext;
			}
		}

		public bool IsBusy()
		{
			for (int i = 0; i < _components.Length; i++)
			{
				if (_components[i].IsBusy == true)
					return true;
			}

			return false;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			// Sync fire tick
			_lastVisibleFireTick = _lastFireTick;
		}

		public override void Render()
		{
			// Check if we should show fire
			if (_lastVisibleFireTick < _lastFireTick)
			{
				for (int i = 0; i < _components.Length; i++)
				{
					_components[i].FireRender();
				}
			}

			_lastVisibleFireTick = _lastFireTick;
		}
	}
}
