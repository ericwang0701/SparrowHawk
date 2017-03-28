﻿using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Valve.VR;

namespace SparrowHawk.Interaction
{
    public class EditPlane : Selection
    {

        OpenTK.Matrix4 mInitialVRtocontroller;
        OpenTK.Matrix4 currentTransform;
        OpenTK.Vector3 planeOrigin = new OpenTK.Vector3(0, 0, 0);
        OpenTK.Vector3 planeNormalV = new OpenTK.Vector3(0, 1, 0);
        OpenTK.Vector3 VRNormalV = new OpenTK.Vector3(0, 1, 0);
        OpenTK.Matrix4 transToPlane = new OpenTK.Matrix4();
        OpenTK.Matrix4 rotM = new OpenTK.Matrix4();

        private DesignPlane planeXY, planeXZ, planeYZ, selectedPlane;
        private string selectedPlaneName;
        private List<RhinoObject> planeList = new List<RhinoObject>();
        OpenTK.Matrix4 M_L;
        OpenTK.Matrix4 mAlignO;

        public EditPlane()
        {

        }

        public EditPlane(ref Scene s, ref DesignPlane xy, ref DesignPlane xz, ref DesignPlane yz)
        {
            mScene = s;
            currentState = State.READY;
            planeXY = xy;
            planeXZ = xz;
            planeYZ = yz;

        }

        public override void draw(bool isTop)
        {
            if (currentState != State.SELECTION || !isTop)
            {
                return;
            }

            //selectedSN.transform = Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx) * mVRtocontroller * currentTransform;

            // M = contoller_start.inverted() * controller_end() [normalize?]
            // planeTransform = planeToVR * M
            OpenTK.Matrix4 M = mInitialVRtocontroller * Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx);


            // M_ControllerPose_end = M_Transform * M_ControllerPose_start
            // M_ControllerPose_end = M_L - VR * M_Transform^ * M_VR - L * M_ControllerPose_start
            // M_Transform * M_ControllerPose_start = M_L - VR * M_Transform^ * M_VR - L * M_ControllerPose_start
            // M_Transform^ =    M_L - VR.inverted * M_Transform * M_ControllerPose_start * M_ControllerPose_start.inverted * M_VR - L.inverted
            // M_Transform^ = M_L - VR.inverted * M_ControllerPose_end * M_ControllerPose_start.inverted * M_VR - L.inverted


            //M_L = selectedPlane.VRtoPlane * Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx) * mInitialVRtocontroller * selectedPlane.VRtoPlane.Inverted();
            //selectedSN.transform = Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx) * mInitialVRtocontroller * selectedPlane.VRtoPlane.Inverted(); //selectedPlane.VRtoPlane.Inverted() * M_L;


            //get translation vector
            OpenTK.Vector3 translateV = new OpenTK.Vector3(0, 0, M_L.M34);
            OpenTK.Matrix4 translationM = OpenTK.Matrix4.CreateTranslation(translateV);
            translationM.Transpose();

            //testing translatioion
            OpenTK.Vector3 translateV2 = new OpenTK.Vector3(0, 0, 0.01f);
            OpenTK.Matrix4 translationM2 = OpenTK.Matrix4.CreateTranslation(translateV);
            translationM2.Transpose();

            //get dominate rotation vetor
            OpenTK.Quaternion q = OpenTK.Quaternion.FromMatrix(new OpenTK.Matrix3(M_L.M11, M_L.M12, M_L.M13,
                                                                                  M_L.M21, M_L.M22, M_L.M23,
                                                                                  M_L.M31, M_L.M32, M_L.M33));
            OpenTK.Vector4 rotationAngle = q.ToAxisAngle();
            float[] angleArray = new float[3];
            float xangle, yangle, zangle;
            OpenTK.Vector3 xaxis = new OpenTK.Vector3(1, 0, 0);
            OpenTK.Vector3 yaxis = new OpenTK.Vector3(0, 1, 0);
            OpenTK.Vector3 zaxis = new OpenTK.Vector3(0, 0, 1);
            q.ToAxisAngle(out xaxis, out xangle);
            angleArray[0] = xangle;
            q.ToAxisAngle(out yaxis, out yangle);
            angleArray[1] = yangle;
            q.ToAxisAngle(out zaxis, out zangle);
            angleArray[2] = zangle;


