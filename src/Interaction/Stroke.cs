﻿using OpenTK;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Valve.VR;

namespace SparrowHawk.Interaction
{
    class Stroke : Interaction
    {
        protected enum State
        {
            READY = 0, PAINT = 1
        };

        protected State currentState;
        protected Geometry.Geometry stroke_g;
        protected Material.Material stroke_m;
        protected uint primaryDeviceIndex;
        protected Guid strokeId;
        protected List<Vector3> reducePoints = new List<Vector3>();

        public Stroke()
        {

        }

        public Stroke(ref Scene s)
        {
            mScene = s;
            stroke_g = new Geometry.GeometryStroke();
            stroke_m = new Material.LineMaterial(1, 0, 0, 1);
            currentState = State.READY;
        }

        public virtual void draw(bool inFront, int trackedDeviceIndex)
        {

            if (currentState != State.PAINT)
            {
                return;
            }

            Vector3 pos = Util.getTranslationVector3(mScene.mDevicePose[trackedDeviceIndex]);
            ((Geometry.GeometryStroke)stroke_g).addPoint(pos);

            if (((Geometry.GeometryStroke)stroke_g).mNumPrimitives == 1)
            {
                SceneNode stroke = new SceneNode("Stroke", ref stroke_g, ref stroke_m);
                mScene.staticGeometry.add(ref stroke);
                strokeId = stroke.guid;
            }

        }

        protected override void onClickOculusGrip(ref VREvent_t vrEvent)
        {
            Rhino.RhinoApp.WriteLine("oculus grip click event test");
            primaryDeviceIndex = vrEvent.trackedDeviceIndex;
            if (currentState == State.READY)
            {
                stroke_g = new Geometry.GeometryStroke();
                currentState = State.PAINT;
            }

        }

        protected override void onReleaseOculusGrip(ref VREvent_t vrEvent)
        {
            Rhino.RhinoApp.WriteLine("oculus grip release event test");
            if (currentState == State.PAINT)
            {
                currentState = State.READY;
            }
        }

        public void simplifyCurve(ref List<Vector3> curvePoints)
        {
            float pointReductionTubeWidth = 0.004f;
            reducePoints = DouglasPeucker(ref curvePoints, 0, curvePoints.Count - 1, pointReductionTubeWidth);
            Rhino.RhinoApp.WriteLine("reduce points from" + curvePoints.Count + " to " + curvePoints.Count);
        }

        //Quick test about Douglas-Peucker for rhino points, return point3d with rhino coordinate system
        public List<Vector3> DouglasPeucker(ref List<Vector3> points, int startIndex, int lastIndex, float epsilon)
        {
            float dmax = 0f;
            int index = startIndex;

            for (int i = index + 1; i < lastIndex; ++i)
            {
                float d = PointLineDistance(points[i], points[startIndex], points[lastIndex]);
                if (d > dmax)
                {
                    index = i;
                    dmax = d;
                }
            }

            if (dmax > epsilon)
            {
                List<Vector3> res1 = DouglasPeucker(ref points, startIndex, index, epsilon);
                List<Vector3> res2 = DouglasPeucker(ref points, index, lastIndex, epsilon);

                //watch out the coordinate system
                List<Vector3> finalRes = new List<Vector3>();
                for (int i = 0; i < res1.Count - 1; ++i)
                {
                    finalRes.Add(res1[i]);
                }

                for (int i = 0; i < res2.Count; ++i)
                {
                    finalRes.Add(res2[i]);
                }

                return finalRes;
            }
            else
            {
                return new List<Vector3>(new Vector3[] { points[startIndex], points[lastIndex] });
            }
        }

        public float PointLineDistance(Vector3 point, Vector3 start, Vector3 end)
        {

            if (start == end)
            {
                return (float)Math.Sqrt(Math.Pow(point.X - start.X, 2) + Math.Pow(point.Y - start.Y, 2) + Math.Pow(point.Z - start.Z, 2));
            }

            Vector3 u = new Vector3(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            Vector3 pq = new Vector3(point.X - start.X, point.Y - start.Y, point.Z - start.Z);

            return Vector3.Cross(pq, u).Length / u.Length;


        }

    }
}