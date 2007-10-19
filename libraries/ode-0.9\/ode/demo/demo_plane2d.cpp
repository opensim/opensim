// Test my Plane2D constraint.
// Uses ode-0.35 collision API.

# include       <stdio.h>
# include       <stdlib.h>
# include       <math.h>
# include       <ode/ode.h>
# include       <drawstuff/drawstuff.h>

#   define drand48()  ((double) (((double) rand()) / ((double) RAND_MAX)))

# define        N_BODIES        40
# define        STAGE_SIZE      8.0  // in m

# define        TIME_STEP       0.01
# define        K_SPRING        10.0
# define        K_DAMP          10.0


static dWorld   dyn_world;
static dBody    dyn_bodies[N_BODIES];
static dReal    bodies_sides[N_BODIES][3];

static dSpaceID coll_space_id;
static dJointID plane2d_joint_ids[N_BODIES];
static dJointGroup
                coll_contacts;



static void     cb_start ()
/*************************/
{
    static float    xyz[3] = { 0.5f*STAGE_SIZE, 0.5f*STAGE_SIZE, 0.65f*STAGE_SIZE};
    static float    hpr[3] = { 90.0f, -90.0f, 0 };

    dsSetViewpoint (xyz, hpr);
}



static void     cb_near_collision (void *data, dGeomID o1, dGeomID o2)
/********************************************************************/
{
    dBodyID     b1 = dGeomGetBody (o1);
    dBodyID     b2 = dGeomGetBody (o2);
    dContact    contact;


    // exit without doing anything if the two bodies are static
    if (b1 == 0 && b2 == 0)
        return;

    // exit without doing anything if the two bodies are connected by a joint
    if (b1 && b2 && dAreConnected (b1, b2))
    {
        /* MTRAP; */
        return;
    }

    contact.surface.mode = 0;
    contact.surface.mu = 0; // frictionless

    if (dCollide (o1, o2, 1, &contact.geom, sizeof (dContactGeom)))
    {
        dJointID c = dJointCreateContact (dyn_world.id(),
                        coll_contacts.id (), &contact);
        dJointAttach (c, b1, b2);
    }
}


static void     track_to_pos (dBody &body, dJointID joint_id,
                              dReal target_x, dReal target_y)
/************************************************************************/
{
    dReal  curr_x = body.getPosition()[0];
    dReal  curr_y = body.getPosition()[1];

    dJointSetPlane2DXParam (joint_id, dParamVel, 1 * (target_x - curr_x));
    dJointSetPlane2DYParam (joint_id, dParamVel, 1 * (target_y - curr_y));
}



static void     cb_sim_step (int pause)
/*************************************/
{
    if (! pause)
    {
        static dReal angle = 0;

        angle += REAL( 0.01 );

        track_to_pos (dyn_bodies[0], plane2d_joint_ids[0],
            dReal( STAGE_SIZE/2 + STAGE_SIZE/2.0 * cos (angle) ),
            dReal( STAGE_SIZE/2 + STAGE_SIZE/2.0 * sin (angle) ));

        /* double   f0 = 0.001; */
        /* for (int b = 0; b < N_BODIES; b ++) */
        /* { */
            /* double   p = 1 + b / (double) N_BODIES; */
            /* double   q = 2 - b / (double) N_BODIES; */
            /* dyn_bodies[b].addForce (f0 * cos (p*angle), f0 * sin (q*angle), 0); */
        /* } */
        /* dyn_bodies[0].addTorque (0, 0, 0.1); */

        const int n = 10;
        for (int i = 0; i < n; i ++)
        {
            dSpaceCollide (coll_space_id, 0, &cb_near_collision);
            dyn_world.step (dReal(TIME_STEP/n));
            coll_contacts.empty ();
        }
    }

# if 1  /* [ */
    {
        // @@@ hack Plane2D constraint error reduction here:
        for (int b = 0; b < N_BODIES; b ++)
        {
            const dReal     *rot = dBodyGetAngularVel (dyn_bodies[b].id());
            const dReal     *quat_ptr;
            dReal           quat[4],
                            quat_len;


            quat_ptr = dBodyGetQuaternion (dyn_bodies[b].id());
            quat[0] = quat_ptr[0];
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = quat_ptr[3];
            quat_len = sqrt (quat[0] * quat[0] + quat[3] * quat[3]);
            quat[0] /= quat_len;
            quat[3] /= quat_len;
            dBodySetQuaternion (dyn_bodies[b].id(), quat);
            dBodySetAngularVel (dyn_bodies[b].id(), 0, 0, rot[2]);
        }
    }
# endif  /* ] */


# if 0  /* [ */
    {
        // @@@ friction
        for (int b = 0; b < N_BODIES; b ++)
        {
            const dReal *vel = dBodyGetLinearVel (dyn_bodies[b].id()),
                        *rot = dBodyGetAngularVel (dyn_bodies[b].id());
            dReal       s = 1.00;
            dReal       t = 0.99;

            dBodySetLinearVel (dyn_bodies[b].id(), s*vel[0],s*vel[1],s*vel[2]);
            dBodySetAngularVel (dyn_bodies[b].id(),t*rot[0],t*rot[1],t*rot[2]);
        }
    }
# endif  /* ] */


    {
        // ode  drawstuff

        dsSetTexture (DS_WOOD);
        for (int b = 0; b < N_BODIES; b ++)
        {
            if (b == 0)
            dsSetColor (1.0, 0.5, 1.0);
            else
            dsSetColor (0, 0.5, 1.0);
#ifdef dDOUBLE
            dsDrawBoxD (dyn_bodies[b].getPosition(), dyn_bodies[b].getRotation(), bodies_sides[b]);
#else
            dsDrawBox (dyn_bodies[b].getPosition(), dyn_bodies[b].getRotation(), bodies_sides[b]);
#endif
        }
    }
}



