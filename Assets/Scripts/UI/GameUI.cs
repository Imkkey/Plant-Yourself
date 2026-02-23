using Fusion;
using UnityEngine;

namespace Projectiles.UI
{
	/// <summary>
	/// Acts as a UI root.
	/// </summary>
	public class GameUI : NetworkBehaviour
	{
		// PUBLIC MEMBERS

		public SceneContext Context => _context;

		// PRIVATE MEMBERS

		[SerializeField]
		private SceneContext _context;
		[SerializeField]
		private AudioEffect[] _audioEffects;
		[SerializeField]
		private AudioSetup _clickSound;

		[Header("Views")]
		[SerializeField]
		private UIGameplayView _gameplayView;

		// PUBLIC METHODS

		public bool PlaySound(AudioSetup effectSetup, EForceBehaviour force = EForceBehaviour.None)
		{
			return _audioEffects.PlaySound(effectSetup, force);
		}

		public bool PlayClickSound()
		{
			return PlaySound(_clickSound);
		}
	}
}
