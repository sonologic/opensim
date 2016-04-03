using System;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Addons.RailInfra.Utils;

namespace OpenSim.Addons.RailInfra
{
	public class TrackPoint
	{
		public SceneObjectGroup ObjectGroup { get; private set; }
		public TrackPoint Next;
		public TrackPoint Prev;

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

		public override string ToString ()
		{
			string next = "null";
			string prev = "null";

			if (Next != null)
				next = Next.ObjectGroup.UUID.ToString();

			if (Prev != null)
				prev = Prev.ObjectGroup.UUID.ToString();
			
			return string.Format ("[TrackPoint {0} ({5}): at {1}, r {2}, n={3}, p={4}]",
				ObjectGroup.UUID.ToString(),
				ObjectGroup.AbsolutePosition.ToString(),
				StringUtils.FormatAxisAngle(ObjectGroup.GroupRotation),
				next,
				prev,
				ObjectGroup.Name
			);
		}
	}
}

