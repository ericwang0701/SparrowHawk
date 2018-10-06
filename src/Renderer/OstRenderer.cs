﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Valve.VR;
using OpenTK.Graphics.OpenGL4;
using SparrowHawk;
using OpenTK;
using System.Xml.Serialization;
using System.IO;

namespace SparrowHawk.Renderer
{
    /// <summary>
    /// An augemented reality (AR) renderer for optical see-through (OST) HMDs,
    /// designed specifically to be compatible with a tracked Meta 2 Headset.
    /// </summary>
    public class OstRenderer : SparrowHawk.Renderer.AbstractRenderer
    {
        Calibration.MetaTwoCalibrationData CalibrationData;

        public OstRenderer (ref CVRSystem hmd, ref Scene scene, uint renderWidth, uint renderHeight) 
        {
            mHMD = hmd;
            mScene = scene;
            FramebufferDesc.CreateFrameBuffer((int) renderWidth / 2, (int) renderHeight, out leftEyeDesc);
            FramebufferDesc.CreateFrameBuffer((int) renderWidth / 2, (int) renderHeight, out rightEyeDesc);


            bool success = false;
            XmlSerializer xmlf = new XmlSerializer(typeof(Calibration.MetaTwoCalibrationData));
            using (FileStream file = File.Open(Calibration.Spaam.CalibrationPath, FileMode.Open))
            {
                CalibrationData = xmlf.Deserialize(file) as Calibration.MetaTwoCalibrationData;
                success = true;
            }
            if (!success)
                Console.WriteLine("Failed to read file " + Calibration.Spaam.CalibrationPath);
            

        }

        /// <summary>
        /// Swap in-place first two elements of the input array. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        protected void swapFirstTwo<T>(ref T[] data)
        {
            if (data.Count() > 1)
            {
                T temp = data[0];
                data[0] = data[1];
                data[1] = temp;
            }
        }

        /// <summary>
        /// Given stereo pair in left and right, renders, flipped, to a window 
        /// assumed to be sent to the OST system. 
        /// Left and right assumed to be same height.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        public void RenderOstWindow(FramebufferDesc left, FramebufferDesc right, bool flipY=true, bool flipX=true)
        {

            int[] xBounds = { 0, left.Width }, yBounds = { 0, left.Height };
            if (flipY) swapFirstTwo(ref yBounds);
            if (flipX) swapFirstTwo(ref xBounds);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, left.renderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BlitFramebuffer(0, 0, left.Width, left.Height, xBounds[0], yBounds[0], xBounds[1], yBounds[1], ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            xBounds[0] = left.Width;
            xBounds[1] = left.Width + right.Width;
            if (flipX) swapFirstTwo(ref xBounds);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, right.renderFramebufferId);
            GL.BlitFramebuffer(0, 0, left.Width, left.Height, xBounds[0], yBounds[0], xBounds[1], yBounds[1], ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.Flush();
            GL.Finish();
        }

        /// <summary>
        /// Renders everything in the current scene from the viewpoint of the given eye.
        /// </summary>
        /// <param name="eye"></param>
        protected void RenderScene(EVREye eye)
        {
            GL.ClearColor(0.1f, 0.0f, 0.1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Matrix4 vp;
            Matrix4 ortho = Matrix4.CreateOrthographic(leftEyeDesc.Width * 2, leftEyeDesc.Height, 0.1f, 10);
            switch (eye)
            {
                case Valve.VR.EVREye.Eye_Left:
                    vp = CalibrationData.leftEyeProjection * mScene.mDevicePose[mScene.leftControllerIdx].Inverted();
                    break;
                default:
                    vp = CalibrationData.rightEyeProjection * mScene.mDevicePose[mScene.leftControllerIdx].Inverted();
                    break;
            }
            vp.Transpose();
            mScene.render(ref vp);
        }

        /// <summary>
        /// Renders the scene to the left and right framebuffer objects.
        /// </summary>
        protected void RenderStereoTargets()
        {
            GL.Enable(EnableCap.Multisample);
            GL.Viewport(0, 0, leftEyeDesc.Width, leftEyeDesc.Height);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, leftEyeDesc.renderFramebufferId);
            RenderScene(Valve.VR.EVREye.Eye_Left);
            leftEyeDesc.BlitToResolve();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, rightEyeDesc.renderFramebufferId);
            RenderScene(Valve.VR.EVREye.Eye_Right);
            rightEyeDesc.BlitToResolve();
        }

        public override void RenderFrame()
        {
            if (mHMD != null)
            {
                GL.DepthFunc(DepthFunction.Less);
                GL.Disable(EnableCap.DepthTest);
                RenderStereoTargets();
                GL.Finish();
                GL.Flush();

                RenderOstWindow(leftEyeDesc, rightEyeDesc);
                SubmitToHmd(leftEyeDesc, rightEyeDesc);

            }
        }
    }
}