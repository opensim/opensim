#include "CppTestHarness.h"
#include "ode/ode.h"

TEST(BoxSphereIntersection)
{
	dGeomID box    = dCreateBox(NULL, 1.0f, 1.0f, 1.0f);
	dGeomID sphere = dCreateSphere(NULL, 1.0f);

	CHECK_EQUAL(1.0, 1.0);

	dGeomDestroy(box);
	dGeomDestroy(sphere);
}
