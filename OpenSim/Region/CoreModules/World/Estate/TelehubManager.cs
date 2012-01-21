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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using System;
using OpenMetaverse;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Estate
{
    public class TelehubManager
    {
        public struct Telehub
        {
            public UUID ObjectID;
            public string ObjectName;
            public Vector3 ObjectPosition;
            public Quaternion ObjectRotation;
            public List<Vector3> SpawnPoint;
        };

        private UUID ObjectID;
        private string ObjectName;
        private Vector3 ObjectPosition;
        Quaternion ObjectRotation;
        List<Vector3> SpawnPoint = new List<Vector3>();
        UUID EstateID;
        bool m_HasTelehub = false;
        Scene m_Scene;
        // This will get an option...
        Vector3 InitialSpawnPoint = new Vector3(0.0f,0.0f,-3.0f);

        public bool HasTelehub
        {
            get { return m_HasTelehub; }
        }

        public TelehubManager(Scene scene)
        {
            m_Scene = scene;
        }

        // Fill our Telehub struct with values
        public Telehub TelehubVals()
        {
            Telehub telehub = new Telehub();

            telehub.ObjectID = ObjectID;
            telehub.ObjectName = ObjectName;
            telehub.ObjectPosition = ObjectPosition;
            telehub.ObjectRotation = ObjectRotation;
            telehub.SpawnPoint = SpawnPoint;
            return telehub;
        }

        // Connect the Telehub
        public Telehub Connect(SceneObjectPart part)
        {
            ObjectID = part.UUID;
            ObjectName = part.Name;
            ObjectPosition = part.AbsolutePosition;
            ObjectRotation = part.GetWorldRotation();
            // Clear this for now
            SpawnPoint.Clear();
            SpawnPoint.Add(InitialSpawnPoint);
            m_HasTelehub = true;

            return TelehubVals();
        }

        // Disconnect the Telehub
        public Telehub DisConnect(SceneObjectPart part)
        {
            ObjectID = UUID.Zero;
            ObjectName = String.Empty;
            ObjectPosition = Vector3.Zero;
            ObjectRotation = Quaternion.Identity;
            SpawnPoint.Clear();
            m_HasTelehub = false;

            return TelehubVals();
        }

        // Add a SpawnPoint to the Telehub
        public Telehub AddSpawnPoint(Vector3 point)
        {
            float dist = (float) Util.GetDistanceTo(ObjectPosition, point);

            Vector3 nvec = Util.GetNormalizedVector(point - ObjectPosition);

            Vector3 spoint = nvec * dist;

            SpawnPoint.Add(spoint);
            return TelehubVals();
        }

        // Remove a SpawnPoint from the Telehub
        public Telehub RemoveSpawnPoint(int spawnpoint)
        {
            SpawnPoint.RemoveAt(spawnpoint);

            return TelehubVals();
        }
    }
}