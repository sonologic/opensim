using System;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Addons.RailInfra
{
	public enum VehicleState { NEW, CENTER, IDLE, RUN };

	public class Vehicle
	{
		public SceneObjectGroup ObjectGroup { get; private set; }
		public VehicleState State { get; private set; }

		public Vehicle(SceneObjectGroup obj) {
			ObjectGroup = obj;
			State = VehicleState.NEW;
		}

		public override string ToString()
		{
			return String.Format ("{0,-36}  {1,-16}  {2,-16}",
				ObjectGroup.UUID.ToString (),
				ObjectGroup.Name,
				ObjectGroup.Description);
		}
	}
}

