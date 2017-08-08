﻿using System;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using Valve.VR;
using Rhino.Geometry;
using Emgu.CV.Structure;
using Emgu.CV;

namespace SparrowHawk
{
    public class VrGame : OpenTK.GameWindow
    {
        CVRSystem mHMD;
        CVRRenderModels mRenderModels;
        Scene mScene;
        VrRenderer mRenderer;
        Rhino.RhinoDoc mDoc;
        String mStrDriver = "No Driver";
        String mStrDisplay = "No Display";
        String mTitleBase;
        uint mRenderWidth = 1280;
        uint mRenderHeight = 720;
        bool mIsLefty;

        TrackedDevicePose_t[] renderPoseArray, gamePoseArray;
        DateTime mLastFrameTime;
        bool mSafeForRobot = false;
        DateTime mLastTrackingTime;
        int mFrameCount;
        double frameRateTimer = 1;

        // Callibration
        List<Vector3> robotCallibrationPoints = new List<Vector3>();
        List<Vector3> robotCallibrationPointsTest = new List<Vector3>();
        List<Vector3> vrCallibrationPoints = new List<Vector3>();
        //Testing controller origins
        List<Vector3> controllerP = new List<Vector3>();
        //using opencv by Eric
        List<MCvPoint3D32f> robotCallibrationPoints_cv = new List<MCvPoint3D32f>();
        List<MCvPoint3D32f> vrCallibrationPoints_cv = new List<MCvPoint3D32f>();
        int lastNumRightControllerPoses;
        int lastNumLeftControllerPoses;
        List<OpenTK.Matrix4> mRightControllerPoses = new List<OpenTK.Matrix4>();
        List<OpenTK.Matrix4> mLeftControllerPoses = new List<OpenTK.Matrix4>();
        Matrix<double> mVRtoRobot;
        Matrix4 glmVRtoMarker;
        byte[] inliers;

        bool manualCallibration = false;

        DesignPlane xzPlane, xyPlane, yzPlane;
        DesignPlane2 xzPlane2, xyPlane2, yzPlane2;

        Geometry.Geometry printStroke;
        Material.Material printStroke_m;

        public VrGame(ref Rhino.RhinoDoc doc, bool isLefty = false)
        {
            mDoc = doc;
            Rhino.RhinoApp.WriteLine("The robot offset is: " + OfflineCalibration.solveForRobotOffsetVector(OfflineCalibration.getHuaishuRobotMeasurements()));
            Rhino.RhinoApp.WriteLine("Working Directory: " + System.IO.Directory.GetCurrentDirectory());
            mIsLefty = isLefty;

            if (init())
                Rhino.RhinoApp.WriteLine("Initialization complete!");

            mScene.pushInteraction(new Interaction.PickPoint(ref mScene, ref vrCallibrationPoints));
        }

