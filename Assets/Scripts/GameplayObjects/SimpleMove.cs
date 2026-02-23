using Fusion;
using UnityEasing;
using UnityEngine;

namespace Projectiles.UI
{
	/// <summary>
	/// Component that ensures object's back and forth movement for debug purposes.
	/// Movement can be either fully predicted (_predictMove set to true) or interpolated.
	/// In case of interpolated movement a NetworkTransform component on the same object is expected.
	/// </summary>
	public class SimpleMove : NetworkBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Vector3 _offset = new(0f, 0f, 10f);
		[SerializeField]
		private float _speed = 10f;
		[SerializeField]
		private Ease _ease = Ease.InOutSine;
		[SerializeField]
		private bool _predictMove = true;

		[Networked]
		private int _startTick { get; set; }
		[Networked]
		private Vector3 _startPosition { get; set; }
		[Networked]
		private Vector3 _targetPosition { get; set; }

		private Rigidbody _rigidbody;
		private float _distance;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			if (HasStateAuthority == true)
			{
				_startTick = Runner.Tick;
				_startPosition = transform.position;
				_targetPosition = _startPosition + transform.rotation * _offset;
			}

			_distance = _offset.magnitude;

			if (_predictMove == true)
			{
				// When movement is predicted, object needs to be
				// simulated on all clients. Be aware that this enables
				// FixedUpdatedNetwork calls for ALL NetworkBehaviour
				// components attached to this NetworkObject.
				Runner.SetIsSimulated(Object, true);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (_predictMove == true || HasStateAuthority == true)
			{
				UpdatePosition(Runner.Tick);
			}

			if (_predictMove == true)
			{
				if (_rigidbody != null)
				{
					// Update colliders position in physics scene
					_rigidbody.position = transform.position;
				}
				else
				{
					Debug.LogError("For predicted movement Rigidbody component is needed to updated collider position (in physics scene) correctly during resimulations");
				}
			}
		}

		public override void Render()
		{
			if (_predictMove == true || HasStateAuthority == true)
			{
				float floatTick = (Runner.LocalRenderTime + Runner.DeltaTime) / Runner.DeltaTime;
				UpdatePosition(floatTick);
			}
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_rigidbody = GetComponent<Rigidbody>();
		}

		// PRIVATE METHODS

		private void UpdatePosition(float floatTick)
		{
			float elapsedTime = (floatTick - _startTick) * Runner.DeltaTime;
			float totalDistance = _speed * elapsedTime;

			float currentDistance = totalDistance % (_distance * 2f);

			if (currentDistance > _distance)
			{
				// Returning
				float progress = (currentDistance - _distance) / _distance;
				transform.position = Vector3.Lerp(_targetPosition, _startPosition, _ease.Get(progress));
			}
			else
			{
				float progress = currentDistance / _distance;
				transform.position = Vector3.Lerp(_startPosition, _targetPosition, _ease.Get(progress));
			}
		}
	}
}
