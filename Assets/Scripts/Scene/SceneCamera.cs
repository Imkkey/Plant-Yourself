using UnityEngine;

namespace Projectiles
{
	public class SceneCamera : MonoBehaviour
	{
		// PUBLIC MEMBERS

		public Camera      Camera        => _camera;
		public ShakeEffect ShakeEffect   => _shakeEffect;

		// PRIVATE MEMBERS

		[SerializeField]
		private Camera _camera;
		[SerializeField]
		private ShakeEffect _shakeEffect;
	}
}
