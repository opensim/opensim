README for GeomTransformGroup by Tim Schmidt.
---------------------------------------------

This is a patch to add the dGeomTransformGroup object to the list of geometry 
objects.

It should work with the cvs version of the ode library from 07/24/2002.

++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

comment by russ smith: this code is easy to use with the rest of ODE.
simply copy GeomTransformGroup.cpp to ode/src and copy GeomTransformGroup.h
to include/ode. then add GeomTransformGroup.cpp to the ODE_SRC variable
in the makefile. rebuild, and you're done! of course i could have done all
this for you, but i prefer to keep GeomTransformGroup separated from the
rest of ODE for now while other issues with the collision system are
resolved.

++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


Description:

The dGeomTransformGroup is an adaption of the TransformGroup known from 
Java3D (and maybe other libraries with a similar scene graph representation).
It can be used to build an arbitrarily structured tree of objects that are 
each positioned relative to the particular parent node.

If you have a plane for example, there is one root node associated with the 
plane's body and another three transformgroups placed 'under' this node. One 
with the fuselage (cappedcylinder) under it and two with the underlying wings 
(flat boxes). And if you want to add engines, simply put them 'next to' the 
wings under another two transformgroups.

bodyTG ---> associated with dBody
    |
    +--fuselageTG
    |   |
    |   +--fuselageCylinder
    |
    +--leftwingTG
    |   |
    |   +--wingBox
    |   |
    |   +--leftengineTG
    |         |
    |         +--leftengineCylinder
    | 
    +--rightwingTG
        |
        +--wingBox
        |
        +--rightengineTG
              |
              +--rightengineCylinder

This is a method to easily compose objects without the necessity of always 
calculating global coordinates. But apart from this there is something else 
that makes dGeomTransformGroups very valuable.

Maybe you remember that some users reported the problem of acquiring the 
correct bodies to be attached by a contactjoint in the nearCallback when 
using dGeomGroups and dGeomTransforms at the same time. This results from the 
fact that dGeomGroups are not associated with bodies while all other 
geometries are.

So, as you can see in the nearCallback of the the test_buggy demo you have to 
attach the contactjoint with the bodies that you get from the geometries that 
are stored in the contact struct (-> dGeomGetBody(contacts[i].geom.g1)). 
Normally you would do this by asking o1 and o2 directly with dGeomGetBody(o1) 
and dGeomGetBody(o2) respectively.

As a first approach you can overcome that problem by testing o1 and o2 if 
they are groups or not to find out how to get the corresponding bodies.

However this will fail if you want grouped transforms that are constructed 
out of dGeomTransforms encapsulated in a dGeomGroup. According to the test 
you use contacts[i].geom.g1 to get the right body. Unfortunately g1 is 
encapsulated in a transform and therefore not attached to any body. In this 
case the dGeomTransform 'in the middle' would have been the right object to 
be asked for the body.

You may now conclude that it is a good idea to unwrap the group encapsulated 
geoms at the beginning of the nearcallback and use dGeomGetBody(o1) 
consistently. But keep in mind that this also means not to invoke 
dCollide(..) on groups at all and therefore not to expoit the capability of 
dGeomGroups to speed up collision detection by the creation of bounding boxes 
around the encapsulated geometry.

Everything becomes even worse if you create a dGeomTransform that contains a 
dGeomGroup of geoms. The function that cares about the collision of 
transforms with other objects uses the position and rotation of the 
respective encapsulated object to compute its final position and orientation. 
Unfortunately dGeomGroups do not have a position and rotation, so the result 
will not be what you have expected.

Here the dGeomTransformGroups comes into operation, because it combines the 
advantages and capabilities of the dGeomGroup and the dGeomTransform.
And as an effect of synergy it is now even possible to set the position of a 
group of geoms with one single command.
Even nested encapsulations of dGeomTransformGroups in dGeomTransformGroups 
should be possible (to be honest, I have not tried that so far ;-) ).

++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

API:

dGeomID dCreateGeomTransformGroup (dSpaceID space);
  - create a GeomTransformGroup 
    
void dGeomTransformGroupAddGeom    (dGeomID tg, dGeomID obj);
  - Comparable to dGeomTransformSetGeom or dGeomGroupAdd
  - add objects to this group
   
void dGeomTransformGroupRemoveGeom (dGeomID tg, dGeomID obj);
  - remove objects from this group

void dGeomTransformGroupSetRelativePosition
            (dGeomID g, dReal x, dReal y, dReal z);
void dGeomTransformGroupSetRelativeRotation
            (dGeomID g, const dMatrix3 R);
  - Comparable to setting the position and rotation of all the
    dGeomTransform encapsulated geometry. The difference 
    is that it is global with respect to this group and therefore
    affects all geoms in this group.
  - The relative position and rotation are attributes of the 
    transformgroup, so the position and rotation of the individual
    geoms are not changed 

const dReal * dGeomTransformGroupGetRelativePosition (dGeomID g);
const dReal * dGeomTransformGroupGetRelativeRotation (dGeomID g);
  - get the relative position and rotation
  
dGeomID dGeomTransformGroupGetGeom (dGeomID tg, int i);
  - Comparable to dGeomGroupGetGeom
  - get a specific geom of the group
  
int dGeomTransformGroupGetNumGeoms (dGeomID tg);
  - Comparable to dGeomGroupGetNumGeoms
  - get the number of geoms in the group
  

++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

Tim Schmidt
student of computer science
University of Paderborn, Germany
tisch@uni-paderborn.de
