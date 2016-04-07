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
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Addons.RailInfra.Utils;



namespace OpenSim.Addons.RailInfra
{

	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RailInfraModule")]
	public class RailInfraModule : ISharedRegionModule
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private delegate void ConsoleCommandHandler(string[] cmd);
		private Dictionary<String[], ConsoleCommandHandler> ConsoleCommandHandlers;


		// config values
		private string ManagerUUID;
		private int Channel;
		private float TrackPointDistanceSquared;
		private float TrackPointAngle;

		// internal book-keeping
		private Dictionary<Scene, Layout> m_layouts;
		private Fleet m_fleet;
		private List<Scene> Scenes;

		// interface implementation:

		public string Name { get { return "RailInfraModule"; } }

		public Type ReplaceableInterface { get { return null; } }

		public void Initialise(IConfigSource config)
		{
			m_log.DebugFormat ("[RailInfra] Initialise()");

			ConsoleCommandHandlers = new Dictionary<String[], ConsoleCommandHandler>();

			String[] cmd1 = new String[2] { "show", "fleet" };
			ConsoleCommandHandlers.Add(cmd1 , HandleShowFleet);

			String[] cmd2 = new String[2] { "show", "layout" };
			ConsoleCommandHandlers.Add(cmd2 , HandleShowLayout);

			String[] cmd3 = new String[2] { "show", "ascii" };
			ConsoleCommandHandlers.Add(cmd3 , HandleShowAscii);

			String[] cmd4 = new String[1] { "reload" };
			ConsoleCommandHandlers.Add(cmd4 , HandleReload);

			/*
				{ { "show", "layout" } , HandleShowLayout },
				{ { "show", "ascii" } , HandleShowAscii },
				{ { "reload" } , HandleReload }*/

			//

			IConfig conf = config.Configs ["RailInfraModule"];

			// read config values
			ManagerUUID = conf.GetString ("ManagerUUID", String.Empty);
			Channel = conf.GetInt ("Channel", -62896351);
			TrackPointDistanceSquared = conf.GetFloat ("TrackPointDistance") * conf.GetFloat ("TrackPointDistance");
			TrackPointAngle = conf.GetFloat ("TrackPointAngle");

			// initialize book-keeping
			Scenes = new List<Scene>();
			m_layouts = new Dictionary<Scene, Layout> ();
			m_fleet = new Fleet ();

			m_log.DebugFormat ("[RailInfra] ManagerUUID = {0}", ManagerUUID);
			m_log.DebugFormat ("[RailInfra] Channel = {0}", Channel);
			m_log.DebugFormat ("[RailInfra] TrackPointDistanceSquared = {0}", TrackPointDistanceSquared);
			m_log.DebugFormat ("[RailInfra] TrackPointAngle = {0}", TrackPointAngle);
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
				"rail show fleet",
				"rail show fleet",
				"Show the RailInfraModule fleet",
				HandleConsoleCommand);
			scene.AddCommand(
				"RailInfra",
				this,
				"rail show layout",
				"rail show layout",
				"Show the RailInfraModule layout (tracks)",
				HandleConsoleCommand);
			scene.AddCommand(
				"RailInfra",
				this,
				"rail show ascii",
				"rail show ascii",
				"Show the RailInfraModule layout (tracks) as ascii art",
				HandleConsoleCommand);
			scene.AddCommand (
				"RailInfra",
				this,
				"rail reload",
				"rail reload",
				"Reloads the RailInfraModule track information",
				HandleConsoleCommand);
			

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
			ScanScenes ();
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

		// helpers:

