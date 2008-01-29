/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using Axiom.Math;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin
{
    public class OdePrim : PhysicsActor
    {
        public PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector m_lastVelocity = new PhysicsVector(0.0f, 0.0f, 0.0f);
        private PhysicsVector m_lastposition = new PhysicsVector(0.0f, 0.0f, 0.0f);
        private PhysicsVector m_rotationalVelocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        private Quaternion _orientation;
        private PhysicsVector m_taintposition;
        private PhysicsVector m_taintsize;
        private Quaternion m_taintrot;
        private bool m_taintshape = false;
        private bool m_taintPhysics = false;
        public bool m_taintremove = false;

        private bool m_taintforce = false;
        private List<PhysicsVector> m_forcelist = new List<PhysicsVector>();

        private IMesh _mesh;
        private PrimitiveBaseShape _pbs;
        private OdeScene _parent_scene;
        public IntPtr m_targetSpace = (IntPtr) 0;
        public IntPtr prim_geom;
        public IntPtr _triMeshData;
        private bool iscolliding = false;
        private bool m_isphysical = false;
        private bool m_throttleUpdates = false;
        private int throttleCounter = 0;
        public bool outofBounds = false;
        private float m_density = 10.000006836f; // Aluminum g/cm3;


        public bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;

        public IntPtr Body = (IntPtr) 0;
        private String m_primName;
        private PhysicsVector _target_velocity;
        public d.Mass pMass;

        private int debugcounter = 0;

        public OdePrim(String primName, OdeScene parent_scene, IntPtr targetSpace, PhysicsVector pos, PhysicsVector size,
                       Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool pisPhysical)
        {
            _velocity = new PhysicsVector();
            _position = pos;
            m_taintposition = pos;
            if (_position.X > 257)
            {
                _position.X = 257;
            }
            if (_position.X < 0)
            {
                _position.X = 0;
            }
            if (_position.Y > 257)
            {
                _position.Y = 257;
            }
            if (_position.Y < 0)
            {
                _position.Y = 0;
            }

            _size = size;
            m_taintsize = _size;
            _acceleration = new PhysicsVector();
            m_rotationalVelocity = PhysicsVector.Zero;
            _orientation = rotation;
            m_taintrot = _orientation;
            _mesh = mesh;
            _pbs = pbs;

            _parent_scene = parent_scene;
            m_targetSpace = targetSpace;

            if (pos.Z < 0)
                m_isphysical = false;
            else
            {
                m_isphysical = pisPhysical;
                // If we're physical, we need to be in the master space for now.
                // linksets *should* be in a space together..  but are not currently
                if (m_isphysical)
                    m_targetSpace = _parent_scene.space;
            }
            m_primName = primName;

            lock (OdeScene.OdeLock)
            {
                if (mesh != null)
                {
                    setMesh(parent_scene, mesh);
                }
                else
                {
                    if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                        {
                            if (((_size.X / 2f) > 0f))
                            {
                                prim_geom = d.CreateSphere(m_targetSpace, _size.X / 2);
                            }
                            else
                            {
                                prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                            }
                        }
                        else
                        {
                            prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                        }
                    }
                    //else if (pbs.ProfileShape == ProfileShape.Circle && pbs.PathCurve == (byte)Extrusion.Straight)
                    //{
                        //Cyllinder
                        //if (_size.X == _size.Y)
                        //{
                            //prim_geom = d.CreateCylinder(m_targetSpace, _size.X / 2, _size.Z);
                        //}
                        //else
                        //{
                            //prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                        //}
                    //}
                    else
                    {

                        prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    }
                }

                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.W = rotation.w;
                myrot.X = rotation.x;
                myrot.Y = rotation.y;
                myrot.Z = rotation.z;
                d.GeomSetQuaternion(prim_geom, ref myrot);


                if (m_isphysical && Body == (IntPtr) 0)
                {
                    enableBody();
                }
                parent_scene.geom_name_map[prim_geom] = primName;
                parent_scene.actor_name_map[prim_geom] = (PhysicsActor) this;
                //  don't do .add() here; old geoms get recycled with the same hash
            }
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Prim; }
            set { return; }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            set { return; }
        }


        public void enableBody()
        {
            // Sets the geom to a body
            Body = d.BodyCreate(_parent_scene.world);

            setMass();
            d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);
            d.Quaternion myrot = new d.Quaternion();
            myrot.W = _orientation.w;
            myrot.X = _orientation.x;
            myrot.Y = _orientation.y;
            myrot.Z = _orientation.z;
            d.BodySetQuaternion(Body, ref myrot);
            d.GeomSetBody(prim_geom, Body);
            d.BodySetAutoDisableFlag(Body, true);
            d.BodySetAutoDisableSteps(Body, 20);

            _parent_scene.addActivePrim(this);
        }

        private float CalculateMass()
        {
            float volume = 0;

            // No material is passed to the physics engines yet..  soo..   
            // we're using the m_density constant in the class definition


            float returnMass = 0;

            switch (_pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    // Profile Volume

                    volume = _size.X*_size.Y*_size.Z;

                    // If the user has 'hollowed out' 
                    // ProfileHollow is one of those 0 to 50000 values :P
                    // we like percentages better..   so turning into a percentage

                    if (((float) _pbs.ProfileHollow/50000f) > 0.0)
                    {
                        float hollowAmount = (float) _pbs.ProfileHollow/50000f;

                        // calculate the hollow volume by it's shape compared to the prim shape
                        float hollowVolume = 0;
                        switch (_pbs.HollowShape)
                        {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                // Cube Hollow volume calculation
                                float hollowsizex = _size.X*hollowAmount;
                                float hollowsizey = _size.Y*hollowAmount;
                                float hollowsizez = _size.Z*hollowAmount;
                                hollowVolume = hollowsizex*hollowsizey*hollowsizez;
                                break;

                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation
                                float hRadius = _size.X/2;
                                float hLength = _size.Z;

                                // pi * r2 * h
                                hollowVolume = ((float) (Math.PI*Math.Pow(hRadius, 2)*hLength)*hollowAmount);
                                break;

                            case HollowShape.Triangle:
                                // Equilateral Triangular Prism volume hollow calculation
                                // Triangle is an Equilateral Triangular Prism with aLength = to _size.Y

                                float aLength = _size.Y;
                                // 1/2 abh
                                hollowVolume = (float) ((0.5*aLength*_size.X*_size.Z)*hollowAmount);
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                        }
                        volume = volume - hollowVolume;
                    }

                    break;

                default:
                    // we don't have all of the volume formulas yet so 
                    // use the common volume formula for all
                    volume = _size.X*_size.Y*_size.Z;
                    break;
            }

            // Calculate Path cut effect on volume
            // Not exact, in the triangle hollow example
            // They should never be zero or less then zero..   
            // we'll ignore it if it's less then zero

            // ProfileEnd and ProfileBegin are values
            // from 0 to 50000

            // Turning them back into percentages so that I can cut that percentage off the volume

            float PathCutEndAmount = _pbs.ProfileEnd;
            float PathCutStartAmount = _pbs.ProfileBegin;
            if (((PathCutStartAmount + PathCutEndAmount)/50000f) > 0.0f)
            {
                float pathCutAmount = ((PathCutStartAmount + PathCutEndAmount)/50000f);

                // Check the return amount for sanity
                if (pathCutAmount >= 0.99f)
                    pathCutAmount = 0.99f;

                volume = volume - (volume*pathCutAmount);
            }

            // Mass = density * volume

            returnMass = m_density*volume;

            return returnMass;
        }

        public void setMass()
        {
            if (Body != (IntPtr) 0)
            {
                d.MassSetBoxTotal(out pMass, CalculateMass(), _size.X, _size.Y, _size.Z);
                d.BodySetMass(Body, ref pMass);
            }
        }


        public void disableBody()
        {
            //this kills the body so things like 'mesh' can re-create it.
            if (Body != (IntPtr) 0)
            {
                _parent_scene.remActivePrim(this);
                d.BodyDestroy(Body);
                Body = (IntPtr) 0;
            }
        }

        public void setMesh(OdeScene parent_scene, IMesh mesh)
        {
            //Kill Body so that mesh can re-make the geom
            if (IsPhysical && Body != (IntPtr) 0)
            {
                disableBody();
            }
            float[] vertexList = mesh.getVertexListAsFloatLocked(); // Note, that vertextList is pinned in memory
            int[] indexList = mesh.getIndexListAsIntLocked(); // Also pinned, needs release after usage
            int VertexCount = vertexList.GetLength(0)/3;
            int IndexCount = indexList.GetLength(0);

            _triMeshData = d.GeomTriMeshDataCreate();

            d.GeomTriMeshDataBuildSimple(_triMeshData, vertexList, 3*sizeof (float), VertexCount, indexList, IndexCount,
                                         3*sizeof (int));
            d.GeomTriMeshDataPreprocess(_triMeshData);

            prim_geom = d.CreateTriMesh(m_targetSpace, _triMeshData, parent_scene.triCallback, null, null);

            if (IsPhysical && Body == (IntPtr) 0)
            {
                // Recreate the body
                enableBody();
            }
        }

        public void ProcessTaints(float timestep)
        {
            if (m_taintposition != _position)
                Move(timestep);

            if (m_taintrot != _orientation)
                rotate(timestep);
            //

            if (m_taintPhysics != m_isphysical)
                changePhysicsStatus(timestep);
            //

            if (m_taintsize != _size)
                changesize(timestep);
            //

            if (m_taintshape)
                changeshape(timestep);
            //

            if (m_taintforce)
                changeAddForce(timestep);
        }

        public void Move(float timestep)
        {
            if (m_isphysical)
            {
                // This is a fallback..   May no longer be necessary.
                if (Body == (IntPtr) 0)
                    enableBody();
                //Prim auto disable after 20 frames, 
                ///if you move it, re-enable the prim manually.
                d.BodyEnable(Body);
                d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);
            }
            else
            {
                string primScenAvatarIn = _parent_scene.whichspaceamIin(_position);
                int[] arrayitem = _parent_scene.calculateSpaceArrayItemFromPos(_position);
                m_targetSpace = _parent_scene.recalculateSpaceForGeom(prim_geom, _position, m_targetSpace);
                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.SpaceAdd(m_targetSpace, prim_geom);
            }

            m_taintposition = _position;
        }

        public void rotate(float timestep)
        {
            d.Quaternion myrot = new d.Quaternion();
            myrot.W = _orientation.w;
            myrot.X = _orientation.x;
            myrot.Y = _orientation.y;
            myrot.Z = _orientation.z;
            d.GeomSetQuaternion(prim_geom, ref myrot);
            if (m_isphysical && Body != (IntPtr) 0)
            {
                d.BodySetQuaternion(Body, ref myrot);
            }

            m_taintrot = _orientation;
        }

        public void changePhysicsStatus(float timestap)
        {
            if (m_isphysical == true)
            {
                if (Body == (IntPtr) 0)
                {
                    enableBody();
                }
            }
            else
            {
                if (Body != (IntPtr) 0)
                {
                    disableBody();
                }
            }


            m_taintPhysics = m_isphysical;
        }

        public void changesize(float timestamp)
        {
            string oldname = _parent_scene.geom_name_map[prim_geom];

            // Cleanup of old prim geometry
            if (_mesh != null)
            {
                // Cleanup meshing here
            }
            //kill body to rebuild 
            if (IsPhysical && Body != (IntPtr) 0)
            {
                disableBody();
            }
            if (d.SpaceQuery(m_targetSpace, prim_geom))
            {
                d.SpaceRemove(m_targetSpace, prim_geom);
            }
            d.GeomDestroy(prim_geom);

            // we don't need to do space calculation because the client sends a position update also.

            // Construction of new prim
            if (_parent_scene.needsMeshing(_pbs))
            {
                // Don't need to re-enable body..   it's done in SetMesh
                IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size);
                // createmesh returns null when it's a shape that isn't a cube.
                if (mesh != null)
                {
                    setMesh(_parent_scene, mesh);
                }
                else
                {
                    if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                        {
                            if (((_size.X / 2f) > 0f) && ((_size.X / 2f) < 1000))
                            {
                                prim_geom = d.CreateSphere(m_targetSpace, _size.X / 2);
                            }
                            else
                            {
                                OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS", "Failed to load a sphere bad size");
                                prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                            }

                        }
                        else
                        {
                            prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                        }
                    }
                    //else if (_pbs.ProfileShape == ProfileShape.Circle && _pbs.PathCurve == (byte)Extrusion.Straight)
                    //{
                        //Cyllinder
                        //if (_size.X == _size.Y)
                        //{
                        //    prim_geom = d.CreateCylinder(m_targetSpace, _size.X / 2, _size.Z);
                        //}
                        //else
                        //{
                            //prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                        //}
                    //}
                    else
                    {

                        prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    }
                    //prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                    d.Quaternion myrot = new d.Quaternion();
                    myrot.W = _orientation.w;
                    myrot.X = _orientation.x;
                    myrot.Y = _orientation.y;
                    myrot.Z = _orientation.z;
                    d.GeomSetQuaternion(prim_geom, ref myrot);
                }
            }
            else
            {
                if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                    {
                        prim_geom = d.CreateSphere(m_targetSpace, _size.X / 2);
                    }
                    else
                    {
                        prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    }
                }
                //else if (_pbs.ProfileShape == ProfileShape.Circle && _pbs.PathCurve == (byte)Extrusion.Straight)
                //{
                    //Cyllinder
                    //if (_size.X == _size.Y)
                    //{
                        //prim_geom = d.CreateCylinder(m_targetSpace, _size.X / 2, _size.Z);
                    //}
                    //else
                    //{
                        //prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    //}
                //}
                else
                {

                    prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                }
                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.W = _orientation.w;
                myrot.X = _orientation.x;
                myrot.Y = _orientation.y;
                myrot.Z = _orientation.z;
                d.GeomSetQuaternion(prim_geom, ref myrot);


                //d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
                if (IsPhysical && Body == (IntPtr) 0)
                {
                    // Re creates body on size.
                    // EnableBody also does setMass()
                    enableBody();
                    d.BodyEnable(Body);
                }
            }

            _parent_scene.geom_name_map[prim_geom] = oldname;

            m_taintsize = _size;
        }

        public void changeshape(float timestamp)
        {
            string oldname = _parent_scene.geom_name_map[prim_geom];

            // Cleanup of old prim geometry and Bodies
            if (IsPhysical && Body != (IntPtr) 0)
            {
                disableBody();
            }
            d.GeomDestroy(prim_geom);
            if (_mesh != null)
            {
                d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
            }

            // Construction of new prim
            if (_parent_scene.needsMeshing(_pbs))
            {
                IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size);
                if (mesh != null)
                {
                    setMesh(_parent_scene, mesh);
                }
                else
                {
                    prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                }
            }
            else
            {
                prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
            }
            if (IsPhysical && Body == (IntPtr) 0)
            {
                //re-create new body
                enableBody();
            }
            else
            {
                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.W = _orientation.w;
                myrot.X = _orientation.x;
                myrot.Y = _orientation.y;
                myrot.Z = _orientation.z;
                d.GeomSetQuaternion(prim_geom, ref myrot);
            }
            _parent_scene.geom_name_map[prim_geom] = oldname;

            m_taintshape = false;
        }

        public void changeAddForce(float timestamp)
        {
            lock (m_forcelist)
            {
                //OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS", "dequeing forcelist");
                if (IsPhysical)
                {
                    PhysicsVector iforce = new PhysicsVector();
                    for (int i = 0; i < m_forcelist.Count; i++)
                    {
                        iforce = iforce + (m_forcelist[i]*100);
                    }   
                    d.BodyEnable(Body);
                    d.BodyAddForce(Body, iforce.X, iforce.Y, iforce.Z);
                }
                m_forcelist.Clear();
            }
            m_taintforce = false;

        }

        public override bool IsPhysical
        {
            get { return m_isphysical; }
            set { m_isphysical = value; }
        }

        public void setPrimForRemoval()
        {
            m_taintremove = true;
        }

        public override bool Flying
        {
            get { return false; //no flying prims for you
            }
            set { }
        }

        public override bool IsColliding
        {
            get { return iscolliding; }
            set { iscolliding = value; }
        }

        public override bool CollidingGround
        {
            get { return false; }
            set { return; }
        }

        public override bool CollidingObj
        {
            get { return false; }
            set { return; }
        }

        public override bool ThrottleUpdates
        {
            get { return m_throttleUpdates; }
            set { m_throttleUpdates = value; }
        }

        public override PhysicsVector Position
        {
            get { return _position; }

            set { _position = value; 
                //OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS", _position.ToString());
            }
        }

        public override PhysicsVector Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public override float Mass
        {
            get { return CalculateMass(); }
        }

        public override PhysicsVector Force
        {
            get { return PhysicsVector.Zero; }
        }

        public override PhysicsVector CenterOfMass
        {
            get { return PhysicsVector.Zero; }
        }

        public override PhysicsVector GeometricCenter
        {
            get { return PhysicsVector.Zero; }
        }

        public override PrimitiveBaseShape Shape
        {
            set { _pbs = value; }
        }

        public override PhysicsVector Velocity
        {
            get
            {
                // Averate previous velocity with the new one so 
                // client object interpolation works a 'little' better
                PhysicsVector returnVelocity = new PhysicsVector();
                returnVelocity.X = (m_lastVelocity.X + _velocity.X)/2;
                returnVelocity.Y = (m_lastVelocity.Y + _velocity.Y)/2;
                returnVelocity.Z = (m_lastVelocity.Z + _velocity.Z)/2;
                return returnVelocity;
            }
            set { _velocity = value; }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override Quaternion Orientation
        {
            get { return _orientation; }
            set { _orientation = value; }
        }

        public override PhysicsVector Acceleration
        {
            get { return _acceleration; }
        }


        public void SetAcceleration(PhysicsVector accel)
        {
            _acceleration = accel;
        }

        public override void AddForce(PhysicsVector force)
        {
            m_forcelist.Add(force);
            m_taintforce = true;
            //OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS", "Added Force:" + force.ToString() +  " to prim at " + Position.ToString());
        }

        public override PhysicsVector RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }

        public void UpdatePositionAndVelocity()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!

            if (Body != (IntPtr) 0)
            {
                d.Vector3 vec = d.BodyGetPosition(Body);
                d.Quaternion ori = d.BodyGetQuaternion(Body);
                d.Vector3 vel = d.BodyGetLinearVel(Body);
                d.Vector3 rotvel = d.BodyGetAngularVel(Body);

                PhysicsVector l_position = new PhysicsVector();
                //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
                if (vec.X < 0.0f) vec.X = 0.0f;
                if (vec.Y < 0.0f) vec.Y = 0.0f;
                if (vec.X > 255.95f) vec.X = 255.95f;
                if (vec.Y > 255.95f) vec.Y = 255.95f;
                m_lastposition = _position;

                l_position.X = vec.X;
                l_position.Y = vec.Y;
                l_position.Z = vec.Z;
                if (l_position.Z < 0)
                {
                    // This is so prim that get lost underground don't fall forever and suck up 
                    // 
                    // Sim resources and memory.
                    // Disables the prim's movement physics....  
                    // It's a hack and will generate a console message if it fails.


                    //IsPhysical = false;
                    base.RaiseOutOfBounds(_position);
                    _velocity.X = 0;
                    _velocity.Y = 0;
                    _velocity.Z = 0;
                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;
                    base.RequestPhysicsterseUpdate();
                    m_throttleUpdates = false;
                    throttleCounter = 0;
                    _zeroFlag = true;
                    //outofBounds = true;
                }

                if ((Math.Abs(m_lastposition.X - l_position.X) < 0.02)
                    && (Math.Abs(m_lastposition.Y - l_position.Y) < 0.02)
                    && (Math.Abs(m_lastposition.Z - l_position.Z) < 0.02))
                {
                    _zeroFlag = true;
                }
                else
                {
                    //System.Console.WriteLine(Math.Abs(m_lastposition.X - l_position.X).ToString());
                    _zeroFlag = false;
                }


                if (_zeroFlag)
                {
                    // Supposedly this is supposed to tell SceneObjectGroup that 
                    // no more updates need to be sent..  
                    // but it seems broken.
                    _velocity.X = 0.0f;
                    _velocity.Y = 0.0f;
                    _velocity.Z = 0.0f;
                    //_orientation.w = 0f;
                    //_orientation.x = 0f;
                    //_orientation.y = 0f;
                    //_orientation.z = 0f;
                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;
                    if (!m_lastUpdateSent)
                    {
                        m_throttleUpdates = false;
                        throttleCounter = 0;
                        m_rotationalVelocity.X = 0;
                        m_rotationalVelocity.Y = 0;
                        m_rotationalVelocity.Z = 0;
                        base.RequestPhysicsterseUpdate();
                        m_lastUpdateSent = true;
                    }
                }
                else
                {
                    m_lastVelocity = _velocity;

                    _position = l_position;

                    _velocity.X = vel.X;
                    _velocity.Y = vel.Y;
                    _velocity.Z = vel.Z;

                    m_rotationalVelocity.X = rotvel.X;
                    m_rotationalVelocity.Y = rotvel.Y;
                    m_rotationalVelocity.Z = rotvel.Z;
                    //System.Console.WriteLine("ODE: " + m_rotationalVelocity.ToString());
                    _orientation.w = ori.W;
                    _orientation.x = ori.X;
                    _orientation.y = ori.Y;
                    _orientation.z = ori.Z;
                    m_lastUpdateSent = false;
                    if (!m_throttleUpdates || throttleCounter > 15)
                    {
                        base.RequestPhysicsterseUpdate();
                    }
                    else
                    {
                        throttleCounter++;
                    }
                }
                m_lastposition = l_position;
            }
            else
            {
                // Not a body..   so Make sure the client isn't interpolating
                _velocity.X = 0;
                _velocity.Y = 0;
                _velocity.Z = 0;
                m_rotationalVelocity.X = 0;
                m_rotationalVelocity.Y = 0;
                m_rotationalVelocity.Z = 0;
                _zeroFlag = true;
            }
        }

        public override void SetMomentum(PhysicsVector momentum)
        {
        }
    }
}