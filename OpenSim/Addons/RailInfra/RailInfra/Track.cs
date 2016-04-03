using System;

namespace OpenSim.Addons.RailInfra
{
	public class Track
	{
		TrackPoint root;

		public Track() {
			root = null;
		}

		public void Add(TrackPoint p)
		{
			if (root == null) {
				root = p;
			} else {

			}
		}
	}
}