        /**
         * Updates the poses of all tracked devices in the Matrix4 format. 
         * Also handles new tracked devices, setting them up in the scene
         * and loading their render models.
         */
        void updateMatrixPose()
        {
            if (mHMD == null)
                return;

            OpenVR.Compositor.WaitGetPoses(mScene.mTrackedDevices, gamePoseArray);

            for (uint i = 0; i < mScene.mTrackedDevices.Length; i++)
            {
                var device = gamePoseArray[i];
                if (device.bPoseIsValid)
                {
                    mScene.mDevicePose[i] = Util.steamVRMatrixToMatrix4(mScene.mTrackedDevices[i].mDeviceToAbsoluteTracking);
                    mHMD.GetTrackedDeviceClass(i);
                    if (mScene.mDeviceClassChar[i] == 0)
                    {
                        switch (mHMD.GetTrackedDeviceClass(i))
                        {
                            case ETrackedDeviceClass.Controller:
                                mScene.mDeviceClassChar[i] = 'C';
                                string name = Util.GetTrackedDeviceString(ref mHMD, i, ETrackedDeviceProperty.Prop_RenderModelName_String);
                                if (name.ToLower().Contains("left"))
                                {
                                    mScene.leftControllerIdx = (int)i;
                                    // Uncomment to show controller model.
                                    //Geometry.Geometry g = new Geometry.Geometry(@"C:/workspace/SparrowHawk/src/resources/external_controller01_left.obj");
                                    //Material.Material m = new Material.RGBNormalMaterial(.5f);
                                    //SceneNode s = new SceneNode("LeftControllerModel", ref g, ref m);
                                    //s.transform = Util.createTranslationMatrix(-mScene.mLeftControllerOffset.M14, -mScene.mLeftControllerOffset.M24, -mScene.mLeftControllerOffset.M34);
                                    //mScene.leftControllerNode.add(ref s);
                                }
                                else if (name.ToLower().Contains("right"))
                                    mScene.rightControllerIdx = (int)i;
                                else if (mScene.leftControllerIdx < 0)
                                    mScene.leftControllerIdx = (int)i;
                                else if (mScene.rightControllerIdx < 0)
                                    mScene.rightControllerIdx = (int)i;
                                break;
                            case ETrackedDeviceClass.HMD: mScene.mDeviceClassChar[i] = 'H'; break;
                            case ETrackedDeviceClass.Invalid: mScene.mDeviceClassChar[i] = 'I'; break;
                            case ETrackedDeviceClass.GenericTracker: mScene.mDeviceClassChar[i] = 'G'; break;
                            case ETrackedDeviceClass.TrackingReference: mScene.mDeviceClassChar[i] = 'T'; break;
                            default: mScene.mDeviceClassChar[i] = '?'; break;
                        }
                    }
                }

            }

            if (gamePoseArray[OpenVR.k_unTrackedDeviceIndex_Hmd].bPoseIsValid)
            {
                mScene.mHMDPose = Util.steamVRMatrixToMatrix4(gamePoseArray[OpenVR.k_unTrackedDeviceIndex_Hmd].mDeviceToAbsoluteTracking).Inverted();
            }

            if (mScene.leftControllerIdx > 0)
                mScene.leftControllerNode.transform = mScene.mDevicePose[mScene.leftControllerIdx] * mScene.mLeftControllerOffset;
            if (mScene.rightControllerIdx > 0)
                mScene.rightControllerNode.transform = mScene.mDevicePose[mScene.rightControllerIdx] * mScene.mRightControllerOffset;
        }

        protected void updateControllerCallibration()
        {
            if (mHMD == null)
                return;
            if (mLeftControllerPoses.Count >= 4 && mLeftControllerPoses.Count > lastNumLeftControllerPoses)
            {
                lastNumLeftControllerPoses = mLeftControllerPoses.Count;
                Vector3 x = Util.solveForOffsetVector(mLeftControllerPoses);
                Rhino.RhinoApp.WriteLine("Left controller offset: " + x);
            }
            if (mRightControllerPoses.Count >= 4 && mRightControllerPoses.Count > lastNumRightControllerPoses)
            {
                lastNumRightControllerPoses = mRightControllerPoses.Count;
                Vector3 x = Util.solveForOffsetVector(mRightControllerPoses);
                Rhino.RhinoApp.WriteLine("Right controller offset: " + x);
            }
        }

        protected void handleInteractions()
        {
            if (mHMD == null)
                return;
            //default interaction
            if (mScene.interactionStackEmpty())
                mScene.pushInteraction(new Interaction.PickPoint(ref mScene));
         

            Interaction.Interaction current_i = mScene.peekInteraction();
            current_i.handleInput();
            current_i.draw(true);

            if (manualCallibration && vrCallibrationPoints.Count == 8)
            {
                manualCallibration = false; // HACK to not reset my cylinder forever.
                //Util.solveForAffineTransform(vrCallibrationPoints, robotCallibrationPoints, ref mScene.vrToRobot);

                //using opencv by Eric                
                foreach (Vector3 p in vrCallibrationPoints)
                {
                    vrCallibrationPoints_cv.Add(new MCvPoint3D32f(p.X, p.Y, p.Z));
                }
                CvInvoke.EstimateAffine3D(vrCallibrationPoints_cv.ToArray(), robotCallibrationPoints_cv.ToArray(), out mVRtoRobot, out inliers, 3, 0.99);

                mScene.vrToRobot = new Matrix4(
                    (float)mVRtoRobot[0, 0], (float)mVRtoRobot[0, 1], (float)mVRtoRobot[0, 2], (float)mVRtoRobot[0, 3],
                    (float)mVRtoRobot[1, 0], (float)mVRtoRobot[1, 1], (float)mVRtoRobot[1, 2], (float)mVRtoRobot[1, 3],
                    (float)mVRtoRobot[2, 0], (float)mVRtoRobot[2, 1], (float)mVRtoRobot[2, 2], (float)mVRtoRobot[2, 3],
                    0, 0, 0, 1
                );

                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.CreateCylinder(ref mScene));
            }

