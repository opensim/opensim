/*************************************************************************
 *                                                                       *
 * Open Dynamics Engine, Copyright (C) 2001-2003 Russell L. Smith.       *
 * All rights reserved.  Email: russ@q12.org   Web: www.q12.org          *
 *                                                                       *
 * This library is free software; you can redistribute it and/or         *
 * modify it under the terms of EITHER:                                  *
 *   (1) The GNU Lesser General Public License as published by the Free  *
 *       Software Foundation; either version 2.1 of the License, or (at  *
 *       your option) any later version. The text of the GNU Lesser      *
 *       General Public License is included with this library in the     *
 *       file LICENSE.TXT.                                               *
 *   (2) The BSD-style license that is included with this library in     *
 *       the file LICENSE-BSD.TXT.                                       *
 *                                                                       *
 * This library is distributed in the hope that it will be useful,       *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the files    *
 * LICENSE.TXT and LICENSE-BSD.TXT for more details.                     *
 *                                                                       *
 *************************************************************************/

#include <ode/collision.h>
#include <ode/matrix.h>
#include <ode/rotation.h>
#include <ode/odemath.h>

#if dTRIMESH_ENABLED

#include "collision_util.h"
#define TRIMESH_INTERNAL
#include "collision_trimesh_internal.h"

#if dTRIMESH_GIMPACT

void dxTriMeshData::Preprocess(){	// stub
}

dTriMeshDataID dGeomTriMeshDataCreate(){
    return new dxTriMeshData();
}

void dGeomTriMeshDataDestroy(dTriMeshDataID g){
    delete g;
}

void dGeomTriMeshSetLastTransform( dxGeom* g, dMatrix4 last_trans ) { //stub
}

dReal* dGeomTriMeshGetLastTransform( dxGeom* g ) {
	return NULL; // stub
}

void dGeomTriMeshDataSet(dTriMeshDataID g, int data_id, void* in_data) { //stub
}

void*  dGeomTriMeshDataGet(dTriMeshDataID g, int data_id) {
    dUASSERT(g, "argument not trimesh data");
	return NULL; // stub
}

void dGeomTriMeshDataBuildSingle1(dTriMeshDataID g,
                                  const void* Vertices, int VertexStride, int VertexCount,
                                  const void* Indices, int IndexCount, int TriStride,
                                  const void* Normals)
{
    dUASSERT(g, "argument not trimesh data");
    dIASSERT(Vertices);
    dIASSERT(Indices);

    g->Build(Vertices, VertexStride, VertexCount,
             Indices, IndexCount, TriStride,
             Normals,
             true);
}


void dGeomTriMeshDataBuildSingle(dTriMeshDataID g,
                                 const void* Vertices, int VertexStride, int VertexCount,
                                 const void* Indices, int IndexCount, int TriStride)
{
    dGeomTriMeshDataBuildSingle1(g, Vertices, VertexStride, VertexCount,
                                 Indices, IndexCount, TriStride, (void*)NULL);
}


void dGeomTriMeshDataBuildDouble1(dTriMeshDataID g,
                                  const void* Vertices, int VertexStride, int VertexCount,
                                 const void* Indices, int IndexCount, int TriStride,
				 const void* Normals)
{
    dUASSERT(g, "argument not trimesh data");

    g->Build(Vertices, VertexStride, VertexCount,
             Indices, IndexCount, TriStride,
             Normals,
             false);
}


void dGeomTriMeshDataBuildDouble(dTriMeshDataID g,
				 const void* Vertices, int VertexStride, int VertexCount,
                                 const void* Indices, int IndexCount, int TriStride) {
    dGeomTriMeshDataBuildDouble1(g, Vertices, VertexStride, VertexCount,
                                 Indices, IndexCount, TriStride, NULL);
}


void dGeomTriMeshDataBuildSimple1(dTriMeshDataID g,
                                  const dReal* Vertices, int VertexCount,
                                 const int* Indices, int IndexCount,
                                 const int* Normals){
#ifdef dSINGLE
    dGeomTriMeshDataBuildSingle1(g,
				Vertices, 4 * sizeof(dReal), VertexCount,
				Indices, IndexCount, 3 * sizeof(unsigned int),
				Normals);
#else
    dGeomTriMeshDataBuildDouble1(g, Vertices, 4 * sizeof(dReal), VertexCount,
				Indices, IndexCount, 3 * sizeof(unsigned int),
				Normals);
#endif
}


void dGeomTriMeshDataBuildSimple(dTriMeshDataID g,
                                 const dReal* Vertices, int VertexCount,
                                 const int* Indices, int IndexCount) {
    dGeomTriMeshDataBuildSimple1(g,
                                 Vertices, VertexCount, Indices, IndexCount,
                                 (const int*)NULL);
}

void dGeomTriMeshDataPreprocess(dTriMeshDataID g)
{
    dUASSERT(g, "argument not trimesh data");
	g->Preprocess();
}

void dGeomTriMeshDataGetBuffer(dTriMeshDataID g, unsigned char** buf, int* bufLen)
{
    dUASSERT(g, "argument not trimesh data");
	*buf = NULL;
	*bufLen = 0;
}

