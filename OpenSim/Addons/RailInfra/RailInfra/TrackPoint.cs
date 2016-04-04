using System;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Addons.RailInfra.Utils;
using OpenMetaverse;
using System.Collections.Generic;

namespace OpenSim.Addons.RailInfra
{
	public abstract class TrackPoint
	{
		public SceneObjectGroup ObjectGroup { get; private set; }
		public TrackPoint Prev;


		public TrackPoint(SceneObjectGroup obj)
		{
			ObjectGroup = obj;
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

		public virtual bool HasLink(TrackPoint tp) {
			return (Prev==tp);
		}

		public virtual List<TrackPoint> Links { 
			get { 
				List<TrackPoint> tp = new List<TrackPoint> ();
				if (Prev != null)
					tp.Add (Prev);
				return new List<TrackPoint> (tp);
			}
		}

		public virtual void ReplaceLink(TrackPoint old_tp, TrackPoint new_tp)
		{
			if (Prev == old_tp)
				Prev = new_tp;
		}

		public override string ToString ()
		{
			return string.Format ("[TrackPoint: ObjectGroup={0}, Links={1}]", ObjectGroup, Links);
		}

		public virtual string ToShortString()
		{
			return String.Format("[TrackPoint: obj={0}, links={1}]", ObjectGroup, Links);
		}

	}

	public class PartialTrackPoint : TrackPoint
	{
		public UUID uuid;

		public PartialTrackPoint(SceneObjectGroup obj) : base(obj)
		{
		}

		public override string ToString ()
		{
			return string.Format ("[PartialTrackPoint: {2} ({1}, {0}), prev=<{3}>]", 
				ObjectGroup.UUID,
				ObjectGroup.Name,
				ObjectGroup.Description,
				(Prev==null)?"null":Prev.ToShortString());
		}

		public override string ToShortString() {
			return String.Format("[PartialTrackPoint: {2} ({1}, {0}), prev={3}]",
				ObjectGroup.UUID,
				ObjectGroup.Name,
				ObjectGroup.Description,
				Prev==null?"null":Prev.ToShortString());
		}
	}

	public class Guide : TrackPoint
	{
		public TrackPoint Next;

		public Guide(SceneObjectGroup obj) : base(obj)
		{
			Next = null;
		}

		public override bool HasLink(TrackPoint tp)
		{
			return (Next==tp) || (Prev==tp);
		}

		public override List<TrackPoint> Links { 
			get { 
				List<TrackPoint> tp = new List<TrackPoint> ();
				if (Prev != null)
					tp.Add (Prev);
				if (Next != null)
					tp.Add (Next);
				return new List<TrackPoint> (tp);
			}
		}

		public override string ToString ()
		{
			return string.Format ("[Guide: {6} ({5}, {0}) at {1}, r {2}, next=<{3}>, prev=<{4}>]",
				ObjectGroup.UUID.ToString(),
				ObjectGroup.AbsolutePosition.ToString(),
				StringUtils.FormatAxisAngle(ObjectGroup.GroupRotation),
				Next==null?"null":Next.ToShortString(),
				Prev==null?"null":Prev.ToShortString(),
				ObjectGroup.Name,
				ObjectGroup.Description
			);
		}

		public override string ToShortString ()
		{
			return string.Format ("[Guide: {0} ({1}, {2}) at {3}, r {4}, next={5}, prev={6}]",
				ObjectGroup.Description,
				ObjectGroup.Name,
				ObjectGroup.UUID,
				ObjectGroup.AbsolutePosition,
				StringUtils.FormatAxisAngle (ObjectGroup.GroupRotation),
				Next==null?"null":Next.ObjectGroup.Description,
				Prev==null?"null":Prev.ObjectGroup.Description
			);
		}

		public override void ReplaceLink(TrackPoint old_tp, TrackPoint new_tp)
		{
			base.ReplaceLink (old_tp, new_tp);
			if (Next == old_tp)
				Next = new_tp;
		}
	}

	public class Switch : TrackPoint
	{
		public TrackPoint Main;
		public TrackPoint Branch;

		public Switch(SceneObjectGroup obj) : base(obj)
		{
			Main = null;
			Branch = null;
		}

		public override bool HasLink(TrackPoint tp)
		{
			return (Prev==tp) || (Main==tp) || (Branch==tp);
		}

		public override List<TrackPoint> Links { 
			get { 
				List<TrackPoint> tp = new List<TrackPoint> ();
				if (Prev != null)
					tp.Add (Prev);
				if (Main != null)
					tp.Add (Main);
				if (Branch != null)
					tp.Add (Branch);
				return new List<TrackPoint> (tp);
			}
		}

		public override string ToString ()
		{
			return string.Format ("[Switch: {7} ({1}, {0}): at {2}, r {3}, main={4}, branch={5}, prev={6}]",
				ObjectGroup.UUID.ToString(),
				ObjectGroup.Name,
				ObjectGroup.AbsolutePosition.ToString(),
				StringUtils.FormatAxisAngle(ObjectGroup.GroupRotation),
				Main==null?"null":Main.ToShortString(),
				Branch==null?"null":Branch.ToShortString(),
				Prev==null?"null":Prev.ToShortString(),
				ObjectGroup.Description
			);
		}

		public override string ToShortString ()
		{
			return String.Format ("[Switch: {0} ({1}, {2}): at {3}, r {4}, main={5}, branch={6}, prev={7}]",
				ObjectGroup.Description,
				ObjectGroup.Name,
				ObjectGroup.UUID,
				ObjectGroup.AbsolutePosition,
				StringUtils.FormatAxisAngle (ObjectGroup.GroupRotation),
				Main==null?"null":Main.ObjectGroup.Description,
				Branch==null?"null":Branch.ObjectGroup.Description,
				Prev==null?"null":Prev.ObjectGroup.Description
			);
		}

		public override void ReplaceLink(TrackPoint old_tp, TrackPoint new_tp)
		{
			base.ReplaceLink (old_tp, new_tp);
			if (Branch == old_tp)
				Branch = new_tp;
			if (Main == old_tp)
				Main = new_tp;
		}
	}
}

