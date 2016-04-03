using System;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using System.Reflection;
using System.Collections.Generic;



namespace OpenSim.RailInfra
{
	public enum VehicleState { NEW, CENTER, IDLE, RUN };

	public class TrackPoint
	{
		public SceneObjectGroup ObjectGroup { get; private set; }
		public TrackPoint Next { get; private set; }
		public TrackPoint Prev { get; private set; }

		public TrackPoint(SceneObjectGroup obj)
		{
			ObjectGroup = obj;
			Next = null;
			Prev = null;
		}

		public float Manhattan(TrackPoint p)
		{
			return Math.Abs (ObjectGroup.AbsolutePosition.X - p.ObjectGroup.AbsolutePosition.X) +
			Math.Abs (ObjectGroup.AbsolutePosition.Y - p.ObjectGroup.AbsolutePosition.Y) +
			Math.Abs (ObjectGroup.AbsolutePosition.Z - p.ObjectGroup.AbsolutePosition.Z);
		}

		public float DistanceSquared(TrackPoint p)
		{
			float dx = Math.Abs (ObjectGroup.AbsolutePosition.X - p.ObjectGroup.AbsolutePosition.X);
			float dy = Math.Abs (ObjectGroup.AbsolutePosition.Y - p.ObjectGroup.AbsolutePosition.Y);
			float dz = Math.Abs (ObjectGroup.AbsolutePosition.Z - p.ObjectGroup.AbsolutePosition.Z);

			return (dx * dx) + (dy * dy) + (dz * dz);
		}
	}

	public class Track
	{
		TrackPoint root;

		public Track() {
			root = null;
		}

		public void Add(TrackPoint p)
		{
			if (root == null) {
				root = p;
			} else {
				
			}
		}
	}

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

	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RailInfraModule")]
	public class RailInfraModule : ISharedRegionModule
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


		// config values
		private string ManagerUUID;
		private int Channel;
		private float TrackPointDistanceSquared;
		private float TrackPointAngle;

		// internal book-keeping
		private Fleet m_fleet;
		private List<Scene> Scenes;


		// interface implementation:

		public string Name { get { return "RailInfraModule"; } }

		public Type ReplaceableInterface { get { return null; } }

		public void Initialise(IConfigSource config)
		{
			m_log.DebugFormat ("[RailInfra] Initialise()");

			IConfig conf = config.Configs ["RailInfraModule"];

			// read config values
			ManagerUUID = conf.GetString ("ManagerUUID", String.Empty);
			Channel = conf.GetInt ("Channel", -62896351);
			TrackPointDistanceSquared = conf.GetFloat ("TrackPointDistance") * conf.GetFloat ("TrackPointDistance");
			TrackPointAngle = conf.GetFloat ("TrackPointAngle");

			// initialize book-keeping
			Scenes = new List<Scene>();
			m_fleet = new Fleet ();

			m_log.DebugFormat ("[RailInfra] ManagerUUID = {0}", ManagerUUID);
			m_log.DebugFormat ("[RailInfra] Channel = {0}", Channel);
		}



		public void AddRegion(Scene scene)
		{
			m_log.DebugFormat ("[RailInfra] add region {0}", scene.RegionInfo.RegionName);

			scene.EventManager.OnChatBroadcast += OnChatFromClient;
			scene.EventManager.OnChatFromWorld += OnChatFromClient;
			scene.EventManager.OnChatFromClient += OnChatFromClient;

			scene.RegisterModuleInterface<RailInfraModule>(this);
			scene.AddCommand(
				"RailInfra",
				this,
				"show fleet",
				"show fleet",
				"Show the RailInfraModule fleet",
				HandleShowFleet);

			Scenes.Add (scene);
		}

		public void RemoveRegion(Scene scene)
		{
			Scenes.Remove (scene);

			m_log.DebugFormat ("[RailInfra] remove region {0}", scene.RegionInfo.RegionName);

			scene.EventManager.OnChatFromClient -= OnChatFromClient;
			scene.EventManager.OnChatFromWorld -= OnChatFromClient;
			scene.EventManager.OnChatBroadcast -= OnChatFromClient;
		}