            //testing controller origins
            if (controllerP.Count == 2)
            {
                Vector3 p1 = controllerP[0];
                Vector3 p2 = controllerP[1];
                Rhino.RhinoApp.WriteLine("P1: " + p1.ToString());
                Rhino.RhinoApp.WriteLine("P2: " + p2.ToString());
                Rhino.RhinoApp.WriteLine("Distance: " + ((p1 - p2).Length).ToString());
                Rhino.RhinoApp.WriteLine("Vector: " + (p1 - p2).ToString());
                controllerP.Clear();
            }
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            if (mFrameCount >= 30)
            {
                this.Title = mTitleBase + " - " + mFrameCount / ((TimeSpan)(System.DateTime.Now - mLastTrackingTime)).TotalSeconds + "fps.";
                mLastTrackingTime = DateTime.Now;
                mFrameCount = 0;
            }
            mFrameCount += 1;
            mScene.gameTime += e.Time;
            mLastFrameTime = DateTime.Now;

        }

        protected void handleSignals()
        {
            SparrowHawkSignal s = SparrowHawkEventListeners.Instance.getOneSignal();
            if (s == null)
                return;
            switch (s.type)
            {
                case SparrowHawkSignal.ESparrowHawkSigalType.InitType:
                    if (s.data.Length >= 3)
                    {
                        //Vector3 robotPoint = new Vector3(s.data[0] - 8, s.data[1], s.data[2] - 240);
                        Vector3 robotPoint = new Vector3(s.data[0], s.data[1], s.data[2]);
                        //robotPoint /= 1000;
                        robotCallibrationPoints.Add(robotPoint);
                        Rhino.RhinoApp.WriteLine("add robotPoint: " + robotPoint.ToString());
                        if (mIsLefty && mScene.leftControllerIdx < 0 || !mIsLefty && mScene.leftControllerIdx < 0)
                            break;
                        //adding vrCallibrationPoints by pressing v
                        //calibrationTest();
                    }
                    break;
                case SparrowHawkSignal.ESparrowHawkSigalType.LineType:
                    if(s.data.Length >= 4)
                    {

                        if (s.data[0] == 0)
                        {
                            printStroke = new Geometry.GeometryStroke(ref mScene);
                            OpenTK.Vector3 p1 = new Vector3(s.data[1], s.data[2], s.data[3]);
                            p1 = Util.platformToVRPoint(ref mScene, p1);
                            OpenTK.Vector3 p2 = new Vector3(s.data[4], s.data[5], s.data[6]);
                            p2 = Util.platformToVRPoint(ref mScene, p2);

                            ((Geometry.GeometryStroke)printStroke).addPoint(p1);
                            ((Geometry.GeometryStroke)printStroke).addPoint(p2);
                            SceneNode stroke = new SceneNode("PrintStroke", ref printStroke, ref printStroke_m);
                            mScene.tableGeometry.add(ref stroke);

                        }
                        else
                        {
                            OpenTK.Vector3 p1 = new Vector3(s.data[1], s.data[2], s.data[3]);
                            p1 = Util.platformToVRPoint(ref mScene, p1);
                            ((Geometry.GeometryStroke)printStroke).addPoint(p1);
                        }
                    }
                    break;
                case SparrowHawkSignal.ESparrowHawkSigalType.EncoderType:

                    //for rhino object
                    OpenTK.Matrix4 currentRotation = mScene.platformRotation;

                    float theta = (float)(s.data[0] / 360f * 2 * Math.PI);
                    Rhino.RhinoApp.WriteLine("Theta = " + theta);
                    Matrix4.CreateRotationZ(-theta, out mScene.platformRotation);
                    mScene.platformRotation.Transpose();                

                    //rotate Rhino objects
                    OpenTK.Matrix4 rotMRhino = mScene.platformRotation * currentRotation.Inverted();
                    Transform transM = new Transform();
                    for (int row = 0; row < 4; row++)
                    {
                        for (int col = 0; col < 4; col++)
                        {
                            transM[row, col] = rotMRhino[row, col];
                        }
                    }

                    Rhino.DocObjects.ObjectEnumeratorSettings settings = new Rhino.DocObjects.ObjectEnumeratorSettings();
                    settings.ObjectTypeFilter = Rhino.DocObjects.ObjectType.Brep;
                    foreach (Rhino.DocObjects.RhinoObject rhObj in mScene.rhinoDoc.Objects.GetObjectList(settings))
                    {
                        if (mScene.brepToSceneNodeDic.ContainsKey(rhObj.Id) && rhObj.Attributes.Name != "planeXY" && rhObj.Attributes.Name != "planeXZ"
                                                                            && rhObj.Attributes.Name != "planeYZ")
                        {
                            SceneNode sn = mScene.brepToSceneNodeDic[rhObj.Id];
                            mScene.brepToSceneNodeDic.Remove(rhObj.Id);

                            Guid newGuid = mScene.rhinoDoc.Objects.Transform(rhObj.Id, transM, true);
                            Rhino.RhinoApp.WriteLine("transM " + transM.ToString());
                            mScene.rhinoDoc.Views.Redraw();

                            mScene.brepToSceneNodeDic.Add(newGuid, sn);
                            mScene.SceneNodeToBrepDic[sn.guid] = mScene.rhinoDoc.Objects.Find(newGuid);
                        }

                    }

                    break;
            }
        }

