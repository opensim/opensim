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
using System.Drawing;
using OpenMetaverse;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Object;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    [Serializable]
    public class TouchEventArgs : EventArgs
    {
        public IAvatar Avatar;

        public Vector3 TouchBiNormal;
        public Vector3 TouchNormal;
        public Vector3 TouchPosition;

        public Vector2 TouchUV;
        public Vector2 TouchST;

        public int TouchMaterialIndex;
    }

    public delegate void OnTouchDelegate(IObject sender, TouchEventArgs e);

    public interface IObject : IEntity
    {
        #region Events

        event OnTouchDelegate OnTouch;

        #endregion

        /// <summary>
        /// Returns whether or not this object is still in the world.
        /// Eg, if you store an IObject reference, however the object
        /// is deleted before you use it, it will throw a NullReference
        /// exception. 'Exists' allows you to check the object is still
        /// in play before utilizing it.
        /// </summary>
        /// <example>
        /// IObject deleteMe = World.Objects[0];
        ///
        /// if (deleteMe.Exists) {
        ///     deleteMe.Say("Hello, I still exist!");
        /// }
        ///
        /// World.Objects.Remove(deleteMe);
        ///
        /// if (!deleteMe.Exists) {
        ///     Host.Console.Info("I was deleted");
        /// }
        /// </example>
        /// <remarks>
        /// Objects should be near-guarunteed to exist for any event which
        /// passes them as an argument. Storing an object for a longer period
        /// of time however will limit their reliability.
        ///
        /// It is a good practice to use Try/Catch blocks handling for
        /// NullReferenceException, when accessing remote objects.
        /// </remarks>
        bool Exists { get; }

        /// <summary>
        /// The local region-unique ID for this object.
        /// </summary>
        uint LocalID { get; }

        /// <summary>
        /// The description assigned to this object.
        /// </summary>
        String Description { get; set; }

        /// <summary>
        /// Returns the UUID of the Owner of the Object.
        /// </summary>
        UUID OwnerId { get; }

        /// <summary>
        /// Returns the UUID of the Creator of the Object.
        /// </summary>
        UUID CreatorId { get; }

        /// <summary>
        /// Returns the root object of a linkset. If this object is the root, it will return itself.
        /// </summary>
        IObject Root { get; }

        /// <summary>
        /// Returns a collection of objects which are linked to the current object. Does not include the root object.
        /// </summary>
        IObject[] Children { get; }

        /// <summary>
        /// Returns a list of materials attached to this object. Each may contain unique texture
        /// and other visual information. For primitive based objects, this correlates with
        /// Object Faces. For mesh based objects, this correlates with Materials.
        /// </summary>
        IObjectMaterial[] Materials { get; }

        /// <summary>
        /// The bounding box of the object. Primitive and Mesh objects alike are scaled to fit within these bounds.
        /// </summary>
        Vector3 Scale { get; set; }

        /// <summary>
        /// The rotation of the object relative to the Scene
        /// </summary>
        Quaternion WorldRotation { get; set; }

        /// <summary>
        /// The rotation of the object relative to a parent object
        /// If root, works the same as WorldRotation
        /// </summary>
        Quaternion OffsetRotation { get; set; }

        /// <summary>
        /// The position of the object relative to a parent object
        /// If root, works the same as WorldPosition
        /// </summary>
        Vector3 OffsetPosition { get; set; }

        Vector3 SitTarget { get; set; }
        String SitTargetText { get; set; }

        String TouchText { get; set; }

        /// <summary>
        /// Text to be associated with this object, in the
        /// Second Life(r) viewer, this is shown above the
        /// object.
        /// </summary>
        String Text { get; set; }

        bool IsRotationLockedX { get; set; } // SetStatus(!ROTATE_X)
        bool IsRotationLockedY { get; set; } // SetStatus(!ROTATE_Y)
        bool IsRotationLockedZ { get; set; } // SetStatus(!ROTATE_Z)
        bool IsSandboxed { get; set; } // SetStatus(SANDBOX)
        bool IsImmotile { get; set; } // SetStatus(BLOCK_GRAB)
        bool IsAlwaysReturned { get; set; } // SetStatus(!DIE_AT_EDGE)
        bool IsTemporary { get; set; } // TEMP_ON_REZ

        bool IsFlexible { get; set; }

        IObjectShape Shape { get; }

        // TODO:
        // PrimHole
        // Repeats, Offsets, Cut/Dimple/ProfileCut
        // Hollow, Twist, HoleSize,
        // Taper[A+B], Shear[A+B], Revolutions,
        // RadiusOffset, Skew

        PhysicsMaterial PhysicsMaterial { get; set; }

        IObjectPhysics Physics { get; }

        IObjectSound Sound { get; }

        /// <summary>
        /// Causes the object to speak to its surroundings,
        /// equivilent to LSL/OSSL llSay
        /// </summary>
        /// <param name="msg">The message to send to the user</param>
        void Say(string msg);

        /// <summary>
        /// Causes the object to speak to on a specific channel,
        /// equivilent to LSL/OSSL llSay
        /// </summary>
        /// <param name="msg">The message to send to the user</param>
        /// <param name="channel">The channel on which to send the message</param>
        void Say(string msg,int channel);

        /// <summary>
        /// Opens a Dialog Panel in the Users Viewer,
        /// equivilent to LSL/OSSL llDialog
        /// </summary>
        /// <param name="avatar">The UUID of the Avatar to which the Dialog should be send</param>
        /// <param name="message">The Message to display at the top of the Dialog</param>
        /// <param name="buttons">The Strings that act as label/value of the Bottons in the Dialog</param>
        /// <param name="chat_channel">The channel on which to send the response</param>
        void Dialog(UUID avatar, string message, string[] buttons, int chat_channel);

        //// <value>
        /// Grants access to the objects inventory
        /// </value>
        IObjectInventory Inventory { get; }
    }

    public enum PhysicsMaterial
    {
        Default,
        Glass,
        Metal,
        Plastic,
        Wood,
        Rubber,
        Stone,
        Flesh
    }

    public enum TextureMapping
    {
        Default,
        Planar
    }

    public interface IObjectMaterial
    {
        Color Color { get; set; }
        UUID Texture { get; set; }
        TextureMapping Mapping { get; set; } // SetPrimParms(PRIM_TEXGEN)
        bool Bright { get; set; } // SetPrimParms(FULLBRIGHT)
        double Bloom { get; set; } // SetPrimParms(GLOW)
        bool Shiny { get; set; } // SetPrimParms(SHINY)
        bool BumpMap { get; set; } // SetPrimParms(BUMPMAP) [DEPRECATE IN FAVOUR OF UUID?]
    }
}
