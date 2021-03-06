﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;

namespace SparrowHawk.Geometry
{

    public class PointMarker : Geometry  
    {
        //private const float l = .005f;
        private const float l = .002f;
        private const float sqrt3 = 1.73205080757f;
        /**
         *  Creates a point marker centered at p, where p is in VR-World-space.
         */
        public PointMarker(Vector3 p) {
            primitiveType = OpenTK.Graphics.OpenGL4.BeginMode.Lines;
            //float c = l / sqrt3;
            float c = 0f;
            mNumPrimitives = 7;
            mGeometry = new float[]
                    {-l,0,0,
                      l,0,0,
                      0,-l,0,
                      0,l,0,
                      0,0,-l,
                      0,0,l,
                      -c,-c,-c,
                      c,c,c,
                      -c,-c,c,
                      c,c,-c,
                      -c,c,-c,
                      c,-c,c,
                      c,-c,-c,
                      -c,c,c };


            mGeometryIndices = new int[2 * mNumPrimitives];
            for (int i = 0; i < 2*mNumPrimitives; i++) {
                mGeometryIndices[i] = i;
            }
        }
    }
}
