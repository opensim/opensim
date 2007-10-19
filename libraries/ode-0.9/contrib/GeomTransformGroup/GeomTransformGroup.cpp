
/* ************************************************************************ */
/* 
   grouped and transformed geometry functions 
   author: Tim Schmidt tisch@uni-paderborn.de
*/


#include <ode/common.h>
#include <ode/geom.h>
#include <ode/rotation.h>
#include <ode/odemath.h>
#include <ode/memory.h>
#include <ode/misc.h>
#include <ode/objects.h>
#include <ode/matrix.h>
#include <ode/GeomTransformGroup.h>
#include "objects.h"
#include "array.h"
#include "geom_internal.h"

// given a pointer `p' to a dContactGeom, return the dContactGeom at
// p + skip bytes.

#define CONTACT(p,skip) ((dContactGeom*) (((char*)p) + (skip)))


// ############################################################################

int dGeomTransformGroupClass = -1;
// ############################################################################

struct dxGeomTransformGroup {
  dArray<dxGeom*> parts;	// all the geoms that make up the group
  dVector3 relativePosition;
  dMatrix3 relativeRotation;
};
// ############################################################################

void dGeomTransformGroupSetRelativePosition (dxGeom *g, dReal x, dReal y, dReal z)
{
  dAASSERT (g);
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  transformGroup->relativePosition[0] = x;
  transformGroup->relativePosition[1] = y;
  transformGroup->relativePosition[2] = z;
}
// ############################################################################

void dGeomTransformGroupSetRelativeRotation (dxGeom *g, const dMatrix3 R)
{
  dAASSERT (g);
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  memcpy (transformGroup->relativeRotation,R,sizeof(dMatrix3));
}
// ############################################################################

const dReal * dGeomTransformGroupGetRelativePosition (dxGeom *g)
{
  dAASSERT (g);
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  return transformGroup->relativePosition;
}
// ############################################################################

const dReal * dGeomTransformGroupGetRelativeRotation (dxGeom *g)
{
  dAASSERT (g);
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  return transformGroup->relativeRotation;
}
// ############################################################################

static void computeFinalTransformation (const dxGeom *tg, const dxGeom *part)
{
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(tg);
  dMULTIPLY0_331 (part->pos,tg->R,transformGroup->relativePosition);
  part->pos[0] += tg->pos[0];
  part->pos[1] += tg->pos[1];
  part->pos[2] += tg->pos[2];
  dMULTIPLY0_333 (part->R,tg->R,transformGroup->relativeRotation);
}
// ############################################################################

int dCollideTransformGroup (const dxGeom *o1, const dxGeom *o2, int flags,
	       dContactGeom *contact, int skip)
{
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(o1);
  if (transformGroup->parts.size() == 0)
  {
    return 0;
  }
  int numleft = flags & NUMC_MASK;
  if (numleft == 0) numleft = 1;
  flags &= ~NUMC_MASK;
  int num=0, i=0;
  while (i < transformGroup->parts.size() && numleft > 0) 
  {
    dUASSERT (transformGroup->parts[i]->spaceid==0,
	      "GeomTransformGroup encapsulated object must not be in a space");
    dUASSERT (transformGroup->parts[i]->body==0,
	      "GeomTransformGroup encapsulated object must not be attached to a body");
    if (!o1->space_aabb) 
    {
      computeFinalTransformation (o1, transformGroup->parts[i]);
    }
    dxBody *bodyBackup = transformGroup->parts[i]->body;
    transformGroup->parts[i]->body = o1->body;
    int n = dCollide (transformGroup->parts[i],const_cast<dxGeom*>(o2),
		      flags | numleft,contact,skip);
    transformGroup->parts[i]->body = bodyBackup;
    contact = CONTACT (contact,skip*n);
    numleft -= n;
    num += n;
    i++;
  }
  return num;
}
// ############################################################################

static dColliderFn * dGeomTransformGroupColliderFn (int num)
{
  return (dColliderFn *) &dCollideTransformGroup;
}
// ############################################################################

static void dGeomTransformGroupAABB (dxGeom *geom, dReal aabb[6])
{
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(geom);
  aabb[0] = dInfinity;
  aabb[1] = -dInfinity;
  aabb[2] = dInfinity;
  aabb[3] = -dInfinity;
  aabb[4] = dInfinity;
  aabb[5] = -dInfinity;
  int i,j;
  for (i=0; i < transformGroup->parts.size(); i++)
  {
    computeFinalTransformation (geom, transformGroup->parts[i]);
    dReal aabb2[6];
    transformGroup->parts[i]->_class->aabb (transformGroup->parts[i],aabb2);
    for (j=0; j<6; j += 2) if (aabb2[j] < aabb[j]) aabb[j] = aabb2[j];
    for (j=1; j<6; j += 2) if (aabb2[j] > aabb[j]) aabb[j] = aabb2[j];
  }
}
// ############################################################################

static void dGeomTransformGroupDtor (dxGeom *geom)
{
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(geom);
  transformGroup->parts.~dArray();
}
// ############################################################################

dxGeom *dCreateGeomTransformGroup (dSpaceID space)
{
  if (dGeomTransformGroupClass == -1) {
    dGeomClass c;
    c.bytes = sizeof (dxGeomTransformGroup);
    c.collider = &dGeomTransformGroupColliderFn;
    c.aabb = &dGeomTransformGroupAABB;
    c.aabb_test = 0;
    c.dtor = dGeomTransformGroupDtor;
    dGeomTransformGroupClass = dCreateGeomClass (&c);
  }
  dxGeom *g = dCreateGeom (dGeomTransformGroupClass);
  if (space)
  {
    dSpaceAdd (space,g);
  }
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  transformGroup->parts.constructor();
  dSetZero (transformGroup->relativePosition,4);
  dRSetIdentity (transformGroup->relativeRotation);
  return g;
}
// ############################################################################

void dGeomTransformGroupAddGeom (dxGeom *g, dxGeom *obj)
{
  dUASSERT (g && g->_class->num == dGeomTransformGroupClass,
	    "argument not a geom TransformGroup");
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  transformGroup->parts.push (obj);
}
// ############################################################################

void dGeomTransformGroupRemoveGeom (dxGeom *g, dxGeom *obj)
{
  dUASSERT (g && g->_class->num == dGeomTransformGroupClass,
	    "argument not a geom TransformGroup");
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  for (int i=0; i < transformGroup->parts.size(); i++) {
    if (transformGroup->parts[i] == obj) {
      transformGroup->parts.remove (i);
      return;
    }
  }
}
// ############################################################################

dxGeom * dGeomTransformGroupGetGeom (dxGeom *g, int i)
{
  dUASSERT (g && g->_class->num == dGeomTransformGroupClass,
	    "argument not a geom TransformGroup");
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  dAASSERT (i >= 0 && i < transformGroup->parts.size());
  return transformGroup->parts[i];
}
// ############################################################################

int dGeomTransformGroupGetNumGeoms (dxGeom *g)
{
  dUASSERT (g && g->_class->num == dGeomTransformGroupClass,
	    "argument not a geom TransformGroup");
  dxGeomTransformGroup *transformGroup = (dxGeomTransformGroup*) CLASSDATA(g);
  return transformGroup->parts.size();
}
