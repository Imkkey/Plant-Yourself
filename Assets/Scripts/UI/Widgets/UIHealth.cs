using TMPro;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIHealth : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private TextMeshProUGUI _healthValue;

		private int _lastValue = -1;

		// PUBLIC METHODS

		public void UpdateHealth(Health health)
		{
			int currentHealth = Mathf.RoundToInt(health.CurrentHealth);
			if (currentHealth == _lastValue)
				return;

			_healthValue.text = currentHealth.ToString();
			_lastValue = currentHealth;
		}
	}
}
