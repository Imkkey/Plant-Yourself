using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class SimulatePhysicsMultipeer : SimulationBehaviour
	{
		private void FixedUpdate()
		{
			if (Runner == null || Runner.IsRunning == false)
				return;

			// In multi-peer mode the physics scenes are not simulated by default. This can be solved by
			// adding Fusion Physics Addon but in case physics simulation is not part of the Fusion simulation
			// it is enough to simulate physics like this. In this sample we use physics for the flying cap visuals.
			if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
			{
				var scene = Runner.GetPhysicsScene();
				scene.Simulate(Time.fixedDeltaTime);
			}
		}
	}
}
