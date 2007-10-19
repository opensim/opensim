The ODE Model Processor
-----------------------

Copyright 2007, Department of Information Science,
University of Otago, Dunedin, New Zealand.

Author: Richard Barrington <barri662@student.otago.ac.nz>

This is a Content Processor and Tag library written for use with
Microsoft Visual C# 2005 Express Edition and Microsoft XNA Game 
Studio Express 1.0.

It can be used to read .x model vertex and index data before 
insertion into the content pipeline. This is used to build ODE
Triangle Meshes which are then used for collision detection that
is more accurate than the default XNA bounding boxes or spheres.

Usage is fairly simple:
Build the library and reference the DLL in your project.
Add the DLL to the Content Pipeline
Set the content processor for you .x models to OdeModelProcessor.

Create triangle meshes as follows:
1) Create a space, but only one for all of models.
2) Create a triangle data.
3) Load the model.
4) Retreive the tag from the model.
6) Build the triangle mesh by calling d.GeomTriMeshDataBuildSimple.

Eg:
IntPtr space = d.SimpleSpaceCreate(IntPtr.Zero);
IntPtr triangleData = d.GeomTriMeshDataCreate();
Model obj = content.Load<Model>("Content\\mycube");
OdeTag tag = (OdeTag)obj.Tag;
IntPtr vertexArray = tag.getVertices();
IntPtr indexArray = tag.getIndices();
d.GeomTriMeshDataBuildSimple
(
	triangleData,
	vertexArray, tag.getVertexStride(), tag.getVertexCount(),
	indexArray, tag.getIndexCount(), tag.getIndexStride()
);
IntPtr triangleMesh = d.CreateTriMesh(space, triangleData, null, null, null);

You can load multiple models and test for collisions with something
like this in the update method:

d.GeomSetPosition(odeTri1, obj1Position.X, obj1Position.Y, obj1Position.Z);
d.GeomSetPosition(odeTri2, obj2Position.X, obj2Position.Y, obj2Position.Z);
int numberOfContacts = d.Collide(odeTri1, odeTri2, ODE_CONTACTS,
                           contactGeom, d.ContactGeom.SizeOf);

Where odeTri1 and odeTri2 are triangle meshes you've created, obj1Position
and obj2Position are the positions of your rendered models in the scene,
ODE_CONTACTS is a constant defining the maximum number of contacts
to test for, contactGeom is a d.ContactGeom[] of length ODE_CONTACTS.

If numberOfContacts is greater than 0, you have a collision.

Other ODE functions such as d.SpaceCollide() also work; see ODE.NET BoxTest.cs.

This library is free software; you can redistribute it and/or
modify it under the same terms as the ODE and ODE.Net libraries.
Specifically, the terms are one of EITHER:

  (1) The GNU Lesser General Public License as published by the Free
      Software Foundation; either version 2.1 of the License, or (at
      your option) any later version. The text of the GNU Lesser
      General Public License is included with this library in the
      file LICENSE.TXT.

  (2) The BSD-style license that is included with this library in
      the file LICENSE-BSD.TXT.
 
This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the files
LICENSE.TXT and LICENSE-BSD.TXT for more details.
