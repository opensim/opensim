using System;
using System.Collections.Generic;
using Drawstuff.NET;

namespace Ode.NET
{
#if dDOUBLE
	using dReal = System.Double;
#else
	using dReal = System.Single;
#endif

	public class TestBoxStack
	{
		#region Description of convex shape

		static dReal[] planes = 
			{
				1.0f, 0.0f, 0.0f, 0.25f,
				0.0f, 1.0f, 0.0f, 0.25f,
				0.0f, 0.0f, 1.0f, 0.25f,
				0.0f, 0.0f, -1.0f, 0.25f,
				0.0f, -1.0f, 0.0f, 0.25f,
				-1.0f, 0.0f , 0.0f, 0.25f
			};

		static dReal[] points =
			{
				0.25f, 0.25f, 0.25f,
				-0.25f, 0.25f, 0.25f,
				0.25f, -0.25f, 0.25f,
				-0.25f, -0.25f, 0.25f,
				0.25f, 0.25f, -0.25f,
				-0.25f,0.25f,-0.25f,
				0.25f,-0.25f,-0.25f,
				-0.25f,-0.25f,-0.25f,
			};

		static int[] polygons =
		{
			4, 0, 2, 6, 4,
			4, 1, 0, 4, 5,
			4, 0, 1, 3, 2,
			4, 3, 1, 5, 7,
			4, 2, 3, 7, 6,
			4, 5, 4, 6, 7,
		};

		#endregion

		const int NUM = 100;
		const float DENSITY = 5.0f;
		const int MAX_CONTACTS = 8;
		
		static IntPtr world;
		static IntPtr space;
		static IntPtr contactgroup;

		static Queue<IntPtr> obj = new Queue<IntPtr>();

		static d.Vector3 xyz = new d.Vector3(2.1640f, -1.3079f, 1.7600f);
		static d.Vector3 hpr = new d.Vector3(125.5000f, -17.0000f, 0.0000f);

		static d.NearCallback nearCallback = near;
		static d.ContactGeom[] contacts = new d.ContactGeom[MAX_CONTACTS];
		static d.Contact contact;


		// Called when window is opened - sets up viewpoint and prints usage
		static void start(int unused)
		{
			ds.SetViewpoint(ref xyz, ref hpr);
			Console.WriteLine("To drop another object, press:");
			Console.WriteLine("   b for box.");
			Console.WriteLine("   s for sphere.");
			Console.WriteLine("   c for capsule.");
			Console.WriteLine("   y for cylinder.");
			Console.WriteLine("   v for a convex object.");
			Console.WriteLine("   x for a composite object.");
			Console.WriteLine("To select an object, press space.");
			Console.WriteLine("To disable the selected object, press d.");
			Console.WriteLine("To enable the selected object, press e.");
			Console.WriteLine("To toggle showing the geom AABBs, press a.");
			Console.WriteLine("To toggle showing the contact points, press t.");
			Console.WriteLine("To toggle dropping from random position/orientation, press r.");
			Console.WriteLine("To save the current state to 'state.dif', press 1.");
		}


		// Near callback - creates contact joints
		static void near(IntPtr space, IntPtr g1, IntPtr g2)
		{
			IntPtr b1 = d.GeomGetBody(g1);
			IntPtr b2 = d.GeomGetBody(g2);
			if (b1 != IntPtr.Zero && b2 != IntPtr.Zero && d.AreConnectedExcluding(b1, b2, d.JointType.Contact))
				return;

			int count = d.Collide(g1, g2, MAX_CONTACTS, contacts, d.ContactGeom.SizeOf);
			for (int i = 0; i < count; ++i)
			{
				contact.geom = contacts[i];
				IntPtr joint = d.JointCreateContact(world, contactgroup, ref contact);
				d.JointAttach(joint, b1, b2);
			}
		}


		// Adds a new object to the scene - attaches a body to the geom and
		// sets the initial position and orientation
		static void addObject(IntPtr geom, d.Mass mass)
		{
			// Create a body for this object
			IntPtr body = d.BodyCreate(world);
			d.GeomSetBody(geom, body);
			d.BodySetMass(body, ref mass);
			obj.Enqueue(geom);

			// Set the position of the new object
			d.Matrix3 R;
			d.BodySetPosition(body, d.RandReal() * 2 - 1, d.RandReal() * 2 - 1, d.RandReal() + 2);
			d.RFromAxisAndAngle(out R, d.RandReal() * 2 - 1, d.RandReal() * 2 - 1, d.RandReal() * 2 - 1, d.RandReal() * 10 - 5);
			d.BodySetRotation(body, ref R);

			// Cap the total number of objects
			if (obj.Count > NUM)
			{
				geom = obj.Dequeue();
				body = d.GeomGetBody(geom);
				d.BodyDestroy(body);
				d.GeomDestroy(geom);
			}
		}


