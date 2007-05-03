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

            switch (code)
            {
                case lslOpcodes.OP_NOOP:
                    break;
                case lslOpcodes.OP_POP:
                    popBytes(4);
                    break;

                default:
                    break;
            }
        }

        byte nextInstruction()
        {
            return 0;
        }

        void popBytes(int num)
        {

        }
    }
}