void dGeomTriMeshDataSetBuffer(dTriMeshDataID g, unsigned char* buf)
{
    dUASSERT(g, "argument not trimesh data");
//	g->UseFlags = buf;
}


// Trimesh

dxTriMesh::dxTriMesh(dSpaceID Space, dTriMeshDataID Data) : dxGeom(Space, 1){
    type = dTriMeshClass;

    dGeomTriMeshSetData(this,Data);

	/* TC has speed/space 'issues' that don't make it a clear
	   win by default on spheres/boxes. */
	this->doSphereTC = true;
	this->doBoxTC = true;
	this->doCapsuleTC = true;

}

dxTriMesh::~dxTriMesh(){

    //Terminate Trimesh
    gim_trimesh_destroy(&m_collision_trimesh);
}


void dxTriMesh::ClearTCCache(){

}


int dxTriMesh::AABBTest(dxGeom* g, dReal aabb[6]){
    return 1;
}


void dxTriMesh::computeAABB()
{
    //update trimesh transform
    mat4f transform;
    IDENTIFY_MATRIX_4X4(transform);
    MakeMatrix(this, transform);
    gim_trimesh_set_tranform(&m_collision_trimesh,transform);

    //Update trimesh boxes
    gim_trimesh_update(&m_collision_trimesh);

    memcpy(aabb,&m_collision_trimesh.m_aabbset.m_global_bound,6*sizeof(GREAL));
}


void dxTriMeshData::UpdateData()
{
//  BVTree.Refit();
}


dGeomID dCreateTriMesh(dSpaceID space,
		       dTriMeshDataID Data,
		       dTriCallback* Callback,
		       dTriArrayCallback* ArrayCallback,
		       dTriRayCallback* RayCallback)
{
    dxTriMesh* Geom = new dxTriMesh(space, Data);
    Geom->Callback = Callback;
    Geom->ArrayCallback = ArrayCallback;
    Geom->RayCallback = RayCallback;

    return Geom;
}

void dGeomTriMeshSetCallback(dGeomID g, dTriCallback* Callback)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	((dxTriMesh*)g)->Callback = Callback;
}

dTriCallback* dGeomTriMeshGetCallback(dGeomID g)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	return ((dxTriMesh*)g)->Callback;
}

void dGeomTriMeshSetArrayCallback(dGeomID g, dTriArrayCallback* ArrayCallback)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	((dxTriMesh*)g)->ArrayCallback = ArrayCallback;
}

dTriArrayCallback* dGeomTriMeshGetArrayCallback(dGeomID g)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	return ((dxTriMesh*)g)->ArrayCallback;
}

void dGeomTriMeshSetRayCallback(dGeomID g, dTriRayCallback* Callback)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	((dxTriMesh*)g)->RayCallback = Callback;
}

dTriRayCallback* dGeomTriMeshGetRayCallback(dGeomID g)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	return ((dxTriMesh*)g)->RayCallback;
}

void dGeomTriMeshSetData(dGeomID g, dTriMeshDataID Data)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
	dxTriMesh* mesh = (dxTriMesh*) g;
	mesh->Data = Data;
        // I changed my data -- I know nothing about my own AABB anymore.
        ((dxTriMesh*)g)->gflags |= (GEOM_DIRTY|GEOM_AABB_BAD);

	// GIMPACT only supports stride 12, so we need to catch the error early.
	dUASSERT
	(
	  Data->m_VertexStride == 3*sizeof(dReal) && Data->m_TriStride == 3*sizeof(int),
          "Gimpact trimesh only supports a stride of 3 dReal/int\n"
	  "This means that you cannot use dGeomTriMeshDataBuildSimple() with Gimpact.\n"
	  "Change the stride, or use Opcode trimeshes instead.\n"
	);

	//Create trimesh
	if ( Data->m_Vertices )
	  gim_trimesh_create_from_data
	  (
	    &mesh->m_collision_trimesh,		// gimpact mesh
	    ( vec3f *)(&Data->m_Vertices[0]),	// vertices
	    Data->m_VertexCount,		// nr of verts
	    0,					// copy verts?
	    ( GUINT *)(&Data->m_Indices[0]),	// indices
	    Data->m_TriangleCount*3,		// nr of indices
	    0,					// copy indices?
	    1					// transformed reply
	  );
}

dTriMeshDataID dGeomTriMeshGetData(dGeomID g)
{
  dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");
  return ((dxTriMesh*)g)->Data;
}



void dGeomTriMeshEnableTC(dGeomID g, int geomClass, int enable)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");

	switch (geomClass)
	{
		case dSphereClass:
			((dxTriMesh*)g)->doSphereTC = (1 == enable);
			break;
		case dBoxClass:
			((dxTriMesh*)g)->doBoxTC = (1 == enable);
			break;
		case dCapsuleClass:
//		case dCCylinderClass:
			((dxTriMesh*)g)->doCapsuleTC = (1 == enable);
			break;
	}
}