		// Keyboard callback
		static void command(int cmd)
		{
			IntPtr geom;
			d.Mass mass;
			d.Vector3 sides = new d.Vector3(d.RandReal() * 0.5f + 0.1f, d.RandReal() * 0.5f + 0.1f, d.RandReal() * 0.5f + 0.1f);

			Char ch = Char.ToLower((Char)cmd);
			switch ((Char)ch)
			{
			case 'b':
				d.MassSetBox(out mass, DENSITY, sides.X, sides.Y, sides.Z);
				geom = d.CreateBox(space, sides.X, sides.Y, sides.Z);
				addObject(geom, mass);
				break;

			case 'c':
				sides.X *= 0.5f;
				d.MassSetCapsule(out mass, DENSITY, 3, sides.X, sides.Y);
				geom = d.CreateCapsule(space, sides.X, sides.Y);
				addObject(geom, mass);
				break;

			case 'v':
				d.MassSetBox(out mass, DENSITY, 0.25f, 0.25f, 0.25f);
				geom = d.CreateConvex(space, planes, planes.Length / 4, points, points.Length / 3, polygons);
				addObject(geom, mass);
				break;
			}
		}


		// Draw an object in the scene
		static void drawGeom(IntPtr geom)
		{
			IntPtr body = d.GeomGetBody(geom);

			d.Vector3 pos;
			d.BodyCopyPosition(body, out pos);

			d.Matrix3 R;
			d.BodyCopyRotation(body, out R);

			d.GeomClassID type = d.GeomGetClass(geom);
			switch (type)
			{
			case d.GeomClassID.BoxClass:
				d.Vector3 sides;
				d.GeomBoxGetLengths(geom, out sides);
				ds.DrawBox(ref pos, ref R, ref sides);
				break;
			case d.GeomClassID.CapsuleClass:
				dReal radius, length;
				d.GeomCapsuleGetParams(geom, out radius, out length);
				ds.DrawCapsule(ref pos, ref R, length, radius);
				break;
			case d.GeomClassID.ConvexClass:
				ds.DrawConvex(ref pos, ref R, planes, planes.Length / 4, points, points.Length / 3, polygons);
				break;
			}
		}


		// Called once per frame; updates the scene
		static void step(int pause)
		{
			d.SpaceCollide(space, IntPtr.Zero, nearCallback);
			if (pause == 0)
				d.WorldQuickStep(world, 0.02f);
			d.JointGroupEmpty(contactgroup);

			ds.SetColor(1.0f, 1.0f, 0.0f);
			ds.SetTexture(ds.Texture.Wood);

			foreach (IntPtr geom in obj)
			{
				drawGeom(geom);
			}
		}


		static void Main(string[] args)
		{
			// Setup pointers to drawstuff callback functions
			ds.Functions fn;
			fn.version = ds.VERSION;
			fn.start = new ds.CallbackFunction(start);
			fn.step = new ds.CallbackFunction(step);
			fn.command = new ds.CallbackFunction(command);
			fn.stop = null;
			fn.path_to_textures = "../../../../drawstuff/textures";
			if (args.Length > 0)
			{
				fn.path_to_textures = args[0];
			}

			// Set up contact response parameters
			contact.surface.mode = d.ContactFlags.Bounce | d.ContactFlags.SoftCFM;
			contact.surface.mu = d.Infinity;
			contact.surface.mu2 = 0.0f;
			contact.surface.bounce = 0.1f;
			contact.surface.bounce_vel = 0.1f;
			contact.surface.soft_cfm = 0.01f;

			// Initialize the scene
			world = d.WorldCreate();
			space = d.HashSpaceCreate(IntPtr.Zero);
			contactgroup = d.JointGroupCreate(0);
			d.WorldSetGravity(world, 0.0f, 0.0f, -0.5f);
			d.WorldSetCFM(world, 1e-5f);
			d.WorldSetAutoDisableFlag(world, true);
			d.WorldSetContactMaxCorrectingVel(world, 0.1f);
			d.WorldSetContactSurfaceLayer(world, 0.001f);
			d.CreatePlane(space, 0, 0, 1, 0);

			// Run the scene
			ds.SimulationLoop(args.Length, args, 352, 288, ref fn);

			// Clean up
			d.JointGroupDestroy(contactgroup);
			d.SpaceDestroy(space);
			d.WorldDestroy(world);
			d.CloseODE();
		}
	}
}
