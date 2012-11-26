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

namespace OpenSim.Region.Physics.BulletSPlugin
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
    public static string[] MaterialNames = { "Stone", "Metal", "Glass", "Wood", 
                                     "Flesh", "Plastic", "Rubber", "Light", "Avatar" };
    public static string[] MaterialAttribs = { "Density", "Friction", "Restitution", 
                                   "ccdMotionThreshold", "ccdSweptSphereRadius" };

    public MaterialAttributes(string t, float d, float f, float r, float ccdM, float ccdS)
    {
        type = t;
        density = d;
        friction = f;
        restitution = r;
        ccdMotionThreshold = ccdM;
        ccdSweptSphereRadius = ccdS;
    }
    public string type;
    public float density;
    public float friction;
    public float restitution;
    public float ccdMotionThreshold;
    public float ccdSweptSphereRadius;
}

public static class BSMaterials
{
    public static MaterialAttributes[] Attributes;

    static BSMaterials()
    {
        // Attribute sets for both the non-physical and physical instances of materials.
        Attributes = new MaterialAttributes[(int)MaterialAttributes.Material.NumberOfTypes * 2];
    }

    // This is where all the default material attributes are defined.
    public static void InitializeFromDefaults(ConfigurationParameters parms)
    {
    // public static string[] MaterialNames = { "Stone", "Metal", "Glass", "Wood", 
      //                                "Flesh", "Plastic", "Rubber", "Light", "Avatar" };
        float dFriction = parms.defaultFriction;
        float dRestitution = parms.defaultRestitution;
        float dDensity = parms.defaultDensity;
        float dCcdM = parms.ccdMotionThreshold;
        float dCcdS = parms.ccdSweptSphereRadius;
        Attributes[(int)MaterialAttributes.Material.Stone] =
            new MaterialAttributes("stone",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Metal] =
            new MaterialAttributes("metal",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Glass] =
            new MaterialAttributes("glass",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Wood] =
            new MaterialAttributes("wood",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Flesh] =
            new MaterialAttributes("flesh",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Plastic] =
            new MaterialAttributes("plastic",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Rubber] =
            new MaterialAttributes("rubber",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Light] =
            new MaterialAttributes("light",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Avatar] =
            new MaterialAttributes("avatar",dDensity,dFriction,dRestitution, dCcdM, dCcdS);

        Attributes[(int)MaterialAttributes.Material.Stone + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("stonePhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Metal + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("metalPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Glass + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("glassPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Wood + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("woodPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Flesh + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("fleshPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Plastic + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("plasticPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Rubber + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("rubberPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Light + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("lightPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
        Attributes[(int)MaterialAttributes.Material.Avatar + (int)MaterialAttributes.Material.NumberOfTypes] =
            new MaterialAttributes("avatarPhysical",dDensity,dFriction,dRestitution, dCcdM, dCcdS);
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
        int matType = 0;
        foreach (string matName in MaterialAttributes.MaterialNames)
        {
            foreach (string attribName in MaterialAttributes.MaterialAttribs)
            {
                string paramName = matName + attribName;
                if (pConfig.Contains(paramName))
                {
                    float paramValue = pConfig.GetFloat(paramName);
                    SetAttributeValue(matType, attribName, paramValue);
                    // set the physical value also
                    SetAttributeValue(matType + (int)MaterialAttributes.Material.NumberOfTypes, attribName, paramValue);
                }
                paramName += "Physical";
                if (pConfig.Contains(paramName))
                {
                    float paramValue = pConfig.GetFloat(paramName);
                    SetAttributeValue(matType + (int)MaterialAttributes.Material.NumberOfTypes, attribName, paramValue);
                }
            }
            matType++;
        }
    }

    private static void SetAttributeValue(int matType, string attribName, float val)
    {
        MaterialAttributes thisAttrib = Attributes[matType];
        FieldInfo fieldInfo = thisAttrib.GetType().GetField(attribName);
        if (fieldInfo != null)
        {
            fieldInfo.SetValue(thisAttrib, val);
            Attributes[matType] = thisAttrib;
        }
    }

    public static MaterialAttributes GetAttributes(MaterialAttributes.Material type, bool isPhysical)
    {
        int ind = (int)type;
        if (isPhysical) ind += (int)MaterialAttributes.Material.NumberOfTypes;
        return Attributes[ind];
    }

}
}
