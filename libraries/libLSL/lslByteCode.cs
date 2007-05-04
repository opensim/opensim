using System;
using System.Collections.Generic;
using System.Text;

namespace libLSL
{
    class lslByteCode
    {
        byte[] bytecode;

        public void executeStep()
        {
            byte ins = nextInstruction();
            lslOpcodes code = (lslOpcodes)ins;

            Object arg1 = (Object)32;
            Object arg2 = (Object)32;

            switch (code)
            {
                case lslOpcodes.OP_NOOP:
                    break;

                case lslOpcodes.OP_POP:
                    popBytes(4);
                    break;

                case lslOpcodes.OP_POPS:
                case lslOpcodes.OP_POPL:
                    // Do Stuff
                    break;

                case lslOpcodes.OP_POPV:
                    popBytes(12);
                    break;
                case lslOpcodes.OP_POPQ:
                    popBytes(16);
                    break;

                case lslOpcodes.OP_POPARG:
                    popBytes((Int32)arg1);
                    break;

                case lslOpcodes.OP_POPIP:
                    // Do Stuff
                    break;

                case lslOpcodes.OP_POPBP:
                    // Do Stuff
                    break;

                case lslOpcodes.OP_POPSP:
                    // Do Stuff
                    break;

                case lslOpcodes.OP_POPSLR:
                    // Do Stuff
                    break;

                case lslOpcodes.OP_DUP:
                    pushBytes(getBytes(4));
                    break;

                case lslOpcodes.OP_DUPS:
                case lslOpcodes.OP_DUPL:
                    // Do Stuff
                    break;

                case lslOpcodes.OP_DUPV:
                    pushBytes(getBytes(12));
                    break;

                case lslOpcodes.OP_DUPQ:
                    pushBytes(getBytes(16));
                    break;

                case lslOpcodes.OP_STORE:
                    // Somefin.
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Advance the instruction pointer, pull the current instruction
        /// </summary>
        /// <returns></returns>
        byte nextInstruction()
        {
            return 0;
        }

        /// <summary>
        /// Removes bytes from the stack
        /// </summary>
        /// <param name="num">Number of bytes</param>
        void popBytes(int num)
        {

        }

        /// <summary>
        /// Pushes Bytes to the stack
        /// </summary>
        /// <param name="bytes">Ze bytes!</param>
        void pushBytes(byte[] bytes)
        {

        }

        /// <summary>
        /// Get Bytes from the stack
        /// </summary>
        /// <param name="num">Number of bytes</param>
        /// <returns>Ze bytes!</returns>
        byte[] getBytes(int num)
        {
            return new byte[1];
        }

        /// <summary>
        /// Saves bytes to the local frame
        /// </summary>
        /// <param name="bytes">Ze bytes!</param>
        /// <param name="index">Index in local frame</param>
        void storeBytes(byte[] bytes, int index)
        {

        }
    }
}
