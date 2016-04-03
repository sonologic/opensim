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
using OpenSim.Addons.RailInfra;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Addons.RailInfra.Utils;



namespace OpenSim.Addons.RailInfra
{

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
		private Layout m_layout;
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
			m_layout = new Layout ();
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
				"show rail fleet",
				"show rail fleet",
				"Show the RailInfraModule fleet",
				HandleShowFleet);
			scene.AddCommand(
				"RailInfra",
				this,
				"show rail layout",
				"show rail layout",
				"Show the RailInfraModule layout (tracks)",
				HandleShowRailLayout);

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
					
					if (obj.GetPartCount()==1 && (obj.Name == "Guide" || obj.Name == "Alt Guide")) {
						guides.Add (new TrackPoint(obj));
						m_log.DebugFormat ("[RailInfra]   found: {0} ({1}) at {2}, rot {3}", obj.UUID, obj.Name, obj.AbsolutePosition.ToString(), obj.GroupRotation.ToString());
					}
				}
					
				foreach (TrackPoint tp1 in guides) {
					TrackPoint candidate = null;
					m_log.DebugFormat ("outer loop tp1 = {0}, partcount={1}", tp1.ObjectGroup.UUID, tp1.ObjectGroup.GetPartCount());
					foreach (TrackPoint tp2 in guides) {
						float dist = tp1.DistanceSquared (tp2);

						if (tp1 != tp2) {
							double ang_obj = GetAngle(tp1, tp2);

							if (dist < TrackPointDistanceSquared && ang_obj <= TrackPointAngle) {
								m_log.DebugFormat ("inner loop, tp1 {0}, {1}, {2}, tp2 {3}, {4}, {5}, distance {6}, angle {7}", 
									tp1.ObjectGroup.UUID, tp1.ObjectGroup.AbsolutePosition,	StringUtils.FormatAxisAngle(tp1.ObjectGroup.GroupRotation),
									tp2.ObjectGroup.UUID, tp2.ObjectGroup.AbsolutePosition,	StringUtils.FormatAxisAngle(tp2.ObjectGroup.GroupRotation),
									dist, ang_obj);

								if (candidate == null) {
									candidate = tp2;
								} else {
									if (candidate.DistanceSquared (tp1) > dist) {
										candidate = tp2;
									}
								}
							}
						}
					}
					tp1.Next = candidate;
					if (candidate != null) {
						candidate.Prev = tp1;
						m_log.DebugFormat ("------ tp1 ({0}) next = {1}", tp1.ObjectGroup.UUID, tp1.Next.ObjectGroup.UUID);
					} else {
						m_log.DebugFormat ("------ tp1 ({0}) next = null", tp1.ObjectGroup.UUID);
					}
				}

				// now we have Next and Prev initialised for each TrackPoint

				// one-by-one add to layout, this will seperate the tp's in disconnected graphs
				foreach (TrackPoint tp in guides) {
					m_layout.Add (tp);					
				}

				m_log.Debug (m_layout.ToString ());
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
			m_log.DebugFormat ("[RailInfra] HandleShowFleet, module {0}, cmd[0] {1}, cmd[1] {2}, cmd[2] {3}", module, cmd [0], cmd [1], cmd[2]);
			if (module == "RailInfra" && cmd.Length == 3 && cmd [0] == "show" && cmd [1] == "rail" && cmd[2] == "fleet") {
				MainConsole.Instance.OutputFormat ("{0,-36}  {1,-36}  {2,-16}  {3,-16}",
					"Key",
					"UUID",
					"Name",
					"Description"
				);
				MainConsole.Instance.Output (m_fleet.ToString ());
			}
		}

		private void HandleShowRailLayout(string module, string[] cmd)
		{
			if (module == "RailInfra" && cmd.Length == 3 && cmd [0] == "show" && cmd [1] == "rail" && cmd[2] == "layout") {
				MainConsole.Instance.OutputFormat ("{0}", m_layout.ToString ());
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



		public static double GetAngle(TrackPoint tp1, TrackPoint tp2)
		{
			// get angle (code copied from SensorRepeat.cs)
			double ang_obj = 0;

			SceneObjectPart SensePoint = tp1.ObjectGroup.GetLinkNumPart(0);

			Vector3 fromRegionPos = SensePoint.GetWorldPosition();

			// pre define some things to avoid repeated definitions in the loop body
			Vector3 toRegionPos;
			//double dis;
			//int objtype;
			//SceneObjectPart part;
			//float dx;
			//float dy;
			//float dz;

			Quaternion q = SensePoint.GetWorldRotation();

			LSL_Types.Quaternion r = new LSL_Types.Quaternion(q);
			LSL_Types.Vector3 forward_dir = (new LSL_Types.Vector3(1, 0, 0) * r);
			double mag_fwd = LSL_Types.Vector3.Mag(forward_dir);

			//Vector3 ZeroVector = new Vector3(0, 0, 0);

			toRegionPos = tp2.ObjectGroup.AbsolutePosition;

			// Calculation is in line for speed
			//dx = toRegionPos.X - fromRegionPos.X;
			//dy = toRegionPos.Y - fromRegionPos.Y;
			//dz = toRegionPos.Z - fromRegionPos.Z;

			//dis = Math.Sqrt(dx * dx + dy * dy + dz * dz);

			// not omni-directional. Can you see it ?
			// vec forward_dir = llRot2Fwd(llGetRot())
			// vec obj_dir = toRegionPos-fromRegionPos
			// dot=dot(forward_dir,obj_dir)
			// mag_fwd = mag(forward_dir)
			// mag_obj = mag(obj_dir)
			// ang = acos(dot /(mag_fwd*mag_obj))

			try {
				Vector3 diff = toRegionPos - fromRegionPos;
				double dot = LSL_Types.Vector3.Dot (forward_dir, diff);
				double mag_obj = LSL_Types.Vector3.Mag (diff);
				ang_obj = Math.Acos (dot / (mag_fwd * mag_obj));
			} catch {
				m_log.ErrorFormat ("exception calculating angle");
			}

			return ang_obj;
		}
	}
}