            OpenTK.Matrix4 rotM;
            if (selectedPlaneName == "XY")
            {
                if (rotationAngle[0] >= rotationAngle[1])
                {
                    rotM = OpenTK.Matrix4.CreateRotationX((float)(2 * Math.Acos(q.W)));
                }
                else
                {
                    rotM = OpenTK.Matrix4.CreateRotationY((float)(2 * Math.Acos(q.W)));
                }

            }
            else if (selectedPlaneName == "XZ")
            {
                if (rotationAngle[0] >= rotationAngle[2])
                {
                    rotM = OpenTK.Matrix4.CreateRotationX((float)(2 * Math.Acos(q.W)));
                }
                else
                {
                    rotM = OpenTK.Matrix4.CreateRotationZ((float)(2 * Math.Acos(q.W)));
                }
            }
            else
            {
                if (rotationAngle[1] >= rotationAngle[2])
                {
                    rotM = OpenTK.Matrix4.CreateRotationY((float)(2 * Math.Acos(q.W)));
                }
                else
                {
                    rotM = OpenTK.Matrix4.CreateRotationZ((float)(2 * Math.Acos(q.W)));
                }
            }

            rotM.Transpose();

            //rotation testing
            //rotM = OpenTK.Matrix4.CreateRotationX((float)(30.0f/180.0f * Math.PI));

            //M_L = translationM;
            //selectedSN.transform = selectedPlane.VRtoPlane.Inverted() * M_L * selectedPlane.VRtoPlane * currentTransform;
            //selectedSN.transform = selectedPlane.VRtoPlane.Inverted() * M_L;
            //selectedSN.transform = selectedPlane.VRtoPlane.Inverted() * translationM2 * selectedPlane.VRtoPlane * currentTransform;
            //selectedSN.transform = selectedPlane.planeToVR * rotM * selectedPlane.planeToVR.Inverted() * currentTransform;


            /*
            //Todo: add contraints
            OpenTK.Matrix4 transM = Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx);
            //ToDo-compute new transM in plane coordinate system
            //transM = transM * rotM * transToPlane;

            //get translation vector
            OpenTK.Vector3 translateV = new OpenTK.Vector3(0, transM.M24, 0);
            OpenTK.Matrix4 translationM = OpenTK.Matrix4.CreateTranslation(translateV);
            translationM.Transpose();
            //get rotation
            float thetaX = (float)Math.Atan2(transM.M32, transM.M33);
            float thetaY = (float)Math.Atan2(-transM.M31, Math.Sqrt(Math.Pow(transM.M32, 2) + Math.Pow(transM.M33, 2)));
            float thetaZ = (float)Math.Atan2(transM.M21, transM.M11);

            OpenTK.Matrix4 localRotM = OpenTK.Matrix4.CreateFromAxisAngle(new OpenTK.Vector3(0, 0, 1), thetaZ);
            localRotM.Transpose();

            //Check the formula - create a plane class with origin and axis data, keep traking the data.     M_ControllerPose * M_VR-Controller = M_L-VR * M_L
            //Calculate the M_L-VR by checking the origin position of the xy-plane (from platformToVR)
            //M_L * M_L-VR is the new M_L-VR that we need.
            //calculate the domonant axis and translation in M_L-VR  
            selectedSN.transform = translationM;
            */

        }

        //only detect the pre-defined plane breps
        protected override void onClickOculusGrip(ref VREvent_t vrEvent)
        {
            Rhino.RhinoApp.WriteLine("Selcet event");
            primaryDeviceIndex = vrEvent.trackedDeviceIndex;
            if (currentState == State.READY)
            {

                //since guid will change after each transformation
                planeList.Add(mScene.rhinoDoc.Objects.Find(planeXY.guid));
                planeList.Add(mScene.rhinoDoc.Objects.Find(planeXZ.guid));
                planeList.Add(mScene.rhinoDoc.Objects.Find(planeYZ.guid));

                OpenTK.Vector4 controller_p = Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx) * new OpenTK.Vector4(0, 0, 0, 1);
                OpenTK.Vector4 controller_pZ = Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx) * new OpenTK.Vector4(0, 0, -1, 1);
                Point3d controller_pRhino = Util.openTkToRhinoPoint(Util.vrToPlatformPoint(ref mScene, new OpenTK.Vector3(controller_p.X, controller_p.Y, controller_p.Z)));
                Point3d controller_pZRhin = Util.openTkToRhinoPoint(Util.vrToPlatformPoint(ref mScene, new OpenTK.Vector3(controller_pZ.X, controller_pZ.Y, controller_pZ.Z)));

