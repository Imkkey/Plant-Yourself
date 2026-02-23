using DG.Tweening;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIScreenEffects : UIBehaviour
	{
		// PRIVATE METHODS

		[SerializeField]
		private CanvasGroup _hitGroup;
		[SerializeField]
		private UIBehaviour _deathGroup;

		[Header("Animation")]
		[SerializeField]
		private float _hitFadeInDuration = 0.1f;
		[SerializeField]
		private float _hitFadeOutDuration = 0.7f;

		[Header("Audio")]
		[SerializeField]
		private AudioSetup _hitSound;
		[SerializeField]
		private AudioSetup _deathSound;

		// PUBLIC METHODS

		public void OnHitTaken(HitData hit)
		{
			if (hit.Amount <= 0)
				return;

			if (hit.Action == EHitAction.Damage)
			{
				float alpha = Mathf.Lerp(0, 1f, hit.Amount / 20f);

				ShowHit(_hitGroup, alpha);
				GameUI.PlaySound(_hitSound, EForceBehaviour.ForceAny);

				if (hit.IsFatal == true)
				{
					_deathGroup.SetActive(true);
					GameUI.PlaySound(_deathSound, EForceBehaviour.ForceAny);
				}
			}
		}

		public void UpdateEffects(PlayerAgent agent)
		{
			_deathGroup.SetActive(agent.Health.IsAlive == false);
		}

		// MONOBEHAVIOUR

		protected void OnEnable()
		{
			_hitGroup.SetActive(true);
			_hitGroup.alpha = 0f;

			_deathGroup.SetActive(false);
		}

		// PRIVATE METHODS

		private void ShowHit(CanvasGroup group, float targetAlpha)
		{
			DOTween.Kill(group);

			group.DOFade(targetAlpha, _hitFadeInDuration);
			group.DOFade(0f, _hitFadeOutDuration).SetDelay(_hitFadeInDuration);
		}
	}
}
