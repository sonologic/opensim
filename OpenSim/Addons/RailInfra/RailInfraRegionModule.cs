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
		public string m_ManagerUUID { get; private set; }
		public int m_channel { get; private set; }
		public float m_TrackPointDistanceSquared { get; private set; }
		public float m_TrackPointAngle { get; private set; }

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

		// <summary>Scan (build layout model) all scenes in m_scenes</summary>
		//
		public void ScanScenes()
		{
			m_layouts = new Dictionary<Scene, Layout>();

			foreach (Scene scene in m_scenes) {
				m_layouts [scene] = new Layout (this, scene);
				m_layouts [scene].ScanScene ();
			}
		}
	}
}