        private void calibrationTest()
        {

            Vector3 vrPoint = Util.getTranslationVector3(Util.getControllerTipPosition(ref mScene, true));
            vrCallibrationPoints.Add(vrPoint);
            Rhino.RhinoApp.WriteLine("add vrCallibrationPoints: " + vrPoint.ToString());
            Util.MarkPoint(ref mScene.staticGeometry, vrPoint, 1, 1, 0);
            if (vrCallibrationPoints.Count == 8)
            {
                Util.solveForAffineTransformOpenCV(vrCallibrationPoints, robotCallibrationPoints, ref mScene.vrToRobot);
                foreach (Vector3 v in robotCallibrationPoints)
                {
                    Vector4 v4 = new Vector4(v.X, v.Y, v.Z, 1);
                    v4 = mScene.vrToRobot.Inverted() * v4;
                    Util.MarkPoint(ref mScene.staticGeometry, new Vector3(v4.X, v4.Y, v4.Z), 0, 1, 0);
                }
                Rhino.RhinoApp.WriteLine(mScene.vrToRobot.ToString());
                robotCallibrationPoints.Clear();
                vrCallibrationPoints.Clear();
            }
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            if (mScene.leftControllerShouldVibrate())
                mHMD.TriggerHapticPulse((uint) mScene.leftControllerIdx, 0, (char) 127);
            if (mScene.rightControllerShouldVibrate())
                mHMD.TriggerHapticPulse((uint)mScene.rightControllerIdx, 0, (char) 127);
            MakeCurrent();
            updateMatrixPose();
            notifyRobotIfSafe();
            handleSignals();
            handleInteractions();
            updateControllerCallibration();
            mRenderer.renderFrame();
            SwapBuffers();
        }

        protected override void Dispose(bool manual)
        {
            base.Dispose(manual);
        }

