/*
 * The ODE Model Processor
 * -----------------------
 * 
 * Copyright 2007, Department of Information Science,
 * University of Otago, Dunedin, New Zealand.
 * 
 * Author: Richard Barrington <barri662@student.otago.ac.nz>
 * 
 * This is a Content Processor and Tag library written for use with
 * Microsoft Visual C# 2005 Express Edition and Microsoft XNA Game 
 * Studio Express 1.0.
 * 
 * It can be used to read .x model vertex and index data before 
 * insertion into the content pipeline. This is used to build ODE
 * Triangle Meshes which are then used for collision detection that
 * is more accurate than the default XNA bounding boxes or spheres.
 * 
 * Usage is simple:
 * Build the library and reference the DLL in your project.
 * Add the DLL to the Content Pipeline
 * Set the content processor for you .x models to OdeModelProcessor.
 * 
 * Create triangle meshes as follows:
 * 1) Create a space, but only one for all of models.
 * 2) Create a triangle data.
 * 3) Load the model.
 * 4) Retreive the tag from the model.
 * 6) Build the triangle mesh by calling d.GeomTriMeshDataBuildSimple.
 * 
 * Eg:
 * IntPtr space = d.SimpleSpaceCreate(IntPtr.Zero);
 * IntPtr triangleData = d.GeomTriMeshDataCreate();
 * Model obj = content.Load<Model>("Content\\mycube");
 * OdeTag tag = (OdeTag)obj.Tag;
 * IntPtr vertexArray = tag.getVertices();
 * IntPtr indexArray = tag.getIndices();
 * d.GeomTriMeshDataBuildSimple
 * (
 *     triangleData,
 *     vertexArray, tag.getVertexStride(), tag.getVertexCount(),
 *     indexArray, tag.getIndexCount(), tag.getIndexStride()
 * );
 * IntPtr triangleMesh = d.CreateTriMesh(space, triangleData, null, null, null);
 * 
 * You can load multiple models and test for collisions with something
 * like this in the update method:
 * 
 * d.GeomSetPosition(odeTri1, obj1Position.X, obj1Position.Y, obj1Position.Z);
 * d.GeomSetPosition(odeTri2, obj2Position.X, obj2Position.Y, obj2Position.Z);
 * int numberOfContacts = d.Collide(odeTri1, odeTri2, ODE_CONTACTS,
 *     contactGeom, d.ContactGeom.SizeOf);
 * 
 * Where odeTri1 and odeTri2 are triangle meshes you've created, obj1Position
 * and obj2Position are the positions of your rendered models in the scene,
 * ODE_CONTACTS is a constant defining the maximum number of contacts
 * to test for, contactGeom is a d.ContactGeom[] of length ODE_CONTACTS.
 * 
 * If numberOfContacts is greater than 0, you have a collision.
 * 
 * Other ODE functions such as d.SpaceCollide() also work; see ODE.NET BoxTest.cs.
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the same terms as the ODE and ODE.Net libraries.
 * Specifically, the terms are one of EITHER:
 * 
 *   (1) The GNU Lesser General Public License as published by the Free
 *       Software Foundation; either version 2.1 of the License, or (at
 *       your option) any later version. The text of the GNU Lesser
 *       General Public License is included with this library in the
 *       file LICENSE.TXT.
 * 
 *   (2) The BSD-style license that is included with this library in
 *       the file LICENSE-BSD.TXT.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the files
 * LICENSE.TXT and LICENSE-BSD.TXT for more details.
 * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Design;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Ode.NET;
using System.Runtime.InteropServices;

namespace OdeModelProcessor
{
    /*
     * Container for vertex and index data in a format
     * that ODE.Net can use
     */
    public class OdeTag
    {

        private float[] vertexData;
        private int[] indexData;
        private const int indexStride = (sizeof(int));
        private const int vertexStride = (3 * sizeof(float));

        /* Constructors */
        public OdeTag()
        {
            vertexData = new float[0];
            indexData = new int[0];
        }
        
        public OdeTag(float[] vertexData, int[] indexData)
        {
            this.vertexData = vertexData;
            this.indexData = indexData;
        }
        
        /* Data setter */
        public void setData(float[] vertexData, int[] indexData)
        {
            this.vertexData = vertexData;
            this.indexData = indexData;
        }

        /* Data appenders */
        public void appendVertexData(float[] vertexData)
        {
            int newVertexDataLength = vertexData.Length;
            float[] tempVertexArray = new float[newVertexDataLength + this.vertexData.Length];
            this.vertexData.CopyTo(tempVertexArray, 0);
            vertexData.CopyTo(tempVertexArray, this.vertexData.Length);
            this.vertexData = tempVertexArray;
        }

        public void appendIndexData(int[] indexData)
        {
            int newIndexDataLength = indexData.Length;
            int[] tempIndexArray = new int[newIndexDataLength + this.indexData.Length];
            this.indexData.CopyTo(tempIndexArray, 0);
            indexData.CopyTo(tempIndexArray, this.indexData.Length);
            this.indexData = tempIndexArray;
        }

        /* Data getters */
        public float[] getVertexData()
        {
            return this.vertexData;
        }

        public int[] getIndexData()
        {
            return this.indexData;
        }

        /* Native data getters */
        public IntPtr getVertices()
        {
            int count = getVertexData().Length;
            int memsize = count * Marshal.SizeOf(getVertexData()[0].GetType());
            IntPtr pointer = Marshal.AllocCoTaskMem(memsize);
            Marshal.Copy(getVertexData(), 0, pointer, count);
            return pointer;
        }

        public IntPtr getIndices()
        {
            int count = getIndexData().Length;
            int memsize = count * Marshal.SizeOf(getIndexData()[0].GetType());
            IntPtr pointer = Marshal.AllocCoTaskMem(memsize);
            Marshal.Copy(getIndexData(), 0, pointer, count);
            return pointer;
        }

        /* Count getters */
        public int getVertexCount()
        {
            return vertexData.Length/3;
        }

        public int getIndexCount()
        {
            return indexData.Length;
        }

        /* Stride getters */
        public int getVertexStride()
        {
            return vertexStride;
        }

        public int getIndexStride()
        {
            return indexStride;
        }

        /*
         * Convienience method to build the mesh and return it. The triangleData
         * is passed in to allow the calling application to delete it afterwards.
         *
         * Be sure to destroy the returned TriangleMesh in the client application.
         * 
         * Can't destroy the index and vertex arrays here though, so best to handle
         * this manually - only use this method if nothing else makes sense.
         */ 
        public IntPtr getTriangleMesh(IntPtr space, IntPtr triangleData)
        {
            d.GeomTriMeshDataBuildSimple(
                triangleData,
                getVertices(), getVertexStride(), getVertexCount(),
                getIndices(), getIndexCount(), getIndexStride()
            );
            return d.CreateTriMesh(space, triangleData, null, null, null);
        }

    }

    /*
     * Subclass of the XNA .x model processor, which creates and appends a tag
     * containing vertex and index data for ODE.Net to use.
     */ 
    [ContentProcessor]
    public class OdeModelProcessor : ModelProcessor
    {
        private OdeTag tag;
        private int indexOffset = 0;

        public override ModelContent Process(NodeContent input, ContentProcessorContext context)
        {
            tag = new OdeTag();
            GenerateVerticesRecursive( input );
            ModelContent model = base.Process(input, context);
            model.Tag = tag;
            indexOffset = 0;
            return model;
        }

        public void GenerateVerticesRecursive(NodeContent input)
        {
            
            MeshContent mesh = input as MeshContent;
            
            if (mesh != null)
            {
                GeometryContentCollection gc = mesh.Geometry;
                foreach (GeometryContent g in gc)
                {
                    VertexContent vc = g.Vertices;
                    IndirectPositionCollection ipc = vc.Positions;
                    IndexCollection ic = g.Indices;

                    float[] vertexData = new float[ipc.Count * 3];
                    for (int i = 0; i < ipc.Count; i++)
                    {
                        
                        Vector3 v0 = ipc[i];
                        vertexData[(i * 3) + 0] = v0.X;
                        vertexData[(i * 3) + 1] = v0.Y;
                        vertexData[(i * 3) + 2] = v0.Z;                        
                        
                    }

                    int[] indexData = new int[ic.Count];
                    for (int j = 0; j < ic.Count; j ++)
                    {

                        indexData[j] = ic[j] + indexOffset;

                    }

                    tag.appendVertexData(vertexData);
                    tag.appendIndexData(indexData);
                    indexOffset += ipc.Count;
                }
                
            }

            foreach (NodeContent child in input.Children)
            {
                GenerateVerticesRecursive(child);
            }

        }

    }

    /* Writer for the OdeTag class */
    [ContentTypeWriter]
    public class OdeTagWriter : ContentTypeWriter<OdeTag>
    {

        protected override void Write(ContentWriter output, OdeTag value)
        {
            float[] vertexData = value.getVertexData();
            int[] indexData = value.getIndexData();
            output.Write(vertexData.Length);
            output.Write(indexData.Length);
            for (int j = 0; j < vertexData.Length; j++)
            {
                output.Write(vertexData[j]);
            }
            for (int i = 0; i < indexData.Length; i++)
            {
                output.Write(indexData[i]);
            }
        }

        public override string GetRuntimeType(TargetPlatform targetPlatform)
        {
            return typeof(OdeTag).AssemblyQualifiedName;
        }

        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            return "OdeModelProcessor.OdeTagReader, OdeModelProcessor, Version=1.0.0.0, Culture=neutral";
        }

    }

    /* Reader for the OdeTag class */
    public class OdeTagReader : ContentTypeReader<OdeTag>
    {
        protected override OdeTag Read(ContentReader input, OdeTag existingInstance)
        {
            float[] vertexData = new float[input.ReadInt32()];
            int[] indexData = new int[input.ReadInt32()];
            for (int j = 0; j < vertexData.Length; j++)
            {
                vertexData[j] = input.ReadSingle();
            }
            for (int i = 0; i < indexData.Length; i++)
            {
                indexData[i] = input.ReadInt32();
            }

            OdeTag tag = null;
            if (existingInstance == null)
            {
                tag = new OdeTag(vertexData, indexData);
            }
            else
            {
                tag = existingInstance;
                tag.setData(vertexData, indexData);
            }
            return tag;
        }
    }
}
