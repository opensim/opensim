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
 * @brief Parse raw source file string into token list.
 *
 * Usage:
 *
 *    emsg = some function to output error messages to
 *    source = string containing entire source file
 *
 *    TokenBegin tokenBegin = TokenBegin.Construct (emsg, source);
 *
 *    tokenBegin = null: tokenizing error
 *                 else: first (dummy) token in file
 *                       the rest are chained by nextToken,prevToken
 *                       final token is always a (dummy) TokenEnd
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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

    public delegate void TokenErrorMessage(Token token, string message);

    /**
     * @brief base class for all tokens
     */
    public class Token
    {
        public static readonly int MAX_NAME_LEN = 255;
        public static readonly int MAX_STRING_LEN = 4096;

        public Token nextToken;
        public Token prevToken;
        public bool nr2l;

        // used for error message printing
        public TokenErrorMessage emsg;
        public string file = "";
        public int line;
        public int posn;
        public Token copiedFrom;

        /**
         * @brief construct a token coming directly from a source file
         * @param emsg = object that error messages get sent to
         * @param file = source file name (or "" if none)
         * @param line = source file line number
         * @param posn = token's position within that source line
         */
        public Token(TokenErrorMessage emsg, string file, int line, int posn)
        {
            this.emsg = emsg;
            this.file = file;
            this.line = line;
            this.posn = posn;
        }

        /**
         * @brief construct a token with same error message parameters
         * @param original = original token to create from
         */
        public Token(Token original)
        {
            if(original != null)
            {
                this.emsg = original.emsg;
                this.file = original.file;
                this.line = original.line;
                this.posn = original.posn;
                this.nr2l = original.nr2l;
            }
        }

        /**
         * @brief output an error message associated with this token
         *        sends the message to the token's error object
         * @param message = error message string
         */
        public void ErrorMsg(string message)
        {
            if(emsg != null)
            {
                emsg(this, message);
            }
        }

        /*
         * Generate a unique string (for use in CIL label names, etc)
         */
        public string Unique
        {
            get
            {
                return file + "_" + line + "_" + posn;
            }
        }

        /*
         * Generate source location string (for use in error messages)
         */
        public string SrcLoc
        {
            get
            {
                string loc = file + "(" + line + "," + posn + ")";
                if(copiedFrom == null)
                    return loc;
                string fromLoc = copiedFrom.SrcLoc;
                if(fromLoc.StartsWith(loc))
                    return fromLoc;
                return loc + ":" + fromLoc;
            }
        }

        /*
         * Used in generic instantiation to copy token.
         * Only valid for parsing tokens, not reduction tokens 
         * because it is a shallow copy.
         */
        public Token CopyToken(Token src)
        {
            Token t = (Token)this.MemberwiseClone();
            t.file = src.file;
            t.line = src.line;
            t.posn = src.posn;
            t.copiedFrom = this;
            return t;
        }

        /*
         * Generate debugging string - should look like source code.
         */
        public virtual void DebString(StringBuilder sb)
        {
            sb.Append(this.ToString());
        }
    }


    /**
     * @brief token that begins a source file
     *        Along with TokenEnd, it keeps insertion/removal of intermediate tokens
     *        simple as the intermediate tokens always have non-null nextToken,prevToken.
     */
    public class TokenBegin: Token
    {
        private class Options
        {
            public bool arrays;         // has seen 'XMROption arrays;'
            public bool advFlowCtl;     // has seen 'XMROption advFlowCtl;'
            public bool tryCatch;       // has seen 'XMROption tryCatch;'
            public bool objects;        // has seen 'XMROption objects;'
            public bool chars;          // has seen 'XMROption chars;'
            public bool noRightToLeft;  // has seen 'XMROption noRightToLeft;'
            public bool dollarsigns;    // has seen 'XMROption dollarsigns;'
        }

        private bool youveAnError;      // there was some error tokenizing
        private int bolIdx;             // index in 'source' at begining of current line
        private int lineNo;             // current line in source file, starting at 0
        private string filNam;          // current source file name
        private string source;          // the whole script source code
        private Token lastToken;        // last token created so far
        private string cameFrom;        // where the source came from
        private TextWriter saveSource;  // save copy of source here (or null)
        private Options options = new Options();

        /**
         * @brief convert a source file in the form of a string
         *        to a list of raw tokens
         * @param cameFrom = where the source came from
         * @param emsg     = where to output messages to
         * @param source   = whole source file contents
         * @returns null: conversion error, message already output
         *          else: list of tokens, starting with TokenBegin, ending with TokenEnd.
         */
        public static TokenBegin Construct(string cameFrom, TextWriter saveSource, TokenErrorMessage emsg, string source, out string sourceHash)
        {
            sourceHash = null;

             // Now do the tokenization.
            TokenBegin tokenBegin = new TokenBegin(emsg, "", 0, 0);
            tokenBegin.cameFrom = cameFrom;
            tokenBegin.saveSource = saveSource;
            tokenBegin.lastToken = tokenBegin;
            tokenBegin.source = source;
            tokenBegin.filNam = cameFrom;
            if(saveSource != null)
                saveSource.WriteLine(source);
            tokenBegin.Tokenize();
            if(tokenBegin.youveAnError)
                return null;
            tokenBegin.AppendToken(new TokenEnd(emsg, tokenBegin.filNam, ++tokenBegin.lineNo, 0));

            /*
             * Return source hash so caller can know if source changes.
             */
            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] hashBytes = md5.ComputeHash(new TokenStream(tokenBegin));
            int hashBytesLen = hashBytes.Length;
            StringBuilder sb = new StringBuilder(hashBytesLen * 2);
            for(int i = 0; i < hashBytesLen; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            sourceHash = sb.ToString();
            if(saveSource != null)
            {
                saveSource.WriteLine("");
                saveSource.WriteLine("********************************************************************************");
                saveSource.WriteLine("****  source hash: " + sourceHash);
                saveSource.WriteLine("********************************************************************************");
            }

            return tokenBegin;
        }

        private TokenBegin(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }

        /*
         * Stream consisting of all the tokens.
         * Null delimeters between the tokens.
         * Used for creating the source hash.
         */
        private class TokenStream: Stream
        {
            private Token curTok;
            private bool delim;
            private byte[] curBuf;
            private int curOfs;
            private int curLen;

            public TokenStream(Token t)
            {
                curTok = t;
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }
            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }
            public override bool CanWrite
            {
                get
                {
                    return false;
                }
            }
            public override long Length
            {
                get
                {
                    return 0;
                }
            }
            public override long Position
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
            }
            public override void Flush()
            {
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                return 0;
            }
            public override void SetLength(long value)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int len, total;
                for(total = 0; total < count; total += len)
                {
                    while((len = curLen - curOfs) <= 0)
                    {
                        if(curTok is TokenEnd)
                            goto done;
                        curTok = curTok.nextToken;
                        if(curTok is TokenEnd)
                            goto done;
                        curBuf = System.Text.Encoding.UTF8.GetBytes(curTok.ToString());
                        curOfs = 0;
                        curLen = curBuf.Length;
                        delim = true;
                    }
                    if(delim)
                    {
                        buffer[offset + total] = 0;
                        delim = false;
                        len = 1;
                    }
                    else
                    {
                        if(len > count - total)
                            len = count - total;
                        Array.Copy(curBuf, curOfs, buffer, offset + total, len);
                        curOfs += len;
                    }
                }
                done:
                return total;
            }
        }

        /*
         * Produces raw token stream: names, numbers, strings, keywords/delimeters.
         * @param this.source = whole source file in one string
         * @returns this.nextToken = filled in with tokens
         *          this.youveAnError = true: some tokenizing error
         *                             false: successful
         */
        private void Tokenize()
        {
            bolIdx = 0;
            lineNo = 0;
            for(int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if(c == '\n')
                {

                     // Increment source line number and set char index of beg of next line.
                    lineNo++;
                    bolIdx = i + 1;

                     // Check for '#' lineno filename newline
                     // lineno is line number of next line in file
                     // If found, save values and remove tokens from stream
                    if((lastToken is TokenStr) &&
                        (lastToken.prevToken is TokenInt) &&
                        (lastToken.prevToken.prevToken is TokenKwHash))
                    {
                        filNam = ((TokenStr)lastToken).val;
                        lineNo = ((TokenInt)lastToken.prevToken).val;
                        lastToken = lastToken.prevToken.prevToken.prevToken;
                        lastToken.nextToken = null;
                    }
                    continue;
                }

                 // Skip over whitespace.
                if(c <= ' ')
                    continue;

                 // Skip over comments.
                if((i + 2 <= source.Length) && source.Substring(i, 2).Equals("//"))
                {
                    while((i < source.Length) && (source[i] != '\n'))
                        i++;
                    lineNo++;
                    bolIdx = i + 1;
                    continue;
                }
                if((i + 2 <= source.Length) && (source.Substring(i, 2).Equals("/*")))
                {
                    i += 2;
                    while((i + 1 < source.Length) && (((c = source[i]) != '*') || (source[i + 1] != '/')))
                    {
                        if(c == '\n')
                        {
                            lineNo++;
                            bolIdx = i + 1;
                        }
                        i++;
                    }
                    i++;
                    continue;
                }

                 // Check for numbers.
                if((c >= '0') && (c <= '9'))
                {
                    int j = TryParseFloat(i);
                    if(j == 0)
                        j = TryParseInt(i);
                    i = --j;
                    continue;
                }
                if((c == '.') && (i + 1 < source.Length) && (source[i + 1] >= '0') && (source[i + 1] <= '9'))
                {
                    int j = TryParseFloat(i);
                    if(j > 0)
                        i = --j;
                    continue;
                }

                 // Check for quoted strings.
                if(c == '"')
                {
                    StringBuilder sb = new StringBuilder();
                    bool backslash;
                    int j;

                    backslash = false;
                    for(j = i; ++j < source.Length;)
                    {
                        c = source[j];
                        if(c == '\\' && !backslash)
                        {
                            backslash = true;
                            continue;
                        }
                        if(c == '\n')
                        {
                            lineNo++;
                            bolIdx = j + 1;
                        }
                        else
                        {
                            if(!backslash && (c == '"'))
                                break;
                            if(backslash && (c == 'n'))
                                c = '\n';
                            if(backslash && (c == 't'))
                            {
                                sb.Append("   ");
                                c = ' ';
                            }
                        }
                        backslash = false;
                        sb.Append(c);
                    }
                    if(j - i > MAX_STRING_LEN)
                    {
                        TokenError(i, "string too long, max " + MAX_STRING_LEN);
                    }
                    else
                    {
                        AppendToken(new TokenStr(emsg, filNam, lineNo, i - bolIdx, sb.ToString()));
                    }
                    i = j;
                    continue;
                }

                 // Check for quoted characters.
                if(c == '\'')
                {
                    char cb = (char)0;
                    bool backslash, overflow, underflow;
                    int j;

                    backslash = false;
                    overflow = false;
                    underflow = true;
                    for(j = i; ++j < source.Length;)
                    {
                        c = source[j];
                        if(c == '\\' && !backslash)
                        {
                            backslash = true;
                            continue;
                        }
                        if(c == '\n')
                        {
                            lineNo++;
                            bolIdx = j + 1;
                        }
                        else
                        {
                            if(!backslash && (c == '\''))
                                break;
                            if(backslash && (c == 'n'))
                                c = '\n';
                            if(backslash && (c == 't'))
                                c = '\t';
                        }
                        backslash = false;
                        overflow = !underflow;
                        underflow = false;
                        cb = c;
                    }
                    if(underflow || overflow)
                    {
                        TokenError(i, "character must be exactly one character");
                    }
                    else
                    {
                        AppendToken(new TokenChar(emsg, filNam, lineNo, i - bolIdx, cb));
                    }
                    i = j;
                    continue;
                }

                 // Check for keywords/names.
                if((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c == '_') || (c == '$' && options.dollarsigns))
                {
                    int j;

                    for(j = i; ++j < source.Length;)
                    {
                        c = source[j];
                        if(c >= 'a' && c <= 'z')
                            continue;
                        if(c >= 'A' && c <= 'Z')
                            continue;
                        if(c >= '0' && c <= '9')
                            continue;
                        if(c == '$' && options.dollarsigns)
                            continue;
                        if(c != '_')
                            break;
                    }
                    if(j - i > MAX_NAME_LEN)
                    {
                        TokenError(i, "name too long, max " + MAX_NAME_LEN);
                    }
                    else
                    {
                        string name = source.Substring(i, j - i);
                        if(name == "quaternion")
                            name = "rotation";  // see lslangtest1.lsl
                        if(keywords.ContainsKey(name))
                        {
                            Object[] args = new Object[] { emsg, filNam, lineNo, i - bolIdx };
                            AppendToken((Token)keywords[name].Invoke(args));
                        }
                        else if(options.arrays && arrayKeywords.ContainsKey(name))
                        {
                            Object[] args = new Object[] { emsg, filNam, lineNo, i - bolIdx };
                            AppendToken((Token)arrayKeywords[name].Invoke(args));
                        }
                        else if(options.advFlowCtl && advFlowCtlKeywords.ContainsKey(name))
                        {
                            Object[] args = new Object[] { emsg, filNam, lineNo, i - bolIdx };
                            AppendToken((Token)advFlowCtlKeywords[name].Invoke(args));
                        }
                        else if(options.tryCatch && tryCatchKeywords.ContainsKey(name))
                        {
                            Object[] args = new Object[] { emsg, filNam, lineNo, i - bolIdx };
                            AppendToken((Token)tryCatchKeywords[name].Invoke(args));
                        }
                        else if(options.objects && objectsKeywords.ContainsKey(name))
                        {
                            Object[] args = new Object[] { emsg, filNam, lineNo, i - bolIdx };
                            AppendToken((Token)objectsKeywords[name].Invoke(args));
                        }
                        else if(options.chars && charsKeywords.ContainsKey(name))
                        {
                            Object[] args = new Object[] { emsg, filNam, lineNo, i - bolIdx };
                            AppendToken((Token)charsKeywords[name].Invoke(args));
                        }
                        else
                        {
                            AppendToken(new TokenName(emsg, filNam, lineNo, i - bolIdx, name));
                        }
                    }
                    i = --j;
                    continue;
                }

                // Check for option enables.

                if ((c == ';') && (lastToken is TokenName) &&
                    (strcasecmp(((TokenName)lastToken).val, "yoptions") == 0))
                {
                    options.advFlowCtl = true;
                    options.tryCatch = true;

                    lastToken = lastToken.prevToken;
                    lastToken.nextToken = null;
                    continue;
                }

                else if ((c == ';') && (lastToken is TokenName) &&
                        (lastToken.prevToken is TokenName) &&
                        (strcasecmp(((TokenName)lastToken.prevToken).val, "yoption") == 0))
                {
                    string opt = ((TokenName)lastToken).val;
                    if(strcasecmp(opt, "allowall") == 0)
                    {
                        options.arrays = true;
                        options.advFlowCtl = true;
                        options.tryCatch = true;
                        options.objects = true;
                        options.chars = true;
                        //                        options.noRightToLeft = true;
                        options.dollarsigns = true;
                    }
                    else if(strcasecmp(opt, "arrays") == 0)
                        options.arrays = true;
                    else if(strcasecmp(opt, "advflowctl") == 0)
                        options.advFlowCtl = true;
                    else if(strcasecmp(opt, "trycatch") == 0)
                        options.tryCatch = true;
                    else if(strcasecmp(opt, "objects") == 0)
                        options.objects = true;
                    else if(strcasecmp(opt, "chars") == 0)
                        options.chars = true;
                    else if(strcasecmp(opt, "norighttoleft") == 0)
                        options.noRightToLeft = true;
                    else if(strcasecmp(opt, "dollarsigns") == 0)
                        options.dollarsigns = true;
                    else
                        lastToken.ErrorMsg("unknown YOption");

                    lastToken = lastToken.prevToken.prevToken;
                    lastToken.nextToken = null;
                    continue;
                }

                 // Lastly, check for delimeters.
                {
                    int j;
                    int len = 0;

                    for(j = 0; j < delims.Length; j++)
                    {
                        len = delims[j].str.Length;
                        if((i + len <= source.Length) && (source.Substring(i, len).Equals(delims[j].str)))
                            break;
                    }
                    if(j < delims.Length)
                    {
                        Object[] args = { emsg, filNam, lineNo, i - bolIdx };
                        Token kwToken = (Token)delims[j].ctorInfo.Invoke(args);
                        AppendToken(kwToken);
                        i += --len;
                        continue;
                    }
                }

                 // Don't know what it is!
                TokenError(i, "unknown character '" + c + "'");
            }
        }

        private static int strcasecmp(String s, String t)
        {
            return String.Compare(s, t, StringComparison.OrdinalIgnoreCase);
        }

        /**
         * @brief try to parse a floating-point number from the source
         * @param i = starting position within this.source of number
         * @returns 0: not a floating point number, try something else
         *       else: position in this.source of terminating character, ie, past number
         *             TokenFloat appended to token list
         *             or error message has been output
         */
        private int TryParseFloat(int i)
        {
            bool decimals, error, negexp, nulexp;
            char c;
            double f, f10;
            int exponent, j, x, y;
            ulong m, mantissa;

            decimals = false;
            error = false;
            exponent = 0;
            mantissa = 0;
            for(j = i; j < source.Length; j++)
            {
                c = source[j];
                if((c >= '0') && (c <= '9'))
                {
                    m = mantissa * 10 + (ulong)(c - '0');
                    if(m / 10 != mantissa)
                    {
                        if(!decimals)
                            exponent++;
                    }
                    else
                    {
                        mantissa = m;
                        if(decimals)
                            exponent--;
                    }
                    continue;
                }
                if(c == '.')
                {
                    if(decimals)
                    {
                        TokenError(i, "more than one decimal point");
                        return j;
                    }
                    decimals = true;
                    continue;
                }
                if((c == 'E') || (c == 'e'))
                {
                    if(++j >= source.Length)
                    {
                        TokenError(i, "floating exponent off end of source");
                        return j;
                    }
                    c = source[j];
                    negexp = (c == '-');
                    if(negexp || (c == '+'))
                        j++;
                    y = 0;
                    nulexp = true;
                    for(; j < source.Length; j++)
                    {
                        c = source[j];
                        if((c < '0') || (c > '9'))
                            break;
                        x = y * 10 + (c - '0');
                        if(x / 10 != y)
                        {
                            if(!error)
                                TokenError(i, "floating exponent overflow");
                            error = true;
                        }
                        y = x;
                        nulexp = false;
                    }
                    if(nulexp)
                    {
                        TokenError(i, "bad or missing floating exponent");
                        return j;
                    }
                    if(negexp)
                    {
                        x = exponent - y;
                        if(x > exponent)
                        {
                            if(!error)
                                TokenError(i, "floating exponent overflow");
                            error = true;
                        }
                    }
                    else
                    {
                        x = exponent + y;
                        if(x < exponent)
                        {
                            if(!error)
                                TokenError(i, "floating exponent overflow");
                            error = true;
                        }
                    }
                    exponent = x;
                }
                if ((c == 'F') || (c == 'f'))
                {
                    if (++j >= source.Length)
                    {
                        TokenError(i, "f at end of source");
                        return j;
                    }

                    c = source[j];
                    if (((c >= '0') && (c <= '9')) || c == '.' || ((c == 'E') || (c == 'e')) || ((c == 'F') || (c == 'f')))
                    {
                        TokenError(j-1, "Syntax error");
                        return j;
                    }
                    break;
                }
                break;
            }
            if(!decimals)
            {
                return 0;
            }

            f = mantissa;
            if((exponent != 0) && (mantissa != 0) && !error)
            {
                f10 = 10.0;
                if(exponent < 0)
                {
                    exponent = -exponent;
                    while(exponent > 0)
                    {
                        if((exponent & 1) != 0)
                        {
                            f /= f10;
                        }
                        exponent /= 2;
                        f10 *= f10;
                    }
                }
                else
                {
                    while(exponent > 0)
                    {
                        if((exponent & 1) != 0)
                        {
                            f *= f10;
                        }
                        exponent /= 2;
                        f10 *= f10;
                    }
                }
            }
            if(!error)
            {
                AppendToken(new TokenFloat(emsg, filNam, lineNo, i - bolIdx, f));
            }
            return j;
        }

        /**
         * @brief try to parse an integer number from the source
         * @param i = starting position within this.source of number
         * @returns 0: not an integer number, try something else
         *       else: position in this.source of terminating character, ie, past number
         *             TokenInt appended to token list
         *             or error message has been output
         */
        private int TryParseInt(int i)
        {
            bool error;
            char c;
            int j;
            uint basse, m, mantissa;

            basse = 10;
            error = false;
            mantissa = 0;
            for(j = i; j < source.Length; j++)
            {
                c = source[j];
                if((c >= '0') && (c <= '9'))
                {
                    m = mantissa * basse + (uint)(c - '0');
                    if(m / basse != mantissa)
                    {
                        if(!error)
                            TokenError(i, "integer overflow");
                        error = true;
                    }
                    mantissa = m;
                    continue;
                }
                if((basse == 16) && ((c >= 'A') && (c <= 'F')))
                {
                    m = mantissa * basse + (uint)(c - 'A') + 10U;
                    if(m / basse != mantissa)
                    {
                        if(!error)
                            TokenError(i, "integer overflow");
                        error = true;
                    }
                    mantissa = m;
                    continue;
                }
                if((basse == 16) && ((c >= 'a') && (c <= 'f')))
                {
                    m = mantissa * basse + (uint)(c - 'a') + 10U;
                    if(m / basse != mantissa)
                    {
                        if(!error)
                            TokenError(i, "integer overflow");
                        error = true;
                    }
                    mantissa = m;
                    continue;
                }
                if(((c == 'x') || (c == 'X')) && (mantissa == 0) && (basse == 10))
                {
                    basse = 16;
                    continue;
                }
                break;
            }
            if(!error)
            {
                AppendToken(new TokenInt(emsg, filNam, lineNo, i - bolIdx, (int)mantissa));
            }
            return j;
        }

        /**
         * @brief append token on to end of list
         * @param newToken = token to append
         * @returns with token appended onto this.lastToken
         */
        private void AppendToken(Token newToken)
        {
            newToken.nextToken = null;
            newToken.prevToken = lastToken;
            newToken.nr2l = this.options.noRightToLeft;
            lastToken.nextToken = newToken;
            lastToken = newToken;
        }

        /**
         * @brief print tokenizing error message
         *        and remember that we've an error
         * @param i = position within source file of the error
         * @param message = error message text
         * @returns with this.youveAnError set
         */
        private void TokenError(int i, string message)
        {
            Token temp = new Token(this.emsg, this.filNam, this.lineNo, i - this.bolIdx);
            temp.ErrorMsg(message);
            youveAnError = true;
        }

        /**
         * @brief get a token's constructor
         * @param tokenType = token's type
         * @returns token's constructor
         */
        private static Type[] constrTypes = new Type[] {
            typeof (TokenErrorMessage), typeof (string), typeof (int), typeof (int)
        };

        private static System.Reflection.ConstructorInfo GetTokenCtor(Type tokenType)
        {
            return tokenType.GetConstructor(constrTypes);
        }

        /**
         * @brief delimeter table
         */
        private class Delim
        {
            public string str;
            public System.Reflection.ConstructorInfo ctorInfo;
            public Delim(string str, Type type)
            {
                this.str = str;
                ctorInfo = GetTokenCtor(type);
            }
        }

        private static Delim[] delims = new Delim[] {
            new Delim ("...", typeof (TokenKwDotDotDot)),
            new Delim ("&&&", typeof (TokenKwAndAndAnd)),
            new Delim ("|||", typeof (TokenKwOrOrOr)),
            new Delim ("<<=", typeof (TokenKwAsnLSh)),
            new Delim (">>=", typeof (TokenKwAsnRSh)),
            new Delim ("<=",  typeof (TokenKwCmpLE)),
            new Delim (">=",  typeof (TokenKwCmpGE)),
            new Delim ("==",  typeof (TokenKwCmpEQ)),
            new Delim ("!=",  typeof (TokenKwCmpNE)),
            new Delim ("++",  typeof (TokenKwIncr)),
            new Delim ("--",  typeof (TokenKwDecr)),
            new Delim ("&&",  typeof (TokenKwAndAnd)),
            new Delim ("||",  typeof (TokenKwOrOr)),
            new Delim ("+=",  typeof (TokenKwAsnAdd)),
            new Delim ("&=",  typeof (TokenKwAsnAnd)),
            new Delim ("-=",  typeof (TokenKwAsnSub)),
            new Delim ("*=",  typeof (TokenKwAsnMul)),
            new Delim ("/=",  typeof (TokenKwAsnDiv)),
            new Delim ("%=",  typeof (TokenKwAsnMod)),
            new Delim ("|=",  typeof (TokenKwAsnOr)),
            new Delim ("^=",  typeof (TokenKwAsnXor)),
            new Delim ("<<",  typeof (TokenKwLSh)),
            new Delim (">>",  typeof (TokenKwRSh)),
            new Delim ("~",   typeof (TokenKwTilde)),
            new Delim ("!",   typeof (TokenKwExclam)),
            new Delim ("@",   typeof (TokenKwAt)),
            new Delim ("%",   typeof (TokenKwMod)),
            new Delim ("^",   typeof (TokenKwXor)),
            new Delim ("&",   typeof (TokenKwAnd)),
            new Delim ("*",   typeof (TokenKwMul)),
            new Delim ("(",   typeof (TokenKwParOpen)),
            new Delim (")",   typeof (TokenKwParClose)),
            new Delim ("-",   typeof (TokenKwSub)),
            new Delim ("+",   typeof (TokenKwAdd)),
            new Delim ("=",   typeof (TokenKwAssign)),
            new Delim ("{",   typeof (TokenKwBrcOpen)),
            new Delim ("}",   typeof (TokenKwBrcClose)),
            new Delim ("[",   typeof (TokenKwBrkOpen)),
            new Delim ("]",   typeof (TokenKwBrkClose)),
            new Delim (";",   typeof (TokenKwSemi)),
            new Delim (":",   typeof (TokenKwColon)),
            new Delim ("<",   typeof (TokenKwCmpLT)),
            new Delim (">",   typeof (TokenKwCmpGT)),
            new Delim (",",   typeof (TokenKwComma)),
            new Delim (".",   typeof (TokenKwDot)),
            new Delim ("?",   typeof (TokenKwQMark)),
            new Delim ("/",   typeof (TokenKwDiv)),
            new Delim ("|",   typeof (TokenKwOr)),
            new Delim ("#",   typeof (TokenKwHash))
        };

        /**
         * @brief keyword tables
         *        The keyword tables translate a keyword string
         *        to the corresponding token constructor.
         */
        private static Dictionary<string, System.Reflection.ConstructorInfo> keywords = BuildKeywords();
        private static Dictionary<string, System.Reflection.ConstructorInfo> arrayKeywords = BuildArrayKeywords();
        private static Dictionary<string, System.Reflection.ConstructorInfo> advFlowCtlKeywords = BuildAdvFlowCtlKeywords();
        private static Dictionary<string, System.Reflection.ConstructorInfo> tryCatchKeywords = BuildTryCatchKeywords();
        private static Dictionary<string, System.Reflection.ConstructorInfo> objectsKeywords = BuildObjectsKeywords();
        private static Dictionary<string, System.Reflection.ConstructorInfo> charsKeywords = BuildCharsKeywords();

        private static Dictionary<string, System.Reflection.ConstructorInfo> BuildKeywords()
        {
            Dictionary<string, System.Reflection.ConstructorInfo> kws = new Dictionary<string, System.Reflection.ConstructorInfo>();

            kws.Add("default", GetTokenCtor(typeof(TokenKwDefault)));
            kws.Add("do", GetTokenCtor(typeof(TokenKwDo)));
            kws.Add("else", GetTokenCtor(typeof(TokenKwElse)));
            kws.Add("float", GetTokenCtor(typeof(TokenTypeFloat)));
            kws.Add("for", GetTokenCtor(typeof(TokenKwFor)));
            kws.Add("if", GetTokenCtor(typeof(TokenKwIf)));
            kws.Add("integer", GetTokenCtor(typeof(TokenTypeInt)));
            kws.Add("list", GetTokenCtor(typeof(TokenTypeList)));
            kws.Add("jump", GetTokenCtor(typeof(TokenKwJump)));
            kws.Add("key", GetTokenCtor(typeof(TokenTypeKey)));
            kws.Add("return", GetTokenCtor(typeof(TokenKwRet)));
            kws.Add("rotation", GetTokenCtor(typeof(TokenTypeRot)));
            kws.Add("state", GetTokenCtor(typeof(TokenKwState)));
            kws.Add("string", GetTokenCtor(typeof(TokenTypeStr)));
            kws.Add("vector", GetTokenCtor(typeof(TokenTypeVec)));
            kws.Add("while", GetTokenCtor(typeof(TokenKwWhile)));

            return kws;
        }

        private static Dictionary<string, System.Reflection.ConstructorInfo> BuildArrayKeywords()
        {
            Dictionary<string, System.Reflection.ConstructorInfo> kws = new Dictionary<string, System.Reflection.ConstructorInfo>();

            kws.Add("array", GetTokenCtor(typeof(TokenTypeArray)));
            kws.Add("foreach", GetTokenCtor(typeof(TokenKwForEach)));
            kws.Add("in", GetTokenCtor(typeof(TokenKwIn)));
            kws.Add("is", GetTokenCtor(typeof(TokenKwIs)));
            kws.Add("object", GetTokenCtor(typeof(TokenTypeObject)));
            kws.Add("undef", GetTokenCtor(typeof(TokenKwUndef)));

            return kws;
        }

        private static Dictionary<string, System.Reflection.ConstructorInfo> BuildAdvFlowCtlKeywords()
        {
            Dictionary<string, System.Reflection.ConstructorInfo> kws = new Dictionary<string, System.Reflection.ConstructorInfo>();

            kws.Add("break", GetTokenCtor(typeof(TokenKwBreak)));
            kws.Add("case", GetTokenCtor(typeof(TokenKwCase)));
            kws.Add("constant", GetTokenCtor(typeof(TokenKwConst)));
            kws.Add("continue", GetTokenCtor(typeof(TokenKwCont)));
            kws.Add("switch", GetTokenCtor(typeof(TokenKwSwitch)));

            return kws;
        }

        private static Dictionary<string, System.Reflection.ConstructorInfo> BuildTryCatchKeywords()
        {
            Dictionary<string, System.Reflection.ConstructorInfo> kws = new Dictionary<string, System.Reflection.ConstructorInfo>();

            kws.Add("catch", GetTokenCtor(typeof(TokenKwCatch)));
            kws.Add("exception", GetTokenCtor(typeof(TokenTypeExc)));
            kws.Add("finally", GetTokenCtor(typeof(TokenKwFinally)));
            kws.Add("throw", GetTokenCtor(typeof(TokenKwThrow)));
            kws.Add("try", GetTokenCtor(typeof(TokenKwTry)));

            return kws;
        }

        private static Dictionary<string, System.Reflection.ConstructorInfo> BuildObjectsKeywords()
        {
            Dictionary<string, System.Reflection.ConstructorInfo> kws = new Dictionary<string, System.Reflection.ConstructorInfo>();

            kws.Add("abstract", GetTokenCtor(typeof(TokenKwAbstract)));
            kws.Add("base", GetTokenCtor(typeof(TokenKwBase)));
            kws.Add("class", GetTokenCtor(typeof(TokenKwClass)));
            kws.Add("constructor", GetTokenCtor(typeof(TokenKwConstructor)));
            kws.Add("delegate", GetTokenCtor(typeof(TokenKwDelegate)));
            kws.Add("destructor", GetTokenCtor(typeof(TokenKwDestructor)));
            kws.Add("final", GetTokenCtor(typeof(TokenKwFinal)));
            kws.Add("get", GetTokenCtor(typeof(TokenKwGet)));
            kws.Add("interface", GetTokenCtor(typeof(TokenKwInterface)));
            kws.Add("new", GetTokenCtor(typeof(TokenKwNew)));
            kws.Add("override", GetTokenCtor(typeof(TokenKwOverride)));
            kws.Add("partial", GetTokenCtor(typeof(TokenKwPartial)));
            kws.Add("private", GetTokenCtor(typeof(TokenKwPrivate)));
            kws.Add("protected", GetTokenCtor(typeof(TokenKwProtected)));
            kws.Add("public", GetTokenCtor(typeof(TokenKwPublic)));
            kws.Add("set", GetTokenCtor(typeof(TokenKwSet)));
            kws.Add("static", GetTokenCtor(typeof(TokenKwStatic)));
            kws.Add("this", GetTokenCtor(typeof(TokenKwThis)));
            kws.Add("typedef", GetTokenCtor(typeof(TokenKwTypedef)));
            kws.Add("virtual", GetTokenCtor(typeof(TokenKwVirtual)));

            return kws;
        }

        private static Dictionary<string, System.Reflection.ConstructorInfo> BuildCharsKeywords()
        {
            Dictionary<string, System.Reflection.ConstructorInfo> kws = new Dictionary<string, System.Reflection.ConstructorInfo>();

            kws.Add("char", GetTokenCtor(typeof(TokenTypeChar)));

            return kws;
        }
    }

    /**
     * @brief All output token types in addition to TokenBegin.
     *        They are all sub-types of Token.
     */

    public class TokenChar: Token
    {
        public char val;
        public TokenChar(TokenErrorMessage emsg, string file, int line, int posn, char val) : base(emsg, file, line, posn)
        {
            this.val = val;
        }
        public TokenChar(Token original, char val) : base(original)
        {
            this.val = val;
        }
        public override string ToString()
        {
            switch(val)
            {
                case '\'':
                    return "'\\''";
                case '\\':
                    return "'\\\\'";
                case '\n':
                    return "'\\n'";
                case '\t':
                    return "'\\t'";
                default:
                    return "'" + val + "'";
            }
        }
    }

    public class TokenFloat: Token
    {
        public double val;
        public TokenFloat(TokenErrorMessage emsg, string file, int line, int posn, double val) : base(emsg, file, line, posn)
        {
            this.val = val;
        }
        public override string ToString()
        {
            return val.ToString();
        }
    }

    public class TokenInt: Token
    {
        public int val;
        public TokenInt(TokenErrorMessage emsg, string file, int line, int posn, int val) : base(emsg, file, line, posn)
        {
            this.val = val;
        }
        public TokenInt(Token original, int val) : base(original)
        {
            this.val = val;
        }
        public override string ToString()
        {
            return val.ToString();
        }
    }

    public class TokenName: Token
    {
        public string val;
        public TokenName(TokenErrorMessage emsg, string file, int line, int posn, string val) : base(emsg, file, line, posn)
        {
            this.val = val;
        }
        public TokenName(Token original, string val) : base(original)
        {
            this.val = val;
        }
        public override string ToString()
        {
            return this.val;
        }
    }

    public class TokenStr: Token
    {
        public string val;
        public TokenStr(TokenErrorMessage emsg, string file, int line, int posn, string val) : base(emsg, file, line, posn)
        {
            this.val = val;
        }
        public override string ToString()
        {
            if((val.IndexOf('"') < 0) &&
                (val.IndexOf('\\') < 0) &&
                (val.IndexOf('\n') < 0) &&
                (val.IndexOf('\t') < 0))
                return "\"" + val + "\"";

            int len = val.Length;
            StringBuilder sb = new StringBuilder(len * 2 + 2);
            sb.Append('"');
            for(int i = 0; i < len; i++)
            {
                char c = val[i];
                switch(c)
                {
                    case '"':
                        {
                            sb.Append('\\');
                            sb.Append('"');
                            break;
                        }
                    case '\\':
                        {
                            sb.Append('\\');
                            sb.Append('\\');
                            break;
                        }
                    case '\n':
                        {
                            sb.Append('\\');
                            sb.Append('n');
                            break;
                        }
                    case '\t':
                        {
                            sb.Append('\\');
                            sb.Append('t');
                            break;
                        }
                    default:
                        {
                            sb.Append(c);
                            break;
                        }
                }
            }
            return sb.ToString();
        }
    }

    /*
     * This one marks the end-of-file.
     */
    public class TokenEnd: Token
    {
        public TokenEnd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
    }

    /*
     * Various keywords and delimeters.
     */
    public delegate object TokenRValConstBinOpDelegate(object left, object right);
    public delegate object TokenRValConstUnOpDelegate(object right);

    public class TokenKw: Token
    {
        public TokenRValConstBinOpDelegate binOpConst;
        public TokenRValConstUnOpDelegate unOpConst;
        public bool sdtClassOp;
        public TokenKw(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn)
        {
        }
        public TokenKw(Token original) : base(original)
        {
        }
    }

    public class TokenKwDotDotDot: TokenKw
    {
        public TokenKwDotDotDot(TokenErrorMessage emsg, string file,
                                int line, int posn) : base(emsg, file, line, posn)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = false;
        }
        public TokenKwDotDotDot(Token original) : base(original)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = false;
        }
        public override string ToString()
        {
            return "...";
        }
    }
    public class TokenKwAndAndAnd: TokenKw
    {
        public TokenKwAndAndAnd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = false;
        }
        public TokenKwAndAndAnd(Token original) : base(original)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = false;
        }
        public override string ToString()
        {
            return "&&&";
        }
    }
    public class TokenKwOrOrOr: TokenKw
    {
        public TokenKwOrOrOr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = false;
        }
        public TokenKwOrOrOr(Token original) : base(original)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = false;
        }
        public override string ToString()
        {
            return "|||";
        }
    }
    public class TokenKwAsnLSh: TokenKw
    {
        public TokenKwAsnLSh(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = true;
        }
        public TokenKwAsnLSh(Token original) : base(original)
        {
            binOpConst = TokenRValConstOps.Null;
            unOpConst = TokenRValConstOps.Null;
            sdtClassOp = true;
        }
        public override string ToString()
        {
            return "<<=";
        }
    }
    public class TokenKwAsnRSh: TokenKw
    {
        public TokenKwAsnRSh(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnRSh(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return ">>=";
        }
    }
    public class TokenKwCmpLE: TokenKw
    {
        public TokenKwCmpLE(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.binConstsLE; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwCmpLE(Token original) : base(original) { binOpConst = TokenRValConstOps.binConstsLE; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "<=";
        }
    }
    public class TokenKwCmpGE: TokenKw
    {
        public TokenKwCmpGE(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.binConstsGE; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwCmpGE(Token original) : base(original) { binOpConst = TokenRValConstOps.binConstsGE; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return ">=";
        }
    }
    public class TokenKwCmpEQ: TokenKw
    {
        public TokenKwCmpEQ(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.binConstsEQ; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwCmpEQ(Token original) : base(original) { binOpConst = TokenRValConstOps.binConstsEQ; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "==";
        }
    }
    public class TokenKwCmpNE: TokenKw
    {
        public TokenKwCmpNE(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.binConstsNE; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwCmpNE(Token original) : base(original) { binOpConst = TokenRValConstOps.binConstsNE; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "!=";
        }
    }
    public class TokenKwIncr: TokenKw
    {
        public TokenKwIncr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwIncr(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "++";
        }
    }
    public class TokenKwDecr: TokenKw
    {
        public TokenKwDecr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwDecr(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "--";
        }
    }
    public class TokenKwAndAnd: TokenKw
    {
        public TokenKwAndAnd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAndAnd(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "&&";
        }
    }
    public class TokenKwOrOr: TokenKw
    {
        public TokenKwOrOr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwOrOr(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "||";
        }
    }
    public class TokenKwAsnAdd: TokenKw
    {
        public TokenKwAsnAdd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnAdd(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "+=";
        }
    }
    public class TokenKwAsnAnd: TokenKw
    {
        public TokenKwAsnAnd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnAnd(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "&=";
        }
    }
    public class TokenKwAsnSub: TokenKw
    {
        public TokenKwAsnSub(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnSub(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "-=";
        }
    }
    public class TokenKwAsnMul: TokenKw
    {
        public TokenKwAsnMul(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnMul(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "*=";
        }
    }
    public class TokenKwAsnDiv: TokenKw
    {
        public TokenKwAsnDiv(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnDiv(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "/=";
        }
    }
    public class TokenKwAsnMod: TokenKw
    {
        public TokenKwAsnMod(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnMod(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "%=";
        }
    }
    public class TokenKwAsnOr: TokenKw
    {
        public TokenKwAsnOr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnOr(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "|=";
        }
    }
    public class TokenKwAsnXor: TokenKw
    {
        public TokenKwAsnXor(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAsnXor(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "^=";
        }
    }
    public class TokenKwLSh: TokenKw
    {
        public TokenKwLSh(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.LSh; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwLSh(Token original) : base(original) { binOpConst = TokenRValConstOps.LSh; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "<<";
        }
    }
    public class TokenKwRSh: TokenKw
    {
        public TokenKwRSh(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.RSh; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwRSh(Token original) : base(original) { binOpConst = TokenRValConstOps.RSh; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return ">>";
        }
    }
    public class TokenKwTilde: TokenKw
    {
        public TokenKwTilde(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Not; sdtClassOp = true; }
        public TokenKwTilde(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Not; sdtClassOp = true; }
        public override string ToString()
        {
            return "~";
        }
    }
    public class TokenKwExclam: TokenKw
    {
        public TokenKwExclam(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwExclam(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "!";
        }
    }
    public class TokenKwAt: TokenKw
    {
        public TokenKwAt(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwAt(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "@";
        }
    }
    public class TokenKwMod: TokenKw
    {
        public TokenKwMod(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Mod; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwMod(Token original) : base(original) { binOpConst = TokenRValConstOps.Mod; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "%";
        }
    }
    public class TokenKwXor: TokenKw
    {
        public TokenKwXor(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Xor; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwXor(Token original) : base(original) { binOpConst = TokenRValConstOps.Xor; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "^";
        }
    }
    public class TokenKwAnd: TokenKw
    {
        public TokenKwAnd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.And; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAnd(Token original) : base(original) { binOpConst = TokenRValConstOps.And; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "&";
        }
    }
    public class TokenKwMul: TokenKw
    {
        public TokenKwMul(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Mul; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwMul(Token original) : base(original) { binOpConst = TokenRValConstOps.Mul; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "*";
        }
    }
    public class TokenKwParOpen: TokenKw
    {
        public TokenKwParOpen(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwParOpen(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "(";
        }
    }
    public class TokenKwParClose: TokenKw
    {
        public TokenKwParClose(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwParClose(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return ")";
        }
    }
    public class TokenKwSub: TokenKw
    {
        public TokenKwSub(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Sub; unOpConst = TokenRValConstOps.Neg; sdtClassOp = true; }
        public TokenKwSub(Token original) : base(original) { binOpConst = TokenRValConstOps.Sub; unOpConst = TokenRValConstOps.Neg; sdtClassOp = true; }
        public override string ToString()
        {
            return "-";
        }
    }
    public class TokenKwAdd: TokenKw
    {
        public TokenKwAdd(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Add; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwAdd(Token original) : base(original) { binOpConst = TokenRValConstOps.Add; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "+";
        }
    }
    public class TokenKwAssign: TokenKw
    {
        public TokenKwAssign(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwAssign(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "=";
        }
    }
    public class TokenKwBrcOpen: TokenKw
    {
        public TokenKwBrcOpen(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwBrcOpen(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "{";
        }
    }
    public class TokenKwBrcClose: TokenKw
    {
        public TokenKwBrcClose(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwBrcClose(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "}";
        }
    }
    public class TokenKwBrkOpen: TokenKw
    {
        public TokenKwBrkOpen(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwBrkOpen(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "[";
        }
    }
    public class TokenKwBrkClose: TokenKw
    {
        public TokenKwBrkClose(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwBrkClose(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "]";
        }
    }
    public class TokenKwSemi: TokenKw
    {
        public TokenKwSemi(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwSemi(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return ";";
        }
    }
    public class TokenKwColon: TokenKw
    {
        public TokenKwColon(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwColon(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return ":";
        }
    }
    public class TokenKwCmpLT: TokenKw
    {
        public TokenKwCmpLT(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.binConstsLT; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwCmpLT(Token original) : base(original) { binOpConst = TokenRValConstOps.binConstsLT; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "<";
        }
    }
    public class TokenKwCmpGT: TokenKw
    {
        public TokenKwCmpGT(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.binConstsGT; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwCmpGT(Token original) : base(original) { binOpConst = TokenRValConstOps.binConstsGT; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return ">";
        }
    }
    public class TokenKwComma: TokenKw
    {
        public TokenKwComma(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwComma(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return ",";
        }
    }
    public class TokenKwDot: TokenKw
    {
        public TokenKwDot(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwDot(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return ".";
        }
    }
    public class TokenKwQMark: TokenKw
    {
        public TokenKwQMark(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwQMark(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "?";
        }
    }
    public class TokenKwDiv: TokenKw
    {
        public TokenKwDiv(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Div; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwDiv(Token original) : base(original) { binOpConst = TokenRValConstOps.Div; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "/";
        }
    }
    public class TokenKwOr: TokenKw
    {
        public TokenKwOr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Or; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public TokenKwOr(Token original) : base(original) { binOpConst = TokenRValConstOps.Or; unOpConst = TokenRValConstOps.Null; sdtClassOp = true; }
        public override string ToString()
        {
            return "|";
        }
    }
    public class TokenKwHash: TokenKw
    {
        public TokenKwHash(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwHash(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "#";
        }
    }

    public class TokenKwAbstract: TokenKw
    {
        public TokenKwAbstract(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwAbstract(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "abstract";
        }
    }
    public class TokenKwBase: TokenKw
    {
        public TokenKwBase(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwBase(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "base";
        }
    }
    public class TokenKwBreak: TokenKw
    {
        public TokenKwBreak(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwBreak(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "break";
        }
    }
    public class TokenKwCase: TokenKw
    {
        public TokenKwCase(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwCase(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "case";
        }
    }
    public class TokenKwCatch: TokenKw
    {
        public TokenKwCatch(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwCatch(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "catch";
        }
    }
    public class TokenKwClass: TokenKw
    {
        public TokenKwClass(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwClass(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "class";
        }
    }
    public class TokenKwConst: TokenKw
    {
        public TokenKwConst(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwConst(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "constant";
        }
    }
    public class TokenKwConstructor: TokenKw
    {
        public TokenKwConstructor(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwConstructor(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "constructor";
        }
    }
    public class TokenKwCont: TokenKw
    {
        public TokenKwCont(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwCont(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "continue";
        }
    }
    public class TokenKwDelegate: TokenKw
    {
        public TokenKwDelegate(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwDelegate(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "delegate";
        }
    }
    public class TokenKwDefault: TokenKw
    {
        public TokenKwDefault(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwDefault(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "default";
        }
    }
    public class TokenKwDestructor: TokenKw
    {
        public TokenKwDestructor(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwDestructor(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "destructor";
        }
    }
    public class TokenKwDo: TokenKw
    {
        public TokenKwDo(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwDo(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "do";
        }
    }
    public class TokenKwElse: TokenKw
    {
        public TokenKwElse(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwElse(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "else";
        }
    }
    public class TokenKwFinal: TokenKw
    {
        public TokenKwFinal(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwFinal(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "final";
        }
    }
    public class TokenKwFinally: TokenKw
    {
        public TokenKwFinally(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwFinally(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "finally";
        }
    }
    public class TokenKwFor: TokenKw
    {
        public TokenKwFor(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwFor(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "for";
        }
    }
    public class TokenKwForEach: TokenKw
    {
        public TokenKwForEach(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwForEach(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "foreach";
        }
    }
    public class TokenKwGet: TokenKw
    {
        public TokenKwGet(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwGet(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "get";
        }
    }
    public class TokenKwIf: TokenKw
    {
        public TokenKwIf(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwIf(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "if";
        }
    }
    public class TokenKwIn: TokenKw
    {
        public TokenKwIn(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwIn(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "in";
        }
    }
    public class TokenKwInterface: TokenKw
    {
        public TokenKwInterface(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwInterface(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "interface";
        }
    }
    public class TokenKwIs: TokenKw
    {
        public TokenKwIs(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwIs(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "is";
        }
    }
    public class TokenKwJump: TokenKw
    {
        public TokenKwJump(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwJump(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "jump";
        }
    }
    public class TokenKwNew: TokenKw
    {
        public TokenKwNew(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwNew(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "new";
        }
    }
    public class TokenKwOverride: TokenKw
    {
        public TokenKwOverride(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwOverride(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "override";
        }
    }
    public class TokenKwPartial: TokenKw
    {
        public TokenKwPartial(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwPartial(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "partial";
        }
    }
    public class TokenKwPrivate: TokenKw
    {
        public TokenKwPrivate(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwPrivate(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "private";
        }
    }
    public class TokenKwProtected: TokenKw
    {
        public TokenKwProtected(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwProtected(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "protected";
        }
    }
    public class TokenKwPublic: TokenKw
    {
        public TokenKwPublic(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwPublic(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "public";
        }
    }
    public class TokenKwRet: TokenKw
    {
        public TokenKwRet(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwRet(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "return";
        }
    }
    public class TokenKwSet: TokenKw
    {
        public TokenKwSet(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwSet(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "set";
        }
    }
    public class TokenKwState: TokenKw
    {
        public TokenKwState(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwState(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "state";
        }
    }
    public class TokenKwStatic: TokenKw
    {
        public TokenKwStatic(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwStatic(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "static";
        }
    }
    public class TokenKwSwitch: TokenKw
    {
        public TokenKwSwitch(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwSwitch(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "switch";
        }
    }
    public class TokenKwThis: TokenKw
    {
        public TokenKwThis(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwThis(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "this";
        }
    }
    public class TokenKwThrow: TokenKw
    {
        public TokenKwThrow(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwThrow(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "throw";
        }
    }
    public class TokenKwTry: TokenKw
    {
        public TokenKwTry(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwTry(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "try";
        }
    }
    public class TokenKwTypedef: TokenKw
    {
        public TokenKwTypedef(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwTypedef(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "typedef";
        }
    }
    public class TokenKwUndef: TokenKw
    {
        public TokenKwUndef(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwUndef(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "undef";
        }
    }
    public class TokenKwVirtual: TokenKw
    {
        public TokenKwVirtual(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwVirtual(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "virtual";
        }
    }
    public class TokenKwWhile: TokenKw
    {
        public TokenKwWhile(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public TokenKwWhile(Token original) : base(original) { binOpConst = TokenRValConstOps.Null; unOpConst = TokenRValConstOps.Null; sdtClassOp = false; }
        public override string ToString()
        {
            return "while";
        }
    }

    /**
     * @brief These static functions attempt to perform arithmetic on two constant
     *        operands to generate the resultant constant.
     *        Likewise for unary operators.
     *
     * @param left  = left-hand value
     * @param right = right-hand value
     * @returns null: not able to perform computation
     *          else: resultant value object
     *
     * Note: it is ok for these to throw any exception (such as overflow or div-by-zero), 
     *       and it will be treated as the 'not able to perform computation' case.
     */
    public class TokenRValConstOps
    {
        public static object Null(object left, object right)
        {
            return null;
        }
        public static object Div(object left, object right)
        {
            double r;
            try
            {
                if ((left is int) && (right is int))
                {
                    return (int)left / (int)right;
                }
                if ((left is double) && (right is int))
                {
                    r = (double)left / (int)right;
                    if (double.IsNaN(r) || double.IsInfinity(r))
                        throw new Exception("Division by Zero");
                    return r;
                }
            }
            catch (DivideByZeroException)
            {
                throw new Exception("Division by Zero");
            }

            if ((left is int) && (right is double))
            {
                r = (int)left / (double)right;
            }
            else if((left is double) && (right is double))
            {
                r= (double)left / (double)right;
            }
            else
                return null;

            if (double.IsNaN(r) || double.IsInfinity(r))
                throw new Exception("Division by Zero");
            return r;
        }
        public static object Mod(object left, object right)
        {
            double r;
            try
            {
                if ((left is int) && (right is int))
                {
                    return (int)left % (int)right;
                }
                if ((left is double) && (right is int))
                {
                    r = (double)left % (int)right;
                    if (double.IsNaN(r) || double.IsInfinity(r))
                        throw new Exception("Division by Zero");
                    return r;
                }
            }
            catch (DivideByZeroException)
            {
                throw new Exception("Division by Zero");
            }

            if ((left is int) && (right is double))
            {
                r = (int)left % (double)right;
            }
            else if ((left is double) && (right is double))
            {
                r = (double)left % (double)right;
            }
            else
                return null;

            if (double.IsNaN(r) || double.IsInfinity(r))
                throw new Exception("Division by Zero");
            return r;
        }

        public static object Mul(object left, object right)
        {
            if((left is int) && (right is int))
            {
                return (int)left * (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left * (double)right;
            }
            if((left is double) && (right is int))
            {
                return (double)left * (int)right;
            }
            if((left is double) && (right is double))
            {
                return (double)left * (double)right;
            }
            return null;
        }
        public static object And(object left, object right)
        {
            if((left is int) && (right is int))
            {
                return (int)left & (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left & (int)(double)right;
            }
            if((left is double) && (right is int))
            {
                return (int)(double)left & (int)right;
            }
            if((left is double) && (right is double))
            {
                return (int)(double)left & (int)(double)right;
            }
            return null;
        }
        public static object LSh(object left, object right)
        {
            if((left is int) && (right is int))
            {
                return (int)left << (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left << (int)(double)right;
            }
            if((left is double) && (right is int))
            {
                return (int)(double)left << (int)right;
            }
            if((left is double) && (right is double))
            {
                return (int)(double)left << (int)(double)right;
            }
            return null;
        }
        public static object Or(object left, object right)
        {
            if((left is int) && (right is int))
            {
                return (int)left | (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left | (int)(double)right;
            }
            if((left is double) && (right is int))
            {
                return (int)(double)left | (int)right;
            }
            if((left is double) && (right is double))
            {
                return (int)(double)left | (int)(double)right;
            }
            return null;
        }
        public static object RSh(object left, object right)
        {
            if((left is int) && (right is int))
            {
                return (int)left >> (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left >> (int)(double)right;
            }
            if((left is double) && (right is int))
            {
                return (int)(double)left >> (int)right;
            }
            if((left is double) && (right is double))
            {
                return (int)(double)left >> (int)(double)right;
            }
            return null;
        }
        public static object Xor(object left, object right)
        {
            if((left is int) && (right is int))
            {
                return (int)left ^ (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left ^ (int)(double)right;
            }
            if((left is double) && (right is int))
            {
                return (int)(double)left ^ (int)right;
            }
            if((left is double) && (right is double))
            {
                return (int)(double)left ^ (int)(double)right;
            }
            return null;
        }
        public static object Add(object left, object right)
        {
            if((left is char) && (right is int))
            {
                return (char)((char)left + (int)right);
            }
            if((left is double) && (right is double))
            {
                return (double)left + (double)right;
            }
            if((left is double) && (right is int))
            {
                return (double)left + (int)right;
            }
            if((left is double) && (right is string))
            {
                return TypeCast.FloatToString((double)left) + (string)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left + (double)right;
            }
            if((left is int) && (right is int))
            {
                return (int)left + (int)right;
            }
            if((left is int) && (right is string))
            {
                return TypeCast.IntegerToString((int)left) + (string)right;
            }
            if((left is string) && (right is char))
            {
                return (string)left + (char)right;
            }
            if((left is string) && (right is double))
            {
                return (string)left + TypeCast.FloatToString((double)right);
            }
            if((left is string) && (right is int))
            {
                return (string)left + TypeCast.IntegerToString((int)right);
            }
            if((left is string) && (right is string))
            {
                return (string)left + (string)right;
            }
            return null;
        }
        public static object Sub(object left, object right)
        {
            if((left is char) && (right is int))
            {
                return (char)((char)left - (int)right);
            }
            if((left is int) && (right is int))
            {
                return (int)left - (int)right;
            }
            if((left is int) && (right is double))
            {
                return (int)left - (double)right;
            }
            if((left is double) && (right is int))
            {
                return (double)left - (int)right;
            }
            if((left is double) && (right is double))
            {
                return (double)left - (double)right;
            }
            return null;
        }
        public static object Null(object right)
        {
            return null;
        }
        public static object Neg(object right)
        {
            if(right is int)
            {
                return -(int)right;
            }
            if(right is double)
            {
                return -(double)right;
            }
            return null;
        }
        public static object Not(object right)
        {
            if(right is int)
            {
                return ~(int)right;
            }
            return null;
        }

        public static int binConstCompare(object left, object right)
        {
            double a;
            if (left is bool lb)
                a = lb ? 1.0 : 0.0;
            else if (left is int li)
                a = li;
            else if (left is float lf)
                a = lf;
            else if (left is double ld)
                a = ld;
            else if(left is string ls)
            {
                if(!(right is string rs))
                    return -2;
                return ls.CompareTo(rs);
            }
            else
                return -2;

            double b;
            if (right is bool rb)
                b = rb ? 1.0 : 0.0;
            else if (right is int ri)
                b = ri;
            else if (right is float rf)
                b = rf;
            else if (right is double rd)
                b = rd;
            else
                return -2;

            return a.CompareTo(b);
        }

        public static object binConstsLT(object left, object right)
        {
            int res = binConstCompare(left, right);
            if (res == -2)
                return null;
            return (res < 0) ? 1 : 0;
        }

        public static object binConstsLE(object left, object right)
        {
            int res = binConstCompare(left, right);
            if (res == -2)
                return null;
            return (res <= 0) ? 1 : 0;
        }

        public static object binConstsGT(object left, object right)
        {
            int res = binConstCompare(left, right);
            if (res == -2)
                return null;
            return (res > 0) ? 1 : 0;
        }

        public static object binConstsGE(object left, object right)
        {
            int res = binConstCompare(left, right);
            if (res == -2)
                return null;
            return (res >= 0) ? 1 : 0;
        }

        public static object binConstsEQ(object left, object right)
        {
            int res = binConstCompare(left, right);
            if (res == -2)
                return null;
            return (res == 0) ? 1 : 0;
        }

        public static object binConstsNE(object left, object right)
        {
            int res = binConstCompare(left, right);
            if (res == -2)
                return null;
            return (res != 0) ? 1 : 0;
        }
    }

    /*
     * Various datatypes.
     */
    public abstract class TokenType: Token
    {

        public TokenType(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenType(Token original) : base(original) { }

        public static TokenType FromSysType(Token original, System.Type typ)
        {
            if(typ == typeof(LSL_List))
                return new TokenTypeList(original);
            if(typ == typeof(LSL_Rotation))
                return new TokenTypeRot(original);
            if(typ == typeof(void))
                return new TokenTypeVoid(original);
            if(typ == typeof(LSL_Vector))
                return new TokenTypeVec(original);
            if(typ == typeof(float))
                return new TokenTypeFloat(original);
            if(typ == typeof(int))
                return new TokenTypeInt(original);
            if(typ == typeof(string))
                return new TokenTypeStr(original);
            if(typ == typeof(double))
                return new TokenTypeFloat(original);
            if(typ == typeof(bool))
                return new TokenTypeBool(original);
            if(typ == typeof(object))
                return new TokenTypeObject(original);
            if(typ == typeof(XMR_Array))
                return new TokenTypeArray(original);
            if(typ == typeof(LSL_Integer))
                return new TokenTypeLSLInt(original);
            if(typ == typeof(LSL_Float))
                return new TokenTypeLSLFloat(original);
            if(typ == typeof(LSL_String))
                return new TokenTypeLSLString(original);
            if(typ == typeof(char))
                return new TokenTypeChar(original);
            if(typ == typeof(Exception))
                return new TokenTypeExc(original);

            throw new Exception("unknown script type " + typ.ToString());
        }

        public static TokenType FromLSLType(Token original, string typ)
        {
            if(typ == "list")
                return new TokenTypeList(original);
            if(typ == "rotation")
                return new TokenTypeRot(original);
            if(typ == "vector")
                return new TokenTypeVec(original);
            if(typ == "float")
                return new TokenTypeFloat(original);
            if(typ == "integer")
                return new TokenTypeInt(original);
            if(typ == "key")
                return new TokenTypeKey(original);
            if(typ == "string")
                return new TokenTypeStr(original);
            if(typ == "object")
                return new TokenTypeObject(original);
            if(typ == "array")
                return new TokenTypeArray(original);
            if(typ == "bool")
                return new TokenTypeBool(original);
            if(typ == "void")
                return new TokenTypeVoid(original);
            if(typ == "char")
                return new TokenTypeChar(original);
            if(typ == "exception")
                return new TokenTypeExc(original);

            throw new Exception("unknown type " + typ);
        }

        /**
         * @brief Estimate the number of bytes of memory taken by one of these
         *        objects.  For objects with widely varying size, return the
         *        smallest it can be.
         */
        public static int StaticSize(System.Type typ)
        {
            if(typ == typeof(LSL_List))
                return 96;
            if(typ == typeof(LSL_Rotation))
                return 80;
            if(typ == typeof(void))
                return 0;
            if(typ == typeof(LSL_Vector))
                return 72;
            if(typ == typeof(float))
                return 8;
            if(typ == typeof(int))
                return 8;
            if(typ == typeof(string))
                return 40;
            if(typ == typeof(double))
                return 8;
            if(typ == typeof(bool))
                return 8;
            if(typ == typeof(XMR_Array))
                return 96;
            if(typ == typeof(object))
                return 32;
            if(typ == typeof(char))
                return 2;

            if(typ == typeof(LSL_Integer))
                return 32;
            if(typ == typeof(LSL_Float))
                return 32;
            if(typ == typeof(LSL_String))
                return 40;

            throw new Exception("unknown type " + typ.ToString());
        }

        /**
         * @brief Return the corresponding system type.
         */
        public abstract Type ToSysType();

        /**
         * @brief Return the equivalent LSL wrapping type.
         *
         *  null: normal
         *  else: LSL-style wrapping, ie, LSL_Integer, LSL_Float, LSL_String
         *        ToSysType()=System.Int32;  lslWrapping=LSL_Integer
         *        ToSysType()=System.Float;  lslWrapping=LSL_Float
         *        ToSysType()=System.String; lslWrapping=LSL_String
         */
        public virtual Type ToLSLWrapType()
        {
            return null;
        }

        /**
         * @brief Assign slots in either the global variable arrays or the script-defined type instance arrays.
         *        These only need to be implemented for script-visible types, ie, those that a script writer 
         *        can actually define a variable as.
         */
        public virtual void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            throw new Exception("not implemented for " + ToString() + " (" + GetType() + ")");
        }

        /**
         * @brief Get heap tracking type.
         *        null indicates there is no heap tracker for the type.
         */
        public virtual Type ToHeapTrackerType()
        {
            return null;
        }
        public virtual ConstructorInfo GetHeapTrackerCtor()
        {
            throw new ApplicationException("no GetHeapTrackerCtor for " + this.GetType());
        }
        public virtual void CallHeapTrackerPopMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            throw new ApplicationException("no CallHeapTrackerPopMeth for " + this.GetType());
        }
        public virtual void CallHeapTrackerPushMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            throw new ApplicationException("no CallHeapTrackerPushMeth for " + this.GetType());
        }
    }

    public class TokenTypeArray: TokenType
    {
        private static readonly FieldInfo iarArraysFieldInfo = typeof(XMRInstArrays).GetField("iarArrays");

        public TokenTypeArray(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeArray(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(XMR_Array);
        }
        public override string ToString()
        {
            return "array";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarArraysFieldInfo;
            declVar.vTableIndex = arSizes.iasArrays++;
        }
    }
    public class TokenTypeBool: TokenType
    {
        public TokenTypeBool(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeBool(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(bool);
        }
        public override string ToString()
        {
            return "bool";
        }
    }
    public class TokenTypeChar: TokenType
    {
        private static readonly FieldInfo iarCharsFieldInfo = typeof(XMRInstArrays).GetField("iarChars");

        public TokenTypeChar(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeChar(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(char);
        }
        public override string ToString()
        {
            return "char";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarCharsFieldInfo;
            declVar.vTableIndex = arSizes.iasChars++;
        }
    }
    public class TokenTypeExc: TokenType
    {
        private static readonly FieldInfo iarObjectsFieldInfo = typeof(XMRInstArrays).GetField("iarObjects");

        public TokenTypeExc(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeExc(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(Exception);
        }
        public override string ToString()
        {
            return "exception";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarObjectsFieldInfo;
            declVar.vTableIndex = arSizes.iasObjects++;
        }
    }
    public class TokenTypeFloat: TokenType
    {
        private static readonly FieldInfo iarFloatsFieldInfo = typeof(XMRInstArrays).GetField("iarFloats");

        public TokenTypeFloat(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeFloat(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(double);
        }
        public override string ToString()
        {
            return "float";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarFloatsFieldInfo;
            declVar.vTableIndex = arSizes.iasFloats++;
        }
    }
    public class TokenTypeInt: TokenType
    {
        private static readonly FieldInfo iarIntegersFieldInfo = typeof(XMRInstArrays).GetField("iarIntegers");

        public TokenTypeInt(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeInt(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(int);
        }
        public override string ToString()
        {
            return "integer";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarIntegersFieldInfo;
            declVar.vTableIndex = arSizes.iasIntegers++;
        }
    }
    public class TokenTypeKey: TokenType
    {
        private static readonly FieldInfo iarStringsFieldInfo = typeof(XMRInstArrays).GetField("iarStrings");

        public TokenTypeKey(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeKey(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(string);
        }
        public override string ToString()
        {
            return "key";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarStringsFieldInfo;
            declVar.vTableIndex = arSizes.iasStrings++;
        }
    }
    public class TokenTypeList: TokenType
    {
        private static readonly FieldInfo iarListsFieldInfo = typeof(XMRInstArrays).GetField("iarLists");
        private static readonly ConstructorInfo htListCtor = typeof(HeapTrackerList).GetConstructor(new Type[] { typeof(XMRInstAbstract) });

        public TokenTypeList(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeList(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(LSL_List);
        }
        public override string ToString()
        {
            return "list";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarListsFieldInfo;
            declVar.vTableIndex = arSizes.iasLists++;
        }
        public override Type ToHeapTrackerType()
        {
            return typeof(HeapTrackerList);
        }
        public override ConstructorInfo GetHeapTrackerCtor()
        {
            return htListCtor;
        }
        public override void CallHeapTrackerPopMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            HeapTrackerList.GenPop(errorAt, ilGen);
        }
        public override void CallHeapTrackerPushMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            HeapTrackerList.GenPush(errorAt, ilGen);
        }
    }
    public class TokenTypeObject: TokenType
    {
        private static readonly FieldInfo iarObjectsFieldInfo = typeof(XMRInstArrays).GetField("iarObjects");
        private static readonly ConstructorInfo htObjectCtor = typeof(HeapTrackerObject).GetConstructor(new Type[] { typeof(XMRInstAbstract) });

        public TokenTypeObject(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeObject(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(object);
        }
        public override string ToString()
        {
            return "object";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarObjectsFieldInfo;
            declVar.vTableIndex = arSizes.iasObjects++;
        }
        public override Type ToHeapTrackerType()
        {
            return typeof(HeapTrackerObject);
        }
        public override ConstructorInfo GetHeapTrackerCtor()
        {
            return htObjectCtor;
        }
        public override void CallHeapTrackerPopMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            HeapTrackerObject.GenPop(errorAt, ilGen);
        }
        public override void CallHeapTrackerPushMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            HeapTrackerObject.GenPush(errorAt, ilGen);
        }
    }
    public class TokenTypeRot: TokenType
    {
        private static readonly FieldInfo iarRotationsFieldInfo = typeof(XMRInstArrays).GetField("iarRotations");

        public TokenTypeRot(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeRot(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(LSL_Rotation);
        }
        public override string ToString()
        {
            return "rotation";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarRotationsFieldInfo;
            declVar.vTableIndex = arSizes.iasRotations++;
        }
    }
    public class TokenTypeStr: TokenType
    {
        private static readonly FieldInfo iarStringsFieldInfo = typeof(XMRInstArrays).GetField("iarStrings");
        private static readonly ConstructorInfo htStringCtor = typeof(HeapTrackerString).GetConstructor(new Type[] { typeof(XMRInstAbstract) });

        public TokenTypeStr(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeStr(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(string);
        }
        public override string ToString()
        {
            return "string";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarStringsFieldInfo;
            declVar.vTableIndex = arSizes.iasStrings++;
        }
        public override Type ToHeapTrackerType()
        {
            return typeof(HeapTrackerString);
        }
        public override ConstructorInfo GetHeapTrackerCtor()
        {
            return htStringCtor;
        }
        public override void CallHeapTrackerPopMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            HeapTrackerString.GenPop(errorAt, ilGen);
        }
        public override void CallHeapTrackerPushMeth(Token errorAt, ScriptMyILGen ilGen)
        {
            HeapTrackerString.GenPush(errorAt, ilGen);
        }
    }
    public class TokenTypeUndef: TokenType
    {  // for the 'undef' constant, ie, null object pointer
        public TokenTypeUndef(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeUndef(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(object);
        }
        public override string ToString()
        {
            return "undef";
        }
    }
    public class TokenTypeVec: TokenType
    {
        private static readonly FieldInfo iarVectorsFieldInfo = typeof(XMRInstArrays).GetField("iarVectors");

        public TokenTypeVec(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeVec(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(LSL_Vector);
        }
        public override string ToString()
        {
            return "vector";
        }
        public override void AssignVarSlot(TokenDeclVar declVar, XMRInstArSizes arSizes)
        {
            declVar.vTableArray = iarVectorsFieldInfo;
            declVar.vTableIndex = arSizes.iasVectors++;
        }
    }
    public class TokenTypeVoid: TokenType
    {  // used only for function/method return types
        public TokenTypeVoid(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeVoid(Token original) : base(original) { }
        public override Type ToSysType()
        {
            return typeof(void);
        }
        public override string ToString()
        {
            return "void";
        }
    }

    public class TokenTypeLSLFloat: TokenTypeFloat
    {
        public TokenTypeLSLFloat(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeLSLFloat(Token original) : base(original) { }
        public override Type ToLSLWrapType()
        {
            return typeof(LSL_Float);
        }
    }
    public class TokenTypeLSLInt: TokenTypeInt
    {
        public TokenTypeLSLInt(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeLSLInt(Token original) : base(original) { }
        public override Type ToLSLWrapType()
        {
            return typeof(LSL_Integer);
        }
    }
    public class TokenTypeLSLKey: TokenTypeKey
    {
        public TokenTypeLSLKey(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeLSLKey(Token original) : base(original) { }
        public override Type ToLSLWrapType()
        {
            return typeof(LSL_Key);
        }
    }
    public class TokenTypeLSLString: TokenTypeStr
    {
        public TokenTypeLSLString(TokenErrorMessage emsg, string file, int line, int posn) : base(emsg, file, line, posn) { }
        public TokenTypeLSLString(Token original) : base(original) { }
        public override Type ToLSLWrapType()
        {
            return typeof(LSL_String);
        }
    }
}