        protected void setupScene()
        {
            mScene = new Scene(ref mDoc, ref mHMD);
            mScene.mIsLefty = mIsLefty;


            if (mStrDriver.Contains("oculus")) mScene.isOculus = true; else mScene.isOculus = false;

            if (mHMD != null)
                mHMD.GetRecommendedRenderTargetSize(ref mRenderWidth, ref mRenderHeight);

            Geometry.Geometry g = new Geometry.Geometry("C:/workspace/Kestrel/resources/meshes/bunny.obj");

            //Material.Material m = new Material.SingleColorMaterial(mDoc,1f,1f,1f,1f);
            Material.Material m = new Material.RGBNormalMaterial(1);
            //Material.Material m = new Material.SingleColorMaterial(1, 0, 1, 1);
            SceneNode cube = new SceneNode("Triangle", ref g, ref m);
            cube.transform = new Matrix4(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1);
            mScene.tableGeometry.add(ref cube);

            g = new Geometry.PointMarker(new Vector3(0, 1, 0));
            m = new Material.SingleColorMaterial(1, 1, 1, 1);
            SceneNode point = new SceneNode("Point 1", ref g, ref m);
            mScene.staticGeometry.add(ref point);
            point.transform = new Matrix4(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1);

            //visualizing axises
            OpenTK.Vector3 x0 = Util.platformToVRPoint(ref mScene, new OpenTK.Vector3(-240, 0, 0));
            OpenTK.Vector3 x1 = Util.platformToVRPoint(ref mScene, new OpenTK.Vector3(240, 0, 0));
            OpenTK.Vector3 y0 = Util.platformToVRPoint(ref mScene, new OpenTK.Vector3(0, -240, 0));
            OpenTK.Vector3 y1 = Util.platformToVRPoint(ref mScene, new OpenTK.Vector3(0, 240, 0));
            OpenTK.Vector3 z0 = Util.platformToVRPoint(ref mScene, new OpenTK.Vector3(0, 0, -240));
            OpenTK.Vector3 z1 = Util.platformToVRPoint(ref mScene, new OpenTK.Vector3(0, 0, 240));
            g = new Geometry.GeometryStroke(ref mScene);
            ((Geometry.GeometryStroke)g).addPoint(x0);
            ((Geometry.GeometryStroke)g).addPoint(x1);
            SceneNode xAxis = new SceneNode("xAxis", ref g, ref m);
            mScene.staticGeometry.add(ref xAxis);

            g = new Geometry.GeometryStroke(ref mScene);
            ((Geometry.GeometryStroke)g).addPoint(y0);
            ((Geometry.GeometryStroke)g).addPoint(y1);
            SceneNode yAxis = new SceneNode("yAxis", ref g, ref m);
            mScene.staticGeometry.add(ref yAxis);
            
            g = new Geometry.GeometryStroke(ref mScene);
            ((Geometry.GeometryStroke)g).addPoint(z0);
            ((Geometry.GeometryStroke)g).addPoint(z1);
            SceneNode zAxis = new SceneNode("zAxis", ref g, ref m);
            mScene.staticGeometry.add(ref zAxis);
           

            // Left
            //g = new Geometry.Geometry("C:/workspace/Kestrel/resources/meshes/bunny.obj");
            //m = new Material.RGBNormalMaterial(1);
            g = new Geometry.PointMarker(new Vector3(0, 0, 0));
            m = new Material.SingleColorMaterial(1, 0, 0, 1);
            point = new SceneNode("Left Cursor", ref g, ref m);
            mScene.rightControllerNode.add(ref point);
            point.transform = new Matrix4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);//mScene.mLeftControllerOffset;

