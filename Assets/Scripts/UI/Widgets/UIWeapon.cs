using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	public class UIWeapon : UIBehaviour
	{
		// PUBLIC MEMBERS

		public int Slot { get; private set; }

		// PRIVATE MEMBERS

		[SerializeField]
		private Image _icon;
		[SerializeField]
		private TextMeshProUGUI _name;
		[SerializeField]
		private TextMeshProUGUI _primaryActionDescription;
		[SerializeField]
		private TextMeshProUGUI _secondaryActionDescription;

		// PUBLIC METHODS

		public void SetData(Weapon weapon)
		{
			if (weapon == null || weapon.Object == null)
				return;

			Slot = weapon.WeaponSlot;

			_name.SetTextSafe(weapon.DisplayName);

			if (_icon != null)
			{
				_icon.sprite = weapon.Icon;
				_icon.SetActive(weapon.Icon != null);
			}

			_primaryActionDescription.SetTextSafe(weapon.PrimaryActionDescription);

			_secondaryActionDescription.SetActive(weapon.SecondaryActionDescription.HasValue());
			_secondaryActionDescription.SetTextSafe(weapon.SecondaryActionDescription);
		}
	}
}
