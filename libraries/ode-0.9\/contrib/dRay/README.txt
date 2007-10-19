From: "Erwin de Vries" <erwin@vo.com>
To: <ode@q12.org>
Subject: [ODE] dRay class
Date: Thu, 25 Jul 2002 13:05:28 +0200

Yesterday and today i've written a dRay class. It interacts with dPlane,
dSphere, dBox and dCCylinder. It does not generate full contact information.
It only generates the pos member. I dont think its useful to anyone to go
through hoops and find a reasonable normal and penetration depth, as i dont
think anyone will want to use it for dynamics. Just for CD.

It should compile in single and double precision mode, and should be
platform independant. I hope.

The next Tri-Collider release using Opcode 1.1 will also implement a ray
collision function along with some other not too interesting improvements.
