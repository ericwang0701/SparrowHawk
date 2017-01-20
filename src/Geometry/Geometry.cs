﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ObjLoader.Loader;
using OpenTK;


namespace SparrowHawk.Geometry
{
    public class Geometry
    {
        public List<float> mGeometry { get; set; } = new List<float>();
        public List<int> mGeometryIndices { get; set; } = new List<int>();
        public List<float> mUvs { get; set; } = new List<float>();
        public List<float> mNormals { get; set; } = new List<float>();
        public List<float> mColors { get; set; } = new List<float>();
        public int mNumPrimitives { get; set; }
        public OpenTK.Graphics.OpenGL4.PrimitiveType primitiveType;

        public Geometry(string filename, OpenTK.Graphics.Color4 color)
        {
            LoadObjFile(filename, color);
        }

        public Geometry()
        {
        }


        // I'm doing this with too many loops but whatever.
        // Also I'm shattering the ability to put multiple meshes in one file.
        void LoadObjFile(string filename, OpenTK.Graphics.Color4 color)
        {
            var factory = new ObjLoader.Loader.Loaders.ObjLoaderFactory();
            var objLoader = factory.Create();
            var fileStream = new System.IO.FileStream(filename, System.IO.FileMode.Open);
            var data = objLoader.Load(fileStream);
            
            foreach (var v in data.Vertices)
            {
                mGeometry.Add(v.X); mGeometry.Add(v.Y); mGeometry.Add(v.Z);
                mColors.Add(color.R); mColors.Add(color.G); mColors.Add(color.B); mColors.Add(color.A);
            }

            foreach (var n in data.Normals)
            {
                mNormals.Add(n.X); mNormals.Add(n.Y); mNormals.Add(n.Z);
            }

            foreach (var uv in data.Textures)
            {
                mUvs.Add(uv.X); mUvs.Add(uv.Y);
            }

            for (int i = 0; i < data.Vertices.Count; i++)
            {
                mGeometryIndices.Add(i);
            }

            // TODO: Add flat shade code;

            mNumPrimitives = mGeometryIndices.Count / 3;
        }

        // TODO: RayTracer

        // TODO: Build AABB
    }
}
