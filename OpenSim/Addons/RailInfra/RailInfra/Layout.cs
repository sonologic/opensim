using System;
using System.Collections.Generic;

namespace OpenSim.Addons.RailInfra
{
	// <summary>A layout is a collection of tracks, each track being
	// a disconnected graph. Each track is represented by it's root
	// node (which is any arbitrary node on the track)</summary>
	public class Layout
	{
		// <summary>List of tracks</summary>
		private Dictionary<int, List<TrackPoint>> tracks;
		private Dictionary<TrackPoint, int> tp_track_ids;

		private int NextTrackId;

		public Layout ()
		{
			tracks = new Dictionary<int, List<TrackPoint>>();
			tp_track_ids = new Dictionary<TrackPoint, int>();
			NextTrackId = 0;
		}

		public void Add(TrackPoint tp)
		{
			if(tracks.Count==0) {
				int track_id = NextTrackId++;
				tracks.Add(track_id, new List<TrackPoint>());
				tracks [track_id].Add (tp);
				tp_track_ids[tp] = track_id;
			} else {
				int? prev_track_id = null;
				int? next_track_id = null;

				if(tp.Prev!=null)
					if (tp_track_ids.ContainsKey(tp.Prev))
						prev_track_id = tp_track_ids[tp.Prev];

				if(tp.Next!=null)
					if (tp_track_ids.ContainsKey(tp.Next))
						next_track_id = tp_track_ids[tp.Next];

				if(next_track_id!=null && prev_track_id!=null) {
					// need to merge?
					int dst;
					int src;
					if (next_track_id < prev_track_id) {
						dst = (int)next_track_id;
						src = (int)prev_track_id;
					} else {
						dst = (int)prev_track_id;
						src = (int)next_track_id;
					}

					if (src != dst) {  // merge
						foreach (TrackPoint track_tp in tracks[src]) {
							tracks [dst].Add (track_tp);
							tp_track_ids [track_tp] = dst;
						}
						tracks.Remove (src);
					}

					tp_track_ids [tp] = dst;
					tracks [dst].Add (tp);
				} else if (next_track_id != null) {
					tp_track_ids [tp] = (int)next_track_id;
					tracks [(int)next_track_id].Add (tp);
				} else if (prev_track_id != null) {
					tp_track_ids [tp] = (int)prev_track_id;
					tracks [(int)prev_track_id].Add (tp);
				} else {
					int track_id = NextTrackId++;
					tracks.Add (track_id, new List<TrackPoint> ());
					tracks [track_id].Add (tp);
					tp_track_ids [tp] = track_id;
				}
			}
		}

		public override string ToString()
		{
			string rv = "";

			rv += String.Format ("number of tracks: {0}\n", tracks.Count);

			foreach (int track_id in tracks.Keys) {
				rv += String.Format ("track: {0}\n", track_id);
				foreach(TrackPoint tp in tracks[track_id]) {
					rv += String.Format ("  > {0}\n", tp.ToString ());
				}
			}

			return rv;
		}
	}
}

