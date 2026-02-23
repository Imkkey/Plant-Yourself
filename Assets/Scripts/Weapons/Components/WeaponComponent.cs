using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// A base for all weapon components. Holds references needed for proper weapon fire processing.
	/// </summary>
	public abstract class WeaponComponent : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public byte               WeaponActionId    { get; set; }
		public Weapon             Weapon            { get; set; }
		public Transform          BarrelTransform   { get; set; }
		public WeaponContext      WeaponContext     { get; set; }

		public NetworkButtons     Buttons           => WeaponContext.Buttons;
		public NetworkButtons     PressedButtons    => WeaponContext.PressedButtons;
		public Transform          FireTransform     => WeaponContext.FireTransform;

		public virtual bool       IsBusy             => false;

		// PUBLIC METHODS

		public virtual bool CanFire() => true;
		public virtual void Fire() {}
		public virtual void FireRender() {}
	}
}