int dGeomTriMeshIsTCEnabled(dGeomID g, int geomClass)
{
	dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");

	switch (geomClass)
	{
		case dSphereClass:
			if (((dxTriMesh*)g)->doSphereTC)
				return 1;
			break;
		case dBoxClass:
			if (((dxTriMesh*)g)->doBoxTC)
				return 1;
			break;
		case dCapsuleClass:
			if (((dxTriMesh*)g)->doCapsuleTC)
				return 1;
			break;
	}
	return 0;
}

void dGeomTriMeshClearTCCache(dGeomID g){
    dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");

    dxTriMesh* Geom = (dxTriMesh*)g;
    Geom->ClearTCCache();
}

/*
 * returns the TriMeshDataID
 */
dTriMeshDataID
dGeomTriMeshGetTriMeshDataID(dGeomID g)
{
    dxTriMesh* Geom = (dxTriMesh*) g;
    return Geom->Data;
}

// Getting data
void dGeomTriMeshGetTriangle(dGeomID g, int Index, dVector3* v0, dVector3* v1, dVector3* v2)
{
    dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");

    dxTriMesh* Geom = (dxTriMesh*)g;
	gim_trimesh_locks_work_data(&Geom->m_collision_trimesh);	
	gim_trimesh_get_triangle_vertices(&Geom->m_collision_trimesh, Index, (*v0),(*v1),(*v2));	
	gim_trimesh_unlocks_work_data(&Geom->m_collision_trimesh);
 
}

void dGeomTriMeshGetPoint(dGeomID g, int Index, dReal u, dReal v, dVector3 Out){
    dUASSERT(g && g->type == dTriMeshClass, "argument not a trimesh");

    dxTriMesh* Geom = (dxTriMesh*)g;
    dVector3 dv[3];
	gim_trimesh_locks_work_data(&Geom->m_collision_trimesh);	
	gim_trimesh_get_triangle_vertices(&Geom->m_collision_trimesh, Index, dv[0],dv[1],dv[2]);
    GetPointFromBarycentric(dv, u, v, Out);
	gim_trimesh_unlocks_work_data(&Geom->m_collision_trimesh);
}

int dGeomTriMeshGetTriangleCount (dGeomID g)
{
    dxTriMesh* Geom = (dxTriMesh*)g;	
	return gim_trimesh_get_triangle_count(&Geom->m_collision_trimesh);
}

void dGeomTriMeshDataUpdate(dTriMeshDataID g) {
    dUASSERT(g, "argument not trimesh data");
    g->UpdateData();
}


//
// GIMPACT TRIMESH-TRIMESH COLLIDER
//

int dCollideTTL(dxGeom* g1, dxGeom* g2, int Flags, dContactGeom* Contacts, int Stride)
{
	dIASSERT (Stride >= (int)sizeof(dContactGeom));
	dIASSERT (g1->type == dTriMeshClass);
	dIASSERT (g2->type == dTriMeshClass);
	dIASSERT ((Flags & NUMC_MASK) >= 1);

    dxTriMesh* TriMesh1 = (dxTriMesh*) g1;
    dxTriMesh* TriMesh2 = (dxTriMesh*) g2;
    //Create contact list
    GDYNAMIC_ARRAY trimeshcontacts;
    GIM_CREATE_CONTACT_LIST(trimeshcontacts);

    //Collide trimeshes
    gim_trimesh_trimesh_collision(&TriMesh1->m_collision_trimesh,&TriMesh2->m_collision_trimesh,&trimeshcontacts);

    if(trimeshcontacts.m_size == 0)
    {
        GIM_DYNARRAY_DESTROY(trimeshcontacts);
        return 0;
    }

    GIM_CONTACT * ptrimeshcontacts = GIM_DYNARRAY_POINTER(GIM_CONTACT,trimeshcontacts);


	unsigned contactcount = trimeshcontacts.m_size;
	unsigned maxcontacts = (unsigned)(Flags & NUMC_MASK);
	if (contactcount > maxcontacts)
	{
		contactcount = maxcontacts;
	}

    dContactGeom* pcontact;
	unsigned i;

	for (i=0;i<contactcount;i++)
	{
        pcontact = SAFECONTACT(Flags, Contacts, i, Stride);

        pcontact->pos[0] = ptrimeshcontacts->m_point[0];
        pcontact->pos[1] = ptrimeshcontacts->m_point[1];
        pcontact->pos[2] = ptrimeshcontacts->m_point[2];
        pcontact->pos[3] = 1.0f;

        pcontact->normal[0] = ptrimeshcontacts->m_normal[0];
        pcontact->normal[1] = ptrimeshcontacts->m_normal[1];
        pcontact->normal[2] = ptrimeshcontacts->m_normal[2];
        pcontact->normal[3] = 0;

        pcontact->depth = ptrimeshcontacts->m_depth;
        pcontact->g1 = g1;
        pcontact->g2 = g2;

        ptrimeshcontacts++;
	}

	GIM_DYNARRAY_DESTROY(trimeshcontacts);

    return (int)contactcount;
}

#endif // dTRIMESH_GIMPACT
#endif // dTRIMESH_ENABLED