		private void ScanScenes()
		{
			// todo: lock access
			m_layouts = new Dictionary<Scene, Layout>();

			foreach (Scene scene in Scenes) {
				

				m_log.DebugFormat ("[RailInfra] scanning region {0}", scene.Name);

				IScriptModule[] engines = scene.RequestModuleInterfaces<IScriptModule>();
				foreach (IScriptModule engine in engines) {
					m_log.DebugFormat ("[RailInfra] script engine: {0}", engine.Name);
				}
				
				// collect guides / alt guides
				List<SceneObjectGroup> objects = scene.GetSceneObjectGroups ();
				List<SceneObjectGroup> guides = new List<SceneObjectGroup>();
				m_log.DebugFormat ("[RailInfra]  List length {0}", objects.Count);

				// loop over objects, searching for guides
				foreach(SceneObjectGroup obj in objects) {

					if (obj.GetPartCount()==1 && (obj.Name == "Guide" || obj.Name == "Alt Guide")) {
						guides.Add (obj);
						m_log.DebugFormat ("[RailInfra]   found: {0} ({1}) at {2}, rot {3}", obj.UUID, obj.Name, obj.AbsolutePosition.ToString(), obj.GroupRotation.ToString());
					}
				}

				List<TrackPoint> track_points = new List<TrackPoint> ();
				Dictionary<SceneObjectGroup, TrackPoint> obj_to_tp = new Dictionary<SceneObjectGroup, TrackPoint> ();

				// loop over guides to fill in links
				foreach (SceneObjectGroup g1 in guides) {
					SceneObjectGroup candidate = null;
					SceneObjectGroup alt_candidate = null;


					m_log.DebugFormat ("out| tp1 = {0}, {1}, {2}, partcount={1}",
						g1.Description, 
						g1.AbsolutePosition,
						StringUtils.FormatAxisAngle(Quaternion.Normalize(g1.GroupRotation)),
						g1.GetPartCount());
					foreach (SceneObjectGroup g2 in guides) {
						float dist = MathUtils.DistanceSquared(g1, g2);

						if (g1 != g2) {
							double ang_obj = MathUtils.GetAngle(g1, g2);

							if (dist <= TrackPointDistanceSquared && ang_obj <= TrackPointAngle) {
								m_log.DebugFormat ("*in| tp2 = {0}, {1}, {2}, distance = {3}, angle = {4}, delta_rot = {5}", 
									g2.Description, g2.AbsolutePosition, StringUtils.FormatAxisAngle( g2.GroupRotation),
									dist, ang_obj, StringUtils.FormatAxisAngle(g2.GroupRotation / g1.GroupRotation));

								// if Guide, potential candidate
								if (g2.Name == "Guide") {
									if (candidate == null) {
										candidate = g2;
									} else {
										if (MathUtils.DistanceSquared (candidate, g1) > dist) {
											candidate = g2;
										}
									}
								} else { // Alt Guide, potential alt_candidate
									if (alt_candidate == null) {
										alt_candidate = g2;
									} else {
										if (MathUtils.DistanceSquared (alt_candidate, g1) > dist) {
											alt_candidate = g2;
										}
									}
								}
							} else {
								m_log.DebugFormat (" in| tp2 = {0}, {1}, {2}, distance = {3}, angle = {4}", 
									g2.Description, g2.AbsolutePosition, StringUtils.FormatAxisAngle (g2.GroupRotation),
									dist, ang_obj);
							}
						}
					}

					TrackPoint new_tp;

					// if g1 itself is alt guide, don't consider promotion to switch
					if (g1.Name == "Alt Guide" && candidate != null && alt_candidate != null) {
						candidate = null;
					}


					// get TrackPoint objects from obj_to_tp dict (create if first seen)
					TrackPoint candidate_tp = null;
					TrackPoint alt_candidate_tp = null;

					if (candidate != null) {
						if (!obj_to_tp.ContainsKey (candidate)) {
							m_log.DebugFormat ("   | candidate first seen, creating PartialTrackpoint");
							obj_to_tp [candidate] = new PartialTrackPoint (candidate);
						}
						candidate_tp = obj_to_tp [candidate];
					}

					// todo: check if alt_candidate angle is not unexpected (ie, not an 
					// alt guide coming in on the direction of this guide), otherwise reject
					// alt_candidate
					if (alt_candidate != null) {
						if (!obj_to_tp.ContainsKey (alt_candidate)) {
							m_log.DebugFormat ("   | alt_candidate first seen, creating PartialTrackpoint");
							obj_to_tp [alt_candidate] = new PartialTrackPoint (alt_candidate);
						}
						alt_candidate_tp = obj_to_tp [alt_candidate];

						m_log.DebugFormat("   | my  pos = {0}, rot = {1}", g1.AbsolutePosition, StringUtils.FormatAxisAngle(g1.GroupRotation));
						m_log.DebugFormat("   | alt pos = {0}, rot = {1}", alt_candidate.AbsolutePosition, StringUtils.FormatAxisAngle(alt_candidate.GroupRotation));	
					}

					if (candidate != null && alt_candidate != null) {  // g1 is switch
						m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", candidate.Description, alt_candidate.Description);
						Switch sw = new Switch (g1);

						sw.Branch = alt_candidate_tp;
						sw.Main = candidate_tp;

						sw.Branch.Prev = sw;
						sw.Main.Prev = sw;

						new_tp = sw;
					} else if (candidate != null) {						// g1 is guide to guide
						Guide guide = new Guide(g1);
						guide.Next = candidate_tp;
						guide.Next.Prev = guide;
						new_tp = guide;
						m_log.DebugFormat ("   | candidate_tp: {0}", candidate_tp);
						m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", candidate.Description, "null");
					} else if (alt_candidate != null) {					// g1 is guide to alt guide
						Guide guide = new Guide(g1);
						guide.Next = alt_candidate_tp;
						guide.Next.Prev = guide;
						new_tp = guide;
						m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", "null", alt_candidate.Description);
					} else {											// g1 is stand-alone
						m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", "null", "null");
						Guide guide = new Guide(g1);
						guide.Next = null;
						new_tp = guide;
					}

					if (obj_to_tp.ContainsKey (g1) && (obj_to_tp [g1].GetType () == typeof(PartialTrackPoint))) {
						m_log.DebugFormat ("   | already present in obj_to_tp, replace");
						m_log.DebugFormat ("   |   old in table {0}", obj_to_tp [g1]);
						m_log.DebugFormat ("   |   new_tp       {0}", new_tp);

						// someone inserted g1 as PartialTrackPoint, replace
						TrackPoint partial_track_point = obj_to_tp [g1];

						new_tp.Prev = obj_to_tp [g1].Prev;
						obj_to_tp [g1] = new_tp;

						// replace all references to Partial
						foreach (TrackPoint tp in obj_to_tp.Values) {
							tp.ReplaceLink (partial_track_point, new_tp);
						}

						m_log.DebugFormat ("   |   after repl   {0}", obj_to_tp [g1]);
					} else {
						obj_to_tp [g1] = new_tp;
					}

					track_points.Add (new_tp);

					/*
					tp1.Next = candidate;
					if (candidate != null) {
						candidate.Prev = tp1;
						m_log.DebugFormat ("------ tp1 ({0}) next = {1}", tp1.ObjectGroup.UUID, tp1.Next.ObjectGroup.UUID);
					} else {
						m_log.DebugFormat ("------ tp1 ({0}) next = null", tp1.ObjectGroup.UUID);
					}*/

				}

				m_log.DebugFormat ("   | Before resolving:");
				foreach (TrackPoint tp in track_points) {
					m_log.DebugFormat ("   |   {0}", tp);
				}
				// resolve any links to PartialTrackPoint's
				foreach (TrackPoint tp in track_points) {
					if (tp.GetType () == typeof(Guide)) {
						Guide g = (Guide)tp;
						if (g.Next != null && g.Next.GetType () == typeof(PartialTrackPoint))
							g.Next = obj_to_tp [g.Next.ObjectGroup];
					} else {
						Switch s = (Switch)tp;
						if (s.Main != null && s.Main.GetType () == typeof(PartialTrackPoint))
							s.Main = obj_to_tp [s.Main.ObjectGroup];
						if (s.Branch != null && s.Branch.GetType () == typeof(PartialTrackPoint))
							s.Branch = obj_to_tp [s.Branch.ObjectGroup];
					}
				}

				// now we have Next and Prev initialised for each TrackPoint

				// one-by-one add to layout, this will seperate the tp's in disconnected graphs
				m_layouts[scene] = new Layout();
				foreach (TrackPoint tp in track_points) {
					m_log.DebugFormat ("add to layout: {0}", tp);
					m_layouts[scene].Add (tp);					
				}

				m_log.Debug (m_layouts[scene].ToString ());
			}
						
		}

