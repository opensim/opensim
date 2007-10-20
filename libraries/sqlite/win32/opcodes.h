/* Automatically generated.  Do not edit */
/* See the mkopcodeh.awk script for details */
#define OP_VCreate                              1
#define OP_MemMax                               2
#define OP_LoadAnalysis                         3
#define OP_RowData                              4
#define OP_CreateIndex                          5
#define OP_Variable                             6
#define OP_MemStore                             7
#define OP_Clear                                8
#define OP_Last                                 9
#define OP_Add                                 78   /* same as TK_PLUS     */
#define OP_MoveGe                              10
#define OP_Sequence                            11
#define OP_Int64                               12
#define OP_VBegin                              13
#define OP_RowKey                              14
#define OP_Divide                              81   /* same as TK_SLASH    */
#define OP_MemInt                              15
#define OP_ResetCount                          17
#define OP_Delete                              18
#define OP_Rowid                               19
#define OP_OpenRead                            20
#define OP_Sort                                21
#define OP_VerifyCookie                        22
#define OP_VColumn                             23
#define OP_MemMove                             24
#define OP_Next                                25
#define OP_Insert                              26
#define OP_Prev                                27
#define OP_IdxGE                               28
#define OP_Not                                 16   /* same as TK_NOT      */
#define OP_Ge                                  72   /* same as TK_GE       */
#define OP_VRename                             29
#define OP_DropTable                           30
#define OP_MakeRecord                          31
#define OP_Null                                32
#define OP_IdxInsert                           33
#define OP_ReadCookie                          34
#define OP_VDestroy                            35
#define OP_DropIndex                           36
#define OP_IsNull                              65   /* same as TK_ISNULL   */
#define OP_MustBeInt                           37
#define OP_Callback                            38
#define OP_IntegrityCk                         39
#define OP_MoveGt                              40
#define OP_MoveLe                              41
#define OP_CollSeq                             42
#define OP_OpenEphemeral                       43
#define OP_HexBlob                            126   /* same as TK_BLOB     */
#define OP_VNext                               44
#define OP_Eq                                  68   /* same as TK_EQ       */
#define OP_String8                             88   /* same as TK_STRING   */
#define OP_Found                               45
#define OP_If                                  46
#define OP_ToBlob                             139   /* same as TK_TO_BLOB  */
#define OP_Multiply                            80   /* same as TK_STAR     */
#define OP_Dup                                 47
#define OP_ShiftRight                          77   /* same as TK_RSHIFT   */
#define OP_Goto                                48
#define OP_Function                            49
#define OP_Pop                                 50
#define OP_Blob                                51
#define OP_MemIncr                             52
#define OP_BitNot                              87   /* same as TK_BITNOT   */
#define OP_IfMemPos                            53
#define OP_FifoWrite                           54
#define OP_IdxGT                               55
#define OP_Gt                                  69   /* same as TK_GT       */
#define OP_Le                                  70   /* same as TK_LE       */
#define OP_NullRow                             56
#define OP_Transaction                         57
#define OP_VUpdate                             58
#define OP_TableLock                           59
#define OP_IdxRowid                            62
#define OP_SetCookie                           63
#define OP_Negative                            85   /* same as TK_UMINUS   */
#define OP_And                                 61   /* same as TK_AND      */
#define OP_ToNumeric                          140   /* same as TK_TO_NUMERIC*/
#define OP_ToText                             138   /* same as TK_TO_TEXT  */
#define OP_ContextPush                         64
#define OP_DropTrigger                         73
#define OP_MoveLt                              84
#define OP_AutoCommit                          86
#define OP_Column                              89
#define OP_AbsValue                            90
#define OP_AddImm                              91
#define OP_Remainder                           82   /* same as TK_REM      */
#define OP_ContextPop                          92
#define OP_IdxDelete                           93
#define OP_Ne                                  67   /* same as TK_NE       */
#define OP_ToInt                              141   /* same as TK_TO_INT   */
#define OP_IncrVacuum                          94
#define OP_AggFinal                            95
#define OP_RealAffinity                        96
#define OP_Concat                              83   /* same as TK_CONCAT   */
#define OP_Return                              97
#define OP_Expire                              98
#define OP_Rewind                              99
#define OP_Statement                          100
#define OP_BitOr                               75   /* same as TK_BITOR    */
#define OP_Integer                            101
#define OP_IfMemZero                          102
#define OP_Destroy                            103
#define OP_IdxLT                              104
#define OP_MakeIdxRec                         105
#define OP_Lt                                  71   /* same as TK_LT       */
#define OP_Subtract                            79   /* same as TK_MINUS    */
#define OP_Vacuum                             106
#define OP_MemNull                            107
#define OP_IfNot                              108
#define OP_Pull                               109
#define OP_FifoRead                           110
#define OP_ParseSchema                        111
#define OP_NewRowid                           112
#define OP_SetNumColumns                      113
#define OP_Explain                            114
#define OP_BitAnd                              74   /* same as TK_BITAND   */
#define OP_String                             115
#define OP_AggStep                            116
#define OP_VRowid                             117
#define OP_VOpen                              118
#define OP_NotExists                          119
#define OP_Close                              120
#define OP_Halt                               121
#define OP_Noop                               122
#define OP_VFilter                            123
#define OP_OpenPseudo                         124
#define OP_Or                                  60   /* same as TK_OR       */
#define OP_ShiftLeft                           76   /* same as TK_LSHIFT   */
#define OP_IfMemNeg                           127
#define OP_ToReal                             142   /* same as TK_TO_REAL  */
#define OP_IsUnique                           128
#define OP_ForceInt                           129
#define OP_OpenWrite                          130
#define OP_Gosub                              131
#define OP_Real                               125   /* same as TK_FLOAT    */
#define OP_Distinct                           132
#define OP_NotNull                             66   /* same as TK_NOTNULL  */
#define OP_MemLoad                            133
#define OP_NotFound                           134
#define OP_CreateTable                        135
#define OP_Push                               136

/* The following opcode values are never used */
#define OP_NotUsed_137                        137

/* Opcodes that are guaranteed to never push a value onto the stack
** contain a 1 their corresponding position of the following mask
** set.  See the opcodeNoPush() function in vdbeaux.c  */
#define NOPUSH_MASK_0 0x278e
#define NOPUSH_MASK_1 0x7e77
#define NOPUSH_MASK_2 0x7f7a
#define NOPUSH_MASK_3 0xbff5
#define NOPUSH_MASK_4 0xffff
#define NOPUSH_MASK_5 0xf8f7
#define NOPUSH_MASK_6 0xb55f
#define NOPUSH_MASK_7 0x9fd2
#define NOPUSH_MASK_8 0x7d5f
#define NOPUSH_MASK_9 0x0000
