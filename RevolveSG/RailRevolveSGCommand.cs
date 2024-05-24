﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RevolveSG
{
    public class RailRevolveSG : Command
    {
        public RailRevolveSG()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }
        /// <summary>
        /// History recording version.
        /// This field is used to ensure the version of the replay function matches
        /// the version that originally created the geometry.
        /// </summary>
        private const int HISTORY_VERSION = 20242405;
        ///<summary>The only instance of this command.</summary>
        public static RailRevolveSG Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "RailRevolveSG"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: start here modifying the behaviour of your command.
            // ---

            doc.Objects.UnselectAll();
            doc.Views.Redraw();

            // get the curves or edges to revolve
            GetObject go = new GetObject();
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            go.SetCommandPrompt("Select curves to revolve");


            while (true)
            {
                GetResult result = go.GetMultiple(1, 0);
                if (go.CommandResult() != Rhino.Commands.Result.Success)
                    return go.CommandResult();

                else if (go.CommandResult() == Result.Success)
                    break;
            }

            Rhino.DocObjects.ObjRef[] objrefs = go.Objects();


            //
            doc.Objects.UnselectAll();
            doc.Views.Redraw();

            // get the rail curve
            GetObject go3 = new GetObject();
            go3.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            go3.SetCommandPrompt("Select rail curve");
            OptionToggle boolOption = new OptionToggle(RevolveSGSettings.use_last_axis, "Off", "On");

            if ((RevolveSGSettings.g != new Guid()) && (doc.Objects.FindId(RevolveSGSettings.g) != null))
                go3.AddOptionToggle("UseLastAxis", ref boolOption);
            else
                RevolveSGSettings.StoreIndex(-1);
            while (true)
            {
                
                GetResult result = go3.Get();
                if (go3.CommandResult() != Rhino.Commands.Result.Success)
                    return go3.CommandResult();
                else if (result == GetResult.Option)
                {
                    continue;
                }
                else if (go3.CommandResult() == Result.Success)
                    break;
            }
            Rhino.DocObjects.ObjRef objref3 = go3.Object(0);
            RevolveSGSettings.UseLastAxis(boolOption.CurrentValue);


            doc.Objects.UnselectAll();
            doc.Views.Redraw();

            // get the axis of revolution
            GetObject go2 = new GetObject();
            go2.SetCommandPrompt("Select axis");
            go2.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            if ((RevolveSGSettings.g != new Guid()) &&
                (RevolveSGSettings.use_last_axis == true) &&
                (doc.Objects.FindId(RevolveSGSettings.g) != null))
            {
                doc.Objects.Select(RevolveSGSettings.g);
                go2.GeometryFilter = Rhino.DocObjects.ObjectType.AnyObject;
            }
            go2.Get();
            doc.Objects.UnselectAll();

            if (go2.CommandResult() != Result.Success)
            {
                doc.Views.Redraw();
                return go2.CommandResult();
            }

            // store the axis in settings
            Rhino.DocObjects.ObjRef objref2 = go2.Object(0);
            Guid g = objref2.ObjectId;
            RevolveSGSettings.StoreAxis(g);

            // see if the selected axis curve is a brep edge
            var r = objref2.Geometry() as BrepEdge;
            if (r != null)
            {
                int index = r.EdgeIndex;
                RhinoApp.WriteLine("{0}", index);
                RevolveSGSettings.StoreIndex(index);
            }


            // convert the axis objref to a curve
            Curve linecrv = null;

            while (true)
            {
                var resu = objref2.Geometry() as Curve;
                if (resu != null)
                {
                    //RhinoApp.WriteLine("it's a curve");
                    linecrv = objref2.Curve();
                    break;
                }

                var re = objref2.Geometry() as Brep;
                if (re != null)
                {
                    // RhinoApp.WriteLine("it's a brep");
                    BrepEdge edge = re.Edges[RevolveSGSettings.store_index];
                    linecrv = edge.ToNurbsCurve();
                    break;
                }
                var res = objref2.Geometry() as Extrusion;
                if (res != null)
                {
                    // RhinoApp.WriteLine("it's an extrusion");
                    BrepEdge edge = res.ToBrep().Edges[RevolveSGSettings.store_index];
                    linecrv = edge.ToNurbsCurve();
                    break;
                }
                break;

            }

            if (linecrv == null)
            {
                doc.Views.Redraw();
                return Result.Failure;
            }

            Line revline = new Line(linecrv.PointAtStart, linecrv.PointAtEnd);
            foreach (Rhino.DocObjects.ObjRef objref in objrefs)
            {
                Curve crv = objref.Curve();
                Curve railcurve = objref3.Curve();
                NurbsSurface rev;
                rev = NurbsSurface.CreateRailRevolvedSurface(crv, railcurve, revline, false);
                Brep brep = rev.ToBrep();
                if (null != brep)
                {
                    // Create a history record
                    Rhino.DocObjects.HistoryRecord history = new Rhino.DocObjects.HistoryRecord(this, HISTORY_VERSION);
                    WriteHistory(history, objref, objref2, ref objref3);
                    doc.Objects.AddBrep(brep, null, history, false);
                }
            }
  
            


            // ---
            doc.Views.Redraw();
            return Result.Success;
        }

        /// <summary>
        /// Rhino calls the virtual ReplayHistory functions to to remake an objects when inputs have changed.  
        /// </summary>

        protected override bool ReplayHistory(Rhino.DocObjects.ReplayHistoryData replay)
        {
            Rhino.DocObjects.ObjRef objref = null;
            Rhino.DocObjects.ObjRef objref2 = null;
            Rhino.DocObjects.ObjRef objref3 = null;

            if (!ReadHistory(replay, ref objref, ref objref2, ref objref3))
                return false;

            // the profile
            Curve curve = objref.Curve();
            if (null == curve)
                return false;

            // the axis
            Curve linecrv;
            linecrv = objref2.Curve();

            if (null == linecrv)
            {
                Brep b = objref2.Brep();
                if (null == b)
                    return false;
                BrepEdge e = b.Edges[RevolveSGSettings.store_index];
                linecrv = e.ToNurbsCurve();
            }
            if (null == linecrv)
            {
                return false;
            }

            // the rail
            Curve railcurve = objref3.Curve();
            if (null == railcurve)
                return false;


            Line revline = new Line(linecrv.PointAtStart, linecrv.PointAtEnd);
            NurbsSurface rev;
            rev = NurbsSurface.CreateRailRevolvedSurface(curve, railcurve, revline, false);
            Brep brep = rev.ToBrep();

            replay.Results[0].UpdateToBrep(brep, null);
            return true;

        }

        /// <summary>
        /// Read a history record and extract the references for the antecedent objects.
        /// </summary>
        private bool ReadHistory(Rhino.DocObjects.ReplayHistoryData replay, ref Rhino.DocObjects.ObjRef objref, ref Rhino.DocObjects.ObjRef objref2, ref Rhino.DocObjects.ObjRef objref3)
        {
            if (HISTORY_VERSION != replay.HistoryVersion)
                return false;

            objref = replay.GetRhinoObjRef(0);
            if (null == objref)
                return false;

            objref2 = replay.GetRhinoObjRef(1);
            if (null == objref2)
                return false;

            objref3 = replay.GetRhinoObjRef(2);
            if (null == objref3)
                return false;

            return true;
        }

        /// <summary>
        /// Write a history record referencing the antecedent objects
        /// The history should contain all the information required to reconstruct the input.
        /// This may include parameters, options, or settings.
        /// </summary>
        private bool WriteHistory(Rhino.DocObjects.HistoryRecord history, Rhino.DocObjects.ObjRef objref, Rhino.DocObjects.ObjRef objref2, ref Rhino.DocObjects.ObjRef objref3)
        {

            if (!history.SetObjRef(0, objref))
                return false;

            if (!history.SetObjRef(1, objref2))
                return false;

            if (!history.SetObjRef(2, objref3))
                return false;

            return true;
        }

    }
}