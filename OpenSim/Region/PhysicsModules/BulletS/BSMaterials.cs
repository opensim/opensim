/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Nini.Config;

namespace OpenSim.Region.PhysicsModule.BulletS
{

public struct MaterialAttributes
{
    // Material type values that correspond with definitions for LSL
    public enum Material : int
    {
        Stone = 0,
        Metal,
        Glass,
        Wood,
        Flesh,
        Plastic,
        Rubber,
        Light,
        // Hereafter are BulletSim additions
        Avatar,
        NumberOfTypes   // the count of types in the enum.
    }

    // Names must be in the order of the above enum.
    // These names must coorespond to the lower case field names in the MaterialAttributes
    //     structure as reflection is used to select the field to put the value in.
    public static readonly string[] MaterialAttribs = { "Density", "Friction", "Restitution"};

    public MaterialAttributes(string t, float d, float f, float r)
    {
        type = t;
        density = d;
        friction = f;
        restitution = r;
    }
    public string type;
    public float density;
    public float friction;
    public float restitution;
}

public static class BSMaterials
{
    // Attributes for each material type
    private static readonly MaterialAttributes[] Attributes;

    // Map of material name to material type code
    public static readonly Dictionary<string, MaterialAttributes.Material> MaterialMap;

    static BSMaterials()
    {
        // Attribute sets for both the non-physical and physical instances of materials.
        Attributes = new MaterialAttributes[(int)MaterialAttributes.Material.NumberOfTypes * 2];

        // Map of name to type code.
        MaterialMap = new Dictionary<string, MaterialAttributes.Material>();
        MaterialMap.Add("Stone", MaterialAttributes.Material.Stone);
        MaterialMap.Add("Metal", MaterialAttributes.Material.Metal);
        MaterialMap.Add("Glass", MaterialAttributes.Material.Glass);
        MaterialMap.Add("Wood", MaterialAttributes.Material.Wood);
        MaterialMap.Add("Flesh", MaterialAttributes.Material.Flesh);
        MaterialMap.Add("Plastic", MaterialAttributes.Material.Plastic);
        MaterialMap.Add("Rubber", MaterialAttributes.Material.Rubber);
        MaterialMap.Add("Light", MaterialAttributes.Material.Light);
        MaterialMap.Add("Avatar", MaterialAttributes.Material.Avatar);
    }

    // This is where all the default material attributes are defined.
    public static void InitializeFromDefaults(ConfigurationParameters parms)
    {
        // Values from http://wiki.secondlife.com/wiki/PRIM_MATERIAL
        float dDensity = parms.defaultDensity;
        float dFriction = parms.defaultFriction;
        float dRestitution = parms.defaultRestitution;
        Attributes[(int)MaterialAttributes.Material.Stone] =
                    new MaterialAttributes("stone",dDensity, 0.8f, 0.4f);
        Attributes[(int)MaterialAttributes.Material.Metal] =
                    new MaterialAttributes("metal",dDensity, 0.3f, 0.4f);
        Attributes[(int)MaterialAttributes.Material.Glass] =
                    new MaterialAttributes("glass",dDensity, 0.2f, 0.7f);
        Attributes[(int)MaterialAttributes.Material.Wood] =
                    new MaterialAttributes("wood",dDensity, 0.6f, 0.5f);
        Attributes[(int)MaterialAttributes.Material.Flesh] =
                    new MaterialAttributes("flesh",dDensity, 0.9f, 0.3f);
        Attributes[(int)MaterialAttributes.Material.Plastic] =
                    new MaterialAttributes("plastic",dDensity, 0.4f, 0.7f);
        Attributes[(int)MaterialAttributes.Material.Rubber] =
                    new MaterialAttributes("rubber",dDensity, 0.9f, 0.9f);
        Attributes[(int)MaterialAttributes.Material.Light] =
                    new MaterialAttributes("light",dDensity, dFriction, dRestitution);
        Attributes[(int)MaterialAttributes.Material.Avatar] =
                    new MaterialAttributes("avatar",3.5f, 0.2f, 0f);

        Attributes[(int)MaterialAttributes.Material.Stone + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("stonePhysical",dDensity, 0.8f, 0.4f);
        Attributes[(int)MaterialAttributes.Material.Metal + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("metalPhysical",dDensity, 0.3f, 0.4f);
        Attributes[(int)MaterialAttributes.Material.Glass + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("glassPhysical",dDensity, 0.2f, 0.7f);
        Attributes[(int)MaterialAttributes.Material.Wood + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("woodPhysical",dDensity, 0.6f, 0.5f);
        Attributes[(int)MaterialAttributes.Material.Flesh + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("fleshPhysical",dDensity, 0.9f, 0.3f);
        Attributes[(int)MaterialAttributes.Material.Plastic + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("plasticPhysical",dDensity, 0.4f, 0.7f);
        Attributes[(int)MaterialAttributes.Material.Rubber + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("rubberPhysical",dDensity, 0.9f, 0.9f);
        Attributes[(int)MaterialAttributes.Material.Light + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("lightPhysical",dDensity, dFriction, dRestitution);
        Attributes[(int)MaterialAttributes.Material.Avatar + (int)MaterialAttributes.Material.NumberOfTypes] =
                    new MaterialAttributes("avatarPhysical",3.5f, 0.2f, 0f);
    }

    // Under the [BulletSim] section, one can change the individual material
    //    attribute values. The format of the configuration parameter is:
    //        <materialName><Attribute>["Physical"] = floatValue
    //    For instance:
    //        [BulletSim]
    //             StoneFriction = 0.2
    //             FleshRestitutionPhysical = 0.8
    // Materials can have different parameters for their static and
    //    physical instantiations. When setting the non-physical value,
    //    both values are changed. Setting the physical value only changes
    //    the physical value.
    public static void InitializefromParameters(IConfig pConfig)
    {
        foreach (KeyValuePair<string, MaterialAttributes.Material> kvp in MaterialMap)
        {
            string matName = kvp.Key;
            foreach (string attribName in MaterialAttributes.MaterialAttribs)
            {
                string paramName = matName + attribName;
                if (pConfig.Contains(paramName))
                {
                    float paramValue = pConfig.GetFloat(paramName);
                    SetAttributeValue((int)kvp.Value, attribName, paramValue);
                    // set the physical value also
                    SetAttributeValue((int)kvp.Value + (int)MaterialAttributes.Material.NumberOfTypes, attribName, paramValue);
                }
                paramName += "Physical";
                if (pConfig.Contains(paramName))
                {
                    float paramValue = pConfig.GetFloat(paramName);
                    SetAttributeValue((int)kvp.Value + (int)MaterialAttributes.Material.NumberOfTypes, attribName, paramValue);
                }
            }
        }
    }

    // Use reflection to set the value in the attribute structure.
    private static void SetAttributeValue(int matType, string attribName, float val)
    {
        // Get the current attribute values for this material
        MaterialAttributes thisAttrib = Attributes[matType];
        // Find the field for the passed attribute name (eg, find field named 'friction')
        FieldInfo fieldInfo = thisAttrib.GetType().GetField(attribName.ToLower());
        if (fieldInfo != null)
        {
            fieldInfo.SetValue(thisAttrib, val);
            // Copy new attributes back to array -- since MaterialAttributes is 'struct', passed by value, not reference.
            Attributes[matType] = thisAttrib;
        }
    }

    // Given a material type, return a structure of attributes.
    public static MaterialAttributes GetAttributes(MaterialAttributes.Material type, bool isPhysical)
    {
        int ind = (int)type;
        if (isPhysical) ind += (int)MaterialAttributes.Material.NumberOfTypes;
        return Attributes[ind];
    }
}
}