		// handlers:


		private void HandleConsoleCommand(string module, string[] cmd)
		{
			m_log.DebugFormat ("[RailInfra] HandleConsoleCommand, module {0}, cmd[] {1}", module, string.Join(" ", cmd));

			if (cmd.Length < 2 || cmd [0] != "rail")
				return;

			foreach(String[] handler_cmd in ConsoleCommandHandlers.Keys) {
				int match=0;
				var cmd_tail = new List<String>();

				for(int i=1;(i<cmd.Length);i++) {
					if(i<handler_cmd.Length+1 && cmd[i]==handler_cmd[i-1]) match++;
					if(i>=handler_cmd.Length+1) cmd_tail.Add(cmd[i]);
				}

				if(match==handler_cmd.Length) {
					ConsoleCommandHandlers[handler_cmd](cmd_tail.ToArray());
				}
			}
		}

		private void HandleShowFleet(string[] cmd)
		{
			m_log.DebugFormat ("[RailInfra] HandleShowFleet");

			MainConsole.Instance.OutputFormat ("{0,-36}  {1,-36}  {2,-16}  {3,-16}",
				"Key",
				"UUID",
				"Name",
				"Description"
			);
			MainConsole.Instance.Output (m_fleet.ToString ());
		}

		private void HandleShowLayout(string[] cmd)
		{
			foreach (Scene scene in Scenes) {
				MainConsole.Instance.OutputFormat ("---[ Region {0}", scene.RegionInfo.RegionName);
				MainConsole.Instance.OutputFormat ("{0}", m_layouts [scene].ToString ());
			}
		}

		private void HandleShowAscii(string[] cmd)
		{
			foreach (Scene scene in Scenes) {
				MainConsole.Instance.OutputFormat ("---[ Region {0}", scene.RegionInfo.RegionName);
				MainConsole.Instance.OutputFormat ("{0}", m_layouts [scene].ToAsciiGrid (100,200));
			}
		}

		private void HandleReload(string[] cmd)
		{
			MainConsole.Instance.OutputFormat ("Initiating track scan..");
			ScanScenes ();
			MainConsole.Instance.OutputFormat ("Track scan complete..");
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

						IScriptModule[] engines = sender.Scene.RequestModuleInterfaces<IScriptModule>();
						foreach (IScriptModule engine in engines) {
							m_log.DebugFormat ("[RailInfra] notifying script engine: {0}", engine.Name);

							object[] resobj = new object[]
							{
								// Event: link_message( integer sender_num, integer num, string str, key id ){ ; }
								new LSL_Types.LSLInteger(0), new LSL_Types.LSLInteger(42), new LSL_Types.LSLString("foo bar"), new LSL_Types.LSLString("")
							};

							engine.PostObjectEvent(sender.UUID, //partItemID,
								"link_message",
								resobj);
						}

					} else {
						m_log.DebugFormat ("Invalid number of tokens: {0}", tokens.Length);
					}
				}
			}

			//}
		}
	}
}

