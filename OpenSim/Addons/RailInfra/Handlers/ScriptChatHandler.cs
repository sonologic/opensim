using System;
using log4net;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.Addons.RailInfra
{
	public class ScriptChatConstants
	{
		public const int REGISTERED = 1;
		public const int ERROR = -1;
	}

	public class ScriptChatHandler
	{

		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private RailInfraModule m_railinfra;

		private delegate object[] ChatHandler(SceneObjectGroup sender, string[] cmd);
		private Dictionary<String[], ChatHandler> m_handlers;

		public ScriptChatHandler (RailInfraModule railinfra)
		{
			m_railinfra = railinfra;

			m_handlers = new Dictionary<string[], ChatHandler> ();

			String[] cmd1 = new String[2] { "vehicle", "register" };
			m_handlers.Add(cmd1 , HandleVehicleRegister);
		}

		public void ProcessChat(Object sender_obj, OSChatMessage chat)
		{
			m_log.DebugFormat ("[RailInfra] chat from {0} ({3}) on channel {2}, \"{1}\"",
				chat.SenderUUID.ToString(), 
				chat.Message,
				chat.Channel,
				chat.From
			);

			SceneObjectGroup sender = ((Scene)chat.Scene).GetSceneObjectGroup(chat.SenderUUID);

			if (sender != null) {
				m_log.DebugFormat ("[RailInfra] sender object group {0} ({1})", sender.AbsolutePosition.ToString (), sender.Name);

				if (chat.Channel == m_railinfra.m_channel) {
					string[] tokens = chat.Message.Split (' ');

					foreach(String[] handler_cmd in m_handlers.Keys) {
						int match=0;
						var cmd_tail = new List<String>();

						for(int i=0;i<tokens.Length;i++) {
							if(i<handler_cmd.Length && tokens[i]==handler_cmd[i]) match++;
							if(i>=handler_cmd.Length) cmd_tail.Add(tokens[i]);
						}

						if(match==handler_cmd.Length) {
							var response = m_handlers[handler_cmd](sender, cmd_tail.ToArray());

							IScriptModule[] engines = sender.Scene.RequestModuleInterfaces<IScriptModule>();
							foreach (IScriptModule engine in engines) {
								m_log.DebugFormat ("[RailInfra] notifying script engine: {0}", engine.Name);
								engine.PostObjectEvent(sender.UUID,
									"link_message",
									response);
							}
						}
					}
				}
			}
		}

		private object[] HandleVehicleRegister(SceneObjectGroup sender, string[] cmd)
		{
			int response_code = ScriptChatConstants.ERROR;
			string response = "";

			m_log.DebugFormat ("[RailInfra] registering vehicle {0}", sender.UUID);

			if (!m_railinfra.m_fleet.ContainsUUID (sender.UUID)) {
				m_railinfra.m_fleet.RegisterVehicle (sender.UUID, sender);
				response = "vehicle registered";
			} else {
				response = "already registered";
			}

			response_code = ScriptChatConstants.REGISTERED;

			return new object[]
			{
				// Event: link_message( integer sender_num, integer num, string str, key id ){ ; }
				new LSL_Types.LSLInteger(0),
				new LSL_Types.LSLInteger(response_code),
				new LSL_Types.LSLString(response),
				new LSL_Types.LSLString(sender.UUID.ToString())
			};

		}

	}
}

