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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace OpenSim.Region.ScriptEngine.Yengine
{

    public class DelegateCommon
    {
        private string sig;  // rettype(arg1type,arg2type,...), eg, "void(list,string,integer)"
        private Type type;   // resultant delegate type

        private static Dictionary<string, DelegateCommon> delegateCommons = new Dictionary<string, DelegateCommon>();
        private static Dictionary<Type, DelegateCommon> delegateCommonsBySysType = new Dictionary<Type, DelegateCommon>();
        private static ModuleBuilder delegateModuleBuilder = null;
        public static Type[] constructorArgTypes = new Type[] { typeof(object), typeof(IntPtr) };

        private DelegateCommon()
        {
        }

        public static Type GetType(System.Type ret, System.Type[] args, string sig)
        {
            DelegateCommon dc;
            lock(delegateCommons)
            {
                if(!delegateCommons.TryGetValue(sig, out dc))
                {
                    dc = new DelegateCommon();
                    dc.sig = sig;
                    dc.type = CreateDelegateType(sig, ret, args);
                    delegateCommons.Add(sig, dc);
                    delegateCommonsBySysType.Add(dc.type, dc);
                }
            }
            return dc.type;
        }

        public static Type TryGetType(string sig)
        {
            DelegateCommon dc;
            lock(delegateCommons)
            {
                if(!delegateCommons.TryGetValue(sig, out dc))
                    dc = null;
            }
            return (dc == null) ? null : dc.type;
        }

        public static string TryGetName(Type t)
        {
            DelegateCommon dc;
            lock(delegateCommons)
            {
                if(!delegateCommonsBySysType.TryGetValue(t, out dc))
                    dc = null;
            }
            return (dc == null) ? null : dc.sig;
        }

        // http://blog.bittercoder.com/PermaLink,guid,a770377a-b1ad-4590-9145-36381757a52b.aspx
        private static Type CreateDelegateType(string name, Type retType, Type[] argTypes)
        {
            if(delegateModuleBuilder == null)
            {
                AssemblyName assembly = new AssemblyName();
                assembly.Name = "CustomDelegateAssembly";
                AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assembly, AssemblyBuilderAccess.Run);
                delegateModuleBuilder = assemblyBuilder.DefineDynamicModule("CustomDelegateModule");
            }

            TypeBuilder typeBuilder = delegateModuleBuilder.DefineType(name,
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class |
                TypeAttributes.AnsiClass | TypeAttributes.AutoClass, typeof(MulticastDelegate));

            ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard, constructorArgTypes);
            constructorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            MethodBuilder methodBuilder = typeBuilder.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot |
                MethodAttributes.Virtual, retType, argTypes);
            methodBuilder.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.Runtime);

            return typeBuilder.CreateType();
        }
    }
}
