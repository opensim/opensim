using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO
{
    public partial class LSL_BaseClass
    {
        //public MemoryStream LSLStack = new MemoryStream();
        public Stack<object> LSLStack = new Stack<object>();
        public Dictionary<UInt32, object> StaticVariables = new Dictionary<UInt32, object>();
        public Dictionary<UInt32, object> GlobalVariables = new Dictionary<UInt32, object>();
        public Dictionary<UInt32, object> LocalVariables = new Dictionary<UInt32, object>();
        //public System.Collections.Generic.List<string> FunctionList = new System.Collections.Generic.List<string>();
        //public void AddFunction(String x) {
        //    FunctionList.Add(x);
        //}
        //public Stack<StackItemStruct> LSLStack = new Stack<StackItemStruct>;
        //public struct StackItemStruct
        //{
        //    public LSO_Enums.Variable_Type_Codes ItemType;
        //    public object Data;
        //}
        public UInt32 State = 0;
        public LSL_BuiltIn_Commands_Interface LSL_Builtins;
        public LSL_BuiltIn_Commands_Interface GetLSL_BuiltIn()
        {
            return LSL_Builtins;
        }


        public LSL_BaseClass() { }


        public virtual int OverrideMe()
        {
            return 0;
        }
        public void Start(LSL_BuiltIn_Commands_Interface LSLBuiltins)
        {
            LSL_Builtins = LSLBuiltins;

            Common.SendToLog("OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass.Start() called");
            //LSL_Builtins.llSay(0, "Test");
            return;
        }

        public void AddToStatic(UInt32 index, object obj)
        {
            Common.SendToDebug("AddToStatic: " + index + " type: " + obj.GetType());
            StaticVariables.Add(index, obj);
        }



    }
}
