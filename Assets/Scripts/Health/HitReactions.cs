using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// A simple component for playing hit feedback on dummy targets (moving spheres).
	/// </summary>
	[RequireComponent(typeof(Health))]
	public class HitReactions : MonoBehaviour
	{
		// PRIVATE MEMBERS

		[Header("Animation")]
		[SerializeField]
		private Animation _animation;
		[SerializeField]
		private AnimationClip _hitClip;
		[SerializeField]
		private AnimationClip _fatalHitClip;

		// MONOBEHAVIOUR

		protected void OnEnable()
		{
			var health = GetComponent<Health>();
			health.HitTaken += OnHitTaken;
		}

		protected void OnDisable()
		{
			var health = GetComponent<Health>();
			health.HitTaken -= OnHitTaken;
		}

		// PRIVATE MEMBERS

		private void OnHitTaken(HitData hitData)
		{
			if (_animation != null)
			{
				var clip = hitData.IsFatal == true ? _fatalHitClip : _hitClip;
				_animation.Play(clip.name);
			}
		}
	}
}
