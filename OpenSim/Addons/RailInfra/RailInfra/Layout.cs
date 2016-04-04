using System;
using System.Collections.Generic;
using log4net;
using System.Reflection;

namespace OpenSim.Addons.RailInfra
{
	// <summary>A layout is a collection of tracks, each track being
	// a disconnected graph. Each track is represented by it's root
	// node (which is any arbitrary node on the track)</summary>
	public class Layout
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

		private void MergeTracks(int id1, int id2)
		{
			
			int dst;
			int src;
			if (id1 < id2) {
				dst = id1;
				src = id2;
			} else {
				dst = id2;
				src = id1;
			}

			m_log.DebugFormat ("Merging tracks {0} and {1}", dst, src);

			if (src != dst) {  // merge
				foreach (TrackPoint track_tp in tracks[src]) {
					tracks [dst].Add (track_tp);
					tp_track_ids [track_tp] = dst;
				}
				tracks.Remove (src);
			}
		}

		public void Add(TrackPoint tp)
		{
			if(tracks.Count==0) {
				int track_id = NextTrackId++;
				tracks.Add(track_id, new List<TrackPoint>());
				tracks [track_id].Add (tp);
				tp_track_ids[tp] = track_id;
			} else {
				List<int> track_ids = new List<int>();

				List<TrackPoint> links = tp.Links;

				foreach(TrackPoint link_tp in links) {
					m_log.DebugFormat ("in add loop, link_tp is {0}", link_tp);
					if(tp_track_ids.ContainsKey(link_tp))
						track_ids.Add(tp_track_ids[link_tp]);
				}

				if(track_ids.Count>1) {
					// need to merge?
					track_ids.Sort();

					for(int i=1;i<track_ids.Count;i++) {
						MergeTracks(track_ids[0], track_ids[i]);
					}

					tp_track_ids [tp] = track_ids[0];
					tracks [track_ids[0]].Add (tp);
				} else if (track_ids.Count==1) {
					tp_track_ids [tp] = track_ids[0];
					tracks [track_ids[0]].Add (tp);
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

