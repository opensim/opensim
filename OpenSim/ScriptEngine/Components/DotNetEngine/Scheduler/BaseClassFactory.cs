using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Scheduler
{
    public class BaseClassFactory
    {        


        public static void MakeBaseClass(ScriptStructure script)
        {
            string asmName = "ScriptAssemblies";
            string ModuleID = asmName;
            string ClassID = "Script";
            string moveToDir = "ScriptEngines";
            string asmFileName = ModuleID + "_" + ClassID + ".dll";
            if (!Directory.Exists(moveToDir))
                Directory.CreateDirectory(moveToDir);

            ILGenerator ilgen;
            AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName(asmName), AssemblyBuilderAccess.RunAndSave);

            // The module builder
            ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule(ModuleID, asmFileName);

            // The class builder
            TypeBuilder classBuilder = modBuilder.DefineType(ClassID, TypeAttributes.Class | TypeAttributes.Public);

            // The default constructor
            ConstructorBuilder ctorBuilder = classBuilder.DefineDefaultConstructor(MethodAttributes.Public);


            Type[] paramsTypeArray = new Type[] {typeof (System.ParamArrayAttribute)};
            Type[] executeFunctionTypeArray = new Type[] {typeof (string), typeof (System.ParamArrayAttribute)};
            foreach (IScriptCommandProvider cp in script.RegionInfo.CommandProviders.Values)
            {
                Type t = cp.GetType();
                foreach (MethodInfo mi in t.GetMethods())
                {
                    MethodBuilder methodBuilder = classBuilder.DefineMethod(mi.Name, mi.Attributes, mi.GetType(), Type.EmptyTypes);
                    methodBuilder.SetParameters(paramsTypeArray);
                    //ParameterBuilder paramBuilder = methodBuilder.DefineParameter(1, ParameterAttributes.None, "args");
                    
                    ilgen = methodBuilder.GetILGenerator();
                    //ilgen.Emit(OpCodes.Nop);
                    //ilgen.Emit(OpCodes.Ldarg_0);
                    //ilgen.Emit(OpCodes.Ldc_I4_0);
                    //ilgen.Emit(OpCodes.Ldelem_Ref);
                    //ilgen.MarkSequencePoint(doc, 6, 1, 6, 100);

                        MethodInfo ExecuteFunction = typeof(ScriptAssemblies.IScript).GetMethod(
                         "ExecuteFunction",
                         executeFunctionTypeArray);

                        ilgen.DeclareLocal(typeof(string));
                        ilgen.Emit(OpCodes.Nop);
                    ilgen.Emit(OpCodes.Ldstr, mi.Name);
                    ilgen.Emit(OpCodes.Stloc_0);
                    ilgen.Emit(OpCodes.Ldarg_0);
                    ilgen.Emit(OpCodes.Ldloc_0);
                    ilgen.Emit(OpCodes.Ldarg_1);

    //                FieldInfo testInfo = classBuilder.
    //BindingFlags.NonPublic | BindingFlags.Instance);

                    //ilgen.Emit(OpCodes.Ldfld, testInfo);

                    //ilgen.EmitCall(OpCodes.Call, ExecuteFunction, executeFunctionTypeArray);
                    ilgen.EmitCall(OpCodes.Call, typeof(System.Console).GetMethod("WriteLine"), executeFunctionTypeArray);

                //    // string.Format("Hello, {0} World!", toWhom)
                //    //
                //    ilgen.Emit(OpCodes.Ldstr, "Hello, {0} World!");
                //    ilgen.Emit(OpCodes.Ldarg_1);
                //    ilgen.Emit(OpCodes.Call, typeof(string).GetMethod
                //("Format", new Type[] { typeof(string), typeof(object) }));

                //    // Console.WriteLine("Hello, World!");
                //    //
                //    ilgen.Emit(OpCodes.Call, typeof(Console).GetMethod
                //    ("WriteLine", new Type[] { typeof(string) }));
                    ilgen.Emit(OpCodes.Ret);

                    

                    //Label eom = ilgen.DefineLabel();
                    //ilgen.Emit(OpCodes.Br_S, eom);
                    //ilgen.MarkLabel(eom);
                    //ilgen.Emit(OpCodes.Ret);
                    //Type test = methodBuilder.SetParameters();


                    //methodBuilder.SetParameters(typeof (object[]));

                    
                }
            }


            //// Two fields: m_firstname, m_lastname
            //FieldBuilder fBuilderFirstName = classBuilder.DefineField("m_firstname", typeof(string), FieldAttributes.Private);
            //FieldBuilder fBuilderLastName = classBuilder.DefineField("m_lastname", typeof(string), FieldAttributes.Private);

            //// Two properties for this object: FirstName, LastName
            //PropertyBuilder pBuilderFirstName = classBuilder.DefineProperty("FirstName", System.Reflection.PropertyAttributes.HasDefault, typeof(string), null);
            //PropertyBuilder pBuilderLastName = classBuilder.DefineProperty("LastName", System.Reflection.PropertyAttributes.HasDefault, typeof(string), null);

            //// Custom attributes for get, set accessors
            //MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

            //// get,set accessors for FirstName
            //MethodBuilder mGetFirstNameBuilder = classBuilder.DefineMethod("get_FirstName", getSetAttr, typeof(string), Type.EmptyTypes);

            //// Code generation
            //ilgen = mGetFirstNameBuilder.GetILGenerator();
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Ldfld, fBuilderFirstName); // returning the firstname field
            //ilgen.Emit(OpCodes.Ret);

            //MethodBuilder mSetFirstNameBuilder = classBuilder.DefineMethod("set_FirstName", getSetAttr, null, new Type[] { typeof(string) });

            //// Code generation
            //ilgen = mSetFirstNameBuilder.GetILGenerator();
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Ldarg_1);
            //ilgen.Emit(OpCodes.Stfld, fBuilderFirstName); // setting the firstname field from the first argument (1)
            //ilgen.Emit(OpCodes.Ret);

            //// get,set accessors for LastName
            //MethodBuilder mGetLastNameBuilder = classBuilder.DefineMethod("get_LastName", getSetAttr, typeof(string), Type.EmptyTypes);

            //// Code generation
            //ilgen = mGetLastNameBuilder.GetILGenerator();
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Ldfld, fBuilderLastName); // returning the firstname field
            //ilgen.Emit(OpCodes.Ret);

            //MethodBuilder mSetLastNameBuilder = classBuilder.DefineMethod("set_LastName", getSetAttr, null, new Type[] { typeof(string) });

            //// Code generation
            //ilgen = mSetLastNameBuilder.GetILGenerator();
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Ldarg_1);
            //ilgen.Emit(OpCodes.Stfld, fBuilderLastName); // setting the firstname field from the first argument (1)
            //ilgen.Emit(OpCodes.Ret);

            //// Assigning get/set accessors
            //pBuilderFirstName.SetGetMethod(mGetFirstNameBuilder);
            //pBuilderFirstName.SetSetMethod(mSetFirstNameBuilder);

            //pBuilderLastName.SetGetMethod(mGetLastNameBuilder);
            //pBuilderLastName.SetSetMethod(mSetLastNameBuilder);

            //// Now, a custom method named GetFullName that concatenates FirstName and LastName properties
            //MethodBuilder mGetFullNameBuilder = classBuilder.DefineMethod("GetFullName", MethodAttributes.Public, typeof(string), Type.EmptyTypes);

            //// Code generation
            //ilgen = mGetFullNameBuilder.GetILGenerator();
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Call, mGetFirstNameBuilder); // getting the firstname
            //ilgen.Emit(OpCodes.Ldstr, " "); // an space
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Call, mGetLastNameBuilder); // getting the lastname

            //// We need the 'Concat' method from string type
            //MethodInfo concatMethod = typeof(String).GetMethod("Concat", new Type[] { typeof(string), typeof(string), typeof(string) });

            //ilgen.Emit(OpCodes.Call, concatMethod); // calling concat and returning the result
            //ilgen.Emit(OpCodes.Ret);

            //// Another constructor that initializes firstname and lastname
            //ConstructorBuilder ctorBuilder2 = classBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(string), typeof(string) });
            //ctorBuilder2.DefineParameter(1, ParameterAttributes.In, "firstname");
            //ctorBuilder2.DefineParameter(2, ParameterAttributes.In, "lastname");

            //// Code generation
            //ilgen = ctorBuilder2.GetILGenerator();

            //// First of all, we need to call the base constructor, 
            //// the Object's constructor in this sample
            //Type objType = Type.GetType("System.Object");
            //ConstructorInfo objCtor = objType.GetConstructor(Type.EmptyTypes);

            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Call, objCtor); // calling the Object's constructor

            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Ldarg_1);
            //ilgen.Emit(OpCodes.Call, mSetFirstNameBuilder); // setting the firstname field from the first argument (1)
            //ilgen.Emit(OpCodes.Ldarg_0);
            //ilgen.Emit(OpCodes.Ldarg_2);
            //ilgen.Emit(OpCodes.Call, mSetLastNameBuilder);  // setting the lastname field from the second argument (2)
            //ilgen.Emit(OpCodes.Ret);

            // Finally, create the type and save the assembly
            classBuilder.CreateType();

            asmBuilder.Save(asmFileName);
            string toFile = Path.Combine(moveToDir, asmFileName);
            if (File.Exists(toFile))
                File.Delete(toFile);
            File.Move(asmFileName, toFile);

            string a = "";
        }
    }
}