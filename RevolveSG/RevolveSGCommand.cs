using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RevolveSG
{
    public class RevolveSG : Command
    {
        public RevolveSG()
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
        private const int HISTORY_VERSION = 20131107;
        ///<summary>The only instance of this command.</summary>
        public static RevolveSG Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "RevolveSG"; }
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
            OptionToggle boolOption = new OptionToggle(RevolveSGSettings.use_last_axis, "Off", "On");
            if ((RevolveSGSettings.g != new Guid()) && (doc.Objects.FindId(RevolveSGSettings.g) !=null))
                go.AddOptionToggle("UseLastAxis", ref boolOption);
            else
                RevolveSGSettings.StoreIndex(-1);

            while (true)
            {
                GetResult result = go.GetMultiple(1,0);
                if (go.CommandResult() != Rhino.Commands.Result.Success)
                    return go.CommandResult();
                else if (result == GetResult.Option)
                {
                    continue;
                }
                else if (go.CommandResult() == Result.Success)
                    break;
            }
            
            RevolveSGSettings.UseLastAxis(boolOption.CurrentValue);
            Rhino.DocObjects.ObjRef[] objrefs = go.Objects();
                        
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
                RevSurface rev = RevSurface.Create(crv, revline);
                Brep brep = Brep.CreateFromRevSurface(rev, false, false);
                if (null != brep)
                {
                    // Create a history record
                    Rhino.DocObjects.HistoryRecord history = new Rhino.DocObjects.HistoryRecord(this, HISTORY_VERSION);
                    WriteHistory(history, objref, objref2);
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

        if (!ReadHistory(replay, ref objref, ref objref2))
            return false;

        Curve curve = objref.Curve();
        if (null == curve)
            return false;
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

        Line revline = new Line(linecrv.PointAtStart, linecrv.PointAtEnd);
        RevSurface rev = RevSurface.Create(curve, revline);
        if (rev == null) return false;

        Brep brep= Brep.CreateFromRevSurface(rev, false, false);
        if (brep == null) return false;

        if (brep.Faces.SplitKinkyFaces() == false) return false;

        replay.Results[0].UpdateToBrep(brep, null);
        return true;

        }

    /// <summary>
    /// Read a history record and extract the references for the antecedent objects.
    /// </summary>
    private bool ReadHistory(Rhino.DocObjects.ReplayHistoryData replay, ref Rhino.DocObjects.ObjRef objref, ref Rhino.DocObjects.ObjRef objref2)
    {
        if (HISTORY_VERSION != replay.HistoryVersion)
            return false;
      
        objref = replay.GetRhinoObjRef(0);
        if (null == objref)
            return false;

        objref2 = replay.GetRhinoObjRef(1);
        if (null == objref2)
            return false;

        return true;
    }

    /// <summary>
    /// Write a history record referencing the antecedent objects
    /// The history should contain all the information required to reconstruct the input.
    /// This may include parameters, options, or settings.
    /// </summary>
    private bool WriteHistory(Rhino.DocObjects.HistoryRecord history, Rhino.DocObjects.ObjRef objref, Rhino.DocObjects.ObjRef objref2)
    {
            
        if (!history.SetObjRef(0, objref))
            return false;

        if (!history.SetObjRef(1, objref2))
            return false;

        return true;
    }
    
}
}
