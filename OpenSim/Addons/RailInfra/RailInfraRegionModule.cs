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



namespace OpenSim.RailInfra
{
	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RailInfraModule")]
	public class RailInfraModule : ISharedRegionModule
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public string Name { get { return "RailInfraModule"; } }

		public Type ReplaceableInterface { get { return null; } }

		public void Initialise(IConfigSource config)
		{
			m_log.DebugFormat ("[RailInfra] Initialise()");
		}

		private void OnChatFrom(Object sender, OSChatMessage chat)
		{
		}

		public void AddRegion(Scene scene)
		{
			m_log.DebugFormat ("[RailInfra] add region {0}", scene.RegionInfo.RegionName);

			//scene.EventManager.OnChatFromClient += null;
		}

		public void RemoveRegion(Scene scene)
		{
			m_log.DebugFormat ("[RailInfra] remove region {0}", scene.RegionInfo.RegionName);
		}

		public void PostInitialise()
		{
			m_log.DebugFormat("[RailInfra]: PostInitialise()");
		}

		public void Close()
		{
			m_log.DebugFormat("[RailInfra]: Close()");
		}

		public void RegionLoaded(Scene scene)
		{
			m_log.DebugFormat ("[RailInfra] loaded region {0}", scene.RegionInfo.RegionName);
		}
	}
}

