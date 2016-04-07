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
//using OpenSim.Region.CoreModules.Framework.InterfaceCommander;



namespace OpenSim.Addons.RailInfra
{

	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RailInfraModule")]
	public class RailInfraModule : ISharedRegionModule
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private ScriptChatHandler m_chat_handler;

		// config values
		private string m_ManagerUUID;
		public int m_channel { get; private set; }
		private float m_TrackPointDistanceSquared;
		private float m_TrackPointAngle;

		// internal book-keeping
		public Dictionary<Scene, Layout> m_layouts { get; private set; }
		public Fleet m_fleet { get; private set; }
		public List<Scene> m_scenes { get; private set; }
		public Dictionary<Scene, ConsoleHandler> m_console_handlers { get; private set; }

		// interface implementation:

		public string Name { get { return "RailInfraModule"; } }

		public Type ReplaceableInterface { get { return null; } }

		public void Initialise(IConfigSource config)
		{
			m_log.DebugFormat ("[RailInfra] Initialise()");


			IConfig conf = config.Configs ["RailInfraModule"];

			// read config values
			m_ManagerUUID = conf.GetString ("ManagerUUID", String.Empty);
			m_channel = conf.GetInt ("Channel", -62896351);
			m_TrackPointDistanceSquared = conf.GetFloat ("TrackPointDistance") * conf.GetFloat ("TrackPointDistance");
			m_TrackPointAngle = conf.GetFloat ("TrackPointAngle");

			// initialize book-keeping
			m_scenes = new List<Scene>();
			m_layouts = new Dictionary<Scene, Layout> ();
			m_fleet = new Fleet ();
			m_console_handlers = new Dictionary<Scene, ConsoleHandler> ();
			m_chat_handler = new ScriptChatHandler (this);

			m_log.DebugFormat ("[RailInfra] ManagerUUID = {0}", m_ManagerUUID);
			m_log.DebugFormat ("[RailInfra] Channel = {0}", m_channel);
			m_log.DebugFormat ("[RailInfra] TrackPointDistanceSquared = {0}", m_TrackPointDistanceSquared);
			m_log.DebugFormat ("[RailInfra] TrackPointAngle = {0}", m_TrackPointAngle);
		}



		public void AddRegion(Scene scene)
		{
			m_log.DebugFormat ("[RailInfra] add region {0}", scene.RegionInfo.RegionName);

			//scene.EventManager.OnChatBroadcast += OnChatFromClient;
			scene.EventManager.OnChatFromWorld += m_chat_handler.ProcessChat;
			//scene.EventManager.OnChatFromClient += OnChatFromClient;

			ConsoleHandler ch = new ConsoleHandler (this, scene);
			m_console_handlers.Add (scene, ch);

			m_scenes.Add (scene);
		}

		public void RemoveRegion(Scene scene)
		{
			m_scenes.Remove (scene);
			m_console_handlers.Remove (scene);
			m_layouts.Remove (scene);

			m_log.DebugFormat ("[RailInfra] remove region {0}", scene.RegionInfo.RegionName);

			//scene.EventManager.OnChatFromClient -= OnChatFromClient;
			scene.EventManager.OnChatFromWorld -= m_chat_handler.ProcessChat;
			//scene.EventManager.OnChatBroadcast -= OnChatFromClient;
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

		public void ScanScenes()
		{
			// todo: lock access
			m_layouts = new Dictionary<Scene, Layout>();

			foreach (Scene scene in m_scenes) {
				

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

							if (dist <= m_TrackPointDistanceSquared && ang_obj <= m_TrackPointAngle) {
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

		/*
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

				if (chat.Channel == m_channel) {
					string[] tokens = chat.Message.Split (' ');

					if (tokens.Length > 1) {
						string cmd = tokens [0];

						m_log.DebugFormat ("cmd is {0}", cmd);

						switch (cmd) {
						case "register":
							m_log.DebugFormat ("registering {0}", sender.UUID);
							if (!m_fleet.ContainsUUID (sender.UUID)) {
								m_fleet.RegisterVehicle (sender.UUID, sender);
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
		}*/
	}
}