extern int      main
/******************/
(
    int         argc,
    char        **argv
)
{
    int         b;
    dsFunctions drawstuff_functions;


	 dInitODE();

    // dynamic world

    dReal  cf_mixing;// = 1 / TIME_STEP * K_SPRING + K_DAMP;
    dReal  err_reduct;// = TIME_STEP * K_SPRING * cf_mixing;
    err_reduct = REAL( 0.5 );
    cf_mixing = REAL( 0.001 );
    dWorldSetERP (dyn_world.id (), err_reduct);
    dWorldSetCFM (dyn_world.id (), cf_mixing);
    dyn_world.setGravity (0, 0.0, -1.0);

    coll_space_id = dSimpleSpaceCreate (0);

    // dynamic bodies
    for (b = 0; b < N_BODIES; b ++)
    {
        int     l = (int) (1 + sqrt ((double) N_BODIES));
        dReal  x = dReal( (0.5 + (b / l)) / l * STAGE_SIZE );
        dReal  y = dReal( (0.5 + (b % l)) / l * STAGE_SIZE );
        dReal  z = REAL( 1.0 ) + REAL( 0.1 ) * (dReal)drand48();

        bodies_sides[b][0] = dReal( 5 * (0.2 + 0.7*drand48()) / sqrt((double)N_BODIES) );
        bodies_sides[b][1] = dReal( 5 * (0.2 + 0.7*drand48()) / sqrt((double)N_BODIES) );
        bodies_sides[b][2] = z;

        dyn_bodies[b].create (dyn_world);
        dyn_bodies[b].setPosition (x, y, z/2);
        dyn_bodies[b].setData ((void*) (size_t)b);
        dBodySetLinearVel (dyn_bodies[b].id (),
            dReal( 3 * (drand48 () - 0.5) ), 
			dReal( 3 * (drand48 () - 0.5) ), 0);

        dMass m;
        m.setBox (1, bodies_sides[b][0],bodies_sides[b][1],bodies_sides[b][2]);
        m.adjust (REAL(0.1) * bodies_sides[b][0] * bodies_sides[b][1]);
        dyn_bodies[b].setMass (&m);

        plane2d_joint_ids[b] = dJointCreatePlane2D (dyn_world.id (), 0);
        dJointAttach (plane2d_joint_ids[b], dyn_bodies[b].id (), 0);
    }

    dJointSetPlane2DXParam (plane2d_joint_ids[0], dParamFMax, 10);
    dJointSetPlane2DYParam (plane2d_joint_ids[0], dParamFMax, 10);


    // collision geoms and joints
    dCreatePlane (coll_space_id,  1, 0, 0, 0);
    dCreatePlane (coll_space_id, -1, 0, 0, -STAGE_SIZE);
    dCreatePlane (coll_space_id,  0,  1, 0, 0);
    dCreatePlane (coll_space_id,  0, -1, 0, -STAGE_SIZE);

    for (b = 0; b < N_BODIES; b ++)
    {
        dGeomID coll_box_id;
        coll_box_id = dCreateBox (coll_space_id,
            bodies_sides[b][0], bodies_sides[b][1], bodies_sides[b][2]);
        dGeomSetBody (coll_box_id, dyn_bodies[b].id ());
    }

    coll_contacts.create (0);

    {
        // simulation loop (by drawstuff lib)
        drawstuff_functions.version = DS_VERSION;
        drawstuff_functions.start = &cb_start;
        drawstuff_functions.step = &cb_sim_step;
        drawstuff_functions.command = 0;
        drawstuff_functions.stop = 0;
        drawstuff_functions.path_to_textures = "../../drawstuff/textures";

        dsSimulationLoop (argc, argv, 352,288,&drawstuff_functions);
    }

	 dCloseODE();
    return 0;
}