		public void PostInitialise()
		{
			m_log.DebugFormat("[RailInfra] PostInitialise()");

			m_log.DebugFormat ("[RailInfra] Scenes:");
			foreach (Scene scene in Scenes) {
				m_log.DebugFormat ("[RailInfra] fetching objects for region {0}", scene.Name);

				List<SceneObjectGroup> objects = scene.GetSceneObjectGroups ();

				List<TrackPoint> guides = new List<TrackPoint>();
				m_log.DebugFormat ("[RailInfra]  List length {0}", objects.Count);
				foreach(SceneObjectGroup obj in objects) {
					
					if (obj.Name == "Guide" || obj.Name == "Alt Guide") {
						guides.Add (new TrackPoint(obj));
						m_log.DebugFormat ("[RailInfra]   found: {0} ({1}) at {2}, rot {3}", obj.UUID, obj.Name, obj.AbsolutePosition.ToString(), obj.GroupRotation.ToString());
					}
				}

				Dictionary<TrackPoint, Dictionary<TrackPoint, float>> distances = new Dictionary<TrackPoint, Dictionary<TrackPoint, float>> ();

				foreach (TrackPoint tp1 in guides) {
					m_log.DebugFormat ("outer loop");
					foreach (TrackPoint tp2 in guides) {
						float dist = tp1.DistanceSquared (tp2);

						m_log.DebugFormat ("inner loop, tp1 {0}, tp2 {1}, distance {2}", tp1.ObjectGroup.UUID, tp2.ObjectGroup.UUID, dist);

						if(!distances.ContainsKey(tp1))
							distances.Add(tp1, new Dictionary<TrackPoint, float>());
						if(!distances.ContainsKey(tp2))
							distances.Add(tp2, new Dictionary<TrackPoint, float>());

						distances [tp1].Add (tp2, dist);

						m_log.DebugFormat ("tp1 rotation {0}. tp2 rotation {1}, diff {2}", 
							FormatAxisAngle(tp1.ObjectGroup.GroupRotation),
							FormatAxisAngle(tp2.ObjectGroup.GroupRotation),
							FormatAxisAngle(tp1.ObjectGroup.GroupRotation - tp2.ObjectGroup.GroupRotation));
					}
				}
			}
		}

		public void Close()
		{
			m_fleet = null;

			m_log.DebugFormat("[RailInfra]: Close()");
		}

		public void RegionLoaded(Scene scene)
		{
			
			m_log.DebugFormat ("[RailInfra] loaded region {0}", scene.RegionInfo.RegionName);


		}


		// handlers:

		private void HandleShowFleet(string module, string[] cmd)
		{
			m_log.DebugFormat ("[RailInfra] HandleShowFleet, module {0}, cmd[0] {1}, cmd[1] {2}", module, cmd [0], cmd [1]);
			if (module == "RailInfra" && cmd.Length == 2 && cmd [0] == "show" && cmd [1] == "fleet") {
				MainConsole.Instance.OutputFormat ("{0,-36}  {1,-36}  {2,-16}  {3,-16}",
					"Key",
					"UUID",
					"Name",
					"Description"
				);
				MainConsole.Instance.Output (m_fleet.ToString ());
			}
		}

		private void OnChatFromClient(Object sender_obj, OSChatMessage chat)
		{
			//if (chat.Channel == Channel) {

			m_log.DebugFormat ("[RailInfra] chat from {0} ({3}) on channel {2}, \"{1}\"",
				chat.SenderUUID.ToString(), 
				chat.Message,
				chat.Channel,
				chat.From
			);

			SceneObjectGroup sender = ((Scene)chat.Scene).GetSceneObjectGroup(chat.SenderUUID);

			if (sender != null) {
				m_log.DebugFormat ("sender object group {0} ({1})", sender.AbsolutePosition.ToString (), sender.Name);

				if (chat.Channel == Channel) {
					string[] tokens = chat.Message.Split (' ');

					if (tokens.Length == 2) {
						string cmd = tokens [0];

						m_log.DebugFormat ("cmd is {0}", cmd);

						switch (cmd) {
						case "register":
							m_log.DebugFormat ("registering {0}", tokens [1]);
							UUID uuid;
							if (UUID.TryParse (tokens [1], out uuid)) {
								if (!m_fleet.ContainsUUID (uuid)) {
									m_fleet.RegisterVehicle (uuid, sender);
								}
							} else {
								m_log.DebugFormat ("could not parse '{0}' as UUID", tokens [1]);
							}
							break;
						default:
							break;
						}
					} else {
						m_log.DebugFormat ("Invalid number of tokens: {0}", tokens.Length);
					}
				}
			}

			//}
		}

		public static string FormatAxisAngle(Quaternion q)
		{
			Vector3 axis;
			float angle;
			q.GetAxisAngle (out axis, out angle);
			return String.Format("{0}, {1}", axis, angle);
		}
	}
}

