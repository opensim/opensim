using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using OpenSim.Region.Scripting;

namespace OpenSim.ScriptEngines.LSL
{

    
    public class Engine
    {
        public void Start(ScriptInfo WorldAPI)
        {
            

            
            // Create Assembly Name
            AssemblyName asmName = new AssemblyName();
            asmName.Name = "TestAssembly";

            // Create Assembly
            AssemblyBuilder asmBuilder =
                Thread.GetDomain().DefineDynamicAssembly
            (asmName, AssemblyBuilderAccess.RunAndSave);

            // Create a module (and save to disk)
            ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule
                            (asmName.Name, asmName.Name  + ".dll");

            // Create a Class (/Type)
            TypeBuilder typeBuilder = modBuilder.DefineType(
                                    "MyClass",
                                    TypeAttributes.Public,
                                    typeof(object),
                                    new Type[] { typeof(LSL_CLRInterface.LSLScript) });



            /*
             * Generate the IL itself
             */

            GenerateIL(WorldAPI, typeBuilder);


            /*
             * Done generating, create a type and run it.
             */

            // Create type object for the class (after defining fields and methods)
            Type type = typeBuilder.CreateType();

            asmBuilder.Save("TestAssembly.dll");

            // Create an instance we can play with
            //LSLScript hello = (LSLScript)Activator.CreateInstance(type);
            LSL_CLRInterface.LSLScript MyScript = (LSL_CLRInterface.LSLScript)Activator.CreateInstance(type);

            // Play with it
            MyScript.event_state_entry("Test");
        }

        private void GenerateIL(ScriptInfo WorldAPI, TypeBuilder typeBuilder)
        {


            // For debug
            LSO_Parser LSOP = new LSO_Parser();
            LSOP.ParseFile("LSO\\CloseToDefault.lso", WorldAPI, ref typeBuilder);
            return;


            // Override a Method / Function
            MethodBuilder methodBuilder = typeBuilder.DefineMethod("event_state_entry",
                         MethodAttributes.Private | MethodAttributes.Virtual,
                         typeof(void),
                         new Type[] { typeof(object) });

            typeBuilder.DefineMethodOverride(methodBuilder,
                    typeof(LSL_CLRInterface.LSLScript).GetMethod("event_state_entry"));

            // Create the IL generator
            ILGenerator il = methodBuilder.GetILGenerator();
                

            /*
             * TRY
             */
            il.BeginExceptionBlock();

            // Push "Hello World!" string to stack
            il.Emit(OpCodes.Ldstr, "Hello World!");

            // Push Console.WriteLine command to stack ... Console.WriteLine("Hello World!");
            il.Emit(OpCodes.Call, typeof(Console).GetMethod
                ("WriteLine", new Type[] { typeof(string) }));

            //il.EmitCall(OpCodes.Callvirt
            //il.Emit(OpCodes.Call, typeof(WorldAPI).GetMethod
                //("TestFunction"));


            //il.ThrowException(typeof(NotSupportedException));

          
            /*
             * CATCH
             */
            il.BeginCatchBlock(typeof(Exception));

            // Push "Hello World!" string to stack
            il.Emit(OpCodes.Ldstr, "Something went wrong: ");

            //call void [mscorlib]System.Console::WriteLine(string)
            il.Emit(OpCodes.Call, typeof(Console).GetMethod
                ("Write", new Type[] { typeof(string) }));

            //callvirt instance string [mscorlib]System.Exception::get_Message()
            il.Emit(OpCodes.Callvirt, typeof(Exception).GetMethod
                ("get_Message"));
                 
            //call void [mscorlib]System.Console::WriteLine(string)
            il.Emit(OpCodes.Call, typeof(Console).GetMethod
                ("WriteLine", new Type[] { typeof(string) }));

            /*
             * END TRY
             */
            il.EndExceptionBlock();


            // Push "Return from current method, with return value if present" to stack
            il.Emit(OpCodes.Ret);


        }
    }
}
