using System;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Addons.RailInfra
{
	public class TrackPoint
	{
		public SceneObjectGroup ObjectGroup { get; private set; }
		public TrackPoint Next { get; private set; }
		public TrackPoint Prev { get; private set; }

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
	}
}