            g = new Geometry.GeometryStroke(ref mScene);
            ((Geometry.GeometryStroke)g).addPoint(new Vector3(0, 0, 0));
            ((Geometry.GeometryStroke)g).addPoint(new Vector3(0, 0, -1));
            SceneNode rayTrace = new SceneNode("PrintStroke", ref g, ref m);
            if (mIsLefty)
                mScene.leftControllerNode.add(ref rayTrace);
            else
                mScene.rightControllerNode.add(ref rayTrace);
            rayTrace.transform = new Matrix4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);//mScene.mLeftControllerOffset;

            g = new Geometry.PointMarker(new Vector3(0, 0, 0));
            m = new Material.SingleColorMaterial(0, 0, 1, 1);
            point = new SceneNode("Right Cursor", ref g, ref m);
            mScene.rightControllerNode.add(ref point);
            point.transform = new Matrix4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);


            xzPlane = new DesignPlane(ref mScene, 1);
            xyPlane = new DesignPlane(ref mScene, 2);
            yzPlane = new DesignPlane(ref mScene, 0);

            //xzPlane2 = new DesignPlane2(ref mScene, "XZ");
            //xyPlane2 = new DesignPlane2(ref mScene, "XY");
            //yzPlane2 = new DesignPlane2(ref mScene, "YZ");

            //Find the Rhino Object start with 'a' and render it
            //Material.Material mesh_m = new Material.RGBNormalMaterial(1); ;
            //Rhino.DocObjects.ObjectEnumeratorSettings settings = new Rhino.DocObjects.ObjectEnumeratorSettings();
            //settings.ObjectTypeFilter = Rhino.DocObjects.ObjectType.Brep
            //int obj_count = 0;
            //foreach (Rhino.DocObjects.RhinoObject rhObj in mScene.rhinoDoc.Objects.GetObjectList(settings))
            //{
            //    if (rhObj.Attributes.Name.StartsWith("a"))
            //    {
            //        Util.addSceneNode(ref mScene, ((Surface)rhObj.Geometry).ToBrep(), ref mesh_m, rhObj.Attributes.Name);
            //        mScene.rhinoDoc.Views.Redraw();
            //    }
            //    obj_count++;
            //}
            //Rhino.RhinoApp.WriteLine(obj_count + " breps found");
        }

        public bool init()
        {

            // Set up HMD
            EVRInitError eError = EVRInitError.None;
            mHMD = OpenVR.Init(ref eError, EVRApplicationType.VRApplication_Scene);

            if (eError == EVRInitError.None)
            {
                Rhino.RhinoApp.WriteLine("Booted VR System");
                renderPoseArray = new TrackedDevicePose_t[Valve.VR.OpenVR.k_unMaxTrackedDeviceCount];
                gamePoseArray = new TrackedDevicePose_t[Valve.VR.OpenVR.k_unMaxTrackedDeviceCount];
            }
            else
            {
                Rhino.RhinoApp.WriteLine("Failed to boot");
                mTitleBase = "SparrowHawk (No VR Detected)";
            }

            
            // Window Setup Info
            mStrDriver = Util.GetTrackedDeviceString(ref mHMD, OpenVR.k_unTrackedDeviceIndex_Hmd, ETrackedDeviceProperty.Prop_TrackingSystemName_String);
            mStrDisplay = Util.GetTrackedDeviceString(ref mHMD, OpenVR.k_unTrackedDeviceIndex_Hmd, ETrackedDeviceProperty.Prop_SerialNumber_String);
            mTitleBase = "SparrowHawk - " + mStrDriver + " " + mStrDisplay;
            Title = mTitleBase;
            MakeCurrent();
            setupScene();


            if (eError == EVRInitError.None)
                mRenderer = new VrRenderer(ref mHMD, ref mScene, mRenderWidth, mRenderHeight);
            else
                mRenderer = new VrRenderer(ref mHMD, ref mScene, mRenderWidth, mRenderHeight);

            //use other 8 points for calibrartion
            robotCallibrationPointsTest.Add(new Vector3(22, 15, -100) / 1000);
            robotCallibrationPointsTest.Add(new Vector3(-10, 40, -153) / 1000);
            robotCallibrationPointsTest.Add(new Vector3(25, -25, -181) / 1000);

            foreach (Vector3 v in robotCallibrationPointsTest)
            {
                Vector4 v4 = new Vector4(v.X, v.Y, v.Z, 1);
                v4 = mScene.vrToRobot.Inverted() * v4;
                Util.MarkPoint(ref mScene.staticGeometry, new Vector3(v4.X, v4.Y, v4.Z), 1, 1, 1);
            }
            robotCallibrationPointsTest.Clear();

            //set default matrix
            mRenderer.ovrvision_controller.setDefaultMatrixHC();

            //testing - rotate rhino object as well
            /*                   
            Transform transM = new Transform();
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    transM[row, col] = mScene.platformRotation[row, col];
                }
            }
            Rhino.DocObjects.ObjectEnumeratorSettings settings = new Rhino.DocObjects.ObjectEnumeratorSettings();
            settings.ObjectTypeFilter = Rhino.DocObjects.ObjectType.Brep;
            foreach (Rhino.DocObjects.RhinoObject rhObj in mScene.rhinoDoc.Objects.GetObjectList(settings))
            {
                mDoc.Objects.Transform(rhObj.Id, transM, true);
            }
            mScene.rhinoDoc.Views.Redraw();
            */

            //testing visualize printStroke
            printStroke = new Geometry.GeometryStroke(ref mScene);
            printStroke_m = new Material.SingleColorMaterial(1, 1, 0, 1);

            return (eError == EVRInitError.None);
        }

        List<Point3d> curvePoints = new List<Point3d>();
        Rhino.Geometry.Brep brep;
        protected override void OnKeyPress(OpenTK.KeyPressEventArgs e)
        {
            if (e.KeyChar == 'C' || e.KeyChar == 'c')
            {
                ((VrRenderer) mRenderer).ovrvision_controller.getMatrixHeadtoCamera(0);
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.CalibrationAR(ref mScene, ref mRenderer.ovrvision_controller));
            }

            if (e.KeyChar == 'D' || e.KeyChar == 'd')
                mRenderer.ovrvision_controller.setDefaultMatrixHC();

            if (e.KeyChar == 'S' || e.KeyChar == 's')
            {
                // i.GetType() == typeof(Interaction.Closedcurve) if we want to combine closedcurve and sweep as an interaction
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Sweep(ref mScene));
            }

            //rhino extrude and curve testing
            if (e.KeyChar == 'R' || e.KeyChar == 'r')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Closedcurve(ref mScene));
            }

            if (e.KeyChar == 'G' || e.KeyChar == 'g')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Grip(ref mScene));
            }

            if (e.KeyChar == 'H' || e.KeyChar == 'h')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.EditPlane(ref mScene, ref xyPlane, ref xzPlane, ref yzPlane));
                //mScene.pushInteraction(new Interaction.EditPlane2(ref mScene, ref xyPlane2, ref xzPlane2, ref yzPlane2));
            }

            if (e.KeyChar == 'J' || e.KeyChar == 'j')
            {
                //mScene.popInteraction();
                //mScene.pushInteraction(new Interaction.EditPlane(ref mScene, ref xyPlane, ref xzPlane, ref yzPlane));
                //mScene.pushInteraction(new Interaction.RotatePlane(ref mScene, ref xyPlane2, ref xzPlane2, ref yzPlane2));

                //clear the stroke
                foreach (SceneNode sn in mScene.tableGeometry.children)
                {
                    if (sn.name == "PrintStroke")
                    {
                        mScene.tableGeometry.children.Remove(sn);
                        break;
                    }
                }
            }

            if (e.KeyChar == 'K' || e.KeyChar == 'k')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Revolve(ref mScene, true));
            }

            if (e.KeyChar == 'L' || e.KeyChar == 'l')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Loft(ref mScene));
            }

            if (e.KeyChar == 'B' || e.KeyChar == 'b')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Delete(ref mScene));
            }

            if (e.KeyChar == 'M' || e.KeyChar == 'm')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Stroke(ref mScene));
            }

            if (e.KeyChar == 'N' || e.KeyChar == 'n')
            {
                mScene.popInteraction();
                //mScene.pushInteraction(new Interaction.Sweep2(ref mScene));
                mScene.pushInteraction(new Interaction.Sweep2(ref mScene,true));
            }

            if (e.KeyChar == 'V' || e.KeyChar == 'v')
            {
                calibrationTest();
            }

            if (e.KeyChar == 'P' || e.KeyChar == 'p')
            {
                mScene.popInteraction();
                controllerP = new List<Vector3>();
                mScene.pushInteraction(new Interaction.PickPoint(ref mScene, ref controllerP));
            }

            if (e.KeyChar == 'A' || e.KeyChar == 'a')
            {
                //mScene.popInteraction();
                //mScene.pushInteraction(new Interaction.Align(ref mScene));

                //for rhino object
                OpenTK.Matrix4 currentRotation =  mScene.platformRotation;

                float theta = (float)(45.0f / 360f * 2 * Math.PI);
                Rhino.RhinoApp.WriteLine("Theta = " + theta);
                Matrix4.CreateRotationZ(theta, out mScene.platformRotation);
                mScene.platformRotation.Transpose();

                //rotate Rhino objects
                OpenTK.Matrix4 rotMRhino =  mScene.platformRotation * currentRotation.Inverted();
                Rhino.DocObjects.ObjectEnumeratorSettings settings = new Rhino.DocObjects.ObjectEnumeratorSettings();
                Transform transM = new Transform();
                for (int row = 0; row < 4; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        transM[row, col] = rotMRhino[row, col];
                    }
                }
                settings.ObjectTypeFilter = Rhino.DocObjects.ObjectType.Brep;
                foreach (Rhino.DocObjects.RhinoObject rhObj in mScene.rhinoDoc.Objects.GetObjectList(settings))
                {                    
                    if (mScene.brepToSceneNodeDic.ContainsKey(rhObj.Id))
                    {
                        SceneNode sn = mScene.brepToSceneNodeDic[rhObj.Id];
                        mScene.brepToSceneNodeDic.Remove(rhObj.Id);

                        Guid newGuid = mScene.rhinoDoc.Objects.Transform(rhObj.Id, transM, true);
                        Rhino.RhinoApp.WriteLine("transM " + transM.ToString());
                        mScene.rhinoDoc.Views.Redraw();

                        mScene.brepToSceneNodeDic.Add(newGuid, sn);
                        mScene.SceneNodeToBrepDic[sn.guid] = mScene.rhinoDoc.Objects.Find(newGuid);
                    }
                    
                }
            }

            if (e.KeyChar == 'W' || e.KeyChar == 'w')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.CreateCircle(ref mScene));
            }

            if (e.KeyChar == 'O' || e.KeyChar == 'o')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.CreateCylinder(ref mScene));
            }

            if (e.KeyChar == 'Q' || e.KeyChar == 'q')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.CreateCircle(ref mScene, true));
            }

            if (e.KeyChar == 'Z' || e.KeyChar == 'z')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.Closedcurve(ref mScene, true));
            }

            if (e.KeyChar == 'X' || e.KeyChar == 'x')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.CreatePatch(ref mScene));
            }

            if (e.KeyChar == '[' || e.KeyChar == '{')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.PickPoint(ref mScene, ref mLeftControllerPoses));
            }

            if (e.KeyChar == ']' || e.KeyChar == '}')
            {
                mScene.popInteraction();
                mScene.pushInteraction(new Interaction.PickPoint(ref mScene, ref mRightControllerPoses));
            }

        }

        protected void notifyRobotIfSafe()
        {
            Vector3 tableOrigin = Util.transformPoint(mScene.vrToRobot.Inverted(), new Vector3(0, 0, 0));
            Vector3 headOrigin = Util.transformPoint(mScene.mHMDPose.Inverted(), new Vector3(0, 0, 0));
            Vector3 displacement = tableOrigin - headOrigin;
            displacement.Y = 0;
            if (displacement.Length > 1.5f && mSafeForRobot == false)
            {
                mSafeForRobot = true;
                Rhino.RhinoApp.WriteLine("Go. " + displacement.Length);
            }
            else if (displacement.Length < 1.5f && mSafeForRobot == true)
            {
                mSafeForRobot = false;
                Rhino.RhinoApp.WriteLine("Stop. " + displacement.Length);
            }
        }
    }
}