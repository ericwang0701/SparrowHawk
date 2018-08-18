﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using LinAlg = MathNet.Numerics.LinearAlgebra;

namespace SparrowHawk.Calibration
{
    public class Spaam
    {
        private static Geometry.Geometry crosshairs = new Geometry.Polyline(new float[] {-1,0,0,1,0,0,0,-1,0,0,1,0});
        private static Material.SingleColorMaterial crosshairMaterial = new Material.SingleColorMaterial(1, 1, 1, 1);
     
        /// <summary>
        /// 
        /// A = GFC 
        /// i.e. P_world = P_mark * T2M * W2T (W2T should be ID)
        /// 
        /// [u,v,w] = G*p^T
        /// x,y = u/w, v/w
        /// 
        /// Based on "SPAAM for optical see-through HMD calibration for AR" by Tuceryan and Navab, 2001
        /// </summary>
        /// <param name="markPoses">The world-to-local transforms for the tracker on the optical HMD</param>
        /// <param name="screenPoints">The pixel coordinates corresponding to the known point in each pose</param>
        /// <param name="knownPoint">The known 3D calibration point in world space</param>
        /// <returns>The 3x4 projection matrix</returns>
        static Matrix3x4 estimateProjectionMatrix3x4(List<Matrix4> markPoses, List<Vector2> screenPoints, Vector4 knownPoint)
        {
            var B = LinAlg.CreateMatrix.Dense<float>(2 * markPoses.Count, 12);
            for (int i = 0; i < markPoses.Count; i++)
            {
                Vector4 p = markPoses[i] * knownPoint;
                Vector2 s = -screenPoints[i];
                p /= p.W;
                B.SetRow(2 * i, new float[] { p.X, p.Y, p.Z, 1, 0, 0, 0, 0, 0, 0, 0, 0, s.X * p.X, s.X * p.Y, s.X * p.Z, s.X });
                B.SetRow(2 * i, new float[] { 0, 0, 0, 0, p.X, p.Y, p.Z, 1, 0, 0, 0, 0, s.Y * p.X, s.Y * p.Y, s.Y * p.Z, s.Y });
            }
            var evd = B.Evd();
            var v = evd.EigenVectors.Column(0);
            Matrix3x4 P = new Matrix3x4(v.At(0), v.At(1), v.At(2), v.At(3),
                                        v.At(4), v.At(5), v.At(6), v.At(7),
                                        v.At(8), v.At(9), v.At(10), v.At(11));
            
            return P;
        }

        
        static Matrix4 constructProjectionMatrix4x4(Matrix3x4 proj, float n, float f, int r, int l, int t, int b)
        {
            // Duplicate last row.
            Matrix4 P = new Matrix4(proj.Row0, proj.Row1, proj.Row2, proj.Row2);
            P.Row2 *= (-f - n);
            P.M34 += f * n;
            return P* Matrix4.CreateOrthographicOffCenter(l, r, b, t, n, f);
        }

        static void renderCrosshairs(Vector2 screenPos, Color4 color, FramebufferDesc framebuffer)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer.renderFramebufferId);
            GL.ClearColor(0,0,0,1);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            crosshairs.mGeometry[1] = screenPos.Y;
            crosshairs.mGeometry[4] = screenPos.Y;
            crosshairs.mGeometry[6] = screenPos.X;
            crosshairs.mGeometry[9] = screenPos.X;
            crosshairMaterial.mColor = color;
            var id = Matrix4.Identity;
            crosshairMaterial.draw(ref crosshairs, ref id, ref id);
        }
    }
}
