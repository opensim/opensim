using System;
using System.Collections.Generic;
using System.Text;

namespace libLSL
{
    

    enum lslVarType
    {
        VARTYPE_VOID = 0,
        VARTYPE_INTEGER = 1,
        VARTYPE_FLOAT = 2,
        VARTYPE_STRING = 3,
        VARTYPE_KEY = 4,
        VARTYPE_VECTOR = 5,
        VARTYPE_ROTATION = 6,
        VARTYPE_LIST = 7
    }

    enum lslEventType
    {
        EVENT_STATE_ENTRY = 0,
        EVENT_STATE_EXIT = 1,
        EVENT_TOUCH_START = 2,
        EVENT_TOUCH = 3,
        EVENT_TOUCH_END = 4,
        EVENT_COLLISION_START = 5,
        EVENT_COLLISION = 6,
        EVENT_COLLISION_END = 7,
        EVENT_LAND_COLLISION_START = 8,
        EVENT_LAND_COLLISION = 9,
        EVENT_LAND_COLLISION_END = 10,
        EVENT_TIMER = 11,
        EVENT_LISTEN = 12,
        EVENT_ON_REZ = 13,
        EVENT_SENSOR = 14,
        EVENT_NO_SENSOR = 15,
        EVENT_CONTROL = 16,
        EVENT_MONEY = 17,
        EVENT_EMAIL = 18,
        EVENT_AT_TARGET = 19,
        EVENT_NOT_AT_TARGET = 20,
        EVENT_AT_ROT_TARGET = 21,
        EVENT_NOT_AT_ROT_TARGET = 22,
        EVENT_RUN_TIME_PERMISSIONS = 23,
        EVENT_CHANGED = 24,
        EVENT_ATTACH = 25,
        EVENT_DATASERVER = 26,
        EVENT_LINK_MESSAGE = 27,
        EVENT_MOVING_START = 28,
        EVENT_MOVING_END = 29,
        EVENT_OBJECT_REZ = 30,
        EVENT_REMOTE_DATA = 31,
        EVENT_HTTP_RESPONSE = 32
    }

    enum lslOpcodes
    {
        // No Operation
        OP_NOOP = 0x00,

        // Pops
        OP_POP = 0x01,
        OP_POPS = 0x02,
        OP_POPL = 0x03,
        OP_POPV = 0x04,
        OP_POPQ = 0x05,
        OP_POPARG = 0x06,
        OP_POPIP = 0x07,
        OP_POPBP = 0x08,
        OP_POPSP = 0x09,
        OP_POPSLR = 0x0A,

        // Dupes
        OP_DUP = 0x20,
        OP_DUPS = 0x21,
        OP_DUPL = 0x22,
        OP_DUPV = 0x23,
        OP_DUPQ = 0x24,

        // Stores
        OP_STORE = 0x30,
        OP_STORES = 0x31,
        OP_STOREL = 0x32,
        OP_STOREV = 0x33,
        OP_STOREQ = 0x34,
        OP_STOREG = 0x35,
        OP_STOREGS = 0x36,
        OP_STOREGL = 0x37,
        OP_STOREGV = 0x38,
        OP_STOREGQ = 0x39,

        // Loads
        OP_LOADP = 0x3A,
        OP_LOADSP = 0x3B,
        OP_LOADLP = 0x3C,
        OP_LOADVP = 0x3D,
        OP_LOADQP = 0x3E,
        OP_LOADGP = 0x3F,
        OP_LOADGSP = 0x40,
        OP_LOADGLP = 0x41,
        OP_LOADGVP = 0x42,
        OP_LOADGQP = 0x43,