                Vector3d direction = new Vector3d(controller_pZRhin.X - controller_pRhino.X, controller_pZRhin.Y - controller_pRhino.Y, controller_pZRhin.Z - controller_pRhino.Z);
                Ray3d ray = new Ray3d(controller_pRhino, direction);

                for (int i = 0; i < planeList.Count; i++)
                {
                    //grip selection
                    RhinoObject rhObj = planeList.ElementAt(i);
                    if (rhObj.Geometry.GetBoundingBox(false).Contains(new Point3d(controller_p.X, -controller_p.Z, controller_p.Y)))
                    {
                        selectedSN = mScene.brepToSceneNodeDic[rhObj.Id];
                        selectedRhObj = rhObj;
                        currentState = State.SELECTION;

                        if (i == 0)
                        {
                            selectedPlane = planeXY;
                            selectedPlaneName = "XY";
                        }
                        else if (i == 1)
                        {
                            selectedPlane = planeXZ;
                            selectedPlaneName = "XZ";
                        }
                        else if (i == 2)
                        {
                            selectedPlane = planeYZ;
                            selectedPlaneName = "YZ";
                        }

                        break;
                    }
                    else //ray casting selection
                    {
                        List<GeometryBase> geometries = new List<GeometryBase>();
                        geometries.Add(rhObj.Geometry);
                        //must be a brep or surface, not mesh
                        Point3d[] rayIntersections = Rhino.Geometry.Intersect.Intersection.RayShoot(ray, geometries, 1);
                        if (rayIntersections != null)
                        {
                            selectedSN = mScene.brepToSceneNodeDic[rhObj.Id];
                            selectedRhObj = rhObj;
                            currentState = State.SELECTION;

                            if (i == 0)
                                selectedPlane = planeXY;
                            else if (i == 1)
                                selectedPlane = planeXZ;
                            else if (i == 2)
                                selectedPlane = planeYZ;

                            break;
                        }
                    }

                }

            }

            if (currentState == State.SELECTION)
            {
                currentTransform = selectedSN.transform;
                mInitialVRtocontroller = Util.getControllerTipPosition(ref mScene, primaryDeviceIndex == mScene.leftControllerIdx).Inverted();
            }

        }

        protected override void onReleaseOculusGrip(ref VREvent_t vrEvent)
        {
            Rhino.RhinoApp.WriteLine("Oculus grip release event");
            if (currentState == State.SELECTION)
            {

                //Watchout!! since the bug of transformation, we need to remove currentTransform first here
                //OpenTK.Matrix4 transMRhino = Util.mGLToRhino * (selectedSN.transform * currentTransform.Inverted()) * Util.mRhinoToGL;

                
                OpenTK.Matrix4 transMRhino = Util.platformToVR(ref mScene).Inverted() * (selectedSN.transform * currentTransform.Inverted()) * Util.platformToVR(ref mScene);
                Transform transM = new Transform();
                for (int row = 0; row < 4; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        transM[row, col] = transMRhino[row, col];
                    }
                }

                // BUG!!! apply the transform to the original one didn't work WHY??? need to delete first
                //((Brep)(gripRhObj.Geometry)).Transform(Transform.Translation(0,0,1));
                //Keep scene node reference before we delete the item
                SceneNode sn = mScene.brepToSceneNodeDic[selectedRhObj.Id];
                mScene.brepToSceneNodeDic.Remove(selectedRhObj.Id);

                Guid newGuid = mScene.rhinoDoc.Objects.Transform(selectedRhObj.Id, transM, true);
                mScene.rhinoDoc.Views.Redraw();

                //add reference SceneNode to brep and vice versa
                mScene.brepToSceneNodeDic.Add(newGuid, sn);
                mScene.SceneNodeToBrepDic[sn.guid] = mScene.rhinoDoc.Objects.Find(newGuid);

                //update the guid on selectPlane
                selectedPlane.guid = newGuid;
                selectedPlane.updateCoordinate(M_L);
                planeList.Clear();

                currentState = State.READY;
                //gripSceneNode.transform = new OpenTK.Matrix4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
            }
        }


    }
}