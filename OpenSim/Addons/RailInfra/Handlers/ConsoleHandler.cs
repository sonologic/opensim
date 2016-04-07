using System;
using log4net;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;
using OpenSim.Framework;
using System.Reflection;

namespace OpenSim.Addons.RailInfra
{
	public class ConsoleHandler
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private Scene m_scene;
		private RailInfraModule m_railinfra;

		private delegate void ConsoleCommandHandler(string[] cmd);
		private Dictionary<String[], ConsoleCommandHandler> ConsoleCommandHandlers;

		public ConsoleHandler (RailInfraModule module, Scene scene)
		{
			m_scene = scene;
			m_railinfra = module;

			ConsoleCommandHandlers = new Dictionary<String[], ConsoleCommandHandler>();

			String[] cmd1 = new String[2] { "show", "fleet" };
			ConsoleCommandHandlers.Add(cmd1 , HandleShowFleet);

			String[] cmd2 = new String[2] { "show", "layout" };
			ConsoleCommandHandlers.Add(cmd2 , HandleShowLayout);

			String[] cmd3 = new String[2] { "show", "ascii" };
			ConsoleCommandHandlers.Add(cmd3 , HandleShowAscii);

			String[] cmd4 = new String[1] { "reload" };
			ConsoleCommandHandlers.Add(cmd4 , HandleReload);

			scene.RegisterModuleInterface<RailInfraModule>(module);

			scene.AddCommand(
				"RailInfra",
				module,
				"rail show fleet",
				"rail show fleet",
				"Show the RailInfraModule fleet",
				HandleConsoleCommand);
			scene.AddCommand(
				"RailInfra",
				module,
				"rail show layout",
				"rail show layout",
				"Show the RailInfraModule layout (tracks)",
				HandleConsoleCommand);
			scene.AddCommand(
				"RailInfra",
				module,
				"rail show ascii",
				"rail show ascii",
				"Show the RailInfraModule layout (tracks) as ascii art",
				HandleConsoleCommand);
			scene.AddCommand (
				"RailInfra",
				module,
				"rail reload",
				"rail reload",
				"Reloads the RailInfraModule track information",
				HandleConsoleCommand);
			
		}

		public void HandleConsoleCommand(string module, string[] cmd)
		{
			m_log.DebugFormat ("[RailInfra] HandleConsoleCommand, module {0}, m_scene {1}, cmd[] {2}", module, m_scene.Name, string.Join(" ", cmd));

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
			MainConsole.Instance.Output (m_railinfra.m_fleet.ToString ());
		}

		private void HandleShowLayout(string[] cmd)
		{
			foreach (Scene scene in m_railinfra.m_scenes) {
				MainConsole.Instance.OutputFormat ("---[ Region {0}", scene.RegionInfo.RegionName);
				MainConsole.Instance.OutputFormat ("{0}", m_railinfra.m_layouts [scene].ToString ());
			}
		}

		private void HandleShowAscii(string[] cmd)
		{
			foreach (Scene scene in m_railinfra.m_scenes) {
				MainConsole.Instance.OutputFormat ("---[ Region {0}", scene.RegionInfo.RegionName);
				MainConsole.Instance.OutputFormat ("{0}", m_railinfra.m_layouts [scene].ToAsciiGrid (100,200));
			}
		}

		private void HandleReload(string[] cmd)
		{
			MainConsole.Instance.OutputFormat ("Initiating track scan..");
			m_railinfra.ScanScenes ();
			MainConsole.Instance.OutputFormat ("Track scan complete..");
		}

	}
}

