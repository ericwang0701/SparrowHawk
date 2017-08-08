﻿using OpenTK;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Valve.VR;

namespace SparrowHawk.Interaction
{
    class Sweep2 : Stroke
    {
        public Geometry.Geometry meshStroke_g;
        Material.Material mesh_m;
        //Rhino.Geometry.NurbsCurve closedCurve;
        Rhino.Geometry.Curve closedCurve;
        List<Point3d> curvePoints = new List<Point3d>();
        Brep startPlane, endPlane;
        Guid sGuid, eGuid;

        public Sweep2(ref Scene scene) : base(ref scene)
        {
            stroke_g = new Geometry.GeometryStroke(ref mScene);
            stroke_m = new Material.SingleColorMaterial(1, 0, 0, 1);
            mesh_m = new Material.RGBNormalMaterial(.5f);
            currentState = State.READY;

        }

        public Sweep2(ref Scene scene, bool drawOnP) : base(ref scene)
        { 
            stroke_g = new Geometry.GeometryStroke(ref mScene);
            stroke_m = new Material.SingleColorMaterial(1, 0, 0, 1);
            mesh_m = new Material.RGBNormalMaterial(.5f);
            currentState = State.READY;

            onPlane = drawOnP;

            if (onPlane)
            {
                //clear previous drawpoint
                if (mScene.tableGeometry.children.Count > 0)
                {
                    foreach (SceneNode sn in mScene.tableGeometry.children)
                    {
                        if (sn.name == "drawPoint")
                        {
                            mScene.tableGeometry.children.Remove(sn);
                            break;
                        }
                    }
                }

                Geometry.Geometry geo = new Geometry.PointMarker(new OpenTK.Vector3(0, 0, 0));
                //Material.Material m = new Material.SingleColorMaterial(250 / 255, 128 / 255, 128 / 255, 0.5f);
                Material.Material m = new Material.SingleColorMaterial(1, 1, 1, 1);
                drawPoint = new SceneNode("drawPoint", ref geo, ref m);
                drawPoint.transform = new OpenTK.Matrix4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
                mScene.tableGeometry.add(ref drawPoint);

                //TODO-support both controllers
                if (mScene.mIsLefty)
                    primaryDeviceIndex = (uint)mScene.leftControllerIdx;
                else
                    primaryDeviceIndex = (uint)mScene.rightControllerIdx;

            }

        }

        public Sweep2(ref Scene scene, ref Rhino.Geometry.Brep brep) : base(ref scene)
        {
            stroke_g = new Geometry.GeometryStroke(ref mScene);
            stroke_m = new Material.SingleColorMaterial(1, 0, 0, 1);
            mesh_m = new Material.SingleColorMaterial(0, 1, 0, 1);
            closedCurve = brep.Curves3D.ElementAt(0);
            currentState = State.READY;
        }

        public override void draw(bool isTop)
        {
            base.draw(isTop);
        }


        public void renderSweep()
        {

            //reduce the points in the curve first
            if (((Geometry.GeometryStroke)(stroke_g)).mPoints.Count >= 2)
            {

                simplifyCurve(ref ((Geometry.GeometryStroke)(stroke_g)).mPoints);

                foreach (OpenTK.Vector3 point in reducePoints)
                {
                    // -y_rhino = z_gl, z_rhino = y_gl
                    //OpenTK.Vector3 p = Util.transformPoint(Util.mGLToRhino, point);
                    ///curvePoints.Add(new Point3d(p.X, p.Y, p.Z));
                    curvePoints.Add(Util.openTkToRhinoPoint(Util.vrToPlatformPoint(ref mScene, point)));
                }

                //Rhino curve and extrude test
                if (curvePoints.Count >= 2)
                {

                    //clear the stroke
                    foreach (SceneNode sn in mScene.tableGeometry.children)
                    {
                        if (sn.guid == strokeId)
                        {
                            mScene.tableGeometry.children.Remove(sn);
                            break;
                        }
                    }

                    Rhino.Geometry.Curve rail = Rhino.Geometry.Curve.CreateInterpolatedCurve(curvePoints.ToArray(), 3);
                    //check targetPRhObj to prevent the user draw outside of the plane and crash
                    if (onPlane && rail != null && targetPRhObj != null)
                    {
                        List<Curve> curveL = new List<Curve>();
                        curveL.Add(rail);
                        mScene.popInteraction();
                        mScene.pushInteraction(new EditPoint(ref mScene, ref targetPRhObj, true, curveL, Guid.Empty, "Sweep-rail"));
                    }


                }
            }
        }

        protected override void onClickOculusTrigger(ref VREvent_t vrEvent)
        {
            curvePoints = new List<Point3d>();
            base.onClickOculusTrigger(ref vrEvent);

        }

        protected override void onReleaseOculusTrigger(ref VREvent_t vrEvent)
        {
            Rhino.RhinoApp.WriteLine("oculus grip release event test");
            if (currentState == State.PAINT)
            {

                //clear the stroke
                /*
                foreach (SceneNode sn in mScene.tableGeometry.children)
                {
                    if (sn.guid == strokeId)
                    {
                        mScene.tableGeometry.children.Remove(sn);
                        break;
                    }
                }*/

                renderSweep();
                currentState = State.READY;

            }
        }

    }
}