/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
namespace Nwc.XmlRpc
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Simple tagging attribute to indicate participation is XML-RPC exposure.
    /// </summary>
    /// <remarks>
    /// If present at the class level it indicates that this class does explicitly 
    /// expose methods. If present at the method level it denotes that the method
    /// is exposed.
    /// </remarks>
    [AttributeUsage(
          AttributeTargets.Class | AttributeTargets.Method,
          AllowMultiple = false,
          Inherited = true
      )]
    public class XmlRpcExposedAttribute : Attribute
    {
        /// <summary>Check if <paramref>obj</paramref> is an object utilizing the XML-RPC exposed Attribute.</summary>
        /// <param name="obj"><c>Object</c> of a class or method to check for attribute.</param>
        /// <returns><c>Boolean</c> true if attribute present.</returns>
        public static Boolean ExposedObject(Object obj)
        {
            return IsExposed(obj.GetType());
        }

        /// <summary>Check if <paramref>obj</paramref>.<paramref>methodName</paramref> is an XML-RPC exposed method.</summary>
        /// <remarks>A method is considered to be exposed if it exists and, either, the object does not use the XmlRpcExposed attribute,
        /// or the object does use the XmlRpcExposed attribute and the method has the XmlRpcExposed attribute as well.</remarks>
        /// <returns><c>Boolean</c> true if the  method is exposed.</returns>
        public static Boolean ExposedMethod(Object obj, String methodName)
        {
            Type type = obj.GetType();
            MethodInfo method = type.GetMethod(methodName);

            if (method == null)
                throw new MissingMethodException("Method " + methodName + " not found.");

            if (!IsExposed(type))
                return true;

            return IsExposed(method);
        }

        /// <summary>Check if <paramref>mi</paramref> is XML-RPC exposed.</summary>
        /// <param name="mi"><c>MemberInfo</c> of a class or method to check for attribute.</param>
        /// <returns><c>Boolean</c> true if attribute present.</returns>
        public static Boolean IsExposed(MemberInfo mi)
        {
            foreach (Attribute attr in mi.GetCustomAttributes(true))
            {
                if (attr is XmlRpcExposedAttribute)
                    return true;
            }
            return false;
        }
    }
}
