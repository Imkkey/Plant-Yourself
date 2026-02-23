using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Handles weapon action ammo.
	/// </summary>
	public class WeaponMagazine : WeaponComponent
	{
		// PUBLIC MEMBERS

		public bool  IsReloading      => _isReloading;
		public float ReloadProgress   => IsReloading == true ? _reloadCooldown.RemainingTime(Runner).Value / _reloadTime : 0f;
		public bool  HasMagazine      => _hasMagazine;
		public bool  HasUnlimitedAmmo => _hasUnlimitedAmmo;
		public int   MagazineAmmo     => _magazineAmmo;
		public int   WeaponAmmo       => _weaponAmmo;

		// PRIVATE MEMBERS

		[SerializeField]
		private int _initialAmmo = 150;
		[SerializeField]
		private bool _hasMagazine = true;
		[SerializeField]
		private int _maxMagazineAmmo = 30;
		[SerializeField]
		private int _maxWeaponAmmo = 120;
		[SerializeField]
		private bool _hasUnlimitedAmmo;
		[SerializeField]
		private float _reloadTime = 2f;

		[Networked]
		private NetworkBool _isReloading { get; set; }
		[Networked]
		private int _magazineAmmo { get; set; }
		[Networked]
		private int _weaponAmmo { get; set; }
		[Networked]
		private TickTimer _reloadCooldown { get; set; }

		// WeaponComponent INTERFACE

		public override bool IsBusy => _reloadCooldown.ExpiredOrNotRunning(Runner) == false;

		public override bool CanFire()
		{
			if (_isReloading == true)
				return false;

			int availableAmmo = _hasMagazine == true ? _magazineAmmo : _weaponAmmo;
			return availableAmmo > 0;
		}

		public override void Fire()
		{
			if (_hasMagazine == true)
			{
				_magazineAmmo--;
			}
			else if (_hasUnlimitedAmmo == false)
			{
				_weaponAmmo--;
			}
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_isReloading == true && _reloadCooldown.Expired(Runner) == true)
			{
				int reloadAmmo = _maxMagazineAmmo - _magazineAmmo;

				if (_hasUnlimitedAmmo == false)
				{
					reloadAmmo = Mathf.Min(reloadAmmo, _weaponAmmo);
					_weaponAmmo -= reloadAmmo;
				}

				_magazineAmmo += reloadAmmo;

				_isReloading = false;
			}

			if (ShouldReload() == true)
			{
				_reloadCooldown = TickTimer.CreateFromSeconds(Runner, _reloadTime);
				_isReloading = true;
			}
		}

		public override void Spawned()
		{
			int initialAmmo = _hasUnlimitedAmmo == true ? int.MaxValue : _initialAmmo;

			_magazineAmmo = _hasMagazine == true ? Mathf.Clamp(initialAmmo, 0, _maxMagazineAmmo) : 0;
			_weaponAmmo = Mathf.Clamp(initialAmmo - _magazineAmmo, 0, _maxWeaponAmmo);
		}

		// PRIVATE MEMBERS

		private bool ShouldReload()
		{
			if (_isReloading == true)
				return false;

			if (_hasMagazine == false)
				return false; // Weapon without reloading

			if (_magazineAmmo == _maxMagazineAmmo)
				return false; // Already max

			if (_weaponAmmo == 0)
				return false; // No ammo to reload

			bool reloadRequested = Buttons.IsSet(EInputButton.Reload) == true || _magazineAmmo == 0;
			if (reloadRequested == false)
				return false;

			return Weapon.IsBusy() == false;
		}
	}
}
