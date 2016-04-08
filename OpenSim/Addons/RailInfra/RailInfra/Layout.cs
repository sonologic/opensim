using System;
using System.Collections.Generic;
using log4net;
using System.Reflection;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Addons.RailInfra.Utils;
using OpenMetaverse;

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
		private Scene m_scene;
		private RailInfraModule m_railinfra;
		private float m_TrackPointDistanceSquared;
		private float m_TrackPointAngle;

		private void Initialize()
		{
			tracks = new Dictionary<int, List<TrackPoint>>();

			tp_track_ids = new Dictionary<TrackPoint, int>();

			NextTrackId = 0;
		}

		public Layout (RailInfraModule module, Scene scene)
		{
			m_scene = scene;
			m_railinfra = module;
			m_TrackPointDistanceSquared = 12 * 12;
			m_TrackPointAngle = 0.16f;

			Initialize ();
		}

		public Layout (RailInfraModule module, Scene scene, float TrackPointDistanceSquared, float TrackPointAngle)
		{
			m_scene = scene;
			m_railinfra = module;
			m_TrackPointDistanceSquared = TrackPointDistanceSquared;
			m_TrackPointAngle = TrackPointAngle;

			Initialize ();
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


		// track model building:

		private List<SceneObjectGroup> GetGuides()
		{
			List<SceneObjectGroup> guides = new List<SceneObjectGroup>();

			List<SceneObjectGroup> objects = m_scene.GetSceneObjectGroups ();
			m_log.DebugFormat ("[RailInfra]  List length {0}", objects.Count);

			// loop over objects, searching for guides
			foreach(SceneObjectGroup obj in objects) {
				if (obj.GetPartCount()==1 && (obj.Name == "Guide" || obj.Name == "Alt Guide")) {
					guides.Add (obj);
					m_log.DebugFormat ("[RailInfra]   found: {0} ({1}) at {2}, rot {3}", obj.UUID, obj.Name, obj.AbsolutePosition.ToString(), obj.GroupRotation.ToString());
				}
			}

			return guides;
		}

		private TrackPoint CreateTrackPointFromCandidates(
			SceneObjectGroup track,
			TrackPoint candidate_tp,
			TrackPoint alt_candidate_tp)
		{
			// create new TrackPoint descendant (Guide or Switch)
			TrackPoint new_tp;

			if (candidate_tp != null && alt_candidate_tp != null) {  // g1 is switch
				m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", candidate_tp.ObjectGroup.Description, alt_candidate_tp.ObjectGroup.Description);
				Switch sw = new Switch (track);

				sw.Branch = alt_candidate_tp;
				sw.Main = candidate_tp;

				sw.Branch.Prev = sw;
				sw.Main.Prev = sw;

				new_tp = sw;
			} else if (candidate_tp != null) {						// g1 is guide to guide
				Guide guide = new Guide(track);
				guide.Next = candidate_tp;
				guide.Next.Prev = guide;
				new_tp = guide;
				m_log.DebugFormat ("   | candidate_tp: {0}", candidate_tp);
				m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", candidate_tp.ObjectGroup.Description, "null");
			} else if (alt_candidate_tp != null) {					// g1 is guide to alt guide
				Guide guide = new Guide(track);
				guide.Next = alt_candidate_tp;
				guide.Next.Prev = guide;
				new_tp = guide;
				m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", "null", alt_candidate_tp.ObjectGroup.Description);
			} else {											// g1 is stand-alone
				m_log.DebugFormat("   | candidate = {0}, alt_candidat = {1}", "null", "null");
				Guide guide = new Guide(track);
				guide.Next = null;
				new_tp = guide;
			}

			return new_tp;
		}

		private TrackPoint GetTrackPointFromCache(Dictionary<SceneObjectGroup, TrackPoint> cache, SceneObjectGroup obj)
		{
			TrackPoint rv = null;

			if (obj != null) {
				if (!cache.ContainsKey (obj)) {
					m_log.DebugFormat ("   | candidate first seen, creating PartialTrackpoint");
					cache [obj] = new PartialTrackPoint (obj);
				}
				rv = cache [obj];
			}

			return rv;
		}

		public void ScanScene()
		{
			// todo: lock access

			m_log.DebugFormat ("[RailInfra] scanning region {0}", m_scene.Name);

			// collect guides / alt guides
			List<SceneObjectGroup> guides = GetGuides();

			List<TrackPoint> track_points = new List<TrackPoint> ();

			// this dictionary will get filled one-by-one when looping over
			// scene objects, entries may be lazily initialized by the
			// PartialTrackPoint placeholder class
			Dictionary<SceneObjectGroup, TrackPoint> obj_to_tp = new Dictionary<SceneObjectGroup, TrackPoint> ();

			// loop over guides to fill in links
			foreach (SceneObjectGroup g1 in guides) {
				SceneObjectGroup candidate = null;
				SceneObjectGroup alt_candidate = null;

				m_log.DebugFormat ("out| tp1 = {0}, {1}, {2}, partcount={1}",
					g1.Description, 
					g1.AbsolutePosition,
					StringUtils.FormatAxisAngle(Quaternion.Normalize(g1.GroupRotation)),
					g1.GetPartCount());
				
				foreach (SceneObjectGroup g2 in guides) {
					if (g1 != g2) {
						float dist = MathUtils.DistanceSquared(g1, g2);
						double ang_obj = MathUtils.GetAngle(g1, g2);


						if (dist <= m_TrackPointDistanceSquared && ang_obj <= m_TrackPointAngle) {
							// g2 is inside g1´s scan cone
							m_log.DebugFormat ("*in| tp2 = {0}, {1}, {2}, distance = {3}, angle = {4}, delta_rot = {5}", 
								g2.Description, g2.AbsolutePosition, StringUtils.FormatAxisAngle( g2.GroupRotation),
								dist, ang_obj, StringUtils.FormatAxisAngle(g2.GroupRotation / g1.GroupRotation));

							// if Guide, potential candidate
							if (g2.Name == "Guide") {
								if (candidate == null) {
									candidate = g2;
								} else {
									if (MathUtils.DistanceSquared (candidate, g1) > dist) {
										candidate = g2;
									}
								}
							} else { // Alt Guide, potential alt_candidate
								if (alt_candidate == null) {
									alt_candidate = g2;
								} else {
									if (MathUtils.DistanceSquared (alt_candidate, g1) > dist) {
										alt_candidate = g2;
									}
								}
							}
						} else {
							// g2 is *not* inside g1´s scan cone
							m_log.DebugFormat (" in| tp2 = {0}, {1}, {2}, distance = {3}, angle = {4}", 
								g2.Description, g2.AbsolutePosition, StringUtils.FormatAxisAngle (g2.GroupRotation),
								dist, ang_obj);
						}
					}
				}

				// if g1 itself is alt guide, don't consider promotion to switch
				if (g1.Name == "Alt Guide" && candidate != null && alt_candidate != null) {
					candidate = null;
				}
					
				// get TrackPoint objects from obj_to_tp dict (create if first seen)
				TrackPoint candidate_tp = GetTrackPointFromCache(obj_to_tp, candidate);

				// todo: check if alt_candidate angle is not unexpected (ie, not an 
				// alt guide coming in on the direction of this guide), otherwise reject
				// alt_candidate
				TrackPoint alt_candidate_tp = GetTrackPointFromCache(obj_to_tp, alt_candidate);

				// create new TrackPoint descendant (Guide or Switch)
				TrackPoint new_tp = CreateTrackPointFromCandidates(g1, candidate_tp, alt_candidate_tp);

				if (obj_to_tp.ContainsKey (g1) && (obj_to_tp [g1].GetType () == typeof(PartialTrackPoint))) {
					m_log.DebugFormat ("   | already present in obj_to_tp, replace");
					m_log.DebugFormat ("   |   old in table {0}", obj_to_tp [g1]);
					m_log.DebugFormat ("   |   new_tp       {0}", new_tp);

					// someone inserted g1 as PartialTrackPoint, replace
					TrackPoint partial_track_point = obj_to_tp [g1];

					new_tp.Prev = obj_to_tp [g1].Prev;
					obj_to_tp [g1] = new_tp;

					// replace all references to Partial
					foreach (TrackPoint tp in obj_to_tp.Values) {
						tp.ReplaceLink (partial_track_point, new_tp);
					}

					m_log.DebugFormat ("   |   after repl   {0}", obj_to_tp [g1]);
				} else {
					obj_to_tp [g1] = new_tp;
				}

				track_points.Add (new_tp);
			} // foreach (SceneObjectGroup g2 in guides)

			m_log.DebugFormat ("   | Before resolving:");
			foreach (TrackPoint tp in track_points) {
				m_log.DebugFormat ("   |   {0}", tp);
			}

			// resolve any links to PartialTrackPoint's
			foreach (TrackPoint tp in track_points) {
				if (tp.GetType () == typeof(Guide)) {
					Guide g = (Guide)tp;
					if (g.Next != null && g.Next.GetType () == typeof(PartialTrackPoint))
						g.Next = obj_to_tp [g.Next.ObjectGroup];
				} else {
					Switch s = (Switch)tp;
					if (s.Main != null && s.Main.GetType () == typeof(PartialTrackPoint))
						s.Main = obj_to_tp [s.Main.ObjectGroup];
					if (s.Branch != null && s.Branch.GetType () == typeof(PartialTrackPoint))
						s.Branch = obj_to_tp [s.Branch.ObjectGroup];
				}
			}

			// now we have Next and Prev initialised for each TrackPoint

			// one-by-one add to layout, this will seperate the tp's in disconnected graphs

			foreach (TrackPoint tp in track_points) {
				m_log.DebugFormat ("add to layout: {0}", tp);
				Add (tp);					
			}

			m_log.Debug (ToString ());

		} // ScanScene()



		// string conversions:

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

		static string track_chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

		static public char TrackIdToChar(int track_id)
		{
			if(track_id<0 || track_id>track_chars.Length)
				return '-';
			return track_chars [track_id];
		}

		public string ToAsciiGrid(int width, int height)
		{
			char[,] grid = new char[width,height];

			for(int y=0;y<height;y++)
				for(int x=0;x<width;x++)
					grid[x,y]='.';

			float? region_w = null;
			float? region_h = null;
			float? factor_w = null;
			float? factor_h = null;

			foreach(int track_id in tracks.Keys) {
				foreach(TrackPoint tp in tracks[track_id]) {
					if(region_w==null) {
						region_w = tp.ObjectGroup.Scene.RegionInfo.RegionSizeX;
						region_h = tp.ObjectGroup.Scene.RegionInfo.RegionSizeY;
						factor_w = width / region_w;
						factor_h = height / region_h;
						m_log.DebugFormat ("region w,h = {0}, {1}  -  factor w,h = {2}, {3}", region_w, region_h, factor_w, factor_h);
					}

					int x = (int)Math.Round((decimal)(tp.ObjectGroup.AbsolutePosition.X * factor_w), 0);
					int y = (int)Math.Round((decimal)(tp.ObjectGroup.AbsolutePosition.Y * factor_h), 0);

					m_log.DebugFormat ("region = ({0}, {1}), grid = ({2}, {3}), {4}",
						tp.ObjectGroup.AbsolutePosition.X,
						tp.ObjectGroup.AbsolutePosition.Y,
						x,
						y,
						tp);
					grid[x,height-y] = TrackIdToChar(track_id);
				}
			}

			string rv="";
			for(int y=0;y<height;y++) {
				for(int x=0;x<width;x++)
					rv += grid[x,y];
				rv += "\n";
			}
			return rv;
			
		}

		public string ToAsciiGrid()
		{
			
			return ToAsciiGrid (80, 24);
		}
	}
}

