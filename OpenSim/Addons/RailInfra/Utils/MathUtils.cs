using System;
using OpenSim.Region.Framework.Scenes;
using log4net;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Shared;
using System.Reflection;

namespace OpenSim.Addons.RailInfra.Utils
{
	public class MathUtils
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static float DistanceSquared(SceneObjectGroup p1, SceneObjectGroup p2)
		{
			float dx = Math.Abs (p1.AbsolutePosition.X - p2.AbsolutePosition.X);
			float dy = Math.Abs (p1.AbsolutePosition.Y - p2.AbsolutePosition.Y);
			float dz = Math.Abs (p1.AbsolutePosition.Z - p2.AbsolutePosition.Z);

			return (dx * dx) + (dy * dy) + (dz * dz);
		}

		public static double GetAngle(SceneObjectGroup tp1, SceneObjectGroup tp2)
		{
			// get angle (code copied from SensorRepeat.cs)
			double ang_obj = 0;

			SceneObjectPart SensePoint = tp1.GetLinkNumPart(0);

			Vector3 fromRegionPos = SensePoint.GetWorldPosition();

			// pre define some things to avoid repeated definitions in the loop body
			Vector3 toRegionPos;

			Quaternion q = SensePoint.GetWorldRotation();

			LSL_Types.Quaternion r = new LSL_Types.Quaternion(q);
			LSL_Types.Vector3 forward_dir = (new LSL_Types.Vector3(1, 0, 0) * r);
			double mag_fwd = LSL_Types.Vector3.Mag(forward_dir);

			toRegionPos = tp2.AbsolutePosition;

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

