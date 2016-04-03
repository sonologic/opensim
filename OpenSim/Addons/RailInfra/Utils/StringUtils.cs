using System;
using OpenMetaverse;

namespace OpenSim.Addons.RailInfra.Utils
{
	public class StringUtils
	{
		public static string FormatAxisAngle(Quaternion q)
		{
			Vector3 axis;
			float angle;
			q.GetAxisAngle (out axis, out angle);
			return String.Format("{0}, {1}", axis, angle);
		}
	}
}

