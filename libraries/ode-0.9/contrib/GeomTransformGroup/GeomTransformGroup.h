 
/* ************************************************************************ */
/* 
   grouped and transformed geometry functions 
   author: Tim Schmidt tisch@uni-paderborn.de
*/


#ifdef __cplusplus
extern "C" {
#endif


extern int dGeomTransformGroupClass;

void dGeomTransformGroupSetRelativePosition (dGeomID g, dReal x, dReal y, dReal z);
void dGeomTransformGroupSetRelativeRotation (dGeomID g, const dMatrix3 R);
const dReal * dGeomTransformGroupGetRelativePosition (dxGeom *g);
const dReal * dGeomTransformGroupGetRelativeRotation (dxGeom *g);
dGeomID dCreateGeomTransformGroup (dSpaceID space);
void dGeomTransformGroupAddGeom    (dGeomID tg, dGeomID obj);
void dGeomTransformGroupRemoveGeom (dGeomID tg, dGeomID obj);
dGeomID dGeomTransformGroupGetGeom (dGeomID tg, int i);
int dGeomTransformGroupGetNumGeoms (dGeomID tg);


#ifdef __cplusplus
}
#endif
