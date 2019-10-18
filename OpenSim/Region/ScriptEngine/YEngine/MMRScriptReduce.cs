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

/**
 * @brief Reduce parser tokens to abstract syntax tree tokens.
 *
 * Usage:
 *
 *  tokenBegin = returned by TokenBegin.Analyze ()
 *               representing the whole script source
 *               as a flat list of tokens
 *
 *  TokenScript tokenScript = Reduce.Analyze (TokenBegin tokenBegin);
 *
 *  tokenScript = represents the whole script source
 *                as a tree of tokens
 */

using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public class ScriptReduce
    {
        public const uint SDT_PRIVATE = 1;
        public const uint SDT_PROTECTED = 2;
        public const uint SDT_PUBLIC = 4;
        public const uint SDT_ABSTRACT = 8;
        public const uint SDT_FINAL = 16;
        public const uint SDT_NEW = 32;
        public const uint SDT_OVERRIDE = 64;
        public const uint SDT_STATIC = 128;
        public const uint SDT_VIRTUAL = 256;

        private const int ASNPR = 50;

        private static Dictionary<Type, int> precedence = PrecedenceInit();

        private static readonly Type[] brkCloseOnly = new Type[] { typeof(TokenKwBrkClose) };
        private static readonly Type[] cmpGTOnly = new Type[] { typeof(TokenKwCmpGT) };
        private static readonly Type[] colonOnly = new Type[] { typeof(TokenKwColon) };
        private static readonly Type[] commaOrBrcClose = new Type[] { typeof(TokenKwComma), typeof(TokenKwBrcClose) };
        private static readonly Type[] colonOrDotDotDot = new Type[] { typeof(TokenKwColon), typeof(TokenKwDotDotDot) };
        private static readonly Type[] parCloseOnly = new Type[] { typeof(TokenKwParClose) };
        private static readonly Type[] semiOnly = new Type[] { typeof(TokenKwSemi) };

        /**
         * @brief Initialize operator precedence table
         * @returns with precedence table pointer
         */
        private static Dictionary<Type, int> PrecedenceInit()
        {
            Dictionary<Type, int> p = new Dictionary<Type, int>();

            // http://www.lslwiki.net/lslwiki/wakka.php?wakka=operators

            p.Add(typeof(TokenKwComma), 30);

            p.Add(typeof(TokenKwAsnLSh), ASNPR);  // all assignment operators of equal precedence
            p.Add(typeof(TokenKwAsnRSh), ASNPR);  // ... so they get processed strictly right-to-left
            p.Add(typeof(TokenKwAsnAdd), ASNPR);
            p.Add(typeof(TokenKwAsnAnd), ASNPR);
            p.Add(typeof(TokenKwAsnSub), ASNPR);
            p.Add(typeof(TokenKwAsnMul), ASNPR);
            p.Add(typeof(TokenKwAsnDiv), ASNPR);
            p.Add(typeof(TokenKwAsnMod), ASNPR);
            p.Add(typeof(TokenKwAsnOr), ASNPR);
            p.Add(typeof(TokenKwAsnXor), ASNPR);
            p.Add(typeof(TokenKwAssign), ASNPR);

            p.Add(typeof(TokenKwQMark), 60);

            p.Add(typeof(TokenKwOrOrOr), 70);
            p.Add(typeof(TokenKwAndAndAnd), 80);

            p.Add(typeof(TokenKwOrOr), 100);

            p.Add(typeof(TokenKwAndAnd), 120);

            p.Add(typeof(TokenKwOr), 140);

            p.Add(typeof(TokenKwXor), 160);

            p.Add(typeof(TokenKwAnd), 180);

            p.Add(typeof(TokenKwCmpEQ), 200);
            p.Add(typeof(TokenKwCmpNE), 200);

            p.Add(typeof(TokenKwCmpLT), 240);
            p.Add(typeof(TokenKwCmpLE), 240);
            p.Add(typeof(TokenKwCmpGT), 240);
            p.Add(typeof(TokenKwCmpGE), 240);

            p.Add(typeof(TokenKwRSh), 260);
            p.Add(typeof(TokenKwLSh), 260);

            p.Add(typeof(TokenKwAdd), 280);
            p.Add(typeof(TokenKwSub), 280);

            p.Add(typeof(TokenKwMul), 320);
            p.Add(typeof(TokenKwDiv), 320);
            p.Add(typeof(TokenKwMod), 320);

            return p;
        }

        /**
         * @brief Reduce raw token stream to a single script token.
         *        Performs a little semantic testing, ie, undefined variables, etc.
         * @param tokenBegin = points to a TokenBegin
         *                     followed by raw tokens
         *                     and last token is a TokenEnd
         * @returns null: not a valid script, error messages have been output
         *          else: valid script top token
         */
        public static TokenScript Reduce(TokenBegin tokenBegin)
        {
            return new ScriptReduce(tokenBegin).tokenScript;
        }

        /*
         * Instance variables.
         */
        private bool errors = false;
        private string lastErrorFile = "";
        private int lastErrorLine = 0;
        private int numTypedefs = 0;
        private TokenDeclVar currentDeclFunc = null;
        private TokenDeclSDType currentDeclSDType = null;
        private TokenScript tokenScript;
        private TokenStmtBlock currentStmtBlock = null;

        /**
         * @brief the constructor does all the processing.
         * @param token = first token of script after the TokenBegin token
         * @returns tokenScript = null: there were errors
         *                        else: successful
         */
        private ScriptReduce(TokenBegin tokenBegin)
        {
             // Create a place to put the top-level script components,
             // eg, state bodies, functions, global variables.
            tokenScript = new TokenScript(tokenBegin.nextToken);

             // 'class', 'delegate', 'instance' all define types.
             // So we pre-scan the source tokens for those keywords
             // to build a script-defined type table and substitute
             // type tokens for those names in the source.  This is
             // done as a separate scan so they can cross-reference
             // each other.  Also does likewise for fixed array types.
             //
             // Also, all 'typedef's are processed here.  Their definitions 
             // remain in the source token stream after this, but they can 
             // be skipped over, because their bodies have been substituted 
             // in the source for any references.
            ParseSDTypePreScanPassOne(tokenBegin);  // catalog definitions
            ParseSDTypePreScanPassTwo(tokenBegin);  // substitute references

            /*
            int braces = 0;
            Token prevTok = null;
            for (Token token = tokenBegin; token != null; token = token.nextToken) {
                if (token is TokenKwParClose) braces -= 2;
                if (token is TokenKwBrcClose) braces -= 4;
                StringBuilder sb = new StringBuilder ("ScriptReduce*: ");
                sb.Append (token.GetHashCode ().ToString ("X8"));
                sb.Append (" ");
                sb.Append (token.line.ToString ().PadLeft (3));
                sb.Append (".");
                sb.Append (token.posn.ToString ().PadLeft (3));
                sb.Append ("  ");
                sb.Append (token.GetType ().Name.PadRight (24));
                sb.Append (" : ");
                for (int i = 0; i < braces; i ++) sb.Append (' ');
                token.DebString (sb);
                Console.WriteLine (sb.ToString ());
                if (token.prevToken != prevTok) {
                    Console.WriteLine ("ScriptReduce*:  -- prevToken link bad => " + token.prevToken.GetHashCode ().ToString ("X8"));
                }
                if (token is TokenKwBrcOpen) braces += 4;
                if (token is TokenKwParOpen) braces += 2;
                prevTok = token;
            }
            */

             // Create a function $globalvarinit to hold all explicit
             // global variable initializations.
            TokenDeclVar gviFunc = new TokenDeclVar(tokenBegin, null, tokenScript);
            gviFunc.name = new TokenName(gviFunc, "$globalvarinit");
            gviFunc.retType = new TokenTypeVoid(gviFunc);
            gviFunc.argDecl = new TokenArgDecl(gviFunc);
            TokenStmtBlock gviBody = new TokenStmtBlock(gviFunc);
            gviBody.function = gviFunc;
            gviFunc.body = gviBody;
            tokenScript.globalVarInit = gviFunc;
            tokenScript.AddVarEntry(gviFunc);

             // Scan through the tokens until we reach the end.
            for(Token token = tokenBegin.nextToken; !(token is TokenEnd);)
            {
                if(token is TokenKwSemi)
                {
                    token = token.nextToken;
                    continue;
                }

                 // Script-defined type declarations.
                if(ParseDeclSDTypes(ref token, null, SDT_PUBLIC))
                    continue;

                 // constant <name> = <rval> ;
                if(token is TokenKwConst)
                {
                    ParseDeclVar(ref token, null);
                    continue;
                }

                 // <type> <name> ;
                 // <type> <name> = <rval> ;
                if((token is TokenType) &&
                    (token.nextToken is TokenName) &&
                    ((token.nextToken.nextToken is TokenKwSemi) ||
                     (token.nextToken.nextToken is TokenKwAssign)))
                {
                    TokenDeclVar var = ParseDeclVar(ref token, gviFunc);
                    if(var != null)
                    {
                        // <name> = <init>;
                        TokenLValName left = new TokenLValName(var.name, tokenScript.variablesStack);
                        DoVarInit(gviFunc, left, var.init);
                    }
                    continue;
                }

                 // <type> <name> { [ get { <body> } ] [ set { <body> } ] }
                if((token is TokenType) &&
                    (token.nextToken is TokenName) &&
                    (token.nextToken.nextToken is TokenKwBrcOpen))
                {
                    ParseProperty(ref token, false, true);
                    continue;
                }

                 // <type> <name> <funcargs> <funcbody>
                 // global function returning specified type
                if(token is TokenType)
                {
                    TokenType tokenType = (TokenType)token;

                    token = token.nextToken;
                    if(!(token is TokenName))
                    {
                        ErrorMsg(token, "expecting variable/function name");
                        token = SkipPastSemi(token);
                        continue;
                    }
                    TokenName tokenName = (TokenName)token;
                    token = token.nextToken;
                    if(!(token is TokenKwParOpen))
                    {
                        ErrorMsg(token, "<type> <name> must be followed by ; = or (");
                        token = SkipPastSemi(token);
                        continue;
                    }
                    token = tokenType;
                    TokenDeclVar tokenDeclFunc = ParseDeclFunc(ref token, false, false, false);
                    if(tokenDeclFunc == null)
                        continue;
                    if(!tokenScript.AddVarEntry(tokenDeclFunc))
                    {
                        ErrorMsg(tokenName, "duplicate function " + tokenDeclFunc.funcNameSig.val);
                    }
                    continue;
                }

                 // <name> <funcargs> <funcbody>
                 // global function returning void
                if(token is TokenName)
                {
                    TokenName tokenName = (TokenName)token;
                    token = token.nextToken;
                    if(!(token is TokenKwParOpen))
                    {
                        ErrorMsg(token, "looking for open paren after assuming " +
                                         tokenName.val + " is a function name");
                        token = SkipPastSemi(token);
                        continue;
                    }
                    token = tokenName;
                    TokenDeclVar tokenDeclFunc = ParseDeclFunc(ref token, false, false, false);
                    if(tokenDeclFunc == null)
                        continue;
                    if(!tokenScript.AddVarEntry(tokenDeclFunc))
                        ErrorMsg(tokenName, "duplicate function " + tokenDeclFunc.funcNameSig.val);

                    continue;
                }

                 // default <statebody>
                if(token is TokenKwDefault)
                {
                    TokenDeclState tokenDeclState = new TokenDeclState(token);
                    token = token.nextToken;
                    tokenDeclState.body = ParseStateBody(ref token);
                    if(tokenDeclState.body == null)
                        continue;
                    if(tokenScript.defaultState != null)
                    {
                        ErrorMsg(tokenDeclState, "default state already declared");
                        continue;
                    }
                    tokenScript.defaultState = tokenDeclState;
                    continue;
                }

                 // state <name> <statebody>
                if(token is TokenKwState)
                {
                    TokenDeclState tokenDeclState = new TokenDeclState(token);
                    token = token.nextToken;
                    if(!(token is TokenName))
                    {
                        ErrorMsg(token, "state must be followed by state name");
                        token = SkipPastSemi(token);
                        continue;
                    }
                    tokenDeclState.name = (TokenName)token;
                    token = token.nextToken;
                    tokenDeclState.body = ParseStateBody(ref token);
                    if(tokenDeclState.body == null)
                        continue;
                    if(tokenScript.states.ContainsKey(tokenDeclState.name.val))
                    {
                        ErrorMsg(tokenDeclState.name, "duplicate state definition");
                        continue;
                    }
                    tokenScript.states.Add(tokenDeclState.name.val, tokenDeclState);
                    continue;
                }

                 // Doesn't fit any of those forms, output message and skip to next statement.
                ErrorMsg(token, "looking for var name, type, state or default, script-defined type declaration");
                token = SkipPastSemi(token);
                continue;
            }

             // Must have a default state to start in.
            if(!errors && (tokenScript.defaultState == null))
            {
                ErrorMsg(tokenScript, "no default state defined");
            }

             // If any error messages were written out, set return value to null.
            if(errors)
                tokenScript = null;
        }

        /**
         * @brief Pre-scan the source for class, delegate, interface, typedef definition keywords.
         *        Clump the keywords and name being defined together, but leave the body intact.
         *        In the case of a delegate with an explicit return type, it reverses the name and return type.
         *        After this completes there shouldn't be any TokenKw{Class,Delegate,Interface,Typedef} 
         *        keywords in the source, they are all replaced by TokenDeclSDType{Class,Delegate,Interface,
         *        Typedef} tokens which also encapsulate the name of the type being defined and any generic 
         *        parameter names.  The body remains intact in the source token stream following the 
         *        TokenDeclSDType* token.
         */
        private void ParseSDTypePreScanPassOne(Token tokenBegin)
        {
            Stack<int> braceLevels = new Stack<int>();
            Stack<TokenDeclSDType> outerLevels = new Stack<TokenDeclSDType>();
            int openBraceLevel = 0;
            braceLevels.Push(-1);
            outerLevels.Push(null);

            for(Token t = tokenBegin; !((t = t.nextToken) is TokenEnd);)
            {
                 // Keep track of nested definitions so we can link them up.
                 // We also need to detect the end of class and interface definitions.
                if(t is TokenKwBrcOpen)
                {
                    openBraceLevel++;
                    continue;
                }
                if(t is TokenKwBrcClose)
                {
                    if(--openBraceLevel < 0)
                    {
                        ErrorMsg(t, "{ } mismatch");
                        return;
                    }
                    if(braceLevels.Peek() == openBraceLevel)
                    {
                        braceLevels.Pop();
                        outerLevels.Pop().endToken = t;
                    }
                    continue;
                }

                 // Check for 'class' or 'interface'.
                 // They always define a new class or interface.
                 // They can contain nested script-defined type definitions.
                if((t is TokenKwClass) || (t is TokenKwInterface))
                {
                    Token kw = t;
                    t = t.nextToken;
                    if(!(t is TokenName))
                    {
                        ErrorMsg(t, "expecting class or interface name");
                        t = SkipPastSemi(t).prevToken;
                        continue;
                    }
                    TokenName name = (TokenName)t;
                    t = t.nextToken;

                     // Malloc the script-defined type object.
                    TokenDeclSDType decl;
                    if(kw is TokenKwClass)
                        decl = new TokenDeclSDTypeClass(name, kw.prevToken is TokenKwPartial);
                    else
                        decl = new TokenDeclSDTypeInterface(name);
                    decl.outerSDType = outerLevels.Peek();

                     // Check for generic parameter list.
                    if(!ParseGenProtoParamList(ref t, decl))
                        continue;

                     // Splice in a TokenDeclSDType token that replaces the keyword and the name tokens 
                     // and any generic parameters including the '<', ','s and '>'.
                     //   kw = points to 'class' or 'interface' keyword.
                     //    t = points to just past last part of class name parsed, hopefully a ':' or '{'.
                    decl.prevToken = decl.isPartial ? kw.prevToken.prevToken : kw.prevToken;
                    decl.nextToken = t;
                    decl.prevToken.nextToken = decl;
                    decl.nextToken.prevToken = decl;

                     // Enter it in name lists so it can be seen by others.
                    Token partialNewBody = CatalogSDTypeDecl(decl);

                     // Start inner type definitions.
                    braceLevels.Push(openBraceLevel);
                    outerLevels.Push(decl);

                     // Scan the body starting on for before the '{'.
                     //
                     // If this body had an old partial merged into it,
                     // resume scanning at the beginning of the new body, 
                     // ie, what used to be the first token after the '{' 
                     // before the old body was spliced in.
                    if(partialNewBody != null)
                    {
                         // We have a partial that has had old partial body merged 
                         // into new partial body.  So resume scanning at the beginning 
                         // of the new partial body so we don't get any duplicate scanning 
                         // of the old partial body.
                         //
                         //   <decl> ... { <oldbody> <newbody> }
                         //                          ^- resume scanning here
                         //                             but inc openBraceLevel because
                         //                             we skipped scanning the '{'
                        openBraceLevel++;
                        t = partialNewBody;
                    }
                    t = t.prevToken;
                    continue;
                }

                 // Check for 'delegate'.
                 // It always defines a new delegate.
                 // Delegates never define nested types.
                if(t is TokenKwDelegate)
                {
                    Token kw = t;
                    t = t.nextToken;

                     // Next thing might be an explicit return type or the delegate's name.
                     // If it's a type token, then it's the return type, simple enough.
                     // But if it's a name token, it might be the name of some other script-defined type.
                     // The way to tell is that the delegate name is followed by a '(', whereas an 
                     // explicit return type is followed by the delegate name.
                    Token retType = t;
                    TokenName delName = null;
                    Token u;
                    int angles = 0;
                    for(u = t; !(u is TokenKwParOpen); u = u.nextToken)
                    {
                        if((u is TokenKwSemi) || (u is TokenEnd))
                            break;
                        if(u is TokenKwCmpLT)
                            angles++;
                        if(u is TokenKwCmpGT)
                            angles--;
                        if(u is TokenKwRSh)
                            angles -= 2;  // idiot >>
                        if((angles == 0) && (u is TokenName))
                            delName = (TokenName)u;
                    }
                    if(!(u is TokenKwParOpen))
                    {
                        ErrorMsg(u, "expecting ( for delegate parameter list");
                        t = SkipPastSemi(t).prevToken;
                        continue;
                    }
                    if(delName == null)
                    {
                        ErrorMsg(u, "expecting delegate name");
                        t = SkipPastSemi(t).prevToken;
                        continue;
                    }
                    if(retType == delName)
                        retType = null;

                     // Malloc the script-defined type object.
                    TokenDeclSDTypeDelegate decl = new TokenDeclSDTypeDelegate(delName);
                    decl.outerSDType = outerLevels.Peek();

                     // Check for generic parameter list.
                    t = delName.nextToken;
                    if(!ParseGenProtoParamList(ref t, decl))
                        continue;

                     // Enter it in name lists so it can be seen by others.
                    CatalogSDTypeDecl(decl);

                     // Splice in the token that replaces the 'delegate' keyword and the whole name 
                     // (including the '<' name ... '>' parts). The return type token(s), if any, 
                     // follow the splice token and come before the '('.
                    decl.prevToken = kw.prevToken;
                    kw.prevToken.nextToken = decl;

                    if(retType == null)
                    {
                        decl.nextToken = t;
                        t.prevToken = decl;
                    }
                    else
                    {
                        decl.nextToken = retType;
                        retType.prevToken = decl;
                        retType.nextToken = t;
                        t.prevToken = retType;
                    }

                     // Scan for terminating ';'.
                     // There cannot be an intervening class, delegate, interfate, typedef, { or }.
                    for(t = decl; !(t is TokenKwSemi); t = u)
                    {
                        u = t.nextToken;
                        if((u is TokenEnd) ||
                            (u is TokenKwClass) ||
                            (u is TokenKwDelegate) ||
                            (u is TokenKwInterface) ||
                            (u is TokenKwTypedef) ||
                            (u is TokenKwBrcOpen) ||
                            (u is TokenKwBrcClose))
                        {
                            ErrorMsg(t, "delegate missing terminating ;");
                            break;
                        }
                    }
                    decl.endToken = t;
                    continue;
                }

                 // Check for 'typedef'.
                 // It always defines a new macro.
                 // Typedefs never define nested types.
                if(t is TokenKwTypedef)
                {
                    Token kw = t;
                    t = t.nextToken;

                    if(!(t is TokenName))
                    {
                        ErrorMsg(t, "expecting typedef name");
                        t = SkipPastSemi(t).prevToken;
                        continue;
                    }
                    TokenName tdName = (TokenName)t;
                    t = t.nextToken;

                     // Malloc the script-defined type object.
                    TokenDeclSDTypeTypedef decl = new TokenDeclSDTypeTypedef(tdName);
                    decl.outerSDType = outerLevels.Peek();

                     // Check for generic parameter list.
                    if(!ParseGenProtoParamList(ref t, decl))
                        continue;

                     // Enter it in name lists so it can be seen by others.
                    CatalogSDTypeDecl(decl);
                    numTypedefs++;

                     // Splice in the token that replaces the 'typedef' keyword and the whole name 
                     // (including the '<' name ... '>' parts).
                    decl.prevToken = kw.prevToken;
                    kw.prevToken.nextToken = decl;
                    decl.nextToken = t;
                    t.prevToken = decl;

                     // Scan for terminating ';'.
                     // There cannot be an intervening class, delegate, interfate, typedef, { or }.
                    Token u;
                    for(t = decl; !(t is TokenKwSemi); t = u)
                    {
                        u = t.nextToken;
                        if((u is TokenEnd) ||
                            (u is TokenKwClass) ||
                            (u is TokenKwDelegate) ||
                            (u is TokenKwInterface) ||
                            (u is TokenKwTypedef) ||
                            (u is TokenKwBrcOpen) ||
                            (u is TokenKwBrcClose))
                        {
                            ErrorMsg(t, "typedef missing terminating ;");
                            break;
                        }
                    }
                    decl.endToken = t;
                    continue;
                }
            }
        }

        /**
         * @brief Parse a possibly generic type definition's parameter list.
         * @param t = points to the possible opening '<' on entry
         *            points just past the closing '>' on return
         * @param decl = the generic type being declared
         * @returns false: parse error
         *           true: decl.genParams = filled in with parameter list
         *                 decl.innerSDTypes = filled in with parameter list
         */
        private bool ParseGenProtoParamList(ref Token t, TokenDeclSDType decl)
        {
             // Maybe there aren't any generic parameters.
             // If so, leave decl.genParams = null.
            if(!(t is TokenKwCmpLT))
                return true;

             // Build list of generic parameter names.
            Dictionary<string, int> parms = new Dictionary<string, int>();
            do
            {
                t = t.nextToken;
                if(!(t is TokenName))
                {
                    ErrorMsg(t, "expecting generic parameter name");
                    break;
                }
                TokenName tn = (TokenName)t;
                if(parms.ContainsKey(tn.val))
                    ErrorMsg(tn, "duplicate use of generic parameter name");
                else
                    parms.Add(tn.val, parms.Count);
                t = t.nextToken;
            } while(t is TokenKwComma);

            if(!(t is TokenKwCmpGT))
            {
                ErrorMsg(t, "expecting , for more params or > to end param list");
                return false;
            }
            t = t.nextToken;
            decl.genParams = parms;

            return true;
        }

        /**
         * @brief Catalog a script-defined type.
         *        Its short name (eg, 'Node') gets put in the next outer level (eg, 'List')'s inner type definition table.
         *        Its long name (eg, 'List.Node') gets put in the global type definition table.
         */
        public Token CatalogSDTypeDecl(TokenDeclSDType decl)
        {
            string longName = decl.longName.val;
            TokenDeclSDType dupDecl;
            if(!tokenScript.sdSrcTypesTryGetValue(longName, out dupDecl))
            {
                tokenScript.sdSrcTypesAdd(longName, decl);
                if(decl.outerSDType != null)
                    decl.outerSDType.innerSDTypes.Add(decl.shortName.val, decl);

                return null;
            }

            if(!dupDecl.isPartial || !decl.isPartial)
            {
                ErrorMsg(decl, "duplicate definition of type " + longName);
                ErrorMsg(dupDecl, "previous definition here");
                return null;
            }

            if(!GenericParametersMatch(decl, dupDecl))
                ErrorMsg(decl, "all partial class generic parameters must match");

             // Have new declaration be the cataloged one because body is going to get 
             // snipped out of old declaration and pasted into new declaration.
            tokenScript.sdSrcTypesRep(longName, decl);
            if(decl.outerSDType != null)
                decl.outerSDType.innerSDTypes[decl.shortName.val] = decl;

             // Find old partial definition's opening brace.
            Token dupBrcOpen;
            for(dupBrcOpen = dupDecl; !(dupBrcOpen is TokenKwBrcOpen); dupBrcOpen = dupBrcOpen.nextToken)
            {
                if(dupBrcOpen == dupDecl.endToken)
                {
                    ErrorMsg(dupDecl, "missing {");
                    return null;
                }
            }

             // Find new partial definition's opening brace.
            Token brcOpen;
            for(brcOpen = decl; !(brcOpen is TokenKwBrcOpen); brcOpen = brcOpen.nextToken)
            {
                if(brcOpen is TokenEnd)
                {
                    ErrorMsg(decl, "missing {");
                    return null;
                }
            }
            Token body = brcOpen.nextToken;

             // Stick old partial definition's extends/implementeds list just 
             // in front of new partial definition's extends/implementeds list.
             //
             //    class    oldextimp    {          oldbody    }                 ...
             //   dupDecl               dupBrcOpen            dupDecl.endToken
             //
             //    class    newextimp    {          newbody    }                 ...
             //   decl                  brcOpen    body       decl.endToken
             //
             // becomes
             //
             //    class             ...
             //   dupDecl
             //   dupDecl.endToken
             //
             //    class    oldextimp   newextimp    {          oldbody   newbody    }              ...
             //   decl                              brcOpen              body       decl.endToken
            if(dupBrcOpen != dupDecl.nextToken)
            {
                dupBrcOpen.prevToken.nextToken = decl.nextToken;
                dupDecl.nextToken.prevToken = decl;
                decl.nextToken.prevToken = dupBrcOpen.prevToken;
                decl.nextToken = dupDecl.nextToken;
            }

             // Stick old partial definition's body just 
             // in front of new partial definition's body.
            if(dupBrcOpen.nextToken != dupDecl.endToken)
            {
                dupBrcOpen.nextToken.prevToken = brcOpen;
                dupDecl.endToken.prevToken.nextToken = body;
                body.prevToken = dupDecl.endToken.prevToken;
                brcOpen.nextToken = dupBrcOpen.nextToken;
            }

             // Null out old definition's extends/implementeds list and body 
             // by having the declaration token be the only thing left.
            dupDecl.nextToken = dupDecl.endToken.nextToken;
            dupDecl.nextToken.prevToken = dupDecl;
            dupDecl.endToken = dupDecl;

            return body;
        }

        /**
         * @brief Determine whether or not the generic parameters of two class declarations match exactly.
         */
        private static bool GenericParametersMatch(TokenDeclSDType c1, TokenDeclSDType c2)
        {
            if((c1.genParams == null) && (c2.genParams == null))
                return true;
            if((c1.genParams == null) || (c2.genParams == null))
                return false;
            Dictionary<string, int> gp1 = c1.genParams;
            Dictionary<string, int> gp2 = c2.genParams;
            if(gp1.Count != gp2.Count)
                return false;
            foreach(KeyValuePair<string, int> kvp1 in gp1)
            {
                int v2;
                if(!gp2.TryGetValue(kvp1.Key, out v2))
                    return false;
                if(v2 != kvp1.Value)
                    return false;
            }
            return true;
        }

        /**
         * @brief Replace all TokenName tokens that refer to the script-defined types with 
         *        corresponding TokenTypeSDType{Class,Delegate,GenParam,Interface} tokens.
         *        Also handle generic references, ie, recognize that 'List<integer>' is an
         *        instantiation of 'List<>' and instantiate the generic.
         */
        private const uint REPEAT_NOTYPE = 1;
        private const uint REPEAT_INSTGEN = 2;
        private const uint REPEAT_SUBST = 4;

        private void ParseSDTypePreScanPassTwo(Token tokenBegin)
        {
            List<Token> noTypes = new List<Token>();
            TokenDeclSDType outerSDType;
            uint repeat;

            do
            {
                repeat = 0;
                outerSDType = null;
                noTypes.Clear();

                for(Token t = tokenBegin; !((t = t.nextToken) is TokenEnd);)
                {
                     // Maybe it's time to pop out of an outer class definition.
                    if((outerSDType != null) && (outerSDType.endToken == t))
                    {
                        outerSDType = outerSDType.outerSDType;
                        continue;
                    }

                     // Skip completely over any script-defined generic prototypes.
                     // We only need to process their instantiations which are non-
                     // generic versions of the generics.
                    if((t is TokenDeclSDType) && (((TokenDeclSDType)t).genParams != null))
                    {
                        t = ((TokenDeclSDType)t).endToken;
                        continue;
                    }

                     // Check for beginning of non-generic script-defined type definitions.
                     // They can have nested definitions in their innerSDTypes[] that match 
                     // name tokens, so add them to the stack.
                     //
                     // But just ignore any preliminary partial definitions as they have had 
                     // their entire contents spliced out and spliced into a subsequent partial 
                     // definition.  So if we originally had:
                     //    partial class Abc { public intenger one; }
                     //    partial class Abc { public intenger two; }
                     // We now have:
                     //    partial_class_Abc    <== if we are here, just ignore the partial_class_Abc token
                     //    partial_class_Abc { public intenger one; public intenger two; }
                    if(t is TokenDeclSDType)
                    {
                        if(((TokenDeclSDType)t).endToken != t)
                            outerSDType = (TokenDeclSDType)t;

                        continue;
                    }

                     // For names not preceded by a '.', scan the script-defined type definition 
                     // stack for that name.  Splice the name out and replace with equivalent token.
                    if((t is TokenName) && !(t.prevToken is TokenKwDot))
                        t = TrySpliceTypeRef(t, outerSDType, ref repeat, noTypes);

                     // This handles types such as integer[,][], List<string>[], etc.
                     // They are an instantiation of an internally generated type of the same name, brackets and all.
                     // Note that to malloc an array, use something like 'new float[,][](3,5)', not 'new float[3,5][]'.
                     //
                     // Note that we must not get confused by $idxprop property declarations such as:
                     //    float [string kee] { get { ... } }
                     // ... and try to convert 'float' '[' to an array type.
                    if((t is TokenType) && (t.nextToken is TokenKwBrkOpen))
                    {
                        if((t.nextToken.nextToken is TokenKwBrkClose) ||
                            (t.nextToken.nextToken is TokenKwComma))
                        {
                            t = InstantiateJaggedArray(t, tokenBegin, ref repeat);
                        }
                    }
                }

                 // If we instantiated a generic, loop back to process its contents
                 // just as if the source code had the instantiated code to begin with.
                 // Also repeat if we found a non-type inside the <> of a generic reference 
                 // provided we have made at least one name->type substitution.
            } while(((repeat & REPEAT_INSTGEN) != 0) ||
                     ((repeat & (REPEAT_NOTYPE | REPEAT_SUBST)) == (REPEAT_NOTYPE | REPEAT_SUBST)));

             // These are places where we required a type be present, 
             // eg, a generic type argument or the body of a typedef.
            foreach(Token t in noTypes)
                ErrorMsg(t, "looking for type");
        }

        /**
         * @brief Try to convert the source token string to a type reference
         *        and splice the type reference into the source token string
         *        replacing the original token(s).
         * @param t = points to the initial TokenName token
         * @param outerSDType = null: this is a top-level code reference
         *                      else: this code is within outerSDType
         * @returns pointer to last token parsed
         *          possibly with spliced-in type token
         *          repeat = possibly set true if need to do another pass
         */
        private Token TrySpliceTypeRef(Token t, TokenDeclSDType outerSDType, ref uint repeat, List<Token> noTypes)
        {
            Token start = t;
            string tnamestr = ((TokenName)t).val;

             // Look for the name as a type declared by outerSDType or anything
             // even farther out than that.  If not found, simply return 
             // without updating t, meaning that t isn't the name of a type.
            TokenDeclSDType decl = null;
            while(outerSDType != null)
            {
                if(outerSDType.innerSDTypes.TryGetValue(tnamestr, out decl))
                    break;
                outerSDType = outerSDType.outerSDType;
            }
            if((outerSDType == null) && !tokenScript.sdSrcTypesTryGetValue(tnamestr, out decl))
                return t;

            TokenDeclSDType instdecl;
            while(true)
            {
                 // If it is a generic type, it must be followed by instantiation arguments.
                instdecl = decl;
                if(decl.genParams != null)
                {
                    t = t.nextToken;
                    if(!(t is TokenKwCmpLT))
                    {
                        ErrorMsg(t, "expecting < for generic argument list");
                        return t;
                    }
                    tnamestr += "<";
                    int nArgs = decl.genParams.Count;
                    TokenType[] genArgs = new TokenType[nArgs];
                    for(int i = 0; i < nArgs;)
                    {
                        t = t.nextToken;
                        if(!(t is TokenType))
                        {
                            repeat |= REPEAT_NOTYPE;
                            noTypes.Add(t);
                            return t.prevToken;  // make sure name gets processed
                                                 // so substitution can occur on it
                        }
                        TokenType ga = (TokenType)t;
                        genArgs[i] = ga;
                        tnamestr += ga.ToString();
                        t = t.nextToken;
                        if(++i < nArgs)
                        {
                            if(!(t is TokenKwComma))
                            {
                                ErrorMsg(t, "expecting , for more generic arguments");
                                return t;
                            }
                            tnamestr += ",";
                        }
                    }
                    if(t is TokenKwRSh)
                    {  // idiot >>
                        Token u = new TokenKwCmpGT(t);
                        Token v = new TokenKwCmpGT(t);
                        v.posn++;
                        u.prevToken = t.prevToken;
                        u.nextToken = v;
                        v.nextToken = t.nextToken;
                        v.prevToken = u;
                        u.prevToken.nextToken = u;
                        v.nextToken.prevToken = v;
                        t = u;
                    }
                    if(!(t is TokenKwCmpGT))
                    {
                        ErrorMsg(t, "expecting > at end of generic argument list");
                        return t;
                    }
                    tnamestr += ">";
                    if(outerSDType != null)
                    {
                        outerSDType.innerSDTypes.TryGetValue(tnamestr, out instdecl);
                    }
                    else
                    {
                        tokenScript.sdSrcTypesTryGetValue(tnamestr, out instdecl);
                    }

                     // Couldn't find 'List<string>' but found 'List' and we have genArgs = 'string'.
                     // Instantiate the generic to create 'List<string>'.  This splices the definition 
                     // of 'List<string>' into the source token stream just as if it had been there all 
                     // along.  We have to then repeat the scan to process the instance's contents.
                    if(instdecl == null)
                    {
                        instdecl = decl.InstantiateGeneric(tnamestr, genArgs, this);
                        CatalogSDTypeDecl(instdecl);
                        repeat |= REPEAT_INSTGEN;
                    }
                }

                 // Maybe caller wants a subtype by putting a '.' following all that.
                if(!(t.nextToken is TokenKwDot))
                    break;
                if(!(t.nextToken.nextToken is TokenName))
                    break;
                tnamestr = ((TokenName)t.nextToken.nextToken).val;
                if(!instdecl.innerSDTypes.TryGetValue(tnamestr, out decl))
                    break;
                t = t.nextToken.nextToken;
                outerSDType = instdecl;
            }

             // Create a reference in the source to the definition
             // that encapsulates the long dotted type name given in
             // the source, and replace the long dotted type name in
             // the source with the reference token, eg, replace 
             // 'Dictionary' '<' 'string' ',' 'integer' '>' '.' 'ValueList'
             // with 'Dictionary<string,integer>.ValueList'.
            TokenType refer = instdecl.MakeRefToken(start);
            if(refer == null)
            {
                // typedef body is not yet a type
                noTypes.Add(start);
                repeat |= REPEAT_NOTYPE;
                return start;
            }
            refer.prevToken = start.prevToken;  // start points right at the first TokenName
            refer.nextToken = t.nextToken;      // t points at the last TokenName or TokenKwCmpGT
            refer.prevToken.nextToken = refer;
            refer.nextToken.prevToken = refer;
            repeat |= REPEAT_SUBST;

            return refer;
        }

        /**
         * @brief We are known to have <type>'[' so make an equivalent array type.
         * @param t = points to the TokenType
         * @param tokenBegin = where we can safely splice in new array class definitions
         * @param repeat = set REPEAT_INSTGEN if new type created
         * @returns pointer to last token parsed
         *          possibly with spliced-in type token
         *          repeat = possibly set true if need to do another pass
         */
        private Token InstantiateJaggedArray(Token t, Token tokenBegin, ref uint repeat)
        {
            Token start = t;
            TokenType ofType = (TokenType)t;

            Stack<int> ranks = new Stack<int>();

             // When script specifies 'float[,][]' it means a two-dimensional matrix
             // that points to one-dimensional vectors of floats.  So we would push 
             // a 2 then a 1 in this parsing code...
            do
            {
                t = t.nextToken;                // point at '['
                int rank = 0;
                do
                {
                    rank++;                // count '[' and ','s
                    t = t.nextToken;        // point at ',' or ']'
                } while(t is TokenKwComma);
                if(!(t is TokenKwBrkClose))
                {
                    ErrorMsg(t, "expecting only [ , or ] for array type specification");
                    return t;
                }
                ranks.Push(rank);
            } while(t.nextToken is TokenKwBrkOpen);

             // Now we build the types in reverse order.  For the example above we will:
             //   first, create a type that is a one-dimensional vector of floats, float[]
             //   second, create a type that is a two-dimensional matrix of that.
             // This keeps declaration and referencing similar, eg, 
             //   float[,][] jag = new float[,][] (3,4);
             //     jag[i,j][k] ... is used to access the elements
            do
            {
                int rank = ranks.Pop();
                TokenDeclSDType decl = InstantiateFixedArray(rank, ofType, tokenBegin, ref repeat);
                ofType = decl.MakeRefToken(ofType);
            } while(ranks.Count > 0);

             // Finally splice in the resultant array type to replace the original tokens.
            ofType.prevToken = start.prevToken;
            ofType.nextToken = t.nextToken;
            ofType.prevToken.nextToken = ofType;
            ofType.nextToken.prevToken = ofType;

             // Resume parsing just after the spliced-in array type token.
            return ofType;
        }

        /**
         * @brief Instantiate a script-defined class type to handle fixed-dimension arrays.
         * @param rank = number of dimensions for the array
         * @param ofType = type of each element of the array
         * @returns script-defined class declaration created to handle the array
         */
        private TokenDeclSDType InstantiateFixedArray(int rank, TokenType ofType, Token tokenBegin, ref uint repeat)
        {
             // Create the array type's name.
             // If starting with a non-array type, just append the rank to it, eg, float + rank=1 -> float[]
             // If starting with an array type, slip this rank in front of existing array, eg, float[] + rank=2 -> float[,][].
             // This makes it consistent with what the script-writer sees for both a type specification and when 
             // referencing elements in a jagged array.
            string name = ofType.ToString();
            StringBuilder sb = new StringBuilder(name);
            int ix = name.IndexOf('[');
            if(ix < 0)
                ix = name.Length;
            sb.Insert(ix++, '[');
            for(int i = 0; ++i < rank;)
            {
                sb.Insert(ix++, ',');
            }
            sb.Insert(ix, ']');
            name = sb.ToString();

            TokenDeclSDType fa;
            if(!tokenScript.sdSrcTypesTryGetValue(name, out fa))
            {
                char suffix = 'O';
                if(ofType is TokenTypeChar)
                    suffix = 'C';
                if(ofType is TokenTypeFloat)
                    suffix = 'F';
                if(ofType is TokenTypeInt)
                    suffix = 'I';

                 // Don't already have one, create a new skeleton struct.
                 // Splice in a definition for the class at beginning of source file.
                 //
                 //    class <arraytypename> {
                fa = new TokenDeclSDTypeClass(new TokenName(tokenScript, name), false);
                CatalogSDTypeDecl(fa);
                repeat |= REPEAT_INSTGEN;
                ((TokenDeclSDTypeClass)fa).arrayOfType = ofType;
                ((TokenDeclSDTypeClass)fa).arrayOfRank = rank;

                Token t = SpliceAfter(tokenBegin, fa);
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                 //        public integer len0;
                 //        public integer len1;
                 //        ...
                 //        public object obj;
                for(int i = 0; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenKwPublic(t));
                    t = SpliceAfter(t, new TokenTypeInt(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                }

                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, new TokenTypeObject(t));
                t = SpliceAfter(t, new TokenName(t, "obj"));
                t = SpliceAfter(t, new TokenKwSemi(t));

                 //        public constructor (integer len0, integer len1, ...) {
                 //            this.len0 = len0;
                 //            this.len1 = len1;
                 //            ...
                 //            this.obj = xmrFixedArrayAlloc<suffix> (len0 * len1 * ...);
                 //        }
                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, new TokenKwConstructor(t));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                for(int i = 0; i < rank; i++)
                {
                    if(i > 0)
                        t = SpliceAfter(t, new TokenKwComma(t));
                    t = SpliceAfter(t, new TokenTypeInt(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                }
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                for(int i = 0; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenKwThis(t));
                    t = SpliceAfter(t, new TokenKwDot(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwAssign(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                }

                t = SpliceAfter(t, new TokenKwThis(t));
                t = SpliceAfter(t, new TokenKwDot(t));
                t = SpliceAfter(t, new TokenName(t, "obj"));
                t = SpliceAfter(t, new TokenKwAssign(t));
                t = SpliceAfter(t, new TokenName(t, "xmrFixedArrayAlloc" + suffix));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                for(int i = 0; i < rank; i++)
                {
                    if(i > 0)
                        t = SpliceAfter(t, new TokenKwMul(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                }
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwSemi(t));
                t = SpliceAfter(t, new TokenKwBrcClose(t));

                 //        public integer Length { get {
                 //            return this.len0 * this.len1 * ... ;
                 //        } }
                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "Length"));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));
                t = SpliceAfter(t, new TokenKwGet(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                t = SpliceAfter(t, new TokenKwRet(t));
                for(int i = 0; i < rank; i++)
                {
                    if(i > 0)
                        t = SpliceAfter(t, new TokenKwMul(t));
                    t = SpliceAfter(t, new TokenKwThis(t));
                    t = SpliceAfter(t, new TokenKwDot(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                }
                t = SpliceAfter(t, new TokenKwSemi(t));

                t = SpliceAfter(t, new TokenKwBrcClose(t));
                t = SpliceAfter(t, new TokenKwBrcClose(t));

                 //        public integer Length (integer dim) {
                 //            switch (dim) {
                 //                case 0: return this.len0;
                 //                case 1: return this.len1;
                 //                ...
                 //            }
                 //            return 0;
                 //        }
                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "Length"));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "dim"));
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                t = SpliceAfter(t, new TokenKwSwitch(t));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                t = SpliceAfter(t, new TokenName(t, "dim"));
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                for(int i = 0; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenKwCase(t));
                    t = SpliceAfter(t, new TokenInt(t, i));
                    t = SpliceAfter(t, new TokenKwColon(t));
                    t = SpliceAfter(t, new TokenKwRet(t));
                    t = SpliceAfter(t, new TokenKwThis(t));
                    t = SpliceAfter(t, new TokenKwDot(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                }
                t = SpliceAfter(t, new TokenKwBrcClose(t));

                t = SpliceAfter(t, new TokenKwRet(t));
                t = SpliceAfter(t, new TokenInt(t, 0));
                t = SpliceAfter(t, new TokenKwSemi(t));
                t = SpliceAfter(t, new TokenKwBrcClose(t));

                 //        public integer Index (integer idx0, integet idx1, ...) {
                 //            integer idx = idx0;
                 //            idx *= this.len1; idx += idx1;
                 //            idx *= this.len2; idx += idx2;
                 //            ...
                 //            return idx;
                 //        }
                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "Index"));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                for(int i = 0; i < rank; i++)
                {
                    if(i > 0)
                        t = SpliceAfter(t, new TokenKwComma(t));
                    t = SpliceAfter(t, new TokenTypeInt(t));
                    t = SpliceAfter(t, new TokenName(t, "idx" + i));
                }
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "idx"));
                t = SpliceAfter(t, new TokenKwAssign(t));
                t = SpliceAfter(t, new TokenName(t, "idx0"));
                t = SpliceAfter(t, new TokenKwSemi(t));

                for(int i = 1; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenName(t, "idx"));
                    t = SpliceAfter(t, new TokenKwAsnMul(t));
                    t = SpliceAfter(t, new TokenKwThis(t));
                    t = SpliceAfter(t, new TokenKwDot(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                    t = SpliceAfter(t, new TokenName(t, "idx"));
                    t = SpliceAfter(t, new TokenKwAsnAdd(t));
                    t = SpliceAfter(t, new TokenName(t, "idx" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                }

                t = SpliceAfter(t, new TokenKwRet(t));
                t = SpliceAfter(t, new TokenName(t, "idx"));
                t = SpliceAfter(t, new TokenKwSemi(t));
                t = SpliceAfter(t, new TokenKwBrcClose(t));

                 //        public <oftype> Get (integer idx0, integet idx1, ...) {
                 //            integer idx = idx0;
                 //            idx *= this.len1; idx += idx1;
                 //            idx *= this.len2; idx += idx2;
                 //            ...
                 //            return (<oftype>) xmrFixedArrayGet<suffix> (this.obj, idx);
                 //        }
                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, ofType.CopyToken(t));
                t = SpliceAfter(t, new TokenName(t, "Get"));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                for(int i = 0; i < rank; i++)
                {
                    if(i > 0)
                        t = SpliceAfter(t, new TokenKwComma(t));
                    t = SpliceAfter(t, new TokenTypeInt(t));
                    t = SpliceAfter(t, new TokenName(t, "idx" + i));
                }
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "idx"));
                t = SpliceAfter(t, new TokenKwAssign(t));
                t = SpliceAfter(t, new TokenName(t, "idx0"));
                t = SpliceAfter(t, new TokenKwSemi(t));

                for(int i = 1; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenName(t, "idx"));
                    t = SpliceAfter(t, new TokenKwAsnMul(t));
                    t = SpliceAfter(t, new TokenKwThis(t));
                    t = SpliceAfter(t, new TokenKwDot(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                    t = SpliceAfter(t, new TokenName(t, "idx"));
                    t = SpliceAfter(t, new TokenKwAsnAdd(t));
                    t = SpliceAfter(t, new TokenName(t, "idx" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                }

                t = SpliceAfter(t, new TokenKwRet(t));
                if(suffix == 'O')
                {
                    t = SpliceAfter(t, new TokenKwParOpen(t));
                    t = SpliceAfter(t, ofType.CopyToken(t));
                    t = SpliceAfter(t, new TokenKwParClose(t));
                }
                t = SpliceAfter(t, new TokenName(t, "xmrFixedArrayGet" + suffix));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                t = SpliceAfter(t, new TokenKwThis(t));
                t = SpliceAfter(t, new TokenKwDot(t));
                t = SpliceAfter(t, new TokenName(t, "obj"));
                t = SpliceAfter(t, new TokenKwComma(t));
                t = SpliceAfter(t, new TokenName(t, "idx"));
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwSemi(t));
                t = SpliceAfter(t, new TokenKwBrcClose(t));

                 //        public void Set (integer idx0, integer idx1, ..., <oftype> val) {
                 //            integer idx = idx0;
                 //            idx *= this.len1; idx += idx1;
                 //            idx *= this.len2; idx += idx2;
                 //            ...
                 //            xmrFixedArraySet<suffix> (this.obj, idx, val);
                 //        }
                t = SpliceAfter(t, new TokenKwPublic(t));
                t = SpliceAfter(t, new TokenTypeVoid(t));
                t = SpliceAfter(t, new TokenName(t, "Set"));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                for(int i = 0; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenTypeInt(t));
                    t = SpliceAfter(t, new TokenName(t, "idx" + i));
                    t = SpliceAfter(t, new TokenKwComma(t));
                }
                t = SpliceAfter(t, ofType.CopyToken(t));
                t = SpliceAfter(t, new TokenName(t, "val"));
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwBrcOpen(t));

                t = SpliceAfter(t, new TokenTypeInt(t));
                t = SpliceAfter(t, new TokenName(t, "idx"));
                t = SpliceAfter(t, new TokenKwAssign(t));
                t = SpliceAfter(t, new TokenName(t, "idx0"));
                t = SpliceAfter(t, new TokenKwSemi(t));
                for(int i = 1; i < rank; i++)
                {
                    t = SpliceAfter(t, new TokenName(t, "idx"));
                    t = SpliceAfter(t, new TokenKwAsnMul(t));
                    t = SpliceAfter(t, new TokenKwThis(t));
                    t = SpliceAfter(t, new TokenKwDot(t));
                    t = SpliceAfter(t, new TokenName(t, "len" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                    t = SpliceAfter(t, new TokenName(t, "idx"));
                    t = SpliceAfter(t, new TokenKwAsnAdd(t));
                    t = SpliceAfter(t, new TokenName(t, "idx" + i));
                    t = SpliceAfter(t, new TokenKwSemi(t));
                }

                t = SpliceAfter(t, new TokenName(t, "xmrFixedArraySet" + suffix));
                t = SpliceAfter(t, new TokenKwParOpen(t));
                t = SpliceAfter(t, new TokenKwThis(t));
                t = SpliceAfter(t, new TokenKwDot(t));
                t = SpliceAfter(t, new TokenName(t, "obj"));
                t = SpliceAfter(t, new TokenKwComma(t));
                t = SpliceAfter(t, new TokenName(t, "idx"));
                t = SpliceAfter(t, new TokenKwComma(t));
                t = SpliceAfter(t, new TokenName(t, "val"));
                t = SpliceAfter(t, new TokenKwParClose(t));
                t = SpliceAfter(t, new TokenKwSemi(t));

                t = SpliceAfter(t, new TokenKwBrcClose(t));
                t = SpliceAfter(t, new TokenKwBrcClose(t));
            }
            return fa;
        }
        private Token SpliceAfter(Token before, Token after)
        {
            after.nextToken = before.nextToken;
            after.prevToken = before;
            before.nextToken = after;
            after.nextToken.prevToken = after;
            return after;
        }

        /**
         * @brief Parse script-defined type declarations.
         * @param token = points to possible script-defined type keyword
         * @param outerSDType = null: top-level type
         *                      else: sub-type of this type
         * @param flags = access level (SDT_{PRIVATE,PROTECTED,PUBLIC})
         * @returns true: something defined; else: not a sd type def
         */
        private bool ParseDeclSDTypes(ref Token token, TokenDeclSDType outerSDType, uint flags)
        {
            if(!(token is TokenDeclSDType))
                return false;

            TokenDeclSDType decl = (TokenDeclSDType)token;

            /*
             * If declaration of generic type, skip it.
             * The instantiations get parsed (ie, when we know the concrete types)
             * below because they appear as non-generic types.
             */
            if(decl.genParams != null)
            {
                token = decl.endToken.nextToken;
                return true;
            }

            /*
             * Also skip over any typedefs.  They were all processed in 
             * ParseSDTypePreScanPassTwo().
             */
            if(decl is TokenDeclSDTypeTypedef)
            {
                token = decl.endToken.nextToken;
                return true;
            }

            /*
             * Non-generic types get parsed inline because we know all their types.
             */
            if(decl is TokenDeclSDTypeClass)
            {
                ParseDeclClass(ref token, outerSDType, flags);
                return true;
            }
            if(decl is TokenDeclSDTypeDelegate)
            {
                ParseDeclDelegate(ref token, outerSDType, flags);
                return true;
            }
            if(decl is TokenDeclSDTypeInterface)
            {
                ParseDeclInterface(ref token, outerSDType, flags);
                return true;
            }

            throw new Exception("unhandled token " + token.GetType().ToString());
        }

        /**
         * @brief Parse a class declaration.
         * @param token = points to TokenDeclSDTypeClass token
         *                points just past closing '}' on return
         * @param outerSDType = null: this is a top-level class
         *                      else: this class is being defined inside this type
         * @param flags = SDT_{PRIVATE,PROTECTED,PUBLIC}
         */
        private void ParseDeclClass(ref Token token, TokenDeclSDType outerSDType, uint flags)
        {
            bool haveExplicitConstructor = false;
            Token u = token;
            TokenDeclSDTypeClass tokdeclcl;

            tokdeclcl = (TokenDeclSDTypeClass)u;
            tokdeclcl.outerSDType = outerSDType;
            tokdeclcl.accessLevel = flags;
            u = u.nextToken;

            // maybe it is a partial class that had its body snipped out
            // by a later partial class declaration of the same class
            if(tokdeclcl.endToken == tokdeclcl)
            {
                token = u;
                return;
            }

            // make this class the currently compiled class
            // used for retrieving stuff like 'this' possibly
            // in field initialization code
            TokenDeclSDType saveCurSDType = currentDeclSDType;
            currentDeclSDType = tokdeclcl;

            // next can be ':' followed by list of implemented
            // interfaces and one extended class
            if(u is TokenKwColon)
            {
                u = u.nextToken;
                while(true)
                {
                    if(u is TokenTypeSDTypeClass)
                    {
                        TokenDeclSDTypeClass c = ((TokenTypeSDTypeClass)u).decl;
                        if(tokdeclcl.extends == null)
                        {
                            tokdeclcl.extends = c;
                        }
                        else if(tokdeclcl.extends != c)
                        {
                            ErrorMsg(u, "can extend from only one class");
                        }
                    }
                    else if(u is TokenTypeSDTypeInterface)
                    {
                        TokenDeclSDTypeInterface i = ((TokenTypeSDTypeInterface)u).decl;
                        i.AddToClassDecl(tokdeclcl);
                    }
                    else
                    {
                        ErrorMsg(u, "expecting class or interface name");
                        if(u is TokenKwBrcOpen)
                            break;
                    }
                    u = u.nextToken;

                    // allow : in case it is spliced from multiple partial class definitions
                    if(!(u is TokenKwComma) && !(u is TokenKwColon))
                        break;
                    u = u.nextToken;
                }
            }

            // next must be '{' to open class declaration body
            if(!(u is TokenKwBrcOpen))
            {
                ErrorMsg(u, "expecting { to open class declaration body");
                token = SkipPastSemi(token);
                goto ret;
            }
            token = u.nextToken;

            // push a var frame to put all the class members in
            tokdeclcl.members.thisClass = tokdeclcl;
            tokenScript.PushVarFrame(tokdeclcl.members);

             // Create a function $instfieldnit to hold all explicit
             // instance field initializations.
            TokenDeclVar ifiFunc = new TokenDeclVar(tokdeclcl, null, tokenScript);
            ifiFunc.name = new TokenName(ifiFunc, "$instfieldinit");
            ifiFunc.retType = new TokenTypeVoid(ifiFunc);
            ifiFunc.argDecl = new TokenArgDecl(ifiFunc);
            ifiFunc.sdtClass = tokdeclcl;
            ifiFunc.sdtFlags = SDT_PUBLIC | SDT_NEW;
            TokenStmtBlock ifiBody = new TokenStmtBlock(ifiFunc);
            ifiBody.function = ifiFunc;
            ifiFunc.body = ifiBody;
            tokdeclcl.instFieldInit = ifiFunc;
            tokenScript.AddVarEntry(ifiFunc);

             // Create a function $staticfieldnit to hold all explicit
             // static field initializations.
            TokenDeclVar sfiFunc = new TokenDeclVar(tokdeclcl, null, tokenScript);
            sfiFunc.name = new TokenName(sfiFunc, "$staticfieldinit");
            sfiFunc.retType = new TokenTypeVoid(sfiFunc);
            sfiFunc.argDecl = new TokenArgDecl(sfiFunc);
            sfiFunc.sdtClass = tokdeclcl;
            sfiFunc.sdtFlags = SDT_PUBLIC | SDT_STATIC | SDT_NEW;
            TokenStmtBlock sfiBody = new TokenStmtBlock(sfiFunc);
            sfiBody.function = sfiFunc;
            sfiFunc.body = sfiBody;
            tokdeclcl.staticFieldInit = sfiFunc;
            tokenScript.AddVarEntry(sfiFunc);

            // process declaration statements until '}'
            while(!(token is TokenKwBrcClose))
            {
                if(token is TokenKwSemi)
                {
                    token = token.nextToken;
                    continue;
                }

                 // Check for all qualifiers.
                 // typedef has an implied 'public' qualifier.
                flags = SDT_PUBLIC;
                if(!(token is TokenDeclSDTypeTypedef))
                {
                    flags = ParseQualifierFlags(ref token);
                }

                 // Parse nested script-defined type definitions.
                if(ParseDeclSDTypes(ref token, tokdeclcl, flags))
                    continue;

                 // constant <name> = <rval> ;
                if(token is TokenKwConst)
                {
                    if((flags & (SDT_ABSTRACT | SDT_NEW | SDT_OVERRIDE | SDT_VIRTUAL)) != 0)
                    {
                        ErrorMsg(token, "cannot have abstract, new, override or virtual field");
                    }
                    TokenDeclVar var = ParseDeclVar(ref token, null);
                    if(var != null)
                    {
                        var.sdtClass = tokdeclcl;
                        var.sdtFlags = flags | SDT_STATIC;
                    }
                    continue;
                }

                 // <type> <name> ;
                 // <type> <name> = <rval> ;
                if((token is TokenType) &&
                    (token.nextToken is TokenName) &&
                    ((token.nextToken.nextToken is TokenKwSemi) ||
                     (token.nextToken.nextToken is TokenKwAssign)))
                {
                    if((flags & (SDT_ABSTRACT | SDT_FINAL | SDT_NEW | SDT_OVERRIDE | SDT_VIRTUAL)) != 0)
                    {
                        ErrorMsg(token, "cannot have abstract, final, new, override or virtual field");
                    }
                    TokenDeclVar var = ParseDeclVar(ref token, ifiFunc);
                    if(var != null)
                    {
                        var.sdtClass = tokdeclcl;
                        var.sdtFlags = flags;
                        if((flags & SDT_STATIC) != 0)
                        {
                            // <type>.<name> = <init>;
                            TokenLValSField left = new TokenLValSField(var.init);
                            left.baseType = tokdeclcl.MakeRefToken(var);
                            left.fieldName = var.name;
                            DoVarInit(sfiFunc, left, var.init);
                        }
                        else if(var.init != null)
                        {
                            // this.<name> = <init>;
                            TokenLValIField left = new TokenLValIField(var.init);
                            left.baseRVal = new TokenRValThis(var.init, tokdeclcl);
                            left.fieldName = var.name;
                            DoVarInit(ifiFunc, left, var.init);
                        }
                    }
                    continue;
                }

                 // <type> <name> [ : <implintfs> ] { [ get { <body> } ] [ set { <body> } ] }
                 // <type> '[' ... ']' [ : <implintfs> ] { [ get { <body> } ] [ set { <body> } ] }
                bool prop = (token is TokenType) &&
                    (token.nextToken is TokenName) &&
                    (token.nextToken.nextToken is TokenKwBrcOpen ||
                     token.nextToken.nextToken is TokenKwColon);
                prop |= (token is TokenType) && (token.nextToken is TokenKwBrkOpen);
                if(prop)
                {
                    TokenDeclVar var = ParseProperty(ref token, (flags & SDT_ABSTRACT) != 0, true);
                    if(var != null)
                    {
                        var.sdtClass = tokdeclcl;
                        var.sdtFlags = flags;
                        if(var.getProp != null)
                        {
                            var.getProp.sdtClass = tokdeclcl;
                            var.getProp.sdtFlags = flags;
                        }
                        if(var.setProp != null)
                        {
                            var.setProp.sdtClass = tokdeclcl;
                            var.setProp.sdtFlags = flags;
                        }
                    }
                    continue;
                }

                 // 'constructor' '(' arglist ')' [ ':' [ 'base' ] '(' baseconstructorcall ')' ] '{' body '}'
                if(token is TokenKwConstructor)
                {
                    ParseSDTClassCtorDecl(ref token, flags, tokdeclcl);
                    haveExplicitConstructor = true;
                    continue;
                }

                 // <type> <name> <funcargs> <funcbody>
                 // method with explicit return type
                if(token is TokenType)
                {
                    ParseSDTClassMethodDecl(ref token, flags, tokdeclcl);
                    continue;
                }

                 // <name> <funcargs> <funcbody>
                 // method returning void
                if((token is TokenName) || ((token is TokenKw) && ((TokenKw)token).sdtClassOp))
                {
                    ParseSDTClassMethodDecl(ref token, flags, tokdeclcl);
                    continue;
                }

                 // That's all we support in a class declaration.
                ErrorMsg(token, "expecting field or method declaration");
                token = SkipPastSemi(token);
            }

             // If script didn't specify any constructor, create a default no-argument one.
            if(!haveExplicitConstructor)
            {
                TokenDeclVar tokenDeclFunc = new TokenDeclVar(token, null, tokenScript);
                tokenDeclFunc.name = new TokenName(token, "$ctor");
                tokenDeclFunc.retType = new TokenTypeVoid(token);
                tokenDeclFunc.argDecl = new TokenArgDecl(token);
                tokenDeclFunc.sdtClass = tokdeclcl;
                tokenDeclFunc.sdtFlags = SDT_PUBLIC | SDT_NEW;
                tokenDeclFunc.body = new TokenStmtBlock(token);
                tokenDeclFunc.body.function = tokenDeclFunc;

                if(tokdeclcl.extends != null)
                {
                    SetUpDefaultBaseCtorCall(tokenDeclFunc);
                }
                else
                {
                    // default constructor that doesn't do anything is trivial
                    tokenDeclFunc.triviality = Triviality.trivial;
                }

                tokenScript.AddVarEntry(tokenDeclFunc);
            }

             // Skip over the closing brace and pop corresponding var frame.
            token = token.nextToken;
            tokenScript.PopVarFrame();
            ret:
            currentDeclSDType = saveCurSDType;
        }

        /**
         * @brief Parse out abstract/override/private/protected/public/static/virtual keywords.
         * @param token = first token to evaluate
         * @returns flags found; token = unprocessed token
         */
        private Dictionary<uint, Token> foundFlags = new Dictionary<uint, Token>();
        private uint ParseQualifierFlags(ref Token token)
        {
            foundFlags.Clear();
            while(true)
            {
                if(token is TokenKwPrivate)
                {
                    token = AddQualifierFlag(token, SDT_PRIVATE, SDT_PROTECTED | SDT_PUBLIC);
                    continue;
                }
                if(token is TokenKwProtected)
                {
                    token = AddQualifierFlag(token, SDT_PROTECTED, SDT_PRIVATE | SDT_PUBLIC);
                    continue;
                }
                if(token is TokenKwPublic)
                {
                    token = AddQualifierFlag(token, SDT_PUBLIC, SDT_PRIVATE | SDT_PROTECTED);
                    continue;
                }
                if(token is TokenKwAbstract)
                {
                    token = AddQualifierFlag(token, SDT_ABSTRACT, SDT_FINAL | SDT_STATIC | SDT_VIRTUAL);
                    continue;
                }
                if(token is TokenKwFinal)
                {
                    token = AddQualifierFlag(token, SDT_FINAL, SDT_ABSTRACT | SDT_VIRTUAL);
                    continue;
                }
                if(token is TokenKwNew)
                {
                    token = AddQualifierFlag(token, SDT_NEW, SDT_OVERRIDE);
                    continue;
                }
                if(token is TokenKwOverride)
                {
                    token = AddQualifierFlag(token, SDT_OVERRIDE, SDT_NEW | SDT_STATIC);
                    continue;
                }
                if(token is TokenKwStatic)
                {
                    token = AddQualifierFlag(token, SDT_STATIC, SDT_ABSTRACT | SDT_OVERRIDE | SDT_VIRTUAL);
                    continue;
                }
                if(token is TokenKwVirtual)
                {
                    token = AddQualifierFlag(token, SDT_VIRTUAL, SDT_ABSTRACT | SDT_STATIC);
                    continue;
                }
                break;
            }

            uint flags = 0;
            foreach(uint flag in foundFlags.Keys)
                flags |= flag;

            if((flags & (SDT_PRIVATE | SDT_PROTECTED | SDT_PUBLIC)) == 0)
                ErrorMsg(token, "must specify exactly one of private, protected or public");

            return flags;
        }
        private Token AddQualifierFlag(Token token, uint add, uint confs)
        {
            while(confs != 0)
            {
                uint conf = (uint)(confs & -confs);
                Token confToken;
                if(foundFlags.TryGetValue(conf, out confToken))
                {
                    ErrorMsg(token, "conflicts with " + confToken.ToString());
                }
                confs -= conf;
            }
            foundFlags[add] = token;
            return token.nextToken;
        }

        /**
         * @brief Parse a property declaration.
         * @param token = points to the property type token on entry
         *                points just past the closing brace on return
         * @param abs = true: property is abstract
         *             false: property is concrete
         * @param imp = allow implemented interface specs
         * @returns null: parse failure
         *          else: property
         *
         * <type> <name> [ : <implintfs> ] { [ get { <body> } ] [ set { <body> } ] }
         * <type> '[' ... ']' [ : <implintfs> ] { [ get { <body> } ] [ set { <body> } ] }
         */
        private TokenDeclVar ParseProperty(ref Token token, bool abs, bool imp)
        {
             // Parse out the property's type and name.
             //   <type> <name>
            TokenType type = (TokenType)token;
            TokenName name;
            TokenArgDecl args;
            Token argTokens = null;
            token = token.nextToken;
            if(token is TokenKwBrkOpen)
            {
                argTokens = token;
                name = new TokenName(token, "$idxprop");
                args = ParseFuncArgs(ref token, typeof(TokenKwBrkClose));
            }
            else
            {
                name = (TokenName)token;
                token = token.nextToken;
                args = new TokenArgDecl(token);
            }

             // Maybe it claims to implement some interface properties.
             //   [ ':' <ifacetype>[.<propname>] ',' ... ]
            TokenIntfImpl implements = null;
            if(token is TokenKwColon)
            {
                implements = ParseImplements(ref token, name);
                if(implements == null)
                    return null;
                if(!imp)
                {
                    ErrorMsg(token, "cannot implement interface property");
                }
            }

             // Should have an opening brace.
            if(!(token is TokenKwBrcOpen))
            {
                ErrorMsg(token, "expect { to open property definition");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;

             // Parse out the getter and/or setter.
             //   'get' { <body> | ';' }
             //   'set' { <body> | ';' }
            TokenDeclVar getFunc = null;
            TokenDeclVar setFunc = null;
            while(!(token is TokenKwBrcClose))
            {
                 // Maybe create a getter function.
                if(token is TokenKwGet)
                {
                    getFunc = new TokenDeclVar(token, null, tokenScript);
                    getFunc.name = new TokenName(token, name.val + "$get");
                    getFunc.retType = type;
                    getFunc.argDecl = args;
                    getFunc.implements = MakePropertyImplements(implements, "$get");

                    token = token.nextToken;
                    if(!ParseFunctionBody(ref token, getFunc, abs))
                    {
                        getFunc = null;
                    }
                    else if(!tokenScript.AddVarEntry(getFunc))
                    {
                        ErrorMsg(getFunc, "duplicate getter");
                    }
                    continue;
                }

                 // Maybe create a setter function.
                if(token is TokenKwSet)
                {
                    TokenArgDecl argDecl = args;
                    if(getFunc != null)
                    {
                        argDecl = (argTokens == null) ? new TokenArgDecl(token) :
                            ParseFuncArgs(ref argTokens, typeof(TokenKwBrkClose));
                    }
                    argDecl.AddArg(type, new TokenName(token, "value"));

                    setFunc = new TokenDeclVar(token, null, tokenScript);
                    setFunc.name = new TokenName(token, name.val + "$set");
                    setFunc.retType = new TokenTypeVoid(token);
                    setFunc.argDecl = argDecl;
                    setFunc.implements = MakePropertyImplements(implements, "$set");

                    token = token.nextToken;
                    if(!ParseFunctionBody(ref token, setFunc, abs))
                    {
                        setFunc = null;
                    }
                    else if(!tokenScript.AddVarEntry(setFunc))
                    {
                        ErrorMsg(setFunc, "duplicate setter");
                    }
                    continue;
                }

                ErrorMsg(token, "expecting get or set");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;

            if((getFunc == null) && (setFunc == null))
            {
                ErrorMsg(name, "must specify at least one of get, set");
                return null;
            }

             // Set up a variable for the property.
            TokenDeclVar tokenDeclVar = new TokenDeclVar(name, null, tokenScript);
            tokenDeclVar.type = type;
            tokenDeclVar.name = name;
            tokenDeclVar.getProp = getFunc;
            tokenDeclVar.setProp = setFunc;

             // Can't be same name already in block.
            if(!tokenScript.AddVarEntry(tokenDeclVar))
            {
                ErrorMsg(tokenDeclVar, "duplicate member " + name.val);
                return null;
            }
            return tokenDeclVar;
        }

        /**
         * @brief Given a list of implemented interface methods, create a similar list with suffix added to all the names
         * @param implements = original list of implemented interface methods
         * @param suffix = string to be added to end of implemented interface method names
         * @returns list similar to implements with suffix added to end of implemented interface method names
         */
        private TokenIntfImpl MakePropertyImplements(TokenIntfImpl implements, string suffix)
        {
            TokenIntfImpl gsimpls = null;
            for(TokenIntfImpl impl = implements; impl != null; impl = (TokenIntfImpl)impl.nextToken)
            {
                TokenIntfImpl gsimpl = new TokenIntfImpl(impl.intfType,
                                                          new TokenName(impl.methName, impl.methName.val + suffix));
                gsimpl.nextToken = gsimpls;
                gsimpls = gsimpl;
            }
            return gsimpls;
        }

        /**
         * @brief Parse a constructor definition for a script-defined type class.
         * @param token = points to 'constructor' keyword
         * @param flags = abstract/override/static/virtual flags
         * @param tokdeclcl = which script-defined type class this method is in
         * @returns with method parsed and cataloged (or error message(s) printed)
         */
        private void ParseSDTClassCtorDecl(ref Token token, uint flags, TokenDeclSDTypeClass tokdeclcl)
        {
            if((flags & (SDT_ABSTRACT | SDT_OVERRIDE | SDT_STATIC | SDT_VIRTUAL)) != 0)
            {
                ErrorMsg(token, "cannot have abstract, override, static or virtual constructor");
            }

            TokenDeclVar tokenDeclFunc = new TokenDeclVar(token, null, tokenScript);
            tokenDeclFunc.name = new TokenName(tokenDeclFunc, "$ctor");
            tokenDeclFunc.retType = new TokenTypeVoid(token);
            tokenDeclFunc.sdtClass = tokdeclcl;
            tokenDeclFunc.sdtFlags = flags | SDT_NEW;

            token = token.nextToken;
            if(!(token is TokenKwParOpen))
            {
                ErrorMsg(token, "expecting ( for constructor argument list");
                token = SkipPastSemi(token);
                return;
            }
            tokenDeclFunc.argDecl = ParseFuncArgs(ref token, typeof(TokenKwParClose));
            if(tokenDeclFunc.argDecl == null)
                return;

            TokenDeclVar saveDeclFunc = currentDeclFunc;
            currentDeclFunc = tokenDeclFunc;
            tokenScript.PushVarFrame(tokenDeclFunc.argDecl.varDict);
            try
            {
                 // Set up reference to base constructor.
                TokenLValBaseField baseCtor = new TokenLValBaseField(token,
                                              new TokenName(token, "$ctor"),
                                              tokdeclcl);

                 // Parse any base constructor call as if it were the first statement of the
                 // constructor itself.
                if(token is TokenKwColon)
                {
                    token = token.nextToken;
                    if(token is TokenKwBase)
                    {
                        token = token.nextToken;
                    }
                    if(!(token is TokenKwParOpen))
                    {
                        ErrorMsg(token, "expecting ( for base constructor call arguments");
                        token = SkipPastSemi(token);
                        return;
                    }
                    TokenRValCall rvc = ParseRValCall(ref token, baseCtor);
                    if(rvc == null)
                        return;
                    if(tokdeclcl.extends != null)
                    {
                        tokenDeclFunc.baseCtorCall = rvc;
                        tokenDeclFunc.unknownTrivialityCalls.AddLast(rvc);
                    }
                    else
                    {
                        ErrorMsg(rvc, "base constructor call cannot be specified if not extending anything");
                    }
                }
                else if(tokdeclcl.extends != null)
                {
                     // Caller didn't specify a constructor but we are extending, so we will 
                     // call the extended class's default constructor.
                    SetUpDefaultBaseCtorCall(tokenDeclFunc);
                }

                 // Parse the constructor body.
                tokenDeclFunc.body = ParseStmtBlock(ref token);
                if(tokenDeclFunc.body == null)
                    return;
                if(tokenDeclFunc.argDecl == null)
                    return;
            }
            finally
            {
                tokenScript.PopVarFrame();
                currentDeclFunc = saveDeclFunc;
            }

             // Add to list of methods defined by this class.
             // It has the name "$ctor(argsig)".
            if(!tokenScript.AddVarEntry(tokenDeclFunc))
            {
                ErrorMsg(tokenDeclFunc, "duplicate constructor definition");
            }
        }

        /**
         * @brief Set up a call from a constructor to its default base constructor.
         */
        private void SetUpDefaultBaseCtorCall(TokenDeclVar thisCtor)
        {
            TokenLValBaseField baseCtor = new TokenLValBaseField(thisCtor,
                                          new TokenName(thisCtor, "$ctor"),
                                          (TokenDeclSDTypeClass)thisCtor.sdtClass);
            TokenRValCall rvc = new TokenRValCall(thisCtor);
            rvc.meth = baseCtor;
            thisCtor.baseCtorCall = rvc;
            thisCtor.unknownTrivialityCalls.AddLast(rvc);
        }

        /**
         * @brief Parse a method definition for a script-defined type class.
         * @param token = points to return type (or method name for implicit return type of void)
         * @param flags = abstract/override/static/virtual flags
         * @param tokdeclcl = which script-defined type class this method is in
         * @returns with method parsed and cataloged (or error message(s) printed)
         */
        private void ParseSDTClassMethodDecl(ref Token token, uint flags, TokenDeclSDTypeClass tokdeclcl)
        {
            TokenDeclVar tokenDeclFunc = ParseDeclFunc(ref token,
                                                        (flags & SDT_ABSTRACT) != 0,
                                                        (flags & SDT_STATIC) == 0,
                                                        (flags & SDT_STATIC) == 0);
            if(tokenDeclFunc != null)
            {
                tokenDeclFunc.sdtClass = tokdeclcl;
                tokenDeclFunc.sdtFlags = flags;
                if(!tokenScript.AddVarEntry(tokenDeclFunc))
                {
                    string funcNameSig = tokenDeclFunc.funcNameSig.val;
                    ErrorMsg(tokenDeclFunc.funcNameSig, "duplicate method name " + funcNameSig);
                }
            }
        }

        /**
         * @brief Parse a delegate declaration statement.
         * @param token = points to TokenDeclSDTypeDelegate token on entry
         *                points just past ';' on return
         * @param outerSDType = null: this is a top-level delegate
         *                      else: this delegate is being defined inside this type
         * @param flags = SDT_{PRIVATE,PROTECTED,PUBLIC}
         */
        private void ParseDeclDelegate(ref Token token, TokenDeclSDType outerSDType, uint flags)
        {
            Token u = token;
            TokenDeclSDTypeDelegate tokdecldel;
            TokenType retType;

            tokdecldel = (TokenDeclSDTypeDelegate)u;
            tokdecldel.outerSDType = outerSDType;
            tokdecldel.accessLevel = flags;

            // first thing following that should be return type
            // but we will fill in 'void' if it is missing
            u = u.nextToken;
            if(u is TokenType)
            {
                retType = (TokenType)u;
                u = u.nextToken;
            }
            else
            {
                retType = new TokenTypeVoid(u);
            }

            // get list of argument types until we see a ')'
            List<TokenType> args = new List<TokenType>();
            bool first = true;
            do
            {
                if(first)
                {

                    // first time should have '(' ')' or '(' <type>
                    if(!(u is TokenKwParOpen))
                    {
                        ErrorMsg(u, "expecting ( after delegate name");
                        token = SkipPastSemi(token);
                        return;
                    }
                    first = false;
                    u = u.nextToken;
                    if(u is TokenKwParClose)
                        break;
                }
                else
                {
                    // other times should have ',' <type>
                    if(!(u is TokenKwComma))
                    {
                        ErrorMsg(u, "expecting , separating arg types");
                        token = SkipPastSemi(token);
                        return;
                    }
                    u = u.nextToken;
                }
                if(!(u is TokenType))
                {
                    ErrorMsg(u, "expecting argument type");
                    token = SkipPastSemi(token);
                    return;
                }
                args.Add((TokenType)u);
                u = u.nextToken;

                // they can put in a dummy name that we toss out
                if(u is TokenName)
                    u = u.nextToken;

                // scanning ends on a ')'
            } while(!(u is TokenKwParClose));

            // fill in the return type and argment type array
            tokdecldel.SetRetArgTypes(retType, args.ToArray());

            // and finally must have ';' to finish the delegate declaration statement
            u = u.nextToken;
            if(!(u is TokenKwSemi))
            {
                ErrorMsg(u, "expecting ; after ) in delegate");
                token = SkipPastSemi(token);
                return;
            }
            token = u.nextToken;
        }

        /**
         * @brief Parse an interface declaration.
         * @param token = points to TokenDeclSDTypeInterface token on entry
         *                points just past closing '}' on return
         * @param outerSDType = null: this is a top-level interface
         *                      else: this interface is being defined inside this type
         * @param flags = SDT_{PRIVATE,PROTECTED,PUBLIC}
         */
        private void ParseDeclInterface(ref Token token, TokenDeclSDType outerSDType, uint flags)
        {
            Token u = token;
            TokenDeclSDTypeInterface tokdeclin;

            tokdeclin = (TokenDeclSDTypeInterface)u;
            tokdeclin.outerSDType = outerSDType;
            tokdeclin.accessLevel = flags;
            u = u.nextToken;

            // next can be ':' followed by list of implemented interfaces
            if(u is TokenKwColon)
            {
                u = u.nextToken;
                while(true)
                {
                    if(u is TokenTypeSDTypeInterface)
                    {
                        TokenDeclSDTypeInterface i = ((TokenTypeSDTypeInterface)u).decl;
                        if(!tokdeclin.implements.Contains(i))
                        {
                            tokdeclin.implements.Add(i);
                        }
                    }
                    else
                    {
                        ErrorMsg(u, "expecting interface name");
                        if(u is TokenKwBrcOpen)
                            break;
                    }
                    u = u.nextToken;
                    if(!(u is TokenKwComma))
                        break;
                    u = u.nextToken;
                }
            }

            // next must be '{' to open interface declaration body
            if(!(u is TokenKwBrcOpen))
            {
                ErrorMsg(u, "expecting { to open interface declaration body");
                token = SkipPastSemi(token);
                return;
            }
            token = u.nextToken;

            // start a var definition frame to collect the interface members
            tokenScript.PushVarFrame(false);
            tokdeclin.methsNProps = tokenScript.variablesStack;

            // process declaration statements until '}'
            while(!(token is TokenKwBrcClose))
            {
                if(token is TokenKwSemi)
                {
                    token = token.nextToken;
                    continue;
                }

                 // Parse nested script-defined type definitions.
                if(ParseDeclSDTypes(ref token, tokdeclin, SDT_PUBLIC))
                    continue;

                 // <type> <name> <funcargs> ;
                 // abstract method with explicit return type
                if((token is TokenType) &&
                    (token.nextToken is TokenName) &&
                    (token.nextToken.nextToken is TokenKwParOpen))
                {
                    Token name = token.nextToken;
                    TokenDeclVar tokenDeclFunc = ParseDeclFunc(ref token, true, false, false);
                    if(tokenDeclFunc == null)
                        continue;
                    if(!tokenScript.AddVarEntry(tokenDeclFunc))
                    {
                        ErrorMsg(name, "duplicate method name");
                        continue;
                    }
                    continue;
                }

                 // <name> <funcargs> ;
                 // abstract method returning void
                if((token is TokenName) &&
                    (token.nextToken is TokenKwParOpen))
                {
                    Token name = token;
                    TokenDeclVar tokenDeclFunc = ParseDeclFunc(ref token, true, false, false);
                    if(tokenDeclFunc == null)
                        continue;
                    if(!tokenScript.AddVarEntry(tokenDeclFunc))
                    {
                        ErrorMsg(name, "duplicate method name");
                    }
                    continue;
                }

                 // <type> <name> { [ get ; ] [ set ; ] }
                 // <type> '[' ... ']' { [ get ; ] [ set ; ] }
                 // abstract property
                bool prop = (token is TokenType) &&
                    (token.nextToken is TokenName) &&
                    (token.nextToken.nextToken is TokenKwBrcOpen ||
                     token.nextToken.nextToken is TokenKwColon);
                prop |= (token is TokenType) && (token.nextToken is TokenKwBrkOpen);
                if(prop)
                {
                    ParseProperty(ref token, true, false);
                    continue;
                }

                 // That's all we support in an interface declaration.
                ErrorMsg(token, "expecting method or property prototype");
                token = SkipPastSemi(token);
            }

             // Skip over the closing brace and pop the corresponding var frame.
            token = token.nextToken;
            tokenScript.PopVarFrame();
        }

        /**
         * @brief parse state body (including all its event handlers)
         * @param token = points to TokenKwBrcOpen
         * @returns null: state body parse error
         *          else: token representing state
         *          token = points past close brace
         */
        private TokenStateBody ParseStateBody(ref Token token)
        {
            TokenStateBody tokenStateBody = new TokenStateBody(token);

            if(!(token is TokenKwBrcOpen))
            {
                ErrorMsg(token, "expecting { at beg of state");
                token = SkipPastSemi(token);
                return null;
            }

            token = token.nextToken;
            while(!(token is TokenKwBrcClose))
            {
                if(token is TokenEnd)
                {
                    ErrorMsg(tokenStateBody, "eof parsing state body");
                    return null;
                }
                TokenDeclVar tokenDeclFunc = ParseDeclFunc(ref token, false, false, false);
                if(tokenDeclFunc == null)
                    return null;
                if(!(tokenDeclFunc.retType is TokenTypeVoid))
                {
                    ErrorMsg(tokenDeclFunc.retType, "event handlers don't have return types");
                    return null;
                }
                tokenDeclFunc.nextToken = tokenStateBody.eventFuncs;
                tokenStateBody.eventFuncs = tokenDeclFunc;
            }
            token = token.nextToken;
            return tokenStateBody;
        }

        /**
         * @brief Parse a function declaration, including its arg list and body
         * @param token = points to function return type token (or function name token if return type void)
         * @param abs = false: concrete function; true: abstract declaration
         * @param imp = allow implemented interface specs
         * @param ops = accept operators (==, +, etc) for function name
         * @returns null: error parsing function definition
         *          else: function declaration
         *          token = advanced just past function, ie, just past the closing brace
         */
        private TokenDeclVar ParseDeclFunc(ref Token token, bool abs, bool imp, bool ops)
        {
            TokenType retType;
            if(token is TokenType)
            {
                retType = (TokenType)token;
                token = token.nextToken;
            }
            else
            {
                retType = new TokenTypeVoid(token);
            }

            TokenName simpleName;
            if((token is TokenKw) && ((TokenKw)token).sdtClassOp)
            {
                if(!ops)
                    ErrorMsg(token, "operator functions disallowed in static contexts");
                simpleName = new TokenName(token, "$op" + token.ToString());
            }
            else if(!(token is TokenName))
            {
                ErrorMsg(token, "expecting function name");
                token = SkipPastSemi(token);
                return null;
            }
            else
            {
                simpleName = (TokenName)token;
            }
            token = token.nextToken;

            return ParseDeclFunc(ref token, abs, imp, retType, simpleName);
        }

        /**
         * @brief Parse a function declaration, including its arg list and body
         *        This version enters with token pointing to the '(' at beginning of arg list
         * @param token = points to the '(' of the arg list
         * @param abs = false: concrete function; true: abstract declaration
         * @param imp = allow implemented interface specs
         * @param retType = return type (TokenTypeVoid if void, never null)
         * @param simpleName = function name without any signature
         * @returns null: error parsing remainder of function definition
         *          else: function declaration
         *          token = advanced just past function, ie, just past the closing brace
         */
        private TokenDeclVar ParseDeclFunc(ref Token token, bool abs, bool imp, TokenType retType, TokenName simpleName)
        {
            TokenDeclVar tokenDeclFunc = new TokenDeclVar(simpleName, null, tokenScript);
            tokenDeclFunc.name = simpleName;
            tokenDeclFunc.retType = retType;
            tokenDeclFunc.argDecl = ParseFuncArgs(ref token, typeof(TokenKwParClose));
            if(tokenDeclFunc.argDecl == null)
                return null;

            if(token is TokenKwColon)
            {
                tokenDeclFunc.implements = ParseImplements(ref token, simpleName);
                if(tokenDeclFunc.implements == null)
                    return null;
                if(!imp)
                {
                    ErrorMsg(tokenDeclFunc.implements, "cannot implement interface method");
                    tokenDeclFunc.implements = null;
                }
            }

            if(!ParseFunctionBody(ref token, tokenDeclFunc, abs))
                return null;
            if(tokenDeclFunc.argDecl == null)
                return null;
            return tokenDeclFunc;
        }

        /**
         * @brief Parse interface implementation list.
         * @param token = points to ':' on entry
         *                points just past list on return
         * @param simpleName = simple name (no arg signature) of method/property that 
         *                     is implementing the interface method/property
         * @returns list of implemented interface methods/properties
         */
        private TokenIntfImpl ParseImplements(ref Token token, TokenName simpleName)
        {
            TokenIntfImpl implements = null;
            do
            {
                token = token.nextToken;
                if(!(token is TokenTypeSDTypeInterface))
                {
                    ErrorMsg(token, "expecting interface type");
                    token = SkipPastSemi(token);
                    return null;
                }
                TokenTypeSDTypeInterface intfType = (TokenTypeSDTypeInterface)token;
                token = token.nextToken;
                TokenName methName = simpleName;
                if((token is TokenKwDot) && (token.nextToken is TokenName))
                {
                    methName = (TokenName)token.nextToken;
                    token = token.nextToken.nextToken;
                }
                TokenIntfImpl intfImpl = new TokenIntfImpl(intfType, methName);
                intfImpl.nextToken = implements;
                implements = intfImpl;
            } while(token is TokenKwComma);
            return implements;
        }


        /**
         * @brief Parse function declaration's body
         * @param token = points to body, ie, ';' or '{'
         * @param tokenDeclFunc = function being declared
         * @param abs = false: concrete function; true: abstract declaration
         * @returns whether or not the function definition parsed correctly
         */
        private bool ParseFunctionBody(ref Token token, TokenDeclVar tokenDeclFunc, bool abs)
        {
            if(token is TokenKwSemi)
            {
                if(!abs)
                {
                    ErrorMsg(token, "concrete function must have body");
                    token = SkipPastSemi(token);
                    return false;
                }
                token = token.nextToken;
                return true;
            }

             // Declare this function as being the one currently being processed
             // for anything that cares.  We also start a variable frame that 
             // includes all the declared parameters.
            TokenDeclVar saveDeclFunc = currentDeclFunc;
            currentDeclFunc = tokenDeclFunc;
            tokenScript.PushVarFrame(tokenDeclFunc.argDecl.varDict);

             // Now parse the function statement block.
            tokenDeclFunc.body = ParseStmtBlock(ref token);

             // Pop the var frame that contains the arguments.
            tokenScript.PopVarFrame();
            currentDeclFunc = saveDeclFunc;

             // Check final errors.
            if(tokenDeclFunc.body == null)
                return false;
            if(abs)
            {
                ErrorMsg(tokenDeclFunc.body, "abstract function must not have body");
                tokenDeclFunc.body = null;
                return false;
            }
            return true;
        }


        /**
         * @brief Parse statement
         * @param token = first token of statement
         * @returns null: parse error
         *          else: token representing whole statement
         *          token = points past statement
         */
        private TokenStmt ParseStmt(ref Token token)
        {
             // Statements that begin with a specific keyword.
            if(token is TokenKwAt)
                return ParseStmtLabel(ref token);
            if(token is TokenKwBrcOpen)
                return ParseStmtBlock(ref token);
            if(token is TokenKwBreak)
                return ParseStmtBreak(ref token);
            if(token is TokenKwCont)
                return ParseStmtCont(ref token);
            if(token is TokenKwDo)
                return ParseStmtDo(ref token);
            if(token is TokenKwFor)
                return ParseStmtFor(ref token);
            if(token is TokenKwForEach)
                return ParseStmtForEach(ref token);
            if(token is TokenKwIf)
                return ParseStmtIf(ref token);
            if(token is TokenKwJump)
                return ParseStmtJump(ref token);
            if(token is TokenKwRet)
                return ParseStmtRet(ref token);
            if(token is TokenKwSemi)
                return ParseStmtNull(ref token);
            if(token is TokenKwState)
                return ParseStmtState(ref token);
            if(token is TokenKwSwitch)
                return ParseStmtSwitch(ref token);
            if(token is TokenKwThrow)
                return ParseStmtThrow(ref token);
            if(token is TokenKwTry)
                return ParseStmtTry(ref token);
            if(token is TokenKwWhile)
                return ParseStmtWhile(ref token);

             // Try to parse anything else as an expression, possibly calling
             // something and/or writing to a variable.
            TokenRVal tokenRVal = ParseRVal(ref token, semiOnly);
            if(tokenRVal != null)
            {
                TokenStmtRVal tokenStmtRVal = new TokenStmtRVal(tokenRVal);
                tokenStmtRVal.rVal = tokenRVal;
                return tokenStmtRVal;
            }

             // Who knows what it is...
            ErrorMsg(token, "unknown statement");
            token = SkipPastSemi(token);
            return null;
        }

        /**
         * @brief parse a statement block, ie, group of statements between braces
         * @param token = points to { token
         * @returns null: error parsing
         *          else: statements bundled in this token
         *          token = advanced just past the } token
         */
        private TokenStmtBlock ParseStmtBlock(ref Token token)
        {
            if(!(token is TokenKwBrcOpen))
            {
                ErrorMsg(token, "statement block body must begin with a {");
                token = SkipPastSemi(token);
                return null;
            }
            TokenStmtBlock tokenStmtBlock = new TokenStmtBlock(token);
            tokenStmtBlock.function = currentDeclFunc;
            tokenStmtBlock.outerStmtBlock = currentStmtBlock;
            currentStmtBlock = tokenStmtBlock;
            VarDict outerVariablesStack = tokenScript.variablesStack;
            try
            {
                Token prevStmt = null;
                token = token.nextToken;
                while(!(token is TokenKwBrcClose))
                {
                    if(token is TokenEnd)
                    {
                        ErrorMsg(tokenStmtBlock, "missing }");
                        return null;
                    }
                    Token thisStmt;
                    if(((token is TokenType) && (token.nextToken is TokenName)) ||
                        (token is TokenKwConst))
                    {
                        thisStmt = ParseDeclVar(ref token, null);
                    }
                    else
                    {
                        thisStmt = ParseStmt(ref token);
                    }
                    if(thisStmt == null)
                        return null;
                    if(prevStmt == null)
                        tokenStmtBlock.statements = thisStmt;
                    else
                        prevStmt.nextToken = thisStmt;
                    prevStmt = thisStmt;
                }
                token = token.nextToken;
            }
            finally
            {
                tokenScript.variablesStack = outerVariablesStack;
                currentStmtBlock = tokenStmtBlock.outerStmtBlock;
            }
            return tokenStmtBlock;
        }

        /**
         * @brief parse a 'break' statement
         * @param token = points to break keyword token
         * @returns null: error parsing
         *          else: statements bundled in this token
         *          token = advanced just past the ; token
         */
        private TokenStmtBreak ParseStmtBreak(ref Token token)
        {
            TokenStmtBreak tokenStmtBreak = new TokenStmtBreak(token);
            token = token.nextToken;
            if(!(token is TokenKwSemi))
            {
                ErrorMsg(token, "expecting ;");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            return tokenStmtBreak;
        }

        /**
         * @brief parse a 'continue' statement
         * @param token = points to continue keyword token
         * @returns null: error parsing
         *          else: statements bundled in this token
         *          token = advanced just past the ; token
         */
        private TokenStmtCont ParseStmtCont(ref Token token)
        {
            TokenStmtCont tokenStmtCont = new TokenStmtCont(token);
            token = token.nextToken;
            if(!(token is TokenKwSemi))
            {
                ErrorMsg(token, "expecting ;");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            return tokenStmtCont;
        }

        /**
         * @brief parse a 'do' statement
         * @params token = points to 'do' keyword token
         * @returns null: parse error
         *          else: pointer to token encapsulating the do statement, including body
         *          token = advanced just past the body statement
         */
        private TokenStmtDo ParseStmtDo(ref Token token)
        {
            currentDeclFunc.triviality = Triviality.complex;
            TokenStmtDo tokenStmtDo = new TokenStmtDo(token);
            token = token.nextToken;
            tokenStmtDo.bodyStmt = ParseStmt(ref token);
            if(tokenStmtDo.bodyStmt == null)
                return null;
            if(!(token is TokenKwWhile))
            {
                ErrorMsg(token, "expecting while clause");
                return null;
            }
            token = token.nextToken;
            tokenStmtDo.testRVal = ParseRValParen(ref token);
            if(tokenStmtDo.testRVal == null)
                return null;
            if(!(token is TokenKwSemi))
            {
                ErrorMsg(token, "while clause must terminate on semicolon");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            return tokenStmtDo;
        }

        /**
         * @brief parse a for statement
         * @param token = points to 'for' keyword token
         * @returns null: parse error
         *          else: pointer to encapsulated for statement token
         *          token = advanced just past for body statement
         */
        private TokenStmt ParseStmtFor(ref Token token)
        {
            currentDeclFunc.triviality = Triviality.complex;

             // Create encapsulating token and skip past 'for ('
            TokenStmtFor tokenStmtFor = new TokenStmtFor(token);
            token = token.nextToken;
            if(!(token is TokenKwParOpen))
            {
                ErrorMsg(token, "for must be followed by (");
                return null;
            }
            token = token.nextToken;

             // If a plain for, ie, not declaring a variable, it's straightforward.
            if(!(token is TokenType))
            {
                tokenStmtFor.initStmt = ParseStmt(ref token);
                if(tokenStmtFor.initStmt == null)
                    return null;
                return ParseStmtFor2(tokenStmtFor, ref token) ? tokenStmtFor : null;
            }

             // Initialization declares a variable, so encapsulate it in a block so
             // variable has scope only in the for statement, including its body.
            TokenStmtBlock forStmtBlock = new TokenStmtBlock(tokenStmtFor);
            forStmtBlock.outerStmtBlock = currentStmtBlock;
            forStmtBlock.function = currentDeclFunc;
            currentStmtBlock = forStmtBlock;
            tokenScript.PushVarFrame(true);

            TokenDeclVar tokenDeclVar = ParseDeclVar(ref token, null);
            if(tokenDeclVar == null)
            {
                tokenScript.PopVarFrame();
                currentStmtBlock = forStmtBlock.outerStmtBlock;
                return null;
            }

            forStmtBlock.statements = tokenDeclVar;
            tokenDeclVar.nextToken = tokenStmtFor;

            bool ok = ParseStmtFor2(tokenStmtFor, ref token);
            tokenScript.PopVarFrame();
            currentStmtBlock = forStmtBlock.outerStmtBlock;
            return ok ? forStmtBlock : null;
        }

        /**
         * @brief parse rest of 'for' statement starting with the test expression.
         * @param tokenStmtFor = token encapsulating the for statement
         * @param token = points to test expression
         * @returns false: parse error
         *           true: successful
         *          token = points just past body statement
         */
        private bool ParseStmtFor2(TokenStmtFor tokenStmtFor, ref Token token)
        {
            if(token is TokenKwSemi)
            {
                token = token.nextToken;
            }
            else
            {
                tokenStmtFor.testRVal = ParseRVal(ref token, semiOnly);
                if(tokenStmtFor.testRVal == null)
                    return false;
            }
            if(token is TokenKwParClose)
            {
                token = token.nextToken;
            }
            else
            {
                tokenStmtFor.incrRVal = ParseRVal(ref token, parCloseOnly);
                if(tokenStmtFor.incrRVal == null)
                    return false;
            }
            tokenStmtFor.bodyStmt = ParseStmt(ref token);
            return tokenStmtFor.bodyStmt != null;
        }

        /**
         * @brief parse a foreach statement
         * @param token = points to 'foreach' keyword token
         * @returns null: parse error
         *          else: pointer to encapsulated foreach statement token
         *          token = advanced just past for body statement
         */
        private TokenStmt ParseStmtForEach(ref Token token)
        {
            currentDeclFunc.triviality = Triviality.complex;

             // Create encapsulating token and skip past 'foreach ('
            TokenStmtForEach tokenStmtForEach = new TokenStmtForEach(token);
            token = token.nextToken;
            if(!(token is TokenKwParOpen))
            {
                ErrorMsg(token, "foreach must be followed by (");
                return null;
            }
            token = token.nextToken;

            if(token is TokenName)
            {
                tokenStmtForEach.keyLVal = new TokenLValName((TokenName)token, tokenScript.variablesStack);
                token = token.nextToken;
            }
            if(!(token is TokenKwComma))
            {
                ErrorMsg(token, "expecting comma");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            if(token is TokenName)
            {
                tokenStmtForEach.valLVal = new TokenLValName((TokenName)token, tokenScript.variablesStack);
                token = token.nextToken;
            }
            if(!(token is TokenKwIn))
            {
                ErrorMsg(token, "expecting 'in'");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            tokenStmtForEach.arrayRVal = GetOperand(ref token);
            if(tokenStmtForEach.arrayRVal == null)
                return null;
            if(!(token is TokenKwParClose))
            {
                ErrorMsg(token, "expecting )");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            tokenStmtForEach.bodyStmt = ParseStmt(ref token);
            if(tokenStmtForEach.bodyStmt == null)
                return null;
            return tokenStmtForEach;
        }

        private TokenStmtIf ParseStmtIf(ref Token token)
        {
            TokenStmtIf tokenStmtIf = new TokenStmtIf(token);
            token = token.nextToken;
            tokenStmtIf.testRVal = ParseRValParen(ref token);
            if(tokenStmtIf.testRVal == null)
                return null;
            tokenStmtIf.trueStmt = ParseStmt(ref token);
            if(tokenStmtIf.trueStmt == null)
                return null;
            if(token is TokenKwElse)
            {
                token = token.nextToken;
                tokenStmtIf.elseStmt = ParseStmt(ref token);
                if(tokenStmtIf.elseStmt == null)
                    return null;
            }
            return tokenStmtIf;
        }

        private TokenStmtJump ParseStmtJump(ref Token token)
        {
             // Create jump statement token to encapsulate the whole statement.
            TokenStmtJump tokenStmtJump = new TokenStmtJump(token);
            token = token.nextToken;
            if(!(token is TokenName) || !(token.nextToken is TokenKwSemi))
            {
                ErrorMsg(token, "expecting label;");
                token = SkipPastSemi(token);
                return null;
            }
            tokenStmtJump.label = (TokenName)token;
            token = token.nextToken.nextToken;

             // If label is already defined, it means this is a backward (looping)
             // jump, so remember the label has backward jump references.
             // We also then assume the function is complex, ie, it has a loop.
            if(currentDeclFunc.labels.ContainsKey(tokenStmtJump.label.val))
            {
                currentDeclFunc.labels[tokenStmtJump.label.val].hasBkwdRefs = true;
                currentDeclFunc.triviality = Triviality.complex;
            }

            return tokenStmtJump;
        }

        /**
         * @brief parse a jump target label statement
         * @param token = points to the '@' token
         * @returns null: error parsing
         *          else: the label
         *          token = advanced just past the ;
         */
        private TokenStmtLabel ParseStmtLabel(ref Token token)
        {
            if(!(token.nextToken is TokenName) ||
                !(token.nextToken.nextToken is TokenKwSemi))
            {
                ErrorMsg(token, "invalid label");
                token = SkipPastSemi(token);
                return null;
            }
            TokenStmtLabel stmtLabel = new TokenStmtLabel(token);
            stmtLabel.name = (TokenName)token.nextToken;
            stmtLabel.block = currentStmtBlock;
            if(currentDeclFunc.labels.ContainsKey(stmtLabel.name.val))
            {
                ErrorMsg(token.nextToken, "duplicate label");
                ErrorMsg(currentDeclFunc.labels[stmtLabel.name.val], "previously defined here");
                token = SkipPastSemi(token);
                return null;
            }
            currentDeclFunc.labels.Add(stmtLabel.name.val, stmtLabel);
            token = token.nextToken.nextToken.nextToken;
            return stmtLabel;
        }

        private TokenStmtNull ParseStmtNull(ref Token token)
        {
            TokenStmtNull tokenStmtNull = new TokenStmtNull(token);
            token = token.nextToken;
            return tokenStmtNull;
        }

        private TokenStmtRet ParseStmtRet(ref Token token)
        {
            TokenStmtRet tokenStmtRet = new TokenStmtRet(token);
            token = token.nextToken;
            if(token is TokenKwSemi)
            {
                token = token.nextToken;
            }
            else
            {
                tokenStmtRet.rVal = ParseRVal(ref token, semiOnly);
                if(tokenStmtRet.rVal == null)
                    return null;
            }
            return tokenStmtRet;
        }

        private TokenStmtSwitch ParseStmtSwitch(ref Token token)
        {
            TokenStmtSwitch tokenStmtSwitch = new TokenStmtSwitch(token);
            token = token.nextToken;
            tokenStmtSwitch.testRVal = ParseRValParen(ref token);
            if(tokenStmtSwitch.testRVal == null)
                return null;
            if(!(token is TokenKwBrcOpen))
            {
                ErrorMsg(token, "expecting open brace");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;
            TokenSwitchCase tokenSwitchCase = null;
            bool haveComplained = false;
            while(!(token is TokenKwBrcClose))
            {
                if(token is TokenKwCase)
                {
                    tokenSwitchCase = new TokenSwitchCase(token);
                    if(tokenStmtSwitch.lastCase == null)
                    {
                        tokenStmtSwitch.cases = tokenSwitchCase;
                    }
                    else
                    {
                        tokenStmtSwitch.lastCase.nextCase = tokenSwitchCase;
                    }
                    tokenStmtSwitch.lastCase = tokenSwitchCase;

                    token = token.nextToken;
                    tokenSwitchCase.rVal1 = ParseRVal(ref token, colonOrDotDotDot);
                    if(tokenSwitchCase.rVal1 == null)
                        return null;
                    if(token is TokenKwDotDotDot)
                    {
                        token = token.nextToken;
                        tokenSwitchCase.rVal2 = ParseRVal(ref token, colonOnly);
                        if(tokenSwitchCase.rVal2 == null)
                            return null;
                    }
                    else
                    {
                        if(!(token is TokenKwColon))
                        {
                            ErrorMsg(token, "expecting : or ...");
                            token = SkipPastSemi(token);
                            return null;
                        }
                        token = token.nextToken;
                    }
                }
                else if(token is TokenKwDefault)
                {
                    tokenSwitchCase = new TokenSwitchCase(token);
                    if(tokenStmtSwitch.lastCase == null)
                    {
                        tokenStmtSwitch.cases = tokenSwitchCase;
                    }
                    else
                    {
                        tokenStmtSwitch.lastCase.nextCase = tokenSwitchCase;
                    }
                    tokenStmtSwitch.lastCase = tokenSwitchCase;

                    token = token.nextToken;
                    if(!(token is TokenKwColon))
                    {
                        ErrorMsg(token, "expecting :");
                        token = SkipPastSemi(token);
                        return null;
                    }
                    token = token.nextToken;
                }
                else if(tokenSwitchCase != null)
                {
                    TokenStmt bodyStmt = ParseStmt(ref token);
                    if(bodyStmt == null)
                        return null;
                    if(tokenSwitchCase.lastStmt == null)
                    {
                        tokenSwitchCase.stmts = bodyStmt;
                    }
                    else
                    {
                        tokenSwitchCase.lastStmt.nextToken = bodyStmt;
                    }
                    tokenSwitchCase.lastStmt = bodyStmt;
                    bodyStmt.nextToken = null;
                }
                else if(!haveComplained)
                {
                    ErrorMsg(token, "expecting case or default label");
                    token = SkipPastSemi(token);
                    haveComplained = true;
                }
            }
            token = token.nextToken;
            return tokenStmtSwitch;
        }

        private TokenStmtState ParseStmtState(ref Token token)
        {
            TokenStmtState tokenStmtState = new TokenStmtState(token);
            token = token.nextToken;
            if((!(token is TokenName) && !(token is TokenKwDefault)) || !(token.nextToken is TokenKwSemi))
            {
                ErrorMsg(token, "expecting state;");
                token = SkipPastSemi(token);
                return null;
            }
            if(token is TokenName)
            {
                tokenStmtState.state = (TokenName)token;
            }
            token = token.nextToken.nextToken;
            return tokenStmtState;
        }

        private TokenStmtThrow ParseStmtThrow(ref Token token)
        {
            TokenStmtThrow tokenStmtThrow = new TokenStmtThrow(token);
            token = token.nextToken;
            if(token is TokenKwSemi)
            {
                token = token.nextToken;
            }
            else
            {
                tokenStmtThrow.rVal = ParseRVal(ref token, semiOnly);
                if(tokenStmtThrow.rVal == null)
                    return null;
            }
            return tokenStmtThrow;
        }

        /**
         * @brief Parse a try { ... } catch { ... } finally { ... } statement block
         * @param token = point to 'try' keyword on entry
         *                points past last '}' processed on return
         * @returns encapsulated try/catch/finally or null if parsing error
         */
        private TokenStmtTry ParseStmtTry(ref Token token)
        {
             // Parse out the 'try { ... }' part
            Token tryKw = token;
            token = token.nextToken;
            TokenStmt body = ParseStmtBlock(ref token);

            while(true)
            {
                TokenStmtTry tokenStmtTry;
                if(token is TokenKwCatch)
                {
                    if(!(token.nextToken is TokenKwParOpen) ||
                        !(token.nextToken.nextToken is TokenType) ||
                        !(token.nextToken.nextToken.nextToken is TokenName) ||
                        !(token.nextToken.nextToken.nextToken.nextToken is TokenKwParClose))
                    {
                        ErrorMsg(token, "catch must be followed by ( <type> <varname> ) { <statement>... }");
                        return null;
                    }
                    token = token.nextToken.nextToken;     // skip over 'catch' '('
                    TokenDeclVar tag = new TokenDeclVar(token.nextToken, currentDeclFunc, tokenScript);
                    tag.type = (TokenType)token;
                    token = token.nextToken;            // skip over <type>
                    tag.name = (TokenName)token;
                    token = token.nextToken.nextToken;  // skip over <varname> ')'

                    if((!(tag.type is TokenTypeExc)) && (!(tag.type is TokenTypeStr)))
                    {
                        ErrorMsg(tag.type, "must be type 'exception' or 'string'");
                    }

                    tokenStmtTry = new TokenStmtTry(tryKw);
                    tokenStmtTry.tryStmt = WrapTryCatFinInBlock(body);
                    tokenStmtTry.catchVar = tag;
                    tokenScript.PushVarFrame(false);
                    tokenScript.AddVarEntry(tag);
                    tokenStmtTry.catchStmt = ParseStmtBlock(ref token);
                    tokenScript.PopVarFrame();
                    if(tokenStmtTry.catchStmt == null)
                        return null;
                    tokenStmtTry.tryStmt.isTry = true;
                    tokenStmtTry.tryStmt.tryStmt = tokenStmtTry;
                    tokenStmtTry.catchStmt.isCatch = true;
                    tokenStmtTry.catchStmt.tryStmt = tokenStmtTry;
                }
                else if(token is TokenKwFinally)
                {
                    token = token.nextToken;

                    tokenStmtTry = new TokenStmtTry(tryKw);
                    tokenStmtTry.tryStmt = WrapTryCatFinInBlock(body);
                    tokenStmtTry.finallyStmt = ParseStmtBlock(ref token);
                    if(tokenStmtTry.finallyStmt == null)
                        return null;
                    tokenStmtTry.tryStmt.isTry = true;
                    tokenStmtTry.tryStmt.tryStmt = tokenStmtTry;
                    tokenStmtTry.finallyStmt.isFinally = true;
                    tokenStmtTry.finallyStmt.tryStmt = tokenStmtTry;
                }
                else
                    break;

                body = tokenStmtTry;
            }

            if(!(body is TokenStmtTry))
            {
                ErrorMsg(body, "try must have a matching catch and/or finally");
                return null;
            }
            return (TokenStmtTry)body;
        }

        /**
         * @brief Wrap a possible try/catch/finally statement block in a block statement.
         *
         * Given body = try { } catch (string s) { }
         *
         * we return { try { } catch (string s) { } }
         *
         * @param body = a TokenStmtTry or a TokenStmtBlock
         * @returns a TokenStmtBlock
         */
        private TokenStmtBlock WrapTryCatFinInBlock(TokenStmt body)
        {
            if(body is TokenStmtBlock)
                return (TokenStmtBlock)body;

            TokenStmtTry innerTry = (TokenStmtTry)body;

            TokenStmtBlock wrapper = new TokenStmtBlock(body);
            wrapper.statements = innerTry;
            wrapper.outerStmtBlock = currentStmtBlock;
            wrapper.function = currentDeclFunc;

            innerTry.tryStmt.outerStmtBlock = wrapper;
            if(innerTry.catchStmt != null)
                innerTry.catchStmt.outerStmtBlock = wrapper;
            if(innerTry.finallyStmt != null)
                innerTry.finallyStmt.outerStmtBlock = wrapper;

            return wrapper;
        }

        private TokenStmtWhile ParseStmtWhile(ref Token token)
        {
            currentDeclFunc.triviality = Triviality.complex;
            TokenStmtWhile tokenStmtWhile = new TokenStmtWhile(token);
            token = token.nextToken;
            tokenStmtWhile.testRVal = ParseRValParen(ref token);
            if(tokenStmtWhile.testRVal == null)
                return null;
            tokenStmtWhile.bodyStmt = ParseStmt(ref token);
            if(tokenStmtWhile.bodyStmt == null)
                return null;
            return tokenStmtWhile;
        }

        /**
         * @brief parse a variable declaration statement, including init value if any.
         * @param token = points to type or 'constant' token
         * @param initFunc = null: parsing a local var declaration
         *                         put initialization code in .init
         *                   else: parsing a global var or field var declaration
         *                         put initialization code in initFunc.body
         * @returns null: parsing error
         *          else: variable declaration encapulating token
         *          token = advanced just past semi-colon
         *          variables = modified to include the new variable
         */
        private TokenDeclVar ParseDeclVar(ref Token token, TokenDeclVar initFunc)
        {
            TokenDeclVar tokenDeclVar = new TokenDeclVar(token.nextToken, currentDeclFunc, tokenScript);

             // Handle constant declaration.
             // It ends up in the declared variables list for the statement block just like
             // any other variable, except it has .constant = true.
             // The code generator will test that the initialization expression is constant.
             //
             //  constant <name> = <value> ;
            if(token is TokenKwConst)
            {
                token = token.nextToken;
                if(!(token is TokenName))
                {
                    ErrorMsg(token, "expecting constant name");
                    token = SkipPastSemi(token);
                    return null;
                }
                tokenDeclVar.name = (TokenName)token;
                token = token.nextToken;
                if(!(token is TokenKwAssign))
                {
                    ErrorMsg(token, "expecting =");
                    token = SkipPastSemi(token);
                    return null;
                }
                token = token.nextToken;
                TokenRVal rVal = ParseRVal(ref token, semiOnly);
                if(rVal == null)
                    return null;
                tokenDeclVar.init = rVal;
                tokenDeclVar.constant = true;
            }

             // Otherwise, normal variable declaration with optional initialization value.
            else
            {
                 // Build basic encapsulating token with type and name.
                tokenDeclVar.type = (TokenType)token;
                token = token.nextToken;
                if(!(token is TokenName))
                {
                    ErrorMsg(token, "expecting variable name");
                    token = SkipPastSemi(token);
                    return null;
                }
                tokenDeclVar.name = (TokenName)token;
                token = token.nextToken;

                 // If just a ;, there is no explicit initialization value.
                 // Otherwise, look for an =RVal; expression that has init value.
                if(token is TokenKwSemi)
                {
                    token = token.nextToken;
                    if(initFunc != null)
                    {
                        tokenDeclVar.init = TokenRValInitDef.Construct(tokenDeclVar);
                    }
                }
                else if(token is TokenKwAssign)
                {
                    token = token.nextToken;
                    if(initFunc != null)
                    {
                        currentDeclFunc = initFunc;
                        tokenDeclVar.init = ParseRVal(ref token, semiOnly);
                        currentDeclFunc = null;
                    }
                    else
                    {
                        tokenDeclVar.init = ParseRVal(ref token, semiOnly);
                    }
                    if(tokenDeclVar.init == null)
                        return null;
                }
                else
                {
                    ErrorMsg(token, "expecting = or ;");
                    token = SkipPastSemi(token);
                    return null;
                }
            }

             // If doing local vars, each var goes in its own var frame,
             // to make sure no code before this point can reference it.
            if(currentStmtBlock != null)
            {
                tokenScript.PushVarFrame(true);
            }

             // Can't be same name already in block.
            if(!tokenScript.AddVarEntry(tokenDeclVar))
            {
                ErrorMsg(tokenDeclVar, "duplicate variable " + tokenDeclVar.name.val);
                return null;
            }
            return tokenDeclVar;
        }

        /**
         * @brief Add variable initialization to $globalvarinit, $staticfieldinit or $instfieldinit function.
         * @param initFunc = $globalvarinit, $staticfieldinit or $instfieldinit function
         * @param left = variable being initialized
         * @param init = null: initialize to default value
         *               else: initialize to this value
         */
        private void DoVarInit(TokenDeclVar initFunc, TokenLVal left, TokenRVal init)
        {
             // Make a statement that assigns the initialization value to the variable.
            TokenStmt stmt;
            if(init == null)
            {
                TokenStmtVarIniDef tsvid = new TokenStmtVarIniDef(left);
                tsvid.var = left;
                stmt = tsvid;
            }
            else
            {
                TokenKw op = new TokenKwAssign(left);
                TokenStmtRVal tsrv = new TokenStmtRVal(init);
                tsrv.rVal = new TokenRValOpBin(left, op, init);
                stmt = tsrv;
            }

             // Add statement to end of initialization function.
             // Be sure to execute them in same order as in source
             // as some doofus scripts depend on it.
            Token lastStmt = initFunc.body.statements;
            if(lastStmt == null)
            {
                initFunc.body.statements = stmt;
            }
            else
            {
                Token nextStmt;
                while((nextStmt = lastStmt.nextToken) != null)
                {
                    lastStmt = nextStmt;
                }
                lastStmt.nextToken = stmt;
            }
        }

        /**
         * @brief parse function declaration argument list
         * @param token = points to TokenKwParOpen
         * @returns null: parse error
         *          else: points to token with types and names
         *          token = updated past the TokenKw{Brk,Par}Close
         */
        private TokenArgDecl ParseFuncArgs(ref Token token, Type end)
        {
            TokenArgDecl tokenArgDecl = new TokenArgDecl(token);

            bool first = true;
            do
            {
                token = token.nextToken;
                if((token.GetType() == end) && first)
                    break;
                if(!(token is TokenType))
                {
                    ErrorMsg(token, "expecting arg type");
                    token = SkipPastSemi(token);
                    return null;
                }
                TokenType type = (TokenType)token;
                token = token.nextToken;
                if(!(token is TokenName))
                {
                    ErrorMsg(token, "expecting arg name");
                    token = SkipPastSemi(token);
                    return null;
                }
                TokenName name = (TokenName)token;
                token = token.nextToken;

                if(!tokenArgDecl.AddArg(type, name))
                {
                    ErrorMsg(name, "duplicate arg name");
                }
                first = false;
            } while(token is TokenKwComma);

            if(token.GetType() != end)
            {
                ErrorMsg(token, "expecting comma or close bracket/paren");
                token = SkipPastSemi(token);
                return null;
            }
            token = token.nextToken;

            return tokenArgDecl;
        }

        /**
         * @brief parse right-hand value expression
         *        this is where arithmetic-like expressions are processed
         * @param token = points to first token expression
         * @param termTokenType = expression termination token type
         * @returns null: not an RVal
         *          else: single token representing whole expression
         *          token = if termTokenType.Length == 1, points just past terminating token
         *                                          else, points right at terminating token
         */
        public TokenRVal ParseRVal(ref Token token, Type[] termTokenTypes)
        {
             // Start with pushing the first operand on operand stack.
            BinOp binOps = null;
            TokenRVal operands = GetOperand(ref token);
            if(operands == null)
                return null;

             // Keep scanning until we hit the termination token.
            while(true)
            {
                Type tokType = token.GetType();
                for(int i = termTokenTypes.Length; --i >= 0;)
                {
                    if(tokType == termTokenTypes[i])
                        goto done;
                }

                 // Special form:
                 //   <operand> is <typeexp>
                if(token is TokenKwIs)
                {
                    TokenRValIsType tokenRValIsType = new TokenRValIsType(token);
                    token = token.nextToken;

                     // Parse the <typeexp>.
                    tokenRValIsType.typeExp = ParseTypeExp(ref token);
                    if(tokenRValIsType.typeExp == null)
                        return null;

                     // Replace top operand with result of <operand> is <typeexp>
                    tokenRValIsType.rValExp = operands;
                    tokenRValIsType.nextToken = operands.nextToken;
                    operands = tokenRValIsType;

                     // token points just past <typeexp> so see if it is another operator.
                    continue;
                }

                 // Peek at next operator.
                BinOp binOp = GetOperator(ref token);
                if(binOp == null)
                    return null;

                 // If there are stacked operators of higher or same precedence than new one,
                 // perform their computation then push result back on operand stack.
                 //
                 //  higher or same = left-to-right application of operators
                 //                   eg, a - b - c becomes (a - b) - c
                 //
                 //  higher precedence = right-to-left application of operators
                 //                      eg, a - b - c becomes a - (b - c)
                 //
                 // Now of course, there is some ugliness necessary:
                 //      we want:  a  - b - c  =>  (a - b) - c    so we do 'higher or same'
                 //  but we want:  a += b = c  =>  a += (b = c)   so we do 'higher only'
                 //
                 // binOps is the first operator (or null if only one)
                 // binOp is the second operator (or first if only one)
                while(binOps != null)
                {
                    if(binOps.preced < binOp.preced)
                        break;       // 1st operator lower than 2nd, so leave 1st on stack to do later
                    if(binOps.preced > binOp.preced)
                        goto do1st;  // 1st op higher than 2nd, so we always do 1st op first
                    if(binOps.preced == ASNPR)
                        break;             // equal preced, if assignment type, leave 1st on stack to do later
                                           //               if non-asn type, do 1st op first (ie left-to-right)
                    do1st:
                    TokenRVal result = PerformBinOp((TokenRVal)operands.prevToken, binOps, (TokenRVal)operands);
                    result.prevToken = operands.prevToken.prevToken;
                    operands = result;
                    binOps = binOps.pop;
                }

                 // Handle conditional expression as a special form:
                 //    <condexp> ? <trueexp> : <falseexp>
                if(binOp.token is TokenKwQMark)
                {
                    TokenRValCondExpr condExpr = new TokenRValCondExpr(binOp.token);
                    condExpr.condExpr = operands;
                    condExpr.trueExpr = ParseRVal(ref token, new Type[] { typeof(TokenKwColon) });
                    condExpr.falseExpr = ParseRVal(ref token, termTokenTypes);
                    condExpr.prevToken = operands.prevToken;
                    operands = condExpr;
                    termTokenTypes = new Type[0];
                    goto done;
                }

                 // Push new operator on its stack.
                binOp.pop = binOps;
                binOps = binOp;

                 // Push next operand on its stack.
                TokenRVal operand = GetOperand(ref token);
                if(operand == null)
                    return null;
                operand.prevToken = operands;
                operands = operand;
            }
            done:

             // At end of expression, perform any stacked computations.
            while(binOps != null)
            {
                TokenRVal result = PerformBinOp((TokenRVal)operands.prevToken, binOps, (TokenRVal)operands);
                result.prevToken = operands.prevToken.prevToken;
                operands = result;
                binOps = binOps.pop;
            }

             // There should be exactly one remaining operand on the stack which is our final result.
            if(operands.prevToken != null)
                throw new Exception("too many operands");

             // If only one terminator type possible, advance past the terminator.
            if(termTokenTypes.Length == 1)
                token = token.nextToken;

            return operands;
        }

        private TokenTypeExp ParseTypeExp(ref Token token)
        {
            TokenTypeExp leftOperand = GetTypeExp(ref token);
            if(leftOperand == null)
                return null;

            while((token is TokenKwAnd) || (token is TokenKwOr))
            {
                Token typeBinOp = token;
                token = token.nextToken;
                TokenTypeExp rightOperand = GetTypeExp(ref token);
                if(rightOperand == null)
                    return null;
                TokenTypeExpBinOp typeExpBinOp = new TokenTypeExpBinOp(typeBinOp);
                typeExpBinOp.leftOp = leftOperand;
                typeExpBinOp.binOp = typeBinOp;
                typeExpBinOp.rightOp = rightOperand;
                leftOperand = typeExpBinOp;
            }
            return leftOperand;
        }

        private TokenTypeExp GetTypeExp(ref Token token)
        {
            if(token is TokenKwTilde)
            {
                TokenTypeExpNot typeExpNot = new TokenTypeExpNot(token);
                token = token.nextToken;
                typeExpNot.typeExp = GetTypeExp(ref token);
                if(typeExpNot.typeExp == null)
                    return null;
                return typeExpNot;
            }
            if(token is TokenKwParOpen)
            {
                TokenTypeExpPar typeExpPar = new TokenTypeExpPar(token);
                token = token.nextToken;
                typeExpPar.typeExp = GetTypeExp(ref token);
                if(typeExpPar.typeExp == null)
                    return null;
                if(!(token is TokenKwParClose))
                {
                    ErrorMsg(token, "expected close parenthesis");
                    token = SkipPastSemi(token);
                    return null;
                }
                return typeExpPar;
            }
            if(token is TokenKwUndef)
            {
                TokenTypeExpUndef typeExpUndef = new TokenTypeExpUndef(token);
                token = token.nextToken;
                return typeExpUndef;
            }
            if(token is TokenType)
            {
                TokenTypeExpType typeExpType = new TokenTypeExpType(token);
                typeExpType.typeToken = (TokenType)token;
                token = token.nextToken;
                return typeExpType;
            }
            ErrorMsg(token, "expected type");
            token = SkipPastSemi(token);
            return null;
        }

        /**
         * @brief get a right-hand operand expression token
         * @param token = first token of operand to parse
         * @returns null: invalid operand
         *          else: token that bundles or wraps the operand
         *          token = points to token following last operand token
         */
        private TokenRVal GetOperand(ref Token token)
        {
             // Prefix unary operators (eg ++, --) requiring an L-value.
            if((token is TokenKwIncr) || (token is TokenKwDecr))
            {
                TokenRValAsnPre asnPre = new TokenRValAsnPre(token);
                asnPre.prefix = token;
                token = token.nextToken;
                TokenRVal op = GetOperand(ref token);
                if(op == null)
                    return null;
                if(!(op is TokenLVal))
                {
                    ErrorMsg(op, "can pre{in,de}crement only an L-value");
                    return null;
                }
                asnPre.lVal = (TokenLVal)op;
                return asnPre;
            }

             // Get the bulk of the operand, ie, without any of the below suffixes.
            TokenRVal operand = GetOperandNoMods(ref token);
            if(operand == null)
                return null;
            modifiers:

             // If followed by '++' or '--', it is post-{in,de}cremented.
            if((token is TokenKwIncr) || (token is TokenKwDecr))
            {
                TokenRValAsnPost asnPost = new TokenRValAsnPost(token);
                asnPost.postfix = token;
                token = token.nextToken;
                if(!(operand is TokenLVal))
                {
                    ErrorMsg(operand, "can post{in,de}crement only an L-value");
                    return null;
                }
                asnPost.lVal = (TokenLVal)operand;
                return asnPost;
            }

             // If followed by a '.', it is an instance field or instance method reference.
            if(token is TokenKwDot)
            {
                token = token.nextToken;
                if(!(token is TokenName))
                {
                    ErrorMsg(token, ". must be followed by field/method name");
                    return null;
                }
                TokenLValIField field = new TokenLValIField(token);
                field.baseRVal = operand;
                field.fieldName = (TokenName)token;
                operand = field;
                token = token.nextToken;
                goto modifiers;
            }

             // If followed by a '[', it is an array subscript.
            if(token is TokenKwBrkOpen)
            {
                TokenLValArEle tokenLValArEle = new TokenLValArEle(token);
                token = token.nextToken;

                 // Parse subscript(s) expression.
                tokenLValArEle.subRVal = ParseRVal(ref token, brkCloseOnly);
                if(tokenLValArEle.subRVal == null)
                {
                    ErrorMsg(tokenLValArEle, "invalid subscript");
                    return null;
                }

                 // See if comma-separated list of values.
                TokenRVal subscriptRVals;
                int numSubscripts = SplitCommaRVals(tokenLValArEle.subRVal, out subscriptRVals);
                if(numSubscripts > 1)
                {
                     // If so, put the values in an LSL_List object.
                    TokenRValList rValList = new TokenRValList(tokenLValArEle);
                    rValList.rVal = subscriptRVals;
                    rValList.nItems = numSubscripts;
                    tokenLValArEle.subRVal = rValList;
                }

                 // Either way, save array variable name
                 // and substitute whole reference for L-value
                tokenLValArEle.baseRVal = operand;
                operand = tokenLValArEle;
                goto modifiers;
            }

             // If followed by a '(', it is a function/method call.
            if(token is TokenKwParOpen)
            {
                operand = ParseRValCall(ref token, operand);
                goto modifiers;
            }

             // If 'new' arraytipe '{', it is an array initializer.
            if((token is TokenKwBrcOpen) && (operand is TokenLValSField) &&
                (((TokenLValSField)operand).fieldName.val == "$new") &&
                ((TokenLValSField)operand).baseType.ToString().EndsWith("]"))
            {
                operand = ParseRValNewArIni(ref token, (TokenLValSField)operand);
                if(operand != null)
                    goto modifiers;
            }

            return operand;
        }

        /**
         * @brief same as GetOperand() except doesn't check for any modifiers
         */
        private TokenRVal GetOperandNoMods(ref Token token)
        {
             // Simple unary operators.
            if((token is TokenKwSub) ||
                (token is TokenKwTilde) ||
                (token is TokenKwExclam))
            {
                Token uop = token;
                token = token.nextToken;
                TokenRVal rVal = GetOperand(ref token);
                if(rVal == null)
                    return null;
                return PerformUnOp(uop, rVal);
            }

             // Type casting.
            if((token is TokenKwParOpen) &&
                (token.nextToken is TokenType) &&
                (token.nextToken.nextToken is TokenKwParClose))
            {
                TokenType type = (TokenType)token.nextToken;
                token = token.nextToken.nextToken.nextToken;
                TokenRVal rVal = GetOperand(ref token);
                if(rVal == null)
                    return null;
                return new TokenRValCast(type, rVal);
            }

             // Parenthesized expression.
            if(token is TokenKwParOpen)
            {
                return ParseRValParen(ref token);
            }

             // Constants.
            if(token is TokenChar)
            {
                TokenRValConst rValConst = new TokenRValConst(token, ((TokenChar)token).val);
                token = token.nextToken;
                return rValConst;
            }
            if(token is TokenFloat)
            {
                TokenRValConst rValConst = new TokenRValConst(token, ((TokenFloat)token).val);
                token = token.nextToken;
                return rValConst;
            }
            if(token is TokenInt)
            {
                TokenRValConst rValConst = new TokenRValConst(token, ((TokenInt)token).val);
                token = token.nextToken;
                return rValConst;
            }
            if(token is TokenStr)
            {
                TokenRValConst rValConst = new TokenRValConst(token, ((TokenStr)token).val);
                token = token.nextToken;
                return rValConst;
            }
            if(token is TokenKwUndef)
            {
                TokenRValUndef rValUndef = new TokenRValUndef((TokenKwUndef)token);
                token = token.nextToken;
                return rValUndef;
            }

             // '<'value,...'>', ie, rotation or vector
            if(token is TokenKwCmpLT)
            {
                Token openBkt = token;
                token = token.nextToken;
                TokenRVal rValAll = ParseRVal(ref token, cmpGTOnly);
                if(rValAll == null)
                    return null;
                TokenRVal rVals;
                int nVals = SplitCommaRVals(rValAll, out rVals);
                switch(nVals)
                {
                    case 3:
                        {
                            TokenRValVec rValVec = new TokenRValVec(openBkt);
                            rValVec.xRVal = rVals;
                            rValVec.yRVal = (TokenRVal)rVals.nextToken;
                            rValVec.zRVal = (TokenRVal)rVals.nextToken.nextToken;
                            return rValVec;
                        }
                    case 4:
                        {
                            TokenRValRot rValRot = new TokenRValRot(openBkt);
                            rValRot.xRVal = rVals;
                            rValRot.yRVal = (TokenRVal)rVals.nextToken;
                            rValRot.zRVal = (TokenRVal)rVals.nextToken.nextToken;
                            rValRot.wRVal = (TokenRVal)rVals.nextToken.nextToken.nextToken;
                            return rValRot;
                        }
                    default:
                        {
                            ErrorMsg(openBkt, "bad rotation/vector");
                            token = SkipPastSemi(token);
                            return null;
                        }
                }
            }

             // '['value,...']', ie, list
            if(token is TokenKwBrkOpen)
            {
                TokenRValList rValList = new TokenRValList(token);
                token = token.nextToken;
                if(token is TokenKwBrkClose)
                {
                    token = token.nextToken;  // empty list
                }
                else
                {
                    TokenRVal rValAll = ParseRVal(ref token, brkCloseOnly);
                    if(rValAll == null)
                        return null;
                    rValList.nItems = SplitCommaRVals(rValAll, out rValList.rVal);
                }
                return rValList;
            }

             // Maybe we have <type>.<name> referencing a static field or method of some type.
            if((token is TokenType) && (token.nextToken is TokenKwDot) && (token.nextToken.nextToken is TokenName))
            {
                TokenLValSField field = new TokenLValSField(token.nextToken.nextToken);
                field.baseType = (TokenType)token;
                field.fieldName = (TokenName)token.nextToken.nextToken;
                token = token.nextToken.nextToken.nextToken;
                return field;
            }

             // Maybe we have 'this' referring to the object of the instance method.
            if(token is TokenKwThis)
            {
                if((currentDeclSDType == null) || !(currentDeclSDType is TokenDeclSDTypeClass))
                {
                    ErrorMsg(token, "using 'this' outside class definition");
                    token = SkipPastSemi(token);
                    return null;
                }
                TokenRValThis zhis = new TokenRValThis(token, (TokenDeclSDTypeClass)currentDeclSDType);
                token = token.nextToken;
                return zhis;
            }

             // Maybe we have 'base' referring to a field/method of the extended class.
            if(token is TokenKwBase)
            {
                if((currentDeclFunc == null) || (currentDeclFunc.sdtClass == null) || !(currentDeclFunc.sdtClass is TokenDeclSDTypeClass))
                {
                    ErrorMsg(token, "using 'base' outside method");
                    token = SkipPastSemi(token);
                    return null;
                }
                if(!(token.nextToken is TokenKwDot) || !(token.nextToken.nextToken is TokenName))
                {
                    ErrorMsg(token, "base must be followed by . then field or method name");
                    TokenRValThis zhis = new TokenRValThis(token, (TokenDeclSDTypeClass)currentDeclFunc.sdtClass);
                    token = token.nextToken;
                    return zhis;
                }
                TokenLValBaseField baseField = new TokenLValBaseField(token,
                                               (TokenName)token.nextToken.nextToken,
                                               (TokenDeclSDTypeClass)currentDeclFunc.sdtClass);
                token = token.nextToken.nextToken.nextToken;
                return baseField;
            }

             // Maybe we have 'new <script-defined-type>' saying to create an object instance.
             // This ends up generating a call to static function <script-defined-type>.$new(...)
             // whose CIL code is generated by GenerateNewobjBody().
            if(token is TokenKwNew)
            {
                if(!(token.nextToken is TokenType))
                {
                    ErrorMsg(token.nextToken, "new must be followed by type");
                    token = SkipPastSemi(token);
                    return null;
                }
                TokenLValSField field = new TokenLValSField(token.nextToken.nextToken);
                field.baseType = (TokenType)token.nextToken;
                field.fieldName = new TokenName(token, "$new");
                token = token.nextToken.nextToken;
                return field;
            }

             // All we got left is <name>, eg, arg, function, global or local variable reference
            if(token is TokenName)
            {
                TokenLValName name = new TokenLValName((TokenName)token, tokenScript.variablesStack);
                token = token.nextToken;
                return name;
            }

             // Who knows what it is supposed to be?
            ErrorMsg(token, "invalid operand token");
            token = SkipPastSemi(token);
            return null;
        }

        /**
         * @brief Parse a call expression
         * @param token = points to arg list '('
         * @param meth = points to method name being called
         * @returns call expression value
         *          token = points just past arg list ')'
         */
        private TokenRValCall ParseRValCall(ref Token token, TokenRVal meth)
        {
             // Set up basic function call struct with function name.
            TokenRValCall rValCall = new TokenRValCall(token);
            rValCall.meth = meth;

             // Parse the call parameters, if any.
            token = token.nextToken;
            if(token is TokenKwParClose)
            {
                token = token.nextToken;
            }
            else
            {
                rValCall.args = ParseRVal(ref token, parCloseOnly);
                if(rValCall.args == null)
                    return null;
                rValCall.nArgs = SplitCommaRVals(rValCall.args, out rValCall.args);
            }

            currentDeclFunc.unknownTrivialityCalls.AddLast(rValCall);

            return rValCall;
        }

        /**
         * @brief decode binary operator token
         * @param token = points to token to decode
         * @returns null: invalid operator token
         *          else: operator token and precedence
         */
        private BinOp GetOperator(ref Token token)
        {
            BinOp binOp = new BinOp();
            if(precedence.TryGetValue(token.GetType(), out binOp.preced))
            {
                binOp.token = (TokenKw)token;
                token = token.nextToken;
                return binOp;
            }

            if((token is TokenKwSemi) || (token is TokenKwBrcOpen) || (token is TokenKwBrcClose))
            {
                ErrorMsg(token, "premature expression end");
            }
            else
            {
                ErrorMsg(token, "invalid operator");
            }
            token = SkipPastSemi(token);
            return null;
        }

        private class BinOp
        {
            public BinOp pop;
            public TokenKw token;
            public int preced;
        }

        /**
         * @brief Return an R-value expression token that will be used to
         *        generate code to perform the operation at runtime.
         * @param left  = left-hand operand
         * @param binOp = operator
         * @param right = right-hand operand
         * @returns resultant expression
         */
        private TokenRVal PerformBinOp(TokenRVal left, BinOp binOp, TokenRVal right)
        {
            return new TokenRValOpBin(left, binOp.token, right);
        }

        /**
         * @brief Return an R-value expression token that will be used to
         *        generate code to perform the operation at runtime.
         * @param unOp  = operator
         * @param right = right-hand operand
         * @returns resultant constant or expression
         */
        private TokenRVal PerformUnOp(Token unOp, TokenRVal right)
        {
            return new TokenRValOpUn((TokenKw)unOp, right);
        }

        /**
         * @brief Parse an array initialization expression.
         * @param token = points to '{' on entry
         * @param newCall = encapsulates a '$new' call
         * @return resultant operand encapsulating '$new' call and initializers
         *             token = points just past terminating '}'
         *         ...or null if parse error
         */
        private TokenRVal ParseRValNewArIni(ref Token token, TokenLValSField newCall)
        {
            Stack<TokenList> stack = new Stack<TokenList>();
            TokenRValNewArIni arini = new TokenRValNewArIni(token);
            arini.arrayType = newCall.baseType;
            TokenList values = null;
            while(true)
            {

                // open brace means start a (sub-)list
                if(token is TokenKwBrcOpen)
                {
                    stack.Push(values);
                    values = new TokenList(token);
                    token = token.nextToken;
                    continue;
                }

                // close brace means end of (sub-)list
                // if final '}' all done parsing
                if(token is TokenKwBrcClose)
                {
                    token = token.nextToken;          // skip over the '}'
                    TokenList innerds = values;        // save the list just closed
                    arini.valueList = innerds;         // it's the top list if it's the last closed
                    values = stack.Pop();             // pop to next outer list
                    if(values == null)
                        return arini;  // final '}', we are done
                    values.tl.Add(innerds);           // put the inner list on end of outer list
                    if(token is TokenKwComma)
                    {       // should have a ',' or '}' next
                        token = token.nextToken;   // skip over the ','
                    }
                    else if(!(token is TokenKwBrcClose))
                    {
                        ErrorMsg(token, "expecting , or } after sublist");
                    }
                    continue;
                }

                // this is a comma that doesn't have a value expression before it
                // so we take it to mean skip initializing element (leave it zeroes/null etc)
                if(token is TokenKwComma)
                {
                    values.tl.Add(token);
                    token = token.nextToken;
                    continue;
                }

                // parse value expression and skip terminating ',' if any
                TokenRVal append = ParseRVal(ref token, commaOrBrcClose);
                if(append == null)
                    return null;
                values.tl.Add(append);
                if(token is TokenKwComma)
                {
                    token = token.nextToken;
                }
            }
        }

        /**
         * @brief parse out a parenthesized expression.
         * @param token = points to open parenthesis
         * @returns null: invalid expression
         *          else: parenthesized expression token or constant token
         *          token = points past the close parenthesis
         */
        private TokenRValParen ParseRValParen(ref Token token)
        {
            if(!(token is TokenKwParOpen))
            {
                ErrorMsg(token, "expecting (");
                token = SkipPastSemi(token);
                return null;
            }
            TokenRValParen tokenRValParen = new TokenRValParen(token);
            token = token.nextToken;
            tokenRValParen.rVal = ParseRVal(ref token, parCloseOnly);
            if(tokenRValParen.rVal == null)
                return null;
            return tokenRValParen;
        }

        /**
         * @brief Split a comma'd RVal into separate expressions
         * @param rValAll = expression containing commas
         * @returns number of comma separated values
         *          rVals = values in a null-terminated list linked by rVals.nextToken
         */
        private int SplitCommaRVals(TokenRVal rValAll, out TokenRVal rVals)
        {
            if(!(rValAll is TokenRValOpBin) || !(((TokenRValOpBin)rValAll).opcode is TokenKwComma))
            {
                rVals = rValAll;
                if(rVals.nextToken != null)
                    throw new Exception("expected null");
                return 1;
            }
            TokenRValOpBin opBin = (TokenRValOpBin)rValAll;
            TokenRVal rValLeft, rValRight;
            int leftCount = SplitCommaRVals(opBin.rValLeft, out rValLeft);
            int rightCount = SplitCommaRVals(opBin.rValRight, out rValRight);
            rVals = rValLeft;
            while(rValLeft.nextToken != null)
                rValLeft = (TokenRVal)rValLeft.nextToken;
            rValLeft.nextToken = rValRight;
            return leftCount + rightCount;
        }

        /**
         * @brief output error message and remember that there is an error.
         * @param token = what token is associated with the error
         * @param message = error message string
         */
        private void ErrorMsg(Token token, string message)
        {
            if(!errors || (token.file != lastErrorFile) || (token.line > lastErrorLine))
            {
                errors = true;
                lastErrorFile = token.file;
                lastErrorLine = token.line;
                token.ErrorMsg(message);
            }
        }

        /**
         * @brief Skip past the next semicolon (or matched braces)
         * @param token = points to token to skip over
         * @returns token just after the semicolon or close brace
         */
        private Token SkipPastSemi(Token token)
        {
            int braceLevel = 0;

            while(!(token is TokenEnd))
            {
                if((token is TokenKwSemi) && (braceLevel == 0))
                {
                    return token.nextToken;
                }
                if(token is TokenKwBrcOpen)
                {
                    braceLevel++;
                }
                if((token is TokenKwBrcClose) && (--braceLevel <= 0))
                {
                    return token.nextToken;
                }
                token = token.nextToken;
            }
            return token;
        }
    }

    /**
     * @brief Script-defined type declarations
     */
    public abstract class TokenDeclSDType: Token
    {
        protected const byte CLASS = 0;
        protected const byte DELEGATE = 1;
        protected const byte INTERFACE = 2;
        protected const byte TYPEDEF = 3;

        // stuff that gets cloned/copied/transformed when instantiating a generic
        // see InstantiateGeneric() below
        public TokenDeclSDType outerSDType;          // null if top-level
                                                     // else points to defining script-defined type
        public Dictionary<string, TokenDeclSDType> innerSDTypes = new Dictionary<string, TokenDeclSDType>();
        // indexed by shortName
        public Token begToken;                       // token that begins the definition (might be this or something like 'public')
        public Token endToken;                       // the '}' or ';' that ends the definition

        // generic instantiation assumes none of the rest needs to be cloned (well except for the shortName)
        public int sdTypeIndex = -1;                 // index in scriptObjCode.sdObjTypesIndx[] array
        public TokenDeclSDTypeClass extends;         // only non-null for TokenDeclSDTypeClass's
        public uint accessLevel;                     // SDT_PRIVATE, SDT_PROTECTED or SDT_PUBLIC
                                                     // ... all top-level types are SDT_PUBLIC
        public VarDict members = new VarDict(false);  // declared fields, methods, properties if any

        public Dictionary<string, int> genParams;    // list of parameters for generic prototypes
                                                     // null for non-generic prototypes
                                                     // eg, for 'Dictionary<K,V>'
                                                     //     ...genParams gives K->0; V->1

        public bool isPartial;                       // was declared with 'partial' keyword
                                                     // classes only, all others always false

        /*
         * Name of the type.
         *   shortName = doesn't include outer class type names
         *               eg, 'Engine' for non-generic
         *                   'Dictionary<,>' for generic prototype
         *                   'Dictionary<string,integer>' for generic instantiation
         *   longName = includes all outer class type names if any
         */
        private TokenName _shortName;
        private TokenName _longName;

        public TokenName shortName
        {
            get
            {
                return _shortName;
            }
            set
            {
                _shortName = value;
                _longName = null;
            }
        }

        public TokenName longName
        {
            get
            {
                if(_longName == null)
                {
                    _longName = _shortName;
                    if(outerSDType != null)
                    {
                        _longName = new TokenName(_shortName, outerSDType.longName.val + "." + _shortName.val);
                    }
                }
                return _longName;
            }
        }

        /*
         * Dictionary used when reading from object file that holds all script-defined types.
         * Not complete though until all types have been read from the object file.
         */
        private Dictionary<string, TokenDeclSDType> sdTypes;

        public TokenDeclSDType(Token t) : base(t) { }
        protected abstract TokenDeclSDType MakeBlank(TokenName shortName);
        public abstract TokenType MakeRefToken(Token t);
        public abstract Type GetSysType();
        public abstract void WriteToFile(BinaryWriter objFileWriter);
        public abstract void ReadFromFile(BinaryReader objFileReader, TextWriter asmFileWriter);

        /**
         * @brief Given that this is a generic prototype, apply the supplied genArgs 
         *        to create an equivalent instantiated non-generic.  This basically 
         *        makes a copy replacing all the parameter types with the actual 
         *        argument types.
         * @param this = the prototype to be instantiated, eg, 'Dictionary<string,integer>.Converter'
         * @param name = short name with arguments, eg, 'Converter<float>'.
         * @param genArgs = argument types of just this level, eg, 'float'.
         * @returns clone of this but with arguments applied and spliced in source token stream
         */
        public TokenDeclSDType InstantiateGeneric(string name, TokenType[] genArgs, ScriptReduce reduce)
        {
             // Malloc the struct and give it a name.
            TokenDeclSDType instdecl = this.MakeBlank(new TokenName(this, name));

             // If the original had an outer type, then so does the new one.
             // The outer type will never be a generic prototype, eg, if this 
             // is 'ValueList' it will always be inside 'Dictionary<string,integer>'
             // not 'Dictionary' at this point.
            if((this.outerSDType != null) && (this.outerSDType.genParams != null))
                throw new Exception();
            instdecl.outerSDType = this.outerSDType;

             // The generic prototype may have stuff like 'public' just before it and we need to copy that too.
            Token prefix;
            for(prefix = this; (prefix = prefix.prevToken) != null;)
            {
                if(!(prefix is TokenKwPublic) && !(prefix is TokenKwProtected) && !(prefix is TokenKwPrivate))
                    break;
            }
            this.begToken = prefix.nextToken;

             // Splice in a copy of the prefix tokens, just before the beginning token of prototype (this.begToken).
            while((prefix = prefix.nextToken) != this)
            {
                SpliceSourceToken(prefix.CopyToken(prefix));
            }

             // Splice instantiation (instdecl) in just before the beginning token of prototype (this.begToken).
            SpliceSourceToken(instdecl);

             // Now for the fun part...  Copy the rest of the prototype body to the 
             // instantiated body, replacing all generic parameter type tokens with 
             // the corresponding generic argument types.  Note that the parameters 
             // are numbered starting with the outermost so we need the full genArgs 
             // array.  Eg if we are doing 'Converter<V=float>' from 
             // 'Dictionary<T=string,U=integer>.Converter<V=float>', any V's are 
             // numbered [2].  Any [0]s or [1]s should be gone by now but it doesn't 
             // matter.
            int index;
            Token it, pt;
            TokenDeclSDType innerProto = this;
            TokenDeclSDType innerInst = instdecl;
            for(pt = this; (pt = pt.nextToken) != this.endToken;)
            {
                 // Coming across a sub-type's declaration involves a deep copy of the 
                 // declaration token.  Fortunately we are early on in parsing, so there 
                 // really isn't much to copy:
                 //   1) short name is the same, eg, doing List of Dictionary<string,integer>.List is same short name as Dictionary<T,U>.List
                 //      if generic, eg doing Converter<W> of Dictionary<T,U>.Converter<W>, we have to manually copy the W as well.
                 //   2) outerSDType is transformed from Dictionary<T,U> to Dictionary<string,integer>.
                 //   3) innerSDTypes is rebuilt when/if we find classes that are inner to this one.
                if(pt is TokenDeclSDType)
                {
                     // Make a new TokenDeclSDType{Class,Delegate,Interface}.
                    TokenDeclSDType ptSDType = (TokenDeclSDType)pt;
                    TokenDeclSDType itSDType = ptSDType.MakeBlank(new TokenName(ptSDType.shortName, ptSDType.shortName.val));

                     // Set up the transformed outerSDType.
                     // Eg, if we are creating Enumerator of Dictionary<string,integer>.Enumerator,
                     // innerProto = Dictionary<T,U> and innerInst = Dictionary<string,integer>.
                    itSDType.outerSDType = innerInst;

                     // This clone is an inner type of its next outer level.
                    reduce.CatalogSDTypeDecl(itSDType);

                     // We need to manually copy any generic parameters of the class declaration being cloned.
                     // eg, if we are cloning Converter<W>, this is where the W gets copied.
                     // Since it is an immutable array of strings, just copy the array pointer, if any.
                    itSDType.genParams = ptSDType.genParams;

                     // We are now processing tokens for this cloned type declaration.
                    innerProto = ptSDType;
                    innerInst = itSDType;

                     // Splice this clone token in.
                    it = itSDType;
                }

                 // Check for an generic parameter to substitute out.
                else if((pt is TokenName) && this.genParams.TryGetValue(((TokenName)pt).val, out index))
                {
                    it = genArgs[index].CopyToken(pt);
                }

                 // Everything else is a simple copy.
                else
                    it = pt.CopyToken(pt);

                 // Whatever we came up with, splice it into the source token stream.
                SpliceSourceToken(it);

                 // Maybe we just finished copying an inner type definition.
                 // If so, remember where it ends and pop it from the stack.
                if(innerProto.endToken == pt)
                {
                    innerInst.endToken = it;
                    innerProto = innerProto.outerSDType;
                    innerInst = innerInst.outerSDType;
                }
            }

             // Clone and insert the terminator, either '}' or ';'
            it = pt.CopyToken(pt);
            SpliceSourceToken(it);
            instdecl.endToken = it;

            return instdecl;
        }

        /**
         * @brief Splice a source token in just before the type's beginning keyword.
         */
        private void SpliceSourceToken(Token it)
        {
            it.nextToken = this.begToken;
            (it.prevToken = this.begToken.prevToken).nextToken = it;
            this.begToken.prevToken = it;
        }

        /**
         * @brief Read one of these in from the object file.
         * @param sdTypes = dictionary of script-defined types, not yet complete
         * @param name = script-visible name of this type
         * @param objFileReader = reads from the object file
         * @param asmFileWriter = writes to the disassembly file (might be null)
         */
        public static TokenDeclSDType ReadFromFile(Dictionary<string, TokenDeclSDType> sdTypes, string name,
                                                    BinaryReader objFileReader, TextWriter asmFileWriter)
        {
            string file = objFileReader.ReadString();
            int line = objFileReader.ReadInt32();
            int posn = objFileReader.ReadInt32();
            byte code = objFileReader.ReadByte();
            TokenName n = new TokenName(null, file, line, posn, name);
            TokenDeclSDType sdt;
            switch(code)
            {
                case CLASS:
                    {
                        sdt = new TokenDeclSDTypeClass(n, false);
                        break;
                    }
                case DELEGATE:
                    {
                        sdt = new TokenDeclSDTypeDelegate(n);
                        break;
                    }
                case INTERFACE:
                    {
                        sdt = new TokenDeclSDTypeInterface(n);
                        break;
                    }
                case TYPEDEF:
                    {
                        sdt = new TokenDeclSDTypeTypedef(n);
                        break;
                    }
                default:
                    throw new Exception();
            }
            sdt.sdTypes = sdTypes;
            sdt.sdTypeIndex = objFileReader.ReadInt32();
            sdt.ReadFromFile(objFileReader, asmFileWriter);
            return sdt;
        }

        /**
         * @brief Convert a typename string to a type token
         * @param name = script-visible name of token to create, 
         *               either a script-defined type or an LSL-defined type
         * @returns type token
         */
        protected TokenType MakeTypeToken(string name)
        {
            TokenDeclSDType sdtdecl;
            if(sdTypes.TryGetValue(name, out sdtdecl))
                return sdtdecl.MakeRefToken(this);
            return TokenType.FromLSLType(this, name);
        }

        // debugging - returns, eg, 'Dictionary<T,U>.Enumerator.Node'
        public override void DebString(StringBuilder sb)
        {
            // get long name broken down into segments from outermost to this
            Stack<TokenDeclSDType> declStack = new Stack<TokenDeclSDType>();
            for(TokenDeclSDType decl = this; decl != null; decl = decl.outerSDType)
            {
                declStack.Push(decl);
            }

            // output each segment's name followed by our args for it
            // starting with outermost and ending with this
            while(declStack.Count > 0)
            {
                TokenDeclSDType decl = declStack.Pop();
                sb.Append(decl.shortName.val);
                if(decl.genParams != null)
                {
                    sb.Append('<');
                    string[] parms = new string[decl.genParams.Count];
                    foreach(KeyValuePair<string, int> kvp in decl.genParams)
                    {
                        parms[kvp.Value] = kvp.Key;
                    }
                    for(int j = 0; j < parms.Length;)
                    {
                        sb.Append(parms[j]);
                        if(++j < parms.Length)
                            sb.Append(',');
                    }
                    sb.Append('>');
                }
                if(declStack.Count > 0)
                    sb.Append('.');
            }
        }
    }

    public class TokenDeclSDTypeClass: TokenDeclSDType
    {
        public List<TokenDeclSDTypeInterface> implements = new List<TokenDeclSDTypeInterface>();
        public TokenDeclVar instFieldInit;         // $instfieldinit function to do instance field initializations
        public TokenDeclVar staticFieldInit;       // $staticfieldinit function to do static field initializations

        public Dictionary<string, int> intfIndices = new Dictionary<string, int>();  // longname => this.iFaces index
        public TokenDeclSDTypeInterface[] iFaces;  // array of implemented interfaces
                                                   //   low-end entries copied from rootward classes
        public TokenDeclVar[][] iImplFunc;         // iImplFunc[i][j]:
                                                   //   low-end [i] entries copied from rootward classes
                                                   //   i = interface number from this.intfIndices[name]
                                                   //   j = method of interface from iface.methods[name].vTableIndex

        public TokenType arrayOfType;     // if array, it's an array of this type, else null
        public int arrayOfRank;           // if array, it has this number of dimensions, else zero

        public bool slotsAssigned;        // set true when slots have been assigned...
        public XMRInstArSizes instSizes = new XMRInstArSizes();
        // number of instance fields of various types
        public int numVirtFuncs;          // number of virtual functions
        public int numInterfaces;         // number of implemented interfaces

        private string extendsStr;
        private string arrayOfTypeStr;
        private List<StackedMethod> stackedMethods;
        private List<StackedIFace> stackedIFaces;

        public DynamicMethod[] vDynMeths;    // virtual method entrypoints
        public Type[] vMethTypes;            // virtual method delegate types
        public DynamicMethod[][] iDynMeths;  // interface method entrypoints
        public Type[][] iMethTypes;          // interface method types
                                             //   low-end [i] entries copied from rootward classes
                                             //   i = interface number from this.intfIndices[name]
                                             //   j = method of interface from iface.methods[name].vTableIndex

        public TokenDeclSDTypeClass(TokenName shortName, bool isPartial) : base(shortName)
        {
            this.shortName = shortName;
            this.isPartial = isPartial;
        }

        protected override TokenDeclSDType MakeBlank(TokenName shortName)
        {
            return new TokenDeclSDTypeClass(shortName, false);
        }

        public override TokenType MakeRefToken(Token t)
        {
            return new TokenTypeSDTypeClass(t, this);
        }

        public override Type GetSysType()
        {
            return typeof(XMRSDTypeClObj);
        }

        /**
         * @brief See if the class implements the interface.
         *        Do a recursive (deep) check in all rootward classes.
         */
        public bool CanCastToIntf(TokenDeclSDTypeInterface intf)
        {
            if(this.implements.Contains(intf))
                return true;
            if(this.extends == null)
                return false;
            return this.extends.CanCastToIntf(intf);
        }

        /**
         * @brief Write enough out so we can reconstruct with ReadFromFile.
         */
        public override void WriteToFile(BinaryWriter objFileWriter)
        {
            objFileWriter.Write(this.file);
            objFileWriter.Write(this.line);
            objFileWriter.Write(this.posn);
            objFileWriter.Write((byte)CLASS);
            objFileWriter.Write(this.sdTypeIndex);

            this.instSizes.WriteToFile(objFileWriter);
            objFileWriter.Write(numVirtFuncs);

            if(extends == null)
            {
                objFileWriter.Write("");
            }
            else
            {
                objFileWriter.Write(extends.longName.val);
            }

            objFileWriter.Write(arrayOfRank);
            if(arrayOfRank > 0)
                objFileWriter.Write(arrayOfType.ToString());

            foreach(TokenDeclVar meth in members)
            {
                if((meth.retType != null) && (meth.vTableIndex >= 0))
                {
                    objFileWriter.Write(meth.vTableIndex);
                    objFileWriter.Write(meth.GetObjCodeName());
                    objFileWriter.Write(meth.GetDelType().decl.GetWholeSig());
                }
            }
            objFileWriter.Write(-1);

            int numIFaces = iImplFunc.Length;
            objFileWriter.Write(numIFaces);
            for(int i = 0; i < numIFaces; i++)
            {
                objFileWriter.Write(iFaces[i].longName.val);
                TokenDeclVar[] meths = iImplFunc[i];
                int numMeths = 0;
                if(meths != null)
                    numMeths = meths.Length;
                objFileWriter.Write(numMeths);
                for(int j = 0; j < numMeths; j++)
                {
                    TokenDeclVar meth = meths[j];
                    objFileWriter.Write(meth.vTableIndex);
                    objFileWriter.Write(meth.GetObjCodeName());
                    objFileWriter.Write(meth.GetDelType().decl.GetWholeSig());
                }
            }
        }

        /**
         * @brief Reconstruct from the file.
         */
        public override void ReadFromFile(BinaryReader objFileReader, TextWriter asmFileWriter)
        {
            instSizes.ReadFromFile(objFileReader);
            numVirtFuncs = objFileReader.ReadInt32();

            extendsStr = objFileReader.ReadString();
            arrayOfRank = objFileReader.ReadInt32();
            if(arrayOfRank > 0)
                arrayOfTypeStr = objFileReader.ReadString();

            if(asmFileWriter != null)
            {
                instSizes.WriteAsmFile(asmFileWriter, extendsStr + "." + shortName.val + ".numInst");
            }

            stackedMethods = new List<StackedMethod>();
            int vTableIndex;
            while((vTableIndex = objFileReader.ReadInt32()) >= 0)
            {
                StackedMethod sm;
                sm.methVTI = vTableIndex;
                sm.methName = objFileReader.ReadString();
                sm.methSig = objFileReader.ReadString();
                stackedMethods.Add(sm);
            }

            int numIFaces = objFileReader.ReadInt32();
            if(numIFaces > 0)
            {
                iDynMeths = new DynamicMethod[numIFaces][];
                iMethTypes = new Type[numIFaces][];
                stackedIFaces = new List<StackedIFace>();
                for(int i = 0; i < numIFaces; i++)
                {
                    string iFaceName = objFileReader.ReadString();
                    intfIndices[iFaceName] = i;
                    int numMeths = objFileReader.ReadInt32();
                    iDynMeths[i] = new DynamicMethod[numMeths];
                    iMethTypes[i] = new Type[numMeths];
                    for(int j = 0; j < numMeths; j++)
                    {
                        StackedIFace si;
                        si.iFaceIndex = i;
                        si.methIndex = j;
                        si.vTableIndex = objFileReader.ReadInt32();
                        si.methName = objFileReader.ReadString();
                        si.methSig = objFileReader.ReadString();
                        stackedIFaces.Add(si);
                    }
                }
            }
        }

        private struct StackedMethod
        {
            public int methVTI;
            public string methName;
            public string methSig;
        }

        private struct StackedIFace
        {
            public int iFaceIndex;   // which implemented interface
            public int methIndex;    // which method of that interface
            public int vTableIndex;  // <0: implemented by non-virtual; else: implemented by virtual
            public string methName;  // object code name of implementing method (GetObjCodeName)
            public string methSig;   // method signature incl return type (GetWholeSig)
        }

        /**
         * @brief Called after all dynamic method code has been generated to fill in vDynMeths and vMethTypes
         *        Also fills in iDynMeths, iMethTypes.
         */
        public void FillVTables(ScriptObjCode scriptObjCode)
        {
            if(extendsStr != null)
            {
                if(extendsStr != "")
                {
                    extends = (TokenDeclSDTypeClass)scriptObjCode.sdObjTypesName[extendsStr];
                    extends.FillVTables(scriptObjCode);
                }
                extendsStr = null;
            }
            if(arrayOfTypeStr != null)
            {
                arrayOfType = MakeTypeToken(arrayOfTypeStr);
                arrayOfTypeStr = null;
            }

            if((numVirtFuncs > 0) && (stackedMethods != null))
            {
                 // Allocate arrays big enough for mine plus type we are extending.
                vDynMeths = new DynamicMethod[numVirtFuncs];
                vMethTypes = new Type[numVirtFuncs];

                 // Fill in low parts from type we are extending.
                if(extends != null)
                {
                    int n = extends.numVirtFuncs;
                    for(int i = 0; i < n; i++)
                    {
                        vDynMeths[i] = extends.vDynMeths[i];
                        vMethTypes[i] = extends.vMethTypes[i];
                    }
                }

                 // Fill in high parts with my own methods.
                 // Might also overwrite lower ones with 'override' methods.
                foreach(StackedMethod sm in stackedMethods)
                {
                    int i = sm.methVTI;
                    string methName = sm.methName;
                    DynamicMethod dm;
                    if(scriptObjCode.dynamicMethods.TryGetValue(methName, out dm))
                    {
                        // method is not abstract
                        vDynMeths[i] = dm;
                        vMethTypes[i] = GetDynamicMethodDelegateType(dm, sm.methSig);
                    }
                }
                stackedMethods = null;
            }

            if(stackedIFaces != null)
            {
                foreach(StackedIFace si in stackedIFaces)
                {
                    int i = si.iFaceIndex;
                    int j = si.methIndex;
                    int vti = si.vTableIndex;
                    string methName = si.methName;
                    DynamicMethod dm = scriptObjCode.dynamicMethods[methName];
                    iDynMeths[i][j] = (vti < 0) ? dm : vDynMeths[vti];
                    iMethTypes[i][j] = GetDynamicMethodDelegateType(dm, si.methSig);
                }
                stackedIFaces = null;
            }
        }

        private Type GetDynamicMethodDelegateType(DynamicMethod dm, string methSig)
        {
            Type retType = dm.ReturnType;
            ParameterInfo[] pi = dm.GetParameters();
            Type[] argTypes = new Type[pi.Length];
            for(int j = 0; j < pi.Length; j++)
            {
                argTypes[j] = pi[j].ParameterType;
            }
            return DelegateCommon.GetType(retType, argTypes, methSig);
        }

        public override void DebString(StringBuilder sb)
        {
             // Don't output if array of some type.
             // They will be re-instantiated as referenced by rest of script.
            if(arrayOfType != null)
                return;

             // This class name and extended/implemented type declaration.
            sb.Append("class ");
            sb.Append(shortName.val);
            bool first = true;
            if(extends != null)
            {
                sb.Append(" : ");
                sb.Append(extends.longName);
                first = false;
            }
            foreach(TokenDeclSDType impld in implements)
            {
                sb.Append(first ? " : " : ", ");
                sb.Append(impld.longName);
                first = false;
            }
            sb.Append(" {");

             // Inner type definitions.
            foreach(TokenDeclSDType subs in innerSDTypes.Values)
            {
                subs.DebString(sb);
            }

             // Members (fields, methods, properties).
            foreach(TokenDeclVar memb in members)
            {
                if((memb == instFieldInit) || (memb == staticFieldInit))
                {
                    memb.DebStringInitFields(sb);
                }
                else if(memb.retType != null)
                {
                    memb.DebString(sb);
                }
            }

            sb.Append('}');
        }
    }

    public class TokenDeclSDTypeDelegate: TokenDeclSDType
    {
        private TokenType retType;
        private TokenType[] argTypes;

        private string argSig;
        private string wholeSig;
        private Type sysType;
        private Type retSysType;
        private Type[] argSysTypes;

        private string retStr;
        private string[] argStrs;

        private static Dictionary<string, TokenDeclSDTypeDelegate> inlines = new Dictionary<string, TokenDeclSDTypeDelegate>();
        private static Dictionary<Type, string> inlrevs = new Dictionary<Type, string>();

        public TokenDeclSDTypeDelegate(TokenName shortName) : base(shortName)
        {
            this.shortName = shortName;
        }
        public void SetRetArgTypes(TokenType retType, TokenType[] argTypes)
        {
            this.retType = retType;
            this.argTypes = argTypes;
        }

        protected override TokenDeclSDType MakeBlank(TokenName shortName)
        {
            return new TokenDeclSDTypeDelegate(shortName);
        }

        public override TokenType MakeRefToken(Token t)
        {
            return new TokenTypeSDTypeDelegate(t, this);
        }

        /**
         * @brief Get system type for the whole delegate.
         */
        public override Type GetSysType()
        {
            if(sysType == null)
                FillInStuff();
            return sysType;
        }

        /**
         * @brief Get the function's return value type (TokenTypeVoid if void, never null)
         */
        public TokenType GetRetType()
        {
            if(retType == null)
                FillInStuff();
            return retType;
        }

        /**
         * @brief Get the function's argument types
         */
        public TokenType[] GetArgTypes()
        {
            if(argTypes == null)
                FillInStuff();
            return argTypes;
        }

        /**
         * @brief Get signature for the whole delegate, eg, "void(integer,list)"
         */
        public string GetWholeSig()
        {
            if(wholeSig == null)
                FillInStuff();
            return wholeSig;
        }

        /**
         * @brief Get signature for the arguments, eg, "(integer,list)"
         */
        public string GetArgSig()
        {
            if(argSig == null)
                FillInStuff();
            return argSig;
        }

        /**
         * @brief Find out how to create one of these delegates.
         */
        public ConstructorInfo GetConstructorInfo()
        {
            if(sysType == null)
                FillInStuff();
            return sysType.GetConstructor(DelegateCommon.constructorArgTypes);
        }

        /**
         * @brief Find out how to call what one of these delegates points to.
         */
        public MethodInfo GetInvokerInfo()
        {
            if(sysType == null)
                FillInStuff();
            return sysType.GetMethod("Invoke", argSysTypes);
        }

        /**
         * @brief Write enough out to a file so delegate can be reconstructed in ReadFromFile().
         */
        public override void WriteToFile(BinaryWriter objFileWriter)
        {
            objFileWriter.Write(this.file);
            objFileWriter.Write(this.line);
            objFileWriter.Write(this.posn);
            objFileWriter.Write((byte)DELEGATE);
            objFileWriter.Write(this.sdTypeIndex);

            objFileWriter.Write(retType.ToString());
            int nArgs = argTypes.Length;
            objFileWriter.Write(nArgs);
            for(int i = 0; i < nArgs; i++)
            {
                objFileWriter.Write(argTypes[i].ToString());
            }
        }

        /**
         * @brief Read that data from file so we can reconstruct.
         *        Don't actually reconstruct yet in case any forward-referenced types are undefined.
         */
        public override void ReadFromFile(BinaryReader objFileReader, TextWriter asmFileWriter)
        {
            retStr = objFileReader.ReadString();
            int nArgs = objFileReader.ReadInt32();
            if(asmFileWriter != null)
            {
                asmFileWriter.Write("  delegate " + retStr + " " + longName.val + "(");
            }
            argStrs = new string[nArgs];
            for(int i = 0; i < nArgs; i++)
            {
                argStrs[i] = objFileReader.ReadString();
                if(asmFileWriter != null)
                {
                    if(i > 0)
                        asmFileWriter.Write(",");
                    asmFileWriter.Write(argStrs[i]);
                }
            }
            if(asmFileWriter != null)
            {
                asmFileWriter.WriteLine(");");
            }
        }

        /**
         * @brief Fill in missing internal data.
         */
        private void FillInStuff()
        {
            int nArgs;

             // This happens when the node was restored via ReadFromFile().
             // It leaves the types in retStr/argStrs for resolution after
             // all definitions have been read from the object file in case
             // there are forward references.
            if(retType == null)
            {
                retType = MakeTypeToken(retStr);
            }
            if(argTypes == null)
            {
                nArgs = argStrs.Length;
                argTypes = new TokenType[nArgs];
                for(int i = 0; i < nArgs; i++)
                {
                    argTypes[i] = MakeTypeToken(argStrs[i]);
                }
            }

             // Fill in system types from token types.
             // Might as well build the signature strings too from token types.
            retSysType = retType.ToSysType();

            nArgs = argTypes.Length;
            StringBuilder sb = new StringBuilder();
            argSysTypes = new Type[nArgs];
            sb.Append('(');
            for(int i = 0; i < nArgs; i++)
            {
                if(i > 0)
                    sb.Append(',');
                sb.Append(argTypes[i].ToString());
                argSysTypes[i] = argTypes[i].ToSysType();
            }
            sb.Append(')');
            argSig = sb.ToString();
            wholeSig = retType.ToString() + argSig;

             // Now we can create a system delegate type from the given
             // return and argument types.  Give it an unique name using
             // the whole signature string.
            sysType = DelegateCommon.GetType(retSysType, argSysTypes, wholeSig);
        }

        /**
         * @brief create delegate reference token for inline functions.
         *        there is just one instance of these per inline function
         *        shared by all scripts, and it is just used when the
         *        script engine is loaded.
         */
        public static TokenDeclSDTypeDelegate CreateInline(TokenType retType, TokenType[] argTypes)
        {
            TokenDeclSDTypeDelegate decldel;

             // Name it after the whole signature string.
            StringBuilder sb = new StringBuilder("$inline");
            sb.Append(retType.ToString());
            sb.Append("(");
            bool first = true;
            foreach(TokenType at in argTypes)
            {
                if(!first)
                    sb.Append(",");
                sb.Append(at.ToString());
                first = false;
            }
            sb.Append(")");
            string inlname = sb.ToString();
            if(!inlines.TryGetValue(inlname, out decldel))
            {
                 // Create the corresponding declaration and link to it
                TokenName name = new TokenName(null, inlname);
                decldel = new TokenDeclSDTypeDelegate(name);
                decldel.retType = retType;
                decldel.argTypes = argTypes;
                inlines.Add(inlname, decldel);
                inlrevs.Add(decldel.GetSysType(), inlname);
            }
            return decldel;
        }

        public static string TryGetInlineName(Type sysType)
        {
            string name;
            if(!inlrevs.TryGetValue(sysType, out name))
                return null;
            return name;
        }

        public static Type TryGetInlineSysType(string name)
        {
            TokenDeclSDTypeDelegate decl;
            if(!inlines.TryGetValue(name, out decl))
                return null;
            return decl.GetSysType();
        }
    }

    public class TokenDeclSDTypeInterface: TokenDeclSDType
    {
        public VarDict methsNProps = new VarDict(false);
        // any class that implements this interface
        // must implement all of these methods & properties

        public List<TokenDeclSDTypeInterface> implements = new List<TokenDeclSDTypeInterface>();
        // any class that implements this interface
        // must also implement all of the methods & properties 
        // of all of these interfaces

        public TokenDeclSDTypeInterface(TokenName shortName) : base(shortName)
        {
            this.shortName = shortName;
        }

        protected override TokenDeclSDType MakeBlank(TokenName shortName)
        {
            return new TokenDeclSDTypeInterface(shortName);
        }

        public override TokenType MakeRefToken(Token t)
        {
            return new TokenTypeSDTypeInterface(t, this);
        }

        public override Type GetSysType()
        {
            // interfaces are implemented as arrays of delegates
            // they are taken from iDynMeths[interfaceIndex] of a script-defined class object
            return typeof(Delegate[]);
        }

        public override void WriteToFile(BinaryWriter objFileWriter)
        {
            objFileWriter.Write(this.file);
            objFileWriter.Write(this.line);
            objFileWriter.Write(this.posn);
            objFileWriter.Write((byte)INTERFACE);
            objFileWriter.Write(this.sdTypeIndex);
        }

        public override void ReadFromFile(BinaryReader objFileReader, TextWriter asmFileWriter)
        {
        }

        /**
         * @brief Add this interface to the list of interfaces implemented by a class if not already.
         *        And also add this interface's implemented interfaces to the class for those not already there, 
         *        just as if the class itself had declared to implement those interfaces.
         */
        public void AddToClassDecl(TokenDeclSDTypeClass tokdeclcl)
        {
            if(!tokdeclcl.implements.Contains(this))
            {
                tokdeclcl.implements.Add(this);
                foreach(TokenDeclSDTypeInterface subimpl in this.implements)
                {
                    subimpl.AddToClassDecl(tokdeclcl);
                }
            }
        }

        /**
         * @brief See if the 'this' interface implements the new interface.
         *        Do a recursive (deep) check.
         */
        public bool Implements(TokenDeclSDTypeInterface newDecl)
        {
            foreach(TokenDeclSDTypeInterface ii in this.implements)
            {
                if(ii == newDecl)
                    return true;
                if(ii.Implements(newDecl))
                    return true;
            }
            return false;
        }

        /**
         * @brief Scan an interface and all its implemented interfaces for a method or property
         * @param scg = script code generator (ie, which script is being compiled)
         * @param fieldName = name of the member being looked for
         * @param argsig = the method's argument signature
         * @returns null: no such member; intf = undefined
         *          else: member; intf = which interface actually found in
         */
        public TokenDeclVar FindIFaceMember(ScriptCodeGen scg, TokenName fieldName, TokenType[] argsig, out TokenDeclSDTypeInterface intf)
        {
            intf = this;
            TokenDeclVar var = scg.FindSingleMember(this.methsNProps, fieldName, argsig);
            if(var == null)
            {
                foreach(TokenDeclSDTypeInterface ii in this.implements)
                {
                    var = ii.FindIFaceMember(scg, fieldName, argsig, out intf);
                    if(var != null)
                        break;
                }
            }
            return var;
        }
    }

    public class TokenDeclSDTypeTypedef: TokenDeclSDType
    {

        public TokenDeclSDTypeTypedef(TokenName shortName) : base(shortName)
        {
            this.shortName = shortName;
        }

        protected override TokenDeclSDType MakeBlank(TokenName shortName)
        {
            return new TokenDeclSDTypeTypedef(shortName);
        }

        public override TokenType MakeRefToken(Token t)
        {
            // if our body is a single type token, that is what we return
            // otherwise return null saying maybe our body needs some substitutions
            if(!(this.nextToken is TokenType))
                return null;
            if(this.nextToken.nextToken != this.endToken)
            {
                this.nextToken.nextToken.ErrorMsg("extra tokens for typedef");
                return null;
            }
            return (TokenType)this.nextToken.CopyToken(t);
        }

        public override Type GetSysType()
        {
            // we are just a macro
            // we are asked for system type because we are cataloged
            // but we don't really have one so return null
            return null;
        }

        public override void WriteToFile(BinaryWriter objFileWriter)
        {
            objFileWriter.Write(this.file);
            objFileWriter.Write(this.line);
            objFileWriter.Write(this.posn);
            objFileWriter.Write((byte)TYPEDEF);
            objFileWriter.Write(this.sdTypeIndex);
        }

        public override void ReadFromFile(BinaryReader objFileReader, TextWriter asmFileWriter)
        {
        }
    }

    /**
     * @brief Script-defined type references.
     *        These occur in the source code wherever it specifies (eg, variable declaration) a script-defined type.
     *        These must be copyable via CopyToken().
     */
    public abstract class TokenTypeSDType: TokenType
    {
        public TokenTypeSDType(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeSDType(Token t) : base(t) { }
        public abstract TokenDeclSDType GetDecl();
        public abstract void SetDecl(TokenDeclSDType decl);
    }

    public class TokenTypeSDTypeClass: TokenTypeSDType
    {
        private static readonly FieldInfo iarSDTClObjsFieldInfo = typeof(XMRInstArrays).GetField("iarSDTClObjs");

        public TokenDeclSDTypeClass decl;

        public TokenTypeSDTypeClass(Token t, TokenDeclSDTypeClass decl) : base(t)
        {
            this.decl = decl;
        }
        public override TokenDeclSDType GetDecl()
        {
            return decl;
        }
        public override void SetDecl(TokenDeclSDType decl)
        {
            this.decl = (TokenDeclSDTypeClass)decl;
        }
        public override string ToString()
        {
            return decl.longName.val;
        }
        public override Type ToSysType()
        {
            return typeof(XMRSDTypeClObj);
        }

        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes ias)
        {
            declVar.vTableArray = iarSDTClObjsFieldInfo;
            declVar.vTableIndex = ias.iasSDTClObjs++;
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append(decl.longName);
        }
    }

    public class TokenTypeSDTypeDelegate: TokenTypeSDType
    {
        private static readonly FieldInfo iarObjectsFieldInfo = typeof(XMRInstArrays).GetField("iarObjects");

        public TokenDeclSDTypeDelegate decl;

        /**
         * @brief create a reference to an explicitly declared delegate
         * @param t = where the reference is being made in the source file
         * @param decl = the explicit delegate declaration
         */
        public TokenTypeSDTypeDelegate(Token t, TokenDeclSDTypeDelegate decl) : base(t)
        {
            this.decl = decl;
        }
        public override TokenDeclSDType GetDecl()
        {
            return decl;
        }
        public override void SetDecl(TokenDeclSDType decl)
        {
            this.decl = (TokenDeclSDTypeDelegate)decl;
        }

        /**
         * @brief create a reference to a possibly anonymous delegate
         * @param t = where the reference is being made in the source file
         * @param retType = return type (TokenTypeVoid if void, never null)
         * @param argTypes = script-visible argument types
         * @param tokenScript = what script this is part of
         */
        public TokenTypeSDTypeDelegate(Token t, TokenType retType, TokenType[] argTypes, TokenScript tokenScript) : base(t)
        {
            TokenDeclSDTypeDelegate decldel;

             // See if we already have a matching declared one cataloged.
            int nArgs = argTypes.Length;
            foreach(TokenDeclSDType decl in tokenScript.sdSrcTypesValues)
            {
                if(decl is TokenDeclSDTypeDelegate)
                {
                    decldel = (TokenDeclSDTypeDelegate)decl;
                    TokenType rt = decldel.GetRetType();
                    TokenType[] ats = decldel.GetArgTypes();
                    if((rt.ToString() == retType.ToString()) && (ats.Length == nArgs))
                    {
                        for(int i = 0; i < nArgs; i++)
                        {
                            if(ats[i].ToString() != argTypes[i].ToString())
                                goto nomatch;
                        }
                        this.decl = decldel;
                        return;
                    }
                }
                nomatch:
                ;
            }

             // No such luck, create a new anonymous declaration.
            StringBuilder sb = new StringBuilder("$anondel$");
            sb.Append(retType.ToString());
            sb.Append("(");
            bool first = true;
            foreach(TokenType at in argTypes)
            {
                if(!first)
                    sb.Append(",");
                sb.Append(at.ToString());
                first = false;
            }
            sb.Append(")");
            TokenName name = new TokenName(t, sb.ToString());
            decldel = new TokenDeclSDTypeDelegate(name);
            decldel.SetRetArgTypes(retType, argTypes);
            tokenScript.sdSrcTypesAdd(name.val, decldel);
            this.decl = decldel;
        }

        public override Type ToSysType()
        {
            return decl.GetSysType();
        }

        public override string ToString()
        {
            return decl.longName.val;
        }

        /**
         * @brief Assign slots in the gblObjects[] array because we have to typecast out in any case.
         *        Likewise with the sdtcObjects[] array.
         */
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes ias)
        {
            declVar.vTableArray = iarObjectsFieldInfo;
            declVar.vTableIndex = ias.iasObjects++;
        }

        /**
         * @brief create delegate reference token for inline functions.
         */
        public TokenTypeSDTypeDelegate(TokenType retType, TokenType[] argTypes) : base(null)
        {
            this.decl = TokenDeclSDTypeDelegate.CreateInline(retType, argTypes);
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append(decl.longName);
        }
    }

    public class TokenTypeSDTypeInterface: TokenTypeSDType
    {
        private static readonly FieldInfo iarSDTIntfObjsFieldInfo = typeof(XMRInstArrays).GetField("iarSDTIntfObjs");

        public TokenDeclSDTypeInterface decl;

        public TokenTypeSDTypeInterface(Token t, TokenDeclSDTypeInterface decl) : base(t)
        {
            this.decl = decl;
        }
        public override TokenDeclSDType GetDecl()
        {
            return decl;
        }
        public override void SetDecl(TokenDeclSDType decl)
        {
            this.decl = (TokenDeclSDTypeInterface)decl;
        }

        public override string ToString()
        {
            return decl.longName.val;
        }
        public override Type ToSysType()
        {
            return typeof(Delegate[]);
        }

        /**
         * @brief Assign slots in the gblSDTIntfObjs[] array
         *        Likewise with the sdtcSDTIntfObjs[] array.
         */
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes ias)
        {
            declVar.vTableArray = iarSDTIntfObjsFieldInfo;
            declVar.vTableIndex = ias.iasSDTIntfObjs++;
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append(decl.longName);
        }
    }

    /**
     * @brief function argument list declaration
     */
    public class TokenArgDecl: Token
    {
        public VarDict varDict = new VarDict(false);

        public TokenArgDecl(Token original) : base(original) { }

        public bool AddArg(TokenType type, TokenName name)
        {
            TokenDeclVar var = new TokenDeclVar(name, null, null);
            var.name = name;
            var.type = type;
            var.vTableIndex = varDict.Count;
            return varDict.AddEntry(var);
        }

        /**
         * @brief Get an array of the argument types.
         */
        private TokenType[] _types;
        public TokenType[] types
        {
            get
            {
                if(_types == null)
                {
                    _types = new TokenType[varDict.Count];
                    foreach(TokenDeclVar var in varDict)
                    {
                        _types[var.vTableIndex] = var.type;
                    }
                }
                return _types;
            }
        }

        /**
         * @brief Access the arguments as an array of variables.
         */
        private TokenDeclVar[] _vars;
        public TokenDeclVar[] vars
        {
            get
            {
                if(_vars == null)
                {
                    _vars = new TokenDeclVar[varDict.Count];
                    foreach(TokenDeclVar var in varDict)
                    {
                        _vars[var.vTableIndex] = var;
                    }
                }
                return _vars;
            }
        }

        /**
         * @brief Get argument signature string, eg, "(list,vector,integer)"
         */
        private string argSig = null;
        public string GetArgSig()
        {
            if(argSig == null)
            {
                argSig = ScriptCodeGen.ArgSigString(types);
            }
            return argSig;
        }
    }

    /**
     * @brief encapsulate a state declaration in a single token
     */
    public class TokenDeclState: Token
    {

        public TokenName name;  // null for default state
        public TokenStateBody body;

        public TokenDeclState(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            if(name == null)
            {
                sb.Append("default");
            }
            else
            {
                sb.Append("state ");
                sb.Append(name);
            }
            body.DebString(sb);
        }
    }

    /**
     * @brief encapsulate the declaration of a field/function/method/property/variable.
     */

    public enum Triviality
    {  // function triviality: has no loops and doesn't call anything that has loops
       // such a function does not need all the CheckRun() and stack serialization stuff
        unknown,   // function's Triviality unknown as of yet
                   // - it does not have any loops or backward gotos
                   // - nothing it calls is known to be complex
        trivial,   // function known to be trivial
                   // - it does not have any loops or backward gotos
                   // - everything it calls is known to be trivial
        complex,   // function known to be complex
                   // - it has loops or backward gotos
                   // - something it calls is known to be complex
        analyzing  // triviality is being analyzed (used to detect recursive loops)
    };

    public class TokenDeclVar: TokenStmt
    {
        public TokenName name;                 // vars: name; funcs: bare name, ie, no signature
        public TokenRVal init;                 // vars: null if none; funcs: null
        public bool constant;                  // vars: 'constant'; funcs: false
        public uint sdtFlags;                  // SDT_<*> flags

        public CompValu location;              // used by codegen to keep track of location
        public FieldInfo vTableArray;
        public int vTableIndex = -1;           // local vars: not used (-1)
                                               // arg vars: index in the arg list
                                               // global vars: which slot in gbl<Type>s[] array it is stored
                                               // instance vars: which slot in inst<Types>s[] array it is stored
                                               // static vars: which slot in gbl<Type>s[] array it is stored
                                               // global funcs: not used (-1)
                                               // virt funcs: which slot in vTable[] array it is stored
                                               // instance func: not used (-1)
        public TokenDeclVar getProp;           // if property, function that reads value
        public TokenDeclVar setProp;           // if property, function that writes value

        public TokenScript tokenScript;        // what script this function is part of
        public TokenDeclSDType sdtClass;       // null: script global member
                                               // else: member is part of this script-defined type

        // function-only data:

        public TokenType retType;              // vars: null; funcs: TokenTypeVoid if void
        public TokenArgDecl argDecl;           // vars: null; funcs: argument list prototypes
        public TokenStmtBlock body;            // vars: null; funcs: statements (null iff abstract)
        public Dictionary<string, TokenStmtLabel> labels = new Dictionary<string, TokenStmtLabel>();
        // all labels defined in the function
        public LinkedList<TokenDeclVar> localVars = new LinkedList<TokenDeclVar>();
        // all local variables declared by this function
        // - doesn't include argument variables
        public TokenIntfImpl implements;       // if script-defined type method, what interface method(s) this func implements
        public TokenRValCall baseCtorCall;     // if script-defined type constructor, call to base constructor, if any
        public Triviality triviality = Triviality.unknown;
        // vars: unknown (not used for any thing); funcs: unknown/trivial/complex
        public LinkedList<TokenRValCall> unknownTrivialityCalls = new LinkedList<TokenRValCall>();
        // reduction puts all calls here
        // compilation sorts it all out

        public ScriptObjWriter ilGen;          // codegen stores emitted code here

        /**
         * @brief Set up a variable declaration token.
         * @param original = original source token that triggered definition
         *                   (for error messages)
         * @param func = null: global variable
         *               else: local to the given function
         */
        public TokenDeclVar(Token original, TokenDeclVar func, TokenScript ts) : base(original)
        {
            if(func != null)
            {
                func.localVars.AddLast(this);
            }
            tokenScript = ts;
        }

        /**
         * @brief Get/Set overall type
         *        For vars, this is the type of the location
         *        For funcs, this is the delegate type
         */
        private TokenType _type;
        public TokenType type
        {
            get
            {
                if(_type == null)
                {
                    GetDelType();
                }
                return _type;
            }
            set
            {
                _type = value;
            }
        }

        /**
         * @brief Full name: <fulltype>.<name>(<argsig>)
         *        (<argsig>) missing for fields/variables
         *        <fulltype>. missing for top-level functions/variables
         */
        public string fullName
        {
            get
            {
                if(sdtClass == null)
                {
                    if(retType == null)
                        return name.val;
                    return funcNameSig.val;
                }
                string ln = sdtClass.longName.val;
                if(retType == null)
                    return ln + "." + name.val;
                return ln + "." + funcNameSig.val;
            }
        }

        /**
         * @brief See if reading or writing the variable is trivial.
         *        Note that for functions, this is reading the function itself, 
         *        as in 'someDelegate = SomeFunction;', not calling it as such.
         *        The triviality of actually calling the function is IsFuncTrivial().
         */
        public bool IsVarTrivial(ScriptCodeGen scg)
        {
            // reading or writing a property involves a function call however
            // so we need to check the triviality of the property functions
            if((getProp != null) && !getProp.IsFuncTrivial(scg))
                return false;
            if((setProp != null) && !setProp.IsFuncTrivial(scg))
                return false;

            // otherwise for variables it is a trivial access
            // and likewise for getting a delegate that points to a function
            return true;
        }

        /***************************\
         *  FUNCTION-only methods  *
        \***************************/

        private TokenName _funcNameSig;  // vars: null; funcs: function name including argumet signature, eg, "PrintStuff(list,string)"
        public TokenName funcNameSig
        {
            get
            {
                if(_funcNameSig == null)
                {
                    if(argDecl == null)
                        return null;
                    _funcNameSig = new TokenName(name, name.val + argDecl.GetArgSig());
                }
                return _funcNameSig;
            }
        }

        /**
         * @brief The bare function name, ie, without any signature info
         */
        public string GetSimpleName()
        {
            return name.val;
        }

        /**
         * @brief The function name as it appears in the object code,
         *        ie, script-defined type name if any,
         *        bare function name and argument signature, 
         *        eg, "MyClass.PrintStuff(string)"
         */
        public string GetObjCodeName()
        {
            string objCodeName = "";
            if(sdtClass != null)
            {
                objCodeName += sdtClass.longName.val + ".";
            }
            objCodeName += funcNameSig.val;
            return objCodeName;
        }

        /**
         * @brief Get delegate type.
         *        This is the function's script-visible type,
         *        It includes return type and all script-visible argument types.
         * @returns null for vars; else delegate type for funcs
         */
        public TokenTypeSDTypeDelegate GetDelType()
        {
            if(argDecl == null)
                return null;
            if(_type == null)
            {
                if(tokenScript == null)
                {
                    // used during startup to define inline function delegate types
                    _type = new TokenTypeSDTypeDelegate(retType, argDecl.types);
                }
                else
                {
                    // used for normal script processing
                    _type = new TokenTypeSDTypeDelegate(this, retType, argDecl.types, tokenScript);
                }
            }
            if(!(_type is TokenTypeSDTypeDelegate))
                return null;
            return (TokenTypeSDTypeDelegate)_type;
        }

        /**
         * @brief See if the function's code itself is trivial or not.
         *        If it contains any loops (calls to CheckRun()), it is not trivial.
         *        If it calls anything that is not trivial, it is not trivial.
         *        Otherwise it is trivial.
         */
        public bool IsFuncTrivial(ScriptCodeGen scg)
        {
             // If not really a function, assume it's a delegate.
             // And since we don't really know what functions it can point to, 
             // assume it can point to a non-trivial one.
            if(retType == null)
                return false;

             // All virtual functions are non-trivial because although a particular 
             // one might be trivial, it might be overidden with a non-trivial one.
            if((sdtFlags & (ScriptReduce.SDT_ABSTRACT | ScriptReduce.SDT_OVERRIDE |
                             ScriptReduce.SDT_VIRTUAL)) != 0)
            {
                return false;
            }

             // Check the triviality status of the function.
            switch(triviality)
            {
                 // Don't yet know if it is trivial.
                 // We know at this point it doesn't have any direct looping.
                 // But if it calls something that has loops, it isn't trivial.
                 // Otherwise it is trivial.
                case Triviality.unknown:
                    {
                         // Mark that we are analyzing this function now.  So if there are 
                         // any recursive call loops, that will show that the function is 
                         // non-trivial and the analysis will terminate at that point.
                        triviality = Triviality.analyzing;

                         // Check out everything else this function calls.  If any say they 
                         // aren't trivial, then we say this function isn't trivial.
                        foreach(TokenRValCall call in unknownTrivialityCalls)
                        {
                            if(!call.IsRValTrivial(scg, null))
                            {
                                triviality = Triviality.complex;
                                return false;
                            }
                        }

                         // All functions called by this function are trivial, and this 
                         // function's code doesn't have any loops, so we can mark this 
                         // function as being trivial.
                        triviality = Triviality.trivial;
                        return true;
                    }

                 // We already know that it is trivial.
                case Triviality.trivial:
                    {
                        return true;
                    }

                 // We either know it is complex or are trying to analyze it already.
                 // If we are already analyzing it, it means it has a recursive loop
                 // and we assume those are non-trivial.
                default:
                    return false;
            }
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            DebStringSDTFlags(sb);

            if(retType == null)
            {
                sb.Append(constant ? "constant" : type.ToString());
                sb.Append(' ');
                sb.Append(name.val);
                if(init != null)
                {
                    sb.Append(" = ");
                    init.DebString(sb);
                }
                sb.Append(';');
            }
            else
            {
                if(!(retType is TokenTypeVoid))
                {
                    sb.Append(retType.ToString());
                    sb.Append(' ');
                }
                string namestr = name.val;
                if(namestr == "$ctor")
                    namestr = "constructor";
                sb.Append(namestr);
                sb.Append(" (");
                for(int i = 0; i < argDecl.vars.Length; i++)
                {
                    if(i > 0)
                        sb.Append(", ");
                    sb.Append(argDecl.vars[i].type.ToString());
                    sb.Append(' ');
                    sb.Append(argDecl.vars[i].name.val);
                }
                sb.Append(')');
                if(body == null)
                    sb.Append(';');
                else
                {
                    sb.Append(' ');
                    body.DebString(sb);
                }
            }
        }

        // debugging
        // - used to output contents of a $globalvarinit(), $instfieldinit() or $statisfieldinit() function
        //   as a series of variable declaration statements with initial value assignments
        //   so we get the initial value assignments done in same order as specified in script
        public void DebStringInitFields(StringBuilder sb)
        {
            if((retType == null) || !(retType is TokenTypeVoid))
                throw new Exception("bad return type " + retType.GetType().Name);
            if(argDecl.vars.Length != 0)
                throw new Exception("has " + argDecl.vars.Length + " arg(s)");

            for(Token stmt = body.statements; stmt != null; stmt = stmt.nextToken)
            {
                 // Body of the function should all be arithmetic statements (not eg for loops, if statements etc).
                TokenRVal rval = ((TokenStmtRVal)stmt).rVal;

                 // And the opcode should be a simple assignment operator.
                TokenRValOpBin rvob = (TokenRValOpBin)rval;
                if(!(rvob.opcode is TokenKwAssign))
                    throw new Exception("bad op type " + rvob.opcode.GetType().Name);

                 // Get field or variable being assigned to.
                TokenDeclVar var = null;
                TokenRVal left = rvob.rValLeft;
                if(left is TokenLValIField)
                {
                    TokenLValIField ifield = (TokenLValIField)left;
                    TokenRValThis zhis = (TokenRValThis)ifield.baseRVal;
                    TokenDeclSDTypeClass sdt = zhis.sdtClass;
                    var = sdt.members.FindExact(ifield.fieldName.val, null);
                }
                if(left is TokenLValName)
                {
                    TokenLValName global = (TokenLValName)left;
                    var = global.stack.FindExact(global.name.val, null);
                }
                if(left is TokenLValSField)
                {
                    TokenLValSField sfield = (TokenLValSField)left;
                    TokenTypeSDTypeClass sdtc = (TokenTypeSDTypeClass)sfield.baseType;
                    TokenDeclSDTypeClass decl = sdtc.decl;
                    var = decl.members.FindExact(sfield.fieldName.val, null);
                }
                if(var == null)
                    throw new Exception("unknown var type " + left.GetType().Name);

                 // Output flags, type name and bare variable name.
                 // This should look like a declaration in the 'sb'
                 // as it is not enclosed in a function.
                var.DebStringSDTFlags(sb);
                var.type.DebString(sb);
                sb.Append(' ');
                sb.Append(var.name.val);

                 // Maybe it has a non-default initialization value.
                if((var.init != null) && !(var.init is TokenRValInitDef))
                {
                    sb.Append(" = ");
                    var.init.DebString(sb);
                }

                 // End of declaration statement.
                sb.Append(';');
            }
        }

        private void DebStringSDTFlags(StringBuilder sb)
        {
            if((sdtFlags & ScriptReduce.SDT_PRIVATE) != 0)
                sb.Append("private ");
            if((sdtFlags & ScriptReduce.SDT_PROTECTED) != 0)
                sb.Append("protected ");
            if((sdtFlags & ScriptReduce.SDT_PUBLIC) != 0)
                sb.Append("public ");
            if((sdtFlags & ScriptReduce.SDT_ABSTRACT) != 0)
                sb.Append("abstract ");
            if((sdtFlags & ScriptReduce.SDT_FINAL) != 0)
                sb.Append("final ");
            if((sdtFlags & ScriptReduce.SDT_NEW) != 0)
                sb.Append("new ");
            if((sdtFlags & ScriptReduce.SDT_OVERRIDE) != 0)
                sb.Append("override ");
            if((sdtFlags & ScriptReduce.SDT_STATIC) != 0)
                sb.Append("static ");
            if((sdtFlags & ScriptReduce.SDT_VIRTUAL) != 0)
                sb.Append("virtual ");
        }
    }

    /**
     * @brief Indicates an interface type.method that is implemented by the function
     */
    public class TokenIntfImpl: Token
    {
        public TokenTypeSDTypeInterface intfType;
        public TokenName methName;  // simple name, no arg signature

        public TokenIntfImpl(TokenTypeSDTypeInterface intfType, TokenName methName) : base(intfType)
        {
            this.intfType = intfType;
            this.methName = methName;
        }
    }

    /**
     * @brief any expression that can go on left side of an "="
     */
    public abstract class TokenLVal: TokenRVal
    {
        public TokenLVal(Token original) : base(original) { }
        public abstract override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig);
        public abstract override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig);
    }

    /**
     * @brief an element of an array is an L-value
     */
    public class TokenLValArEle: TokenLVal
    {
        public TokenRVal baseRVal;
        public TokenRVal subRVal;

        public TokenLValArEle(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenType baseType = baseRVal.GetRValType(scg, null);

             // Maybe referencing element of a fixed-dimension array.
            if((baseType is TokenTypeSDTypeClass) && (((TokenTypeSDTypeClass)baseType).decl.arrayOfType != null))
            {
                return ((TokenTypeSDTypeClass)baseType).decl.arrayOfType;
            }

             // Maybe referencing $idxprop property of script-defined class or interface.
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclSDTypeClass sdtDecl = ((TokenTypeSDTypeClass)baseType).decl;
                TokenDeclVar idxProp = scg.FindSingleMember(sdtDecl.members, new TokenName(this, "$idxprop"), null);
                if(idxProp != null)
                    return idxProp.type;
            }
            if(baseType is TokenTypeSDTypeInterface)
            {
                TokenDeclSDTypeInterface sdtDecl = ((TokenTypeSDTypeInterface)baseType).decl;
                TokenDeclVar idxProp = sdtDecl.FindIFaceMember(scg, new TokenName(this, "$idxprop"), null, out sdtDecl);
                if(idxProp != null)
                    return idxProp.type;
            }

             // Maybe referencing single character of a string.
            if((baseType is TokenTypeKey) || (baseType is TokenTypeStr))
            {
                return new TokenTypeChar(this);
            }

             // Assume XMR_Array element or extracting element from list.
            if((baseType is TokenTypeArray) || (baseType is TokenTypeList))
            {
                return new TokenTypeObject(this);
            }

            scg.ErrorMsg(this, "unknown array reference");
            return new TokenTypeVoid(this);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return baseRVal.IsRValTrivial(scg, null) && subRVal.IsRValTrivial(scg, null);
        }

        public override void DebString(StringBuilder sb)
        {
            baseRVal.DebString(sb);
            sb.Append('[');
            subRVal.DebString(sb);
            sb.Append(']');
        }
    }

    /**
     * @brief 'base.' being used to reference a field/method of the extended class.
     */
    public class TokenLValBaseField: TokenLVal
    {
        public TokenName fieldName;
        private TokenDeclSDTypeClass thisClass;

        public TokenLValBaseField(Token original, TokenName fieldName, TokenDeclSDTypeClass thisClass) : base(original)
        {
            this.fieldName = fieldName;
            this.thisClass = thisClass;
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenDeclVar var = scg.FindThisMember(thisClass.extends, fieldName, argsig);
            if(var != null)
                return var.type;
            scg.ErrorMsg(fieldName, "unknown member of " + thisClass.extends.ToString());
            return new TokenTypeVoid(fieldName);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenDeclVar var = scg.FindThisMember(thisClass.extends, fieldName, argsig);
            return (var != null) && var.IsVarTrivial(scg);
        }

        public override bool IsCallTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenDeclVar var = scg.FindThisMember(thisClass.extends, fieldName, argsig);
            return (var != null) && var.IsFuncTrivial(scg);
        }
    }

    /**
     * @brief a field within an struct is an L-value
     */
    public class TokenLValIField: TokenLVal
    {
        public TokenRVal baseRVal;
        public TokenName fieldName;

        public TokenLValIField(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenType baseType = baseRVal.GetRValType(scg, null);
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclVar var = scg.FindThisMember((TokenTypeSDTypeClass)baseType, fieldName, argsig);
                if(var != null)
                    return var.type;
            }
            if(baseType is TokenTypeSDTypeInterface)
            {
                TokenDeclSDTypeInterface baseIntfDecl = ((TokenTypeSDTypeInterface)baseType).decl;
                TokenDeclVar var = baseIntfDecl.FindIFaceMember(scg, fieldName, argsig, out baseIntfDecl);
                if(var != null)
                    return var.type;
            }
            if(baseType is TokenTypeArray)
            {
                return XMR_Array.GetRValType(fieldName);
            }
            if((baseType is TokenTypeRot) || (baseType is TokenTypeVec))
            {
                return new TokenTypeFloat(fieldName);
            }
            scg.ErrorMsg(fieldName, "unknown member of " + baseType.ToString());
            return new TokenTypeVoid(fieldName);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
             // If getting pointer to instance isn't trivial, then accessing the member isn't trivial either.
            if(!baseRVal.IsRValTrivial(scg, null))
                return false;

             // Accessing a member of a class depends on the member.
             // In the case of a method, this is accessing it as a delegate, not calling it, and 
             // argsig simply serves as selecting which of possibly overloaded methods to select.
             // The case of accessing a property, however, depends on the property implementation, 
             // as there could be looping inside the property code.
            TokenType baseType = baseRVal.GetRValType(scg, null);
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclVar var = scg.FindThisMember((TokenTypeSDTypeClass)baseType, fieldName, argsig);
                return (var != null) && var.IsVarTrivial(scg);
            }

             // Accessing the members of anything else (arrays, rotations, vectors) is always trivial.
            return true;
        }

        /**
         * @brief Check to see if the case of calling an instance method of some object is trivial.
         * @param scg = script making the call
         * @param argsig = argument types of the call (used to select among overloads)
         * @returns true iff we can tell at compile time that the call will always call a trivial method
         */
        public override bool IsCallTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
             // If getting pointer to instance isn't trivial, then calling the method isn't trivial either.
            if(!baseRVal.IsRValTrivial(scg, null))
                return false;

             // Calling a method of a class depends on the method.
            TokenType baseType = baseRVal.GetRValType(scg, null);
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclVar var = scg.FindThisMember((TokenTypeSDTypeClass)baseType, fieldName, argsig);
                return (var != null) && var.IsFuncTrivial(scg);
            }

             // Calling via a pointer to an interface instance is never trivial.
             // (It is really a pointer to an array of delegates).
             // We can't tell for this call site whether the actual method being called is trivial or not,
             // so we have to assume it isn't.
             // ??? We could theoretically check to see if *all* implementations of this method of 
             //     this interface are trivial, then we could conclude that this call is trivial.
            if(baseType is TokenTypeSDTypeInterface)
                return false;

             // Calling a method of anything else (arrays, rotations, vectors) is always trivial.
             // Even methods of delegates, such as ".GetArgTypes()" that we may someday do is trivial.
            return true;
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            baseRVal.DebString(sb);
            sb.Append('.');
            sb.Append(fieldName.val);
        }
    }

    /**
     * @brief a name is being used as an L-value
     */
    public class TokenLValName: TokenLVal
    {
        public TokenName name;
        public VarDict stack;

        public TokenLValName(TokenName name, VarDict stack) : base(name)
        {
             // Save name of variable/method/function/field.
            this.name = name;

             // Save where in the stack it can be looked up.
             // If the current stack is for locals, do not allow forward references.
             //     this allows idiocy like:
             //         list buttons = [ 1, 2, 3 ];
             //         x () {
             //             list buttons = llList2List (buttons, 0, 1);
             //             llOwnerSay (llList2CSV (buttons));
             //         }
             // If it is not locals, allow forward references.
             //     this allows function X() to call Y() and Y() to call X().
            this.stack = stack.FreezeLocals();
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenDeclVar var = scg.FindNamedVar(this, argsig);
            if(var != null)
                return var.type;
            scg.ErrorMsg(name, "undefined name " + name.val + ScriptCodeGen.ArgSigString(argsig));
            return new TokenTypeVoid(name);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenDeclVar var = scg.FindNamedVar(this, argsig);
            return (var != null) && var.IsVarTrivial(scg);
        }

        /**
         * @brief Check to see if the case of calling a global method is trivial.
         * @param scg = script making the call
         * @param argsig = argument types of the call (used to select among overloads)
         * @returns true iff we can tell at compile time that the call will always call a trivial method
         */
        public override bool IsCallTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenDeclVar var = scg.FindNamedVar(this, argsig);
            return (var != null) && var.IsFuncTrivial(scg);
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append(name.val);
        }
    }

    /**
     * @brief a static field within a struct is an L-value
     */
    public class TokenLValSField: TokenLVal
    {
        public TokenType baseType;
        public TokenName fieldName;

        public TokenLValSField(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclVar var = scg.FindThisMember((TokenTypeSDTypeClass)baseType, fieldName, argsig);
                if(var != null)
                    return var.type;
            }
            scg.ErrorMsg(fieldName, "unknown member of " + baseType.ToString());
            return new TokenTypeVoid(fieldName);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
             // Accessing a member of a class depends on the member.
             // In the case of a method, this is accessing it as a delegate, not calling it, and 
             // argsig simply serves as selecting which of possibly overloaded methods to select.
             // The case of accessing a property, however, depends on the property implementation, 
             // as there could be looping inside the property code.
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclVar var = scg.FindThisMember((TokenTypeSDTypeClass)baseType, fieldName, argsig);
                return (var != null) && var.IsVarTrivial(scg);
            }

             // Accessing the fields/methods/properties of anything else (arrays, rotations, vectors) is always trivial.
            return true;
        }

        /**
         * @brief Check to see if the case of calling a class' static method is trivial.
         * @param scg = script making the call
         * @param argsig = argument types of the call (used to select among overloads)
         * @returns true iff we can tell at compile time that the call will always call a trivial method
         */
        public override bool IsCallTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
             // Calling a static method of a class depends on the method.
            if(baseType is TokenTypeSDTypeClass)
            {
                TokenDeclVar var = scg.FindThisMember((TokenTypeSDTypeClass)baseType, fieldName, argsig);
                return (var != null) && var.IsFuncTrivial(scg);
            }

             // Calling a static method of anything else (arrays, rotations, vectors) is always trivial.
            return true;
        }

        public override void DebString(StringBuilder sb)
        {
            if(fieldName.val == "$new")
            {
                sb.Append("new ");
                baseType.DebString(sb);
            }
            else
            {
                baseType.DebString(sb);
                sb.Append('.');
                fieldName.DebString(sb);
            }
        }
    }

    /**
     * @brief any expression that can go on right side of "="
     */
    public delegate TokenRVal TCCLookup(TokenRVal rVal, ref bool didOne);
    public abstract class TokenRVal: Token
    {
        public TokenRVal(Token original) : base(original) { }

        /**
         * @brief Tell us the type of the expression.
         */
        public abstract TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig);

        /**
         * @brief Tell us if reading and writing the value is trivial.
         *
         * @param scg = script code generator of script making the access
         * @param argsig = argument types of the call (used to select among overloads)
         * @returns true: we can tell at compile time that reading/writing this location
         *                will always be trivial (no looping or CheckRun() calls possible).
         *         false: otherwise
         */
        public abstract bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig);

        /**
         * @brief Tell us if calling the method is trivial.
         *
         *        This is the default implementation that returns false.
         *        It is only used if the location is holding a delegate
         *        and the method that the delegate is pointing to is being
         *        called.  Since we can't tell if the actual runtime method
         *        is trivial or not, we assume it isn't.
         *
         *        For the more usual ways of calling functions, see the
         *        various overrides of IsCallTrivial().
         *
         * @param scg = script code generator of script making the call
         * @param argsig = argument types of the call (used to select among overloads)
         * @returns true: we can tell at compile time that this call will always
         *                be to a trivial function/method (no looping or CheckRun() 
         *                calls possible).
         *         false: otherwise
         */
        public virtual bool IsCallTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return false;
        }

        /**
         * @brief If the result of the expression is a constant,
         *        create a TokenRValConst equivalent, set didOne, and return that.
         *        Otherwise, just return the original without changing didOne.
         */
        public virtual TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            return lookup(this, ref didOne);
        }
    }

    /**
     * @brief a postfix operator and corresponding L-value
     */
    public class TokenRValAsnPost: TokenRVal
    {
        public TokenLVal lVal;
        public Token postfix;

        public TokenRValAsnPost(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return lVal.GetRValType(scg, argsig);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return lVal.IsRValTrivial(scg, null);
        }

        public override void DebString(StringBuilder sb)
        {
            lVal.DebString(sb);
            sb.Append(' ');
            postfix.DebString(sb);
        }
    }

    /**
     * @brief a prefix operator and corresponding L-value
     */
    public class TokenRValAsnPre: TokenRVal
    {
        public Token prefix;
        public TokenLVal lVal;

        public TokenRValAsnPre(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return lVal.GetRValType(scg, argsig);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return lVal.IsRValTrivial(scg, null);
        }

        public override void DebString(StringBuilder sb)
        {
            prefix.DebString(sb);
            sb.Append(' ');
            lVal.DebString(sb);
        }
    }

    /**
     * @brief calling a function or method, ie, may have side-effects
     */
    public class TokenRValCall: TokenRVal
    {

        public TokenRVal meth;  // points to the function to be called
                                // - might be a reference to a global function (TokenLValName)
                                // - or an instance method of a class (TokenLValIField)
                                // - or a static method of a class (TokenLValSField)
                                // - or a delegate stored in a variable (assumption for anything else)
        public TokenRVal args;  // null-terminated TokenRVal list
        public int nArgs;       // number of elements in args

        public TokenRValCall(Token original) : base(original) { }

        private TokenType[] myArgSig;

        /**
         * @brief The type of a call is the type of the return value.
         */
        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
             // Build type signature so we select correct overloaded function.
            if(myArgSig == null)
            {
                myArgSig = new TokenType[nArgs];
                int i = 0;
                for(Token t = args; t != null; t = t.nextToken)
                {
                    myArgSig[i++] = ((TokenRVal)t).GetRValType(scg, null);
                }
            }

             // Get the type of the method itself.  This should get us a delegate type.
            TokenType delType = meth.GetRValType(scg, myArgSig);
            if(!(delType is TokenTypeSDTypeDelegate))
            {
                scg.ErrorMsg(meth, "must be function or method");
                return new TokenTypeVoid(meth);
            }

             // Get the return type from the delegate type.
            return ((TokenTypeSDTypeDelegate)delType).decl.GetRetType();
        }

        /**
         * @brief See if the call to the function/method is trivial.
         *        It is trivial if all the argument computations are trivial and
         *        the function is not being called via virtual table or delegate
         *        and the function body is trivial.
         */
        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
             // Build type signature so we select correct overloaded function.
            if(myArgSig == null)
            {
                myArgSig = new TokenType[nArgs];
                int i = 0;
                for(Token t = args; t != null; t = t.nextToken)
                {
                    myArgSig[i++] = ((TokenRVal)t).GetRValType(scg, null);
                }
            }

             // Make sure all arguments can be computed trivially.
            for(Token t = args; t != null; t = t.nextToken)
            {
                if(!((TokenRVal)t).IsRValTrivial(scg, null))
                    return false;
            }

             // See if the function call itself and the function body are trivial.
            return meth.IsCallTrivial(scg, myArgSig);
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            meth.DebString(sb);
            sb.Append(" (");
            bool first = true;
            for(Token t = args; t != null; t = t.nextToken)
            {
                if(!first)
                    sb.Append(", ");
                t.DebString(sb);
                first = false;
            }
            sb.Append(")");
        }
    }

    /**
     * @brief encapsulates a typecast, ie, (type)
     */
    public class TokenRValCast: TokenRVal
    {
        public TokenType castTo;
        public TokenRVal rVal;

        public TokenRValCast(TokenType type, TokenRVal value) : base(type)
        {
            castTo = type;
            rVal = value;
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return castTo;
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            argsig = null;
            if(castTo is TokenTypeSDTypeDelegate)
            {
                argsig = ((TokenTypeSDTypeDelegate)castTo).decl.GetArgTypes();
            }
            return rVal.IsRValTrivial(scg, argsig);
        }

        /**
         * @brief If operand is constant, maybe we can say the whole thing is a constant.
         */
        public override TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            rVal = rVal.TryComputeConstant(lookup, ref didOne);
            if(rVal is TokenRValConst)
            {
                try
                {
                    object val = ((TokenRValConst)rVal).val;
                    object nval = null;
                    if(castTo is TokenTypeChar)
                    {
                        if(val is char)
                            return rVal;
                        if(val is int)
                            nval = (char)(int)val;
                    }
                    if(castTo is TokenTypeFloat)
                    {
                        if(val is double)
                            return rVal;
                        if(val is int)
                            nval = (double)(int)val;
                        if(val is string)
                            nval = new LSL_Float((string)val).value;
                    }
                    if(castTo is TokenTypeInt)
                    {
                        if(val is int)
                            return rVal;
                        if(val is char)
                            nval = (int)(char)val;
                        if(val is double)
                            nval = (int)(double)val;
                        if(val is string)
                            nval = new LSL_Integer((string)val).value;
                    }
                    if(castTo is TokenTypeRot)
                    {
                        if(val is LSL_Rotation)
                            return rVal;
                        if(val is string)
                            nval = new LSL_Rotation((string)val);
                    }
                    if((castTo is TokenTypeKey) || (castTo is TokenTypeStr))
                    {
                        if(val is string)
                            nval = val;  // in case of key/string conversion
                        if(val is char)
                            nval = TypeCast.CharToString((char)val);
                        if(val is double)
                            nval = TypeCast.FloatToString((double)val);
                        if(val is int)
                            nval = TypeCast.IntegerToString((int)val);
                        if(val is LSL_Rotation)
                            nval = TypeCast.RotationToString((LSL_Rotation)val);
                        if(val is LSL_Vector)
                            nval = TypeCast.VectorToString((LSL_Vector)val);
                    }
                    if(castTo is TokenTypeVec)
                    {
                        if(val is LSL_Vector)
                            return rVal;
                        if(val is string)
                            nval = new LSL_Vector((string)val);
                    }
                    if(nval != null)
                    {
                        TokenRVal rValConst = new TokenRValConst(castTo, nval);
                        didOne = true;
                        return rValConst;
                    }
                }
                catch
                {
                }
            }
            return this;
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append('(');
            castTo.DebString(sb);
            sb.Append(')');
            rVal.DebString(sb);
        }
    }

    /**
     * @brief Encapsulate a conditional expression:
     *        <condExpr> ? <trueExpr> : <falseExpr>
     */
    public class TokenRValCondExpr: TokenRVal
    {
        public TokenRVal condExpr;
        public TokenRVal trueExpr;
        public TokenRVal falseExpr;

        public TokenRValCondExpr(Token original) : base(original)
        {
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            TokenType trueType = trueExpr.GetRValType(scg, argsig);
            TokenType falseType = falseExpr.GetRValType(scg, argsig);
            if(trueType.ToString() != falseType.ToString())
            {
                scg.ErrorMsg(condExpr, "true & false expr types don't match");
            }
            return trueType;
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return condExpr.IsRValTrivial(scg, null) &&
                   trueExpr.IsRValTrivial(scg, argsig) &&
                  falseExpr.IsRValTrivial(scg, argsig);
        }

        /**
         * @brief If condition is constant, then the whole expression is constant
         *      iff the corresponding trueExpr or falseExpr is constant.
         */
        public override TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            TokenRVal rValCond = condExpr.TryComputeConstant(lookup, ref didOne);
            if(rValCond is TokenRValConst)
            {
                didOne = true;
                bool isTrue = ((TokenRValConst)rValCond).IsConstBoolTrue();
                return (isTrue ? trueExpr : falseExpr).TryComputeConstant(lookup, ref didOne);
            }
            return this;
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            condExpr.DebString(sb);
            sb.Append(" ? ");
            trueExpr.DebString(sb);
            sb.Append(" : ");
            falseExpr.DebString(sb);
        }
    }

    /**
     * @brief all constants supposed to end up here
     */
    public enum TokenRValConstType: byte { CHAR = 0, FLOAT = 1, INT = 2, KEY = 3, STRING = 4 };
    public class TokenRValConst: TokenRVal
    {
        public object val;  // always a system type (char, int, double, string), never LSL-wrapped
        public TokenRValConstType type;
        public TokenType tokType;

        public TokenRValConst(Token original, object value) : base(original)
        {
            val = value;

            TokenType tt = null;
            if(val is char)
            {
                type = TokenRValConstType.CHAR;
                tt = new TokenTypeChar(this);
            }
            else if(val is int)
            {
                type = TokenRValConstType.INT;
                tt = new TokenTypeInt(this);
            }
            else if(val is double)
            {
                type = TokenRValConstType.FLOAT;
                tt = new TokenTypeFloat(this);
            }
            else if(val is string)
            {
                type = TokenRValConstType.STRING;
                tt = new TokenTypeStr(this);
            }
            else
            {
                throw new Exception("invalid constant type " + val.GetType());
            }

            tokType = (original is TokenType) ? (TokenType)original : tt;
            if(tokType is TokenTypeKey)
            {
                type = TokenRValConstType.KEY;
            }
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return tokType;
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return true;
        }

        public CompValu GetCompValu()
        {
            switch(type)
            {
                case TokenRValConstType.CHAR:
                    {
                        return new CompValuChar(tokType, (char)val);
                    }
                case TokenRValConstType.FLOAT:
                    {
                        return new CompValuFloat(tokType, (double)val);
                    }
                case TokenRValConstType.INT:
                    {
                        return new CompValuInteger(tokType, (int)val);
                    }
                case TokenRValConstType.KEY:
                case TokenRValConstType.STRING:
                    {
                        return new CompValuString(tokType, (string)val);
                    }
                default:
                    throw new Exception("unknown type");
            }
        }

        public override TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            // gotta end somewhere
            return this;
        }

        public bool IsConstBoolTrue()
        {
            switch(type)
            {
                case TokenRValConstType.CHAR:
                    {
                        return (char)val != 0;
                    }
                case TokenRValConstType.FLOAT:
                    {
                        return (double)val != 0;
                    }
                case TokenRValConstType.INT:
                    {
                        return (int)val != 0;
                    }
                case TokenRValConstType.KEY:
                    {
                        return (string)val != "" && (string)val != ScriptBaseClass.NULL_KEY;
                    }
                case TokenRValConstType.STRING:
                    {
                        return (string)val != "";
                    }
                default:
                    throw new Exception("unknown type");
            }
        }

        public override void DebString(StringBuilder sb)
        {
            if(val is char)
            {
                sb.Append('\'');
                EscapeQuotes(sb, new string(new char[] { (char)val }));
                sb.Append('\'');
            }
            else if(val is int)
            {
                sb.Append((int)val);
            }
            else if(val is double)
            {
                string str = ((double)val).ToString();
                sb.Append(str);
                if((str.IndexOf('.') < 0) &&
                    (str.IndexOf('E') < 0) &&
                    (str.IndexOf('e') < 0))
                {
                    sb.Append(".0");
                }
            }
            else if(val is string)
            {
                sb.Append('"');
                EscapeQuotes(sb, (string)val);
                sb.Append('"');
            }
            else
            {
                throw new Exception("invalid constant type " + val.GetType());
            }
        }
        private static void EscapeQuotes(StringBuilder sb, string s)
        {
            foreach(char c in s)
            {
                switch(c)
                {
                    case '\n':
                        {
                            sb.Append("\\n");
                            break;
                        }
                    case '\t':
                        {
                            sb.Append("\\t");
                            break;
                        }
                    case '\\':
                        {
                            sb.Append("\\\\");
                            break;
                        }
                    case '\'':
                        {
                            sb.Append("\\'");
                            break;
                        }
                    case '\"':
                        {
                            sb.Append("\\\"");
                            break;
                        }
                    default:
                        {
                            sb.Append(c);
                            break;
                        }
                }
            }
        }
    }

    /**
     * @brief Default initialization value for the corresponding variable.
     */
    public class TokenRValInitDef: TokenRVal
    {
        public TokenType type;

        public static TokenRValInitDef Construct(TokenDeclVar tokenDeclVar)
        {
            TokenRValInitDef zhis = new TokenRValInitDef(tokenDeclVar);
            zhis.type = tokenDeclVar.type;
            return zhis;
        }
        private TokenRValInitDef(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return type;
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            // it's always just a constant so it's always very trivial
            return true;
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("<default ");
            sb.Append(type.ToString());
            sb.Append('>');
        }
    }

    /**
     * @brief encapsulation of <rval> is <typeexp>
     */
    public class TokenRValIsType: TokenRVal
    {
        public TokenRVal rValExp;
        public TokenTypeExp typeExp;

        public TokenRValIsType(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return new TokenTypeBool(rValExp);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return rValExp.IsRValTrivial(scg, argsig);
        }
    }

    /**
     * @brief an R-value enclosed in brackets is an LSLList
     */
    public class TokenRValList: TokenRVal
    {

        public TokenRVal rVal;  // null-terminated list of TokenRVal objects
        public int nItems;

        public TokenRValList(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return new TokenTypeList(rVal);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            for(Token t = rVal; t != null; t = t.nextToken)
            {
                if(!((TokenRVal)t).IsRValTrivial(scg, null))
                    return false;
            }
            return true;
        }

        public override void DebString(StringBuilder sb)
        {
            bool first = true;
            sb.Append('[');
            for(Token t = rVal; t != null; t = t.nextToken)
            {
                if(!first)
                    sb.Append(',');
                sb.Append(' ');
                t.DebString(sb);
                first = false;
            }
            sb.Append(" ]");
        }
    }

    /**
     * @brief encapsulates '$new' arraytype '{' ... '}'
     */
    public class TokenRValNewArIni: TokenRVal
    {
        public TokenType arrayType;
        public TokenList valueList;  // TokenList : a sub-list
                                     // TokenKwComma : a default value
                                     // TokenRVal    : init expression

        public TokenRValNewArIni(Token original) : base(original)
        {
            valueList = new TokenList(original);
        }

        // type of the expression = the array type allocated by $new()
        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return arrayType;
        }

        // The expression is trivial if all the initializers are trivial.
        // An array's constructor is always trivial (no CheckRun() calls).
        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return ListIsTrivial(scg, valueList);
        }
        private bool ListIsTrivial(ScriptCodeGen scg, TokenList valList)
        {
            foreach(Token val in valList.tl)
            {
                if(val is TokenRVal)
                {
                    if(!((TokenRVal)val).IsRValTrivial(scg, null))
                        return false;
                }
                if(val is TokenList)
                {
                    if(!ListIsTrivial(scg, (TokenList)val))
                        return false;
                }
            }
            return true;
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("new ");
            arrayType.DebString(sb);
            sb.Append(' ');
            valueList.DebString(sb);
        }
    }
    public class TokenList: Token
    {
        public List<Token> tl = new List<Token>();
        public TokenList(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append('{');
            bool first = true;
            foreach(Token t in tl)
            {
                if(!first)
                    sb.Append(", ");
                t.DebString(sb);
                first = false;
            }
            sb.Append('}');
        }
    }

    /**
     * @brief a binary operator and its two operands
     */
    public class TokenRValOpBin: TokenRVal
    {
        public TokenRVal rValLeft;
        public TokenKw opcode;
        public TokenRVal rValRight;

        public TokenRValOpBin(TokenRVal left, TokenKw op, TokenRVal right) : base(op)
        {
            rValLeft = left;
            opcode = op;
            rValRight = right;
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
             // Comparisons and the like always return bool.
            string opstr = opcode.ToString();
            if((opstr == "==") || (opstr == "!=") || (opstr == ">=") || (opstr == ">") ||
                (opstr == "&&") || (opstr == "||") || (opstr == "<=") || (opstr == "<") ||
                (opstr == "&&&") || (opstr == "|||"))
            {
                return new TokenTypeBool(opcode);
            }

             // Comma is always type of right-hand operand.
            if(opstr == ",")
                return rValRight.GetRValType(scg, argsig);

             // Assignments are always the type of the left-hand operand, 
             // including stuff like "+=".
            if(opstr.EndsWith("="))
            {
                return rValLeft.GetRValType(scg, argsig);
            }

             // string+something or something+string is always string.
             // except list+something or something+list is always a list.
            string lType = rValLeft.GetRValType(scg, argsig).ToString();
            string rType = rValRight.GetRValType(scg, argsig).ToString();
            if((opstr == "+") && ((lType == "list") || (rType == "list")))
            {
                return new TokenTypeList(opcode);
            }
            if((opstr == "+") && ((lType == "key") || (lType == "string") ||
                                   (rType == "key") || (rType == "string")))
            {
                return new TokenTypeStr(opcode);
            }

             // Everything else depends on both operands.
            string key = lType + opstr + rType;
            BinOpStr binOpStr;
            if(BinOpStr.defined.TryGetValue(key, out binOpStr))
            {
                return TokenType.FromSysType(opcode, binOpStr.outtype);
            }

            scg.ErrorMsg(opcode, "undefined operation " + key);
            return new TokenTypeVoid(opcode);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return rValLeft.IsRValTrivial(scg, null) && rValRight.IsRValTrivial(scg, null);
        }

        /**
         * @brief If both operands are constants, maybe we can say the whole thing is a constant.
         */
        public override TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            rValLeft = rValLeft.TryComputeConstant(lookup, ref didOne);
            rValRight = rValRight.TryComputeConstant(lookup, ref didOne);
            if((rValLeft is TokenRValConst) && (rValRight is TokenRValConst))
            {
//                try
                {
                    object val = opcode.binOpConst(((TokenRValConst)rValLeft).val,
                                                    ((TokenRValConst)rValRight).val);
                    TokenRVal rValConst = new TokenRValConst(opcode, val);
                    didOne = true;
                    return rValConst;
                }
//                catch
                {
                }
            }
            return this;
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            rValLeft.DebString(sb);
            sb.Append(' ');
            sb.Append(opcode.ToString());
            sb.Append(' ');
            rValRight.DebString(sb);
        }
    }

    /**
     * @brief an unary operator and its one operand
     */
    public class TokenRValOpUn: TokenRVal
    {
        public TokenKw opcode;
        public TokenRVal rVal;

        public TokenRValOpUn(TokenKw op, TokenRVal right) : base(op)
        {
            opcode = op;
            rVal = right;
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            if(opcode is TokenKwExclam)
                return new TokenTypeInt(opcode);
            return rVal.GetRValType(scg, null);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return rVal.IsRValTrivial(scg, null);
        }

        /**
         * @brief If operand is constant, maybe we can say the whole thing is a constant.
         */
        public override TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            rVal = rVal.TryComputeConstant(lookup, ref didOne);
            if(rVal is TokenRValConst)
            {
                try
                {
                    object val = opcode.unOpConst(((TokenRValConst)rVal).val);
                    TokenRVal rValConst = new TokenRValConst(opcode, val);
                    didOne = true;
                    return rValConst;
                }
                catch
                {
                }
            }
            return this;
        }

        /**
         * @brief Serialization/Deserialization.
         */
        public TokenRValOpUn(Token original) : base(original) { }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append(opcode.ToString());
            rVal.DebString(sb);
        }
    }

    /**
     * @brief an R-value enclosed in parentheses
     */
    public class TokenRValParen: TokenRVal
    {

        public TokenRVal rVal;

        public TokenRValParen(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            // pass argsig through in this simple case, ie, let 
            // them do something like (llOwnerSay)("blabla...");
            return rVal.GetRValType(scg, argsig);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            // pass argsig through in this simple case, ie, let 
            // them do something like (llOwnerSay)("blabla...");
            return rVal.IsRValTrivial(scg, argsig);
        }

        /**
         * @brief If operand is constant, we can say the whole thing is a constant.
         */
        public override TokenRVal TryComputeConstant(TCCLookup lookup, ref bool didOne)
        {
            rVal = rVal.TryComputeConstant(lookup, ref didOne);
            if(rVal is TokenRValConst)
            {
                didOne = true;
                return rVal;
            }
            return this;
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append('(');
            rVal.DebString(sb);
            sb.Append(')');
        }
    }

    public class TokenRValRot: TokenRVal
    {

        public TokenRVal xRVal;
        public TokenRVal yRVal;
        public TokenRVal zRVal;
        public TokenRVal wRVal;

        public TokenRValRot(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return new TokenTypeRot(xRVal);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return xRVal.IsRValTrivial(scg, null) &&
                   yRVal.IsRValTrivial(scg, null) &&
                   zRVal.IsRValTrivial(scg, null) &&
                   wRVal.IsRValTrivial(scg, null);
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append('<');
            xRVal.DebString(sb);
            sb.Append(',');
            yRVal.DebString(sb);
            sb.Append(',');
            zRVal.DebString(sb);
            sb.Append(',');
            wRVal.DebString(sb);
            sb.Append('>');
        }
    }

    /**
     * @brief 'this' is being used as an rval inside an instance method.
     */
    public class TokenRValThis: TokenRVal
    {
        public Token original;
        public TokenDeclSDTypeClass sdtClass;

        public TokenRValThis(Token original, TokenDeclSDTypeClass sdtClass) : base(original)
        {
            this.original = original;
            this.sdtClass = sdtClass;
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return sdtClass.MakeRefToken(original);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return true;  // ldarg.0/starg.0 can't possibly loop
        }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append("this");
        }
    }

    /**
     * @brief the 'undef' keyword is being used as a value in an expression.
     *        It is the null object pointer and has type TokenTypeUndef.
     */
    public class TokenRValUndef: TokenRVal
    {
        Token original;

        public TokenRValUndef(Token original) : base(original)
        {
            this.original = original;
        }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return new TokenTypeUndef(original);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return true;
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("undef");
        }
    }

    /**
     * @brief put 3 RVals together as a Vector value.
     */
    public class TokenRValVec: TokenRVal
    {

        public TokenRVal xRVal;
        public TokenRVal yRVal;
        public TokenRVal zRVal;

        public TokenRValVec(Token original) : base(original) { }

        public override TokenType GetRValType(ScriptCodeGen scg, TokenType[] argsig)
        {
            return new TokenTypeVec(xRVal);
        }

        public override bool IsRValTrivial(ScriptCodeGen scg, TokenType[] argsig)
        {
            return xRVal.IsRValTrivial(scg, null) &&
                   yRVal.IsRValTrivial(scg, null) &&
                   zRVal.IsRValTrivial(scg, null);
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append('<');
            xRVal.DebString(sb);
            sb.Append(',');
            yRVal.DebString(sb);
            sb.Append(',');
            zRVal.DebString(sb);
            sb.Append('>');
        }
    }

    /**
     * @brief encapsulates the whole script in a single token
     */
    public class TokenScript: Token
    {
        public int expiryDays = Int32.MaxValue;
        public TokenDeclState defaultState;
        public Dictionary<string, TokenDeclState> states = new Dictionary<string, TokenDeclState>();
        public VarDict variablesStack = new VarDict(false);  // initial one is used for global functions and variables
        public TokenDeclVar globalVarInit;                    // $globalvarinit function
                                                              // - performs explicit global var and static field inits

        private Dictionary<string, TokenDeclSDType> sdSrcTypes = new Dictionary<string, TokenDeclSDType>();
        private bool sdSrcTypesSealed = false;

        public TokenScript(Token original) : base(original) { }

        /*
         * Handle variable definition stack.
         * Generally a '{' pushes a new frame and a '}' pops the frame.
         * Function parameters are pushed in an additional frame (just outside the body's { ... } block)
         */
        public void PushVarFrame(bool locals)
        {
            PushVarFrame(new VarDict(locals));
        }
        public void PushVarFrame(VarDict newFrame)
        {
            newFrame.outerVarDict = variablesStack;
            variablesStack = newFrame;
        }
        public void PopVarFrame()
        {
            variablesStack = variablesStack.outerVarDict;
        }
        public bool AddVarEntry(TokenDeclVar var)
        {
            return variablesStack.AddEntry(var);
        }

        /*
         * Handle list of script-defined types.
         */
        public void sdSrcTypesSeal()
        {
            sdSrcTypesSealed = true;
        }
        public bool sdSrcTypesContainsKey(string key)
        {
            return sdSrcTypes.ContainsKey(key);
        }
        public bool sdSrcTypesTryGetValue(string key, out TokenDeclSDType value)
        {
            return sdSrcTypes.TryGetValue(key, out value);
        }
        public void sdSrcTypesAdd(string key, TokenDeclSDType value)
        {
            if(sdSrcTypesSealed)
                throw new Exception("sdSrcTypes is sealed");
            value.sdTypeIndex = sdSrcTypes.Count;
            sdSrcTypes.Add(key, value);
        }
        public void sdSrcTypesRep(string key, TokenDeclSDType value)
        {
            if(sdSrcTypesSealed)
                throw new Exception("sdSrcTypes is sealed");
            value.sdTypeIndex = sdSrcTypes[key].sdTypeIndex;
            sdSrcTypes[key] = value;
        }
        public void sdSrcTypesReplace(string key, TokenDeclSDType value)
        {
            if(sdSrcTypesSealed)
                throw new Exception("sdSrcTypes is sealed");
            sdSrcTypes[key] = value;
        }
        public Dictionary<string, TokenDeclSDType>.ValueCollection sdSrcTypesValues
        {
            get
            {
                return sdSrcTypes.Values;
            }
        }
        public int sdSrcTypesCount
        {
            get
            {
                return sdSrcTypes.Count;
            }
        }

        /**
         * @brief Debug output.
         */
        public override void DebString(StringBuilder sb)
        {
            /*
             * Script-defined types.
             */
            foreach(TokenDeclSDType srcType in sdSrcTypes.Values)
            {
                srcType.DebString(sb);
            }

            /*
             * Global constants.
             * Variables are handled by outputting the $globalvarinit function.
             */
            foreach(TokenDeclVar var in variablesStack)
            {
                if(var.constant)
                {
                    var.DebString(sb);
                }
            }

            /*
             * Global functions.
             */
            foreach(TokenDeclVar var in variablesStack)
            {
                if(var == globalVarInit)
                {
                    var.DebStringInitFields(sb);
                }
                else if(var.retType != null)
                {
                    var.DebString(sb);
                }
            }

            /*
             * States and their event handler functions.
             */
            defaultState.DebString(sb);
            foreach(TokenDeclState st in states.Values)
            {
                st.DebString(sb);
            }
        }
    }

    /**
     * @brief state body declaration
     */
    public class TokenStateBody: Token
    {

        public TokenDeclVar eventFuncs;

        public int index = -1;  // (codegen) row in ScriptHandlerEventTable (0=default)

        public TokenStateBody(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append(" { ");
            for(Token t = eventFuncs; t != null; t = t.nextToken)
            {
                t.DebString(sb);
            }
            sb.Append(" } ");
        }
    }

    /**
     * @brief a single statement, such as ending on a semicolon or enclosed in braces
     * TokenStmt includes the terminating semicolon or the enclosing braces
     * Also includes @label; for jump targets.
     * Also includes stray ; null statements.
     * Also includes local variable declarations with or without initialization value.
     */
    public class TokenStmt: Token
    {
        public TokenStmt(Token original) : base(original) { }
    }

    /**
     * @brief a group of statements enclosed in braces
     */
    public class TokenStmtBlock: TokenStmt
    {
        public Token statements;               // null-terminated list of statements, can also have TokenDeclVar's in here
        public TokenStmtBlock outerStmtBlock;  // next outer stmtBlock or null if top-level, ie, function definition
        public TokenDeclVar function;          // function it is part of
        public bool isTry;                     // true iff it's a try statement block
        public bool isCatch;                   // true iff it's a catch statement block
        public bool isFinally;                 // true iff it's a finally statement block
        public TokenStmtTry tryStmt;           // set iff isTry|isCatch|isFinally is set

        public TokenStmtBlock(Token original) : base(original) { }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            sb.Append("{ ");
            for(Token stmt = statements; stmt != null; stmt = stmt.nextToken)
            {
                stmt.DebString(sb);
            }
            sb.Append("} ");
        }
    }

    /**
     * @brief definition of branch target name
     */
    public class TokenStmtLabel: TokenStmt
    {
        public TokenName name;        // the label's name
        public TokenStmtBlock block;  // which block it is defined in
        public bool hasBkwdRefs = false;

        public bool labelTagged;      // code gen: location of label
        public ScriptMyLabel labelStruct;

        public TokenStmtLabel(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append('@');
            name.DebString(sb);
            sb.Append(';');
        }
    }

    /**
     * @brief those types of RVals with a semi-colon on the end
     *        that are allowed to stand alone as statements
     */
    public class TokenStmtRVal: TokenStmt
    {
        public TokenRVal rVal;

        public TokenStmtRVal(Token original) : base(original) { }

        // debugging
        public override void DebString(StringBuilder sb)
        {
            rVal.DebString(sb);
            sb.Append("; ");
        }
    }

    public class TokenStmtBreak: TokenStmt
    {
        public TokenStmtBreak(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("break;");
        }
    }

    public class TokenStmtCont: TokenStmt
    {
        public TokenStmtCont(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("continue;");
        }
    }

    /**
     * @brief "do" statement
     */
    public class TokenStmtDo: TokenStmt
    {

        public TokenStmt bodyStmt;
        public TokenRValParen testRVal;

        public TokenStmtDo(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("do ");
            bodyStmt.DebString(sb);
            sb.Append(" while ");
            testRVal.DebString(sb);
            sb.Append(';');
        }
    }

    /**
     * @brief "for" statement
     */
    public class TokenStmtFor: TokenStmt
    {

        public TokenStmt initStmt;  // there is always an init statement, though it may be a null statement
        public TokenRVal testRVal;  // there may or may not be a test (null if not)
        public TokenRVal incrRVal;  // there may or may not be an increment (null if not)
        public TokenStmt bodyStmt;  // there is always a body statement, though it may be a null statement

        public TokenStmtFor(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("for (");
            if(initStmt != null)
                initStmt.DebString(sb);
            else
                sb.Append(';');
            if(testRVal != null)
                testRVal.DebString(sb);
            sb.Append(';');
            if(incrRVal != null)
                incrRVal.DebString(sb);
            sb.Append(") ");
            bodyStmt.DebString(sb);
        }
    }

    /**
     * @brief "foreach" statement
     */
    public class TokenStmtForEach: TokenStmt
    {

        public TokenLVal keyLVal;
        public TokenLVal valLVal;
        public TokenRVal arrayRVal;
        public TokenStmt bodyStmt;  // there is always a body statement, though it may be a null statement

        public TokenStmtForEach(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("foreach (");
            if(keyLVal != null)
                keyLVal.DebString(sb);
            sb.Append(',');
            if(valLVal != null)
                valLVal.DebString(sb);
            sb.Append(" in ");
            arrayRVal.DebString(sb);
            sb.Append(')');
            bodyStmt.DebString(sb);
        }
    }

    public class TokenStmtIf: TokenStmt
    {

        public TokenRValParen testRVal;
        public TokenStmt trueStmt;
        public TokenStmt elseStmt;

        public TokenStmtIf(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("if ");
            testRVal.DebString(sb);
            sb.Append(" ");
            trueStmt.DebString(sb);
            if(elseStmt != null)
            {
                sb.Append(" else ");
                elseStmt.DebString(sb);
            }
        }
    }

    public class TokenStmtJump: TokenStmt
    {

        public TokenName label;

        public TokenStmtJump(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("jump ");
            label.DebString(sb);
            sb.Append(';');
        }
    }

    public class TokenStmtNull: TokenStmt
    {
        public TokenStmtNull(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append(';');
        }
    }

    public class TokenStmtRet: TokenStmt
    {
        public TokenRVal rVal;  // null if void

        public TokenStmtRet(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("return");
            if(rVal != null)
            {
                sb.Append(' ');
                rVal.DebString(sb);
            }
            sb.Append(';');
        }
    }

    /**
     * @brief statement that changes the current state.
     */
    public class TokenStmtState: TokenStmt
    {
        public TokenName state;  // null for default

        public TokenStmtState(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("state ");
            sb.Append((state == null) ? "default" : state.val);
            sb.Append(';');
        }
    }

    /**
     * @brief Encapsulates a whole switch statement including the body and all cases.
     */
    public class TokenStmtSwitch: TokenStmt
    {
        public TokenRValParen testRVal;          // the integer index expression
        public TokenSwitchCase cases = null;     // list of all cases, linked by .nextCase
        public TokenSwitchCase lastCase = null;  // used during reduce to point to last in 'cases' list

        public TokenStmtSwitch(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("switch ");
            testRVal.DebString(sb);
            sb.Append('{');
            for(TokenSwitchCase kase = cases; kase != null; kase = kase.nextCase)
            {
                kase.DebString(sb);
            }
            sb.Append('}');
        }
    }

    /**
     * @brief Encapsulates a case/default clause from a switch statement including the
     *        two values and the corresponding body statements.
     */
    public class TokenSwitchCase: Token
    {
        public TokenSwitchCase nextCase;  // next case in source-code order
        public TokenRVal rVal1;           // null means 'default', else 'case'
        public TokenRVal rVal2;           // null means 'case expr:', else 'case expr ... expr:'
        public TokenStmt stmts;           // statements associated with the case
        public TokenStmt lastStmt;        // used during reduce for building statement list

        public int val1;                        // codegen: value of rVal1 here
        public int val2;                        // codegen: value of rVal2 here
        public ScriptMyLabel label;             // codegen: target label here
        public TokenSwitchCase nextSortedCase;  // codegen: next case in ascending val order

        public string str1;
        public string str2;
        public TokenSwitchCase lowerCase;
        public TokenSwitchCase higherCase;

        public TokenSwitchCase(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            if(rVal1 == null)
            {
                sb.Append("default: ");
            }
            else
            {
                sb.Append("case ");
                rVal1.DebString(sb);
                if(rVal2 != null)
                {
                    sb.Append(" ... ");
                    rVal2.DebString(sb);
                }
                sb.Append(": ");
            }
            for(Token t = stmts; t != null; t = t.nextToken)
            {
                t.DebString(sb);
            }
        }
    }

    public class TokenStmtThrow: TokenStmt
    {
        public TokenRVal rVal;  // null if rethrow style

        public TokenStmtThrow(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("throw ");
            rVal.DebString(sb);
            sb.Append(';');
        }
    }

    /**
     * @brief Encapsulates related try, catch and finally statements.
     */
    public class TokenStmtTry: TokenStmt
    {
        public TokenStmtBlock tryStmt;
        public TokenDeclVar catchVar;       // null iff catchStmt is null
        public TokenStmtBlock catchStmt;    // can be null
        public TokenStmtBlock finallyStmt;  // can be null
        public Dictionary<string, IntermediateLeave> iLeaves = new Dictionary<string, IntermediateLeave>();

        public TokenStmtTry(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("try ");
            tryStmt.DebString(sb);
            if(catchStmt != null)
            {
                sb.Append("catch (");
                sb.Append(catchVar.type.ToString());
                sb.Append(' ');
                sb.Append(catchVar.name.val);
                sb.Append(") ");
                catchStmt.DebString(sb);
            }
            if(finallyStmt != null)
            {
                sb.Append("finally ");
                finallyStmt.DebString(sb);
            }
        }
    }

    public class IntermediateLeave
    {
        public ScriptMyLabel jumpIntoLabel;
        public ScriptMyLabel jumpAwayLabel;
    }

    public class TokenStmtVarIniDef: TokenStmt
    {
        public TokenLVal var;
        public TokenStmtVarIniDef(Token original) : base(original) { }
    }

    public class TokenStmtWhile: TokenStmt
    {
        public TokenRValParen testRVal;
        public TokenStmt bodyStmt;

        public TokenStmtWhile(Token original) : base(original) { }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("while ");
            testRVal.DebString(sb);
            sb.Append(' ');
            bodyStmt.DebString(sb);
        }
    }

    /**
     * @brief type expressions (right-hand of 'is' keyword).
     */
    public class TokenTypeExp: Token
    {
        public TokenTypeExp(Token original) : base(original) { }
    }

    public class TokenTypeExpBinOp: TokenTypeExp
    {
        public TokenTypeExp leftOp;
        public Token binOp;
        public TokenTypeExp rightOp;

        public TokenTypeExpBinOp(Token original) : base(original) { }
    }

    public class TokenTypeExpNot: TokenTypeExp
    {
        public TokenTypeExp typeExp;

        public TokenTypeExpNot(Token original) : base(original) { }
    }

    public class TokenTypeExpPar: TokenTypeExp
    {
        public TokenTypeExp typeExp;

        public TokenTypeExpPar(Token original) : base(original) { }
    }

    public class TokenTypeExpType: TokenTypeExp
    {
        public TokenType typeToken;

        public TokenTypeExpType(Token original) : base(original) { }
    }

    public class TokenTypeExpUndef: TokenTypeExp
    {
        public TokenTypeExpUndef(Token original) : base(original) { }
    }
}
