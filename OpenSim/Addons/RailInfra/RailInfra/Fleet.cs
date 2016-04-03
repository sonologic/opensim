using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Addons.RailInfra
{
	public class Fleet
	{
		public Dictionary<UUID, Vehicle> Vehicles { get; private set; }

		public Fleet()
		{
			Vehicles = new Dictionary<UUID, Vehicle> ();
		}

		public bool ContainsUUID(UUID uuid)
		{
			return Vehicles.ContainsKey (uuid);
		}

		public void RegisterVehicle(Vehicle vehicle)
		{
			if (ContainsUUID (vehicle.ObjectGroup.UUID)) {
				throw new Exception ("Vehicle already registered");
			}
			Vehicles.Add (vehicle.ObjectGroup.UUID, vehicle);
		}

		public void RegisterVehicle(UUID uuid, SceneObjectGroup object_group)
		{
			RegisterVehicle (new Vehicle (object_group));
		}

		public override string ToString()
		{
			string rv = "";

			foreach (KeyValuePair<UUID, Vehicle> entry in Vehicles) {
				Vehicle vehicle = entry.Value;
				if (rv.Length > 0)
					rv += "\n";
				rv += String.Format("{0,-36}  {1}", entry.Key, vehicle.ToString ());
			}
			return rv;
		}
	}
}