        // Pushes
        OP_PUSH = 0x50,
        OP_PUSHS = 0x51,
        OP_PUSHL = 0x52,
        OP_PUSHV = 0x53,
        OP_PUSHQ = 0x54,
        OP_PUSHG = 0x55,
        OP_PUSHGS = 0x56,
        OP_PUSHGL = 0x57,
        OP_PUSHGV = 0x58,
        OP_PUSHGQ = 0x59,
        OP_PUSHIP = 0x5A,
        OP_PUSHBP = 0x5B,
        OP_PUSHSP = 0x5C,
        OP_PUSHARGB = 0x5D,
        OP_PUSHARGI = 0x5E,
        OP_PUSHARGF = 0x5F,
        OP_PUSHARGS = 0x60,
        OP_PUSHARGV = 0x61,
        OP_PUSHARGQ = 0x62,
        OP_PUSHE = 0x63,
        OP_PUSHEV = 0x64,
        OP_PUSHEQ = 0x65,
        OP_PUSHARGE = 0x66,

        // Numerics
        OP_ADD = 0x70,
        OP_SUB = 0x71,
        OP_MUL = 0x72,
        OP_DIV = 0x73, 
        OP_MOD = 0x74,
        OP_EQ = 0x75,
        OP_NEQ = 0x76,
        OP_LEQ = 0x77,
        OP_GEQ = 0x78,
        OP_LESS = 0x79,
        OP_GREATER = 0x7A,
        OP_BITAND = 0x7B,
        OP_BITOR = 0x7C,
        OP_BITXOR = 0x7D,
        OP_BOOLAND = 0x7E,
        OP_BOOLOR = 0x7F,
        OP_NEG = 0x80,
        OP_BITNOT = 0x81,
        OP_BOOLNOT = 0x82,
        
        // Sequence
        OP_JUMP = 0x90,
        OP_JUMPIF = 0x91,
        OP_JUMPNIF = 0x92,
        OP_STATE = 0x93,
        OP_CALL = 0x94, 
        OP_RETURN = 0x95,

        // Cast
        OP_CAST = 0xA0,

        // Stack
        OP_STACKTOS = 0xB0,
        OP_STACKTOL = 0xB1,

        // Debug
        OP_PRINT = 0xC0,

        // Library
        OP_CALLLIB = 0xD0,
        OP_CALLLIB_TWO_BYTE = 0xD1,
        
        // More Numerics
        OP_SHL = 0xE0,
        OP_SHR = 0xE1
    }

    class lslHeader
    {
        int TM;     // Top of memory
        int IP;     // Instruction pointer
        int VN;     // Version Number (0x00000200)
        int BP;     // Base Pointer
        int SP;     // Stack Pointer
        int HR;     // Heap Register
        int HP;     // Heap Pointer
        int CS;     // Current State
        int NS;     // Next State
        int CE;     // Current Events (Which events need running still?)
        int IE;     // In Event
        int ER;     // Event Register
        int FR;     // Fault Register
        int SLR;    // Sleep Register
        int GVR;    // Global Variable Register (Pointer)
        int GFR;    // Global Function Register (Pointer)
        int PR;     // Parameter Register - OnRez Int?
        int ESR;    // Energy Supply Register
        int SR;     // State Register
        long NCE;   // Extended Current Events
        long NIE;   // Extended In Event
        long NER;   // Extended Event Register

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslStaticBlock
    {
        int length;        // Length (bytes)
        lslVarType varType;// Variable Type
        byte unknown;       // Unknown
        Object varObject;     // Variable Object

        public void readFromBytes(byte[] data)
        {

        }

    }

    class lslHeapBlock
    {
        int length;
        lslVarType varType;
        short referenceCount;
        Object varObject;

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslStatePointer
    {
        int location;
        long eventMask;

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslStateFrameBlock
    {
        int number;
        lslStatePointer[] pointers;

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslStateBlockElement
    {
        int pointerToCode;
        int callFrameSize;

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslStateBlock
    {
        int length;
        byte unknown;

        lslStateBlockElement[] handlers; // ?

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslFunctioBlock
    {
        int number;
        int[] pointers; // Relative to this -> codechunk

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslCodeArgument
    {
        lslVarType type;
        byte empty;

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslCodeChunkHeader
    {
        int length;
        string comment;
        lslVarType returnType;
        lslCodeArgument[] arguments;
        byte empty;

        public void readFromBytes(byte[] data)
        {

        }
    }

    class lslCodeChunk
    {
        lslCodeChunkHeader header;
        byte[] bytecode;

        public void readFromBytes(byte[] data)
        {

        }
    }
}
