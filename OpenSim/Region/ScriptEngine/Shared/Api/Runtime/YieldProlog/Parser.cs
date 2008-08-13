/*
 * Copyright (C) 2007-2008, Jeff Thompson
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the copyright holder nor the names of its contributors
 *       may be used to endorse or promote products derived from this software
 *       without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;

        // disable warning on l1, don't see how we can
        // code this differently
        #pragma warning disable 0168, 0219, 0162

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
{
    public class Parser
    {
        public static IEnumerable<bool> read_term2(object Term, object Options)
        {
            Variable Answer = new Variable();
            Variable Variables = new Variable();
            foreach (bool l1 in read_termOptions(Options, Variables))
            {
                foreach (bool l2 in portable_read3(Answer, Variables, new Variable()))
                {
                    foreach (bool l3 in remove_pos(Answer, Term))
                        yield return false;
                }
            }
        }

        public static IEnumerable<bool> read_term3(object Input, object Term, object Options)
        {
            Variable SaveInput = new Variable();
            Variable Answer = new Variable();
            Variable Variables = new Variable();
            foreach (bool l1 in read_termOptions(Options, Variables))
            {
                foreach (bool l2 in YP.current_input(SaveInput))
                {
                    try
                    {
                        YP.see(Input);
                        foreach (bool l3 in portable_read3(Answer, Variables, new Variable()))
                        {
                            foreach (bool l4 in remove_pos(Answer, Term))
                                yield return false;
                        }
                    }
                    finally
                    {
                        YP.see(SaveInput);
                    }
                }
            }
        }

        /// <summary>
        /// For read_term, check if Options has variable_names(Variables).
        /// Otherwise, ignore Options.
        /// </summary>
        /// <param name="Options"></param>
        /// <param name="Variables"></param>
        /// <returns></returns>
        private static IEnumerable<bool> read_termOptions(object Options, object Variables)
        {
            Options = YP.getValue(Options);
            if (Options is Variable)
                throw new PrologException(Atom.a("instantiation_error"), "Options is an unbound variable");
            // First try to match Options = [variable_names(Variables)]
            foreach (bool l1 in YP.unify(Options, ListPair.make(new Functor1("variable_names", Variables))))
            {
                yield return false;
                yield break;
            }
            // Default: Ignore Options.
            yield return false;
        }

        public static IEnumerable<bool> read1(object Term)
        {
            return read_term2(Term, Atom.NIL);
        }

        public static IEnumerable<bool> read2(object Input, object Term)
        {
            return read_term3(Input, Term, Atom.NIL);
        }

        public static IEnumerable<bool> formatError(object Output, object Format, object Arguments)
        {
            // Debug: Simple implementation for now.
            YP.write(Format);
            YP.write(Arguments);
            YP.nl();
            yield return false;
        }


        // Debug: Hand-modify this central predicate to do tail recursion.
        public static IEnumerable<bool> read_tokens(object arg1, object arg2, object arg3)
        {
            bool repeat = true;
            while (repeat)
            {
                repeat = false;
                {
                    object C1 = arg1;
                    object Dict = arg2;
                    object Tokens = arg3;
                    Variable C2 = new Variable();
                    if (YP.lessThanOrEqual(C1, new ListPair(32, Atom.NIL)))
                    {
                        if (YP.greaterThanOrEqual(C1, 0))
                        {
                            foreach (bool l4 in YP.get_code(C2))
                            {
#if false
                                foreach (bool l5 in read_tokens(C2, Dict, Tokens))
                                {
                                    yield return false;
                                }
#endif
                                arg1 = YP.getValue(C2);
                                arg2 = YP.getValue(Dict);
                                arg3 = YP.getValue(Tokens);
                                repeat = true;
                            }
                        }
                        goto cutIf1;
                    }
                    if (YP.greaterThanOrEqual(C1, new ListPair(97, Atom.NIL)))
                    {
                        if (YP.lessThanOrEqual(C1, new ListPair(122, Atom.NIL)))
                        {
                            foreach (bool l4 in read_identifier(C1, Dict, Tokens))
                            {
                                yield return false;
                            }
                            goto cutIf2;
                        }
                    }
                    if (YP.greaterThanOrEqual(C1, new ListPair(65, Atom.NIL)))
                    {
                        if (YP.lessThanOrEqual(C1, new ListPair(90, Atom.NIL)))
                        {
                            foreach (bool l4 in read_variable(C1, Dict, Tokens))
                            {
                                yield return false;
                            }
                            goto cutIf3;
                        }
                    }
                    if (YP.greaterThanOrEqual(C1, new ListPair(48, Atom.NIL)))
                    {
                        if (YP.lessThanOrEqual(C1, new ListPair(57, Atom.NIL)))
                        {
                            foreach (bool l4 in read_number(C1, Dict, Tokens))
                            {
                                yield return false;
                            }
                            goto cutIf4;
                        }
                    }
                    if (YP.lessThan(C1, 127))
                    {
                        foreach (bool l3 in read_special(C1, Dict, Tokens))
                        {
                            yield return false;
                        }
                        goto cutIf5;
                    }
                    if (YP.lessThanOrEqual(C1, 160))
                    {
                        foreach (bool l3 in YP.get_code(C2))
                        {
#if false
                            foreach (bool l4 in read_tokens(C2, Dict, Tokens))
                            {
                                yield return false;
                            }
#endif
                            arg1 = YP.getValue(C2);
                            arg2 = YP.getValue(Dict);
                            arg3 = YP.getValue(Tokens);
                            repeat = true;
                        }
                        goto cutIf6;
                    }
                    if (YP.greaterThanOrEqual(C1, 223))
                    {
                        if (YP.notEqual(C1, 247))
                        {
                            foreach (bool l4 in read_identifier(C1, Dict, Tokens))
                            {
                                yield return false;
                            }
                            goto cutIf7;
                        }
                    }
                    if (YP.greaterThanOrEqual(C1, 192))
                    {
                        if (YP.notEqual(C1, 215))
                        {
                            foreach (bool l4 in read_variable(C1, Dict, Tokens))
                            {
                                yield return false;
                            }
                            goto cutIf8;
                        }
                    }
                    if (YP.notEqual(C1, 170))
                    {
                        if (YP.notEqual(C1, 186))
                        {
                            foreach (bool l4 in read_symbol(C1, Dict, Tokens))
                            {
                                yield return false;
                            }
                            goto cutIf9;
                        }
                    }
                    foreach (bool l2 in read_identifier(C1, Dict, Tokens))
                    {
                        yield return false;
                    }
                cutIf9:
                cutIf8:
                cutIf7:
                cutIf6:
                cutIf5:
                cutIf4:
                cutIf3:
                cutIf2:
                cutIf1:
                    { }
                }
            }
        }

        // Compiler output follows.

        class YPInnerClass { }
        // static Type getDeclaringClass() { return typeof(YPInnerClass).DeclaringType; }

        public static IEnumerable<bool> parseInput(object TermList)
        {
            {
                Variable TermAndVariables = new Variable();
                FindallAnswers findallAnswers1 = new FindallAnswers(TermAndVariables);
                foreach (bool l2 in parseInputHelper(TermAndVariables))
                {
                    findallAnswers1.add();
                }
                foreach (bool l2 in findallAnswers1.result(TermList))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> parseInputHelper(object arg1)
        {
            {
                Variable Term = new Variable();
                Variable Variables = new Variable();
                Variable Answer = new Variable();
                Variable x4 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("f", Term, Variables)))
                {
                    foreach (bool l3 in YP.repeat())
                    {
                        foreach (bool l4 in portable_read3(Answer, Variables, x4))
                        {
                            foreach (bool l5 in remove_pos(Answer, Term))
                            {
                                if (YP.termEqual(Term, Atom.a("end_of_file")))
                                {
                                    yield break;
                                    goto cutIf1;
                                }
                                yield return false;
                            cutIf1:
                                { }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> clear_errors()
        {
            {
                yield return false;
            }
        }

        public static IEnumerable<bool> remove_pos(object arg1, object arg2)
        {
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, X))
                {
                    foreach (bool l3 in YP.unify(arg2, X))
                    {
                        if (YP.var(X))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object X = arg2;
                Variable _Pos = new Variable();
                Variable _Name = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("$VAR", _Pos, _Name, X)))
                {
                    if (YP.var(X))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                Variable H = new Variable();
                Variable T = new Variable();
                Variable NH = new Variable();
                Variable NT = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(H, T)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(NH, NT)))
                    {
                        foreach (bool l4 in remove_pos(H, NH))
                        {
                            foreach (bool l5 in remove_pos(T, NT))
                            {
                                yield return false;
                            }
                        }
                        yield break;
                    }
                }
            }
            {
                Variable A = new Variable();
                Variable B = new Variable();
                Variable NA = new Variable();
                Variable NB = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", A, B)))
                {
                    foreach (bool l3 in YP.unify(arg2, new Functor2(",", NA, NB)))
                    {
                        foreach (bool l4 in remove_pos(A, NA))
                        {
                            foreach (bool l5 in remove_pos(B, NB))
                            {
                                yield return false;
                            }
                        }
                        yield break;
                    }
                }
            }
            {
                Variable Atom_1 = new Variable();
                Variable _F = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom_1))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom_1))
                    {
                        foreach (bool l4 in YP.functor(Atom_1, _F, 0))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                object Term = arg1;
                object NewTerm = arg2;
                Variable Func = new Variable();
                Variable _Pos = new Variable();
                Variable Args = new Variable();
                Variable NArgs = new Variable();
                if (YP.nonvar(Term))
                {
                    foreach (bool l3 in YP.univ(Term, new ListPair(Func, new ListPair(_Pos, Args))))
                    {
                        foreach (bool l4 in remove_pos(Args, NArgs))
                        {
                            foreach (bool l5 in YP.univ(NewTerm, new ListPair(Func, NArgs)))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> portable_read_position(object Term, object PosTerm, object Syntax)
        {
            {
                foreach (bool l2 in portable_read(PosTerm, Syntax))
                {
                    foreach (bool l3 in remove_pos(PosTerm, Term))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> portable_read(object Answer, object Syntax)
        {
            {
                Variable Tokens = new Variable();
                Variable ParseTokens = new Variable();
                foreach (bool l2 in read_tokens1(Tokens))
                {
                    foreach (bool l3 in remove_comments(Tokens, ParseTokens, Syntax))
                    {
                        foreach (bool l4 in parse2(ParseTokens, Answer))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> portable_read3(object Answer, object Variables, object Syntax)
        {
            {
                Variable Tokens = new Variable();
                Variable ParseTokens = new Variable();
                foreach (bool l2 in read_tokens2(Tokens, Variables))
                {
                    foreach (bool l3 in remove_comments(Tokens, ParseTokens, Syntax))
                    {
                        foreach (bool l4 in parse2(ParseTokens, Answer))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> remove_comments(object arg1, object arg2, object arg3)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.NIL))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.NIL))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                object Ys = arg2;
                Variable S = new Variable();
                Variable E = new Variable();
                Variable Xs = new Variable();
                Variable Zs = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("comment", S, E), Xs)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("comment", S, E), Zs)))
                    {
                        foreach (bool l4 in remove_comments(Xs, Ys, Zs))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Xs = new Variable();
                Variable Ys = new Variable();
                Variable Pos2 = new Variable();
                Variable Zs = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("/", Atom.a("["), Pos), Xs)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Atom.a("["), Ys)))
                    {
                        foreach (bool l4 in YP.unify(arg3, new ListPair(new Functor2("list", Pos, Pos2), Zs)))
                        {
                            foreach (bool l5 in YP.unify(Pos2, YP.add(Pos, 1)))
                            {
                                foreach (bool l6 in remove_comments(Xs, Ys, Zs))
                                {
                                    yield return false;
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Xs = new Variable();
                Variable Ys = new Variable();
                Variable Pos2 = new Variable();
                Variable Zs = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("/", Atom.a("]"), Pos), Xs)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Atom.a("]"), Ys)))
                    {
                        foreach (bool l4 in YP.unify(arg3, new ListPair(new Functor2("list", Pos, Pos2), Zs)))
                        {
                            foreach (bool l5 in YP.unify(Pos2, YP.add(Pos, 1)))
                            {
                                foreach (bool l6 in remove_comments(Xs, Ys, Zs))
                                {
                                    yield return false;
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object Zs = arg3;
                Variable Token = new Variable();
                Variable Xs = new Variable();
                Variable Ys = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, Xs)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Token, Ys)))
                    {
                        foreach (bool l4 in remove_comments(Xs, Ys, Zs))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> expect(object Token, object arg2, object arg3)
        {
            {
                object Rest = arg3;
                foreach (bool l2 in YP.unify(arg2, new ListPair(Token, Rest)))
                {
                    yield return true;
                    yield break;
                }
            }
            {
                object S0 = arg2;
                object x3 = arg3;
                foreach (bool l2 in syntax_error(ListPair.make(new object[] { Token, Atom.a("or"), Atom.a("operator"), Atom.a("expected") }), S0))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> parse2(object Tokens, object Answer)
        {
            {
                Variable Term = new Variable();
                Variable LeftOver = new Variable();
                foreach (bool l2 in clear_errors())
                {
                    foreach (bool l3 in parse(Tokens, 1200, Term, LeftOver))
                    {
                        foreach (bool l4 in all_read(LeftOver))
                        {
                            foreach (bool l5 in YP.unify(Answer, Term))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                    }
                    foreach (bool l3 in syntax_error(Tokens))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> all_read(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    yield return false;
                }
            }
            {
                Variable Token = new Variable();
                Variable S = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, S)))
                {
                    foreach (bool l3 in syntax_error(ListPair.make(new object[] { Atom.a("operator"), Atom.a("expected"), Atom.a("after"), Atom.a("expression") }), new ListPair(Token, S)))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> parse(object arg1, object arg2, object arg3, object arg4)
        {
            {
                object x1 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in syntax_error(new ListPair(Atom.a("expression"), new ListPair(Atom.a("expected"), Atom.NIL)), Atom.NIL))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Precedence = arg2;
                object Term = arg3;
                object LeftOver = arg4;
                Variable Token = new Variable();
                Variable RestTokens = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, RestTokens)))
                {
                    foreach (bool l3 in parse5(Token, RestTokens, Precedence, Term, LeftOver))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> parse5(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            {
                object S0 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                foreach (bool l2 in YP.unify(arg1, Atom.a("}")))
                {
                    foreach (bool l3 in cannot_start(Atom.a("}"), S0))
                    {
                        yield return false;
                    }
                }
            }
            {
                object S0 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                foreach (bool l2 in YP.unify(arg1, Atom.a("]")))
                {
                    foreach (bool l3 in cannot_start(Atom.a("]"), S0))
                    {
                        yield return false;
                    }
                }
            }
            {
                object S0 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                foreach (bool l2 in YP.unify(arg1, Atom.a(")")))
                {
                    foreach (bool l3 in cannot_start(Atom.a(")"), S0))
                    {
                        yield return false;
                    }
                }
            }
            {
                object S0 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                foreach (bool l2 in YP.unify(arg1, Atom.a(",")))
                {
                    foreach (bool l3 in cannot_start(Atom.a(","), S0))
                    {
                        yield return false;
                    }
                }
            }
            {
                object S0 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                foreach (bool l2 in YP.unify(arg1, Atom.a("|")))
                {
                    foreach (bool l3 in cannot_start(Atom.a("|"), S0))
                    {
                        yield return false;
                    }
                }
            }
            {
                object S0 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Codes = new Variable();
                Variable Term = new Variable();
                Variable A = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("string", Codes)))
                {
                    foreach (bool l3 in YP.current_prolog_flag(Atom.a("double_quotes"), Atom.a("atom")))
                    {
                        foreach (bool l4 in YP.atom_codes(Term, Codes))
                        {
                            foreach (bool l5 in exprtl0(S0, Term, Precedence, Answer, S))
                            {
                                yield return false;
                            }
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in YP.current_prolog_flag(Atom.a("double_quotes"), Atom.a("chars")))
                    {
                        foreach (bool l4 in YP.atom_codes(A, Codes))
                        {
                            foreach (bool l5 in YP.atom_chars(A, Term))
                {
                                foreach (bool l6 in exprtl0(S0, Term, Precedence, Answer, S))
                    {
                        yield return false;
                    }
                }
            }
                        goto cutIf2;
                    }
                    foreach (bool l3 in YP.unify(Term, Codes))
                {
                        foreach (bool l4 in exprtl0(S0, Term, Precedence, Answer, S))
                    {
                        yield return false;
                    }
                }
                cutIf2:
                cutIf1:
                    { }
                }
            }
            {
                object S0 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Number = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("number", Number)))
                {
                    foreach (bool l3 in exprtl0(S0, Number, Precedence, Answer, S))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("[")))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Atom.a("]"), S1)))
                    {
                        foreach (bool l4 in read_atom(new Functor2("/", Atom.NIL, 0), S1, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object S1 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Arg1 = new Variable();
                Variable S2 = new Variable();
                Variable RestArgs = new Variable();
                Variable S3 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("[")))
                {
                    foreach (bool l3 in parse(S1, 999, Arg1, S2))
                    {
                        foreach (bool l4 in read_list(S2, RestArgs, S3))
                        {
                            foreach (bool l5 in exprtl0(S3, new ListPair(Arg1, RestArgs), Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object S1 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Term = new Variable();
                Variable S2 = new Variable();
                Variable S3 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("(")))
                {
                    foreach (bool l3 in parse(S1, 1200, Term, S2))
                    {
                        foreach (bool l4 in expect(Atom.a(")"), S2, S3))
                        {
                            foreach (bool l5 in exprtl0(S3, Term, Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object S1 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Term = new Variable();
                Variable S2 = new Variable();
                Variable S3 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a(" (")))
                {
                    foreach (bool l3 in parse(S1, 1200, Term, S2))
                    {
                        foreach (bool l4 in expect(Atom.a(")"), S2, S3))
                        {
                            foreach (bool l5 in exprtl0(S3, Term, Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable _Pos = new Variable();
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", Atom.a("{"), _Pos)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Atom.a("}"), S1)))
                    {
                        foreach (bool l4 in read_atom(Atom.a("{}"), S1, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object S1 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Pos = new Variable();
                Variable Term = new Variable();
                Variable S2 = new Variable();
                Variable S3 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", Atom.a("{"), Pos)))
                {
                    foreach (bool l3 in parse(S1, 1200, Term, S2))
                    {
                        foreach (bool l4 in expect(Atom.a("}"), S2, S3))
                        {
                            foreach (bool l5 in exprtl0(S3, new Functor2("{}", Pos, Term), Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Variable_1 = new Variable();
                Variable Name = new Variable();
                Variable Pos = new Variable();
                Variable S1 = new Variable();
                Variable Arg1 = new Variable();
                Variable S2 = new Variable();
                Variable RestArgs = new Variable();
                Variable S3 = new Variable();
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("var", Variable_1, Name, Pos)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Atom.a("("), S1)))
                    {
                        foreach (bool l4 in parse(S1, 999, Arg1, S2))
                        {
                            foreach (bool l5 in read_args(S2, RestArgs, S3))
                            {
                                foreach (bool l6 in YP.univ(Term, new ListPair(Atom.a("call"), new ListPair(new Functor3("$VAR", Pos, Name, Variable_1), new ListPair(Arg1, RestArgs)))))
                                {
                                    foreach (bool l7 in exprtl0(S3, Term, Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                                yield break;
                            }
                        }
                        yield break;
                    }
                }
            }
            {
                object S0 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Variable_1 = new Variable();
                Variable Name = new Variable();
                Variable Pos = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("var", Variable_1, Name, Pos)))
                {
                    foreach (bool l3 in exprtl0(S0, new Functor3("$VAR", Pos, Name, Variable_1), Precedence, Answer, S))
                    {
                        yield return false;
                    }
                }
            }
            {
                object S0 = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Atom_1 = new Variable();
                Variable P = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("atom", Atom_1, P)))
                {
                    foreach (bool l3 in read_atom(new Functor2("/", Atom_1, P), S0, Precedence, Answer, S))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> read_atom(object arg1, object arg2, object Precedence, object Answer, object S)
        {
            {
                Variable _Pos = new Variable();
                Variable Number = new Variable();
                Variable S1 = new Variable();
                Variable Negative = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", Atom.a("-"), _Pos)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(new Functor1("number", Number), S1)))
                    {
                        foreach (bool l4 in YP.unify(Negative, YP.negate(Number)))
                        {
                            foreach (bool l5 in exprtl0(S1, Negative, Precedence, Answer, S))
                            {
                                yield return false;
                            }
                        }
                        yield break;
                    }
                }
            }
            {
                Variable Functor_1 = new Variable();
                Variable Pos = new Variable();
                Variable S1 = new Variable();
                Variable Arg1 = new Variable();
                Variable S2 = new Variable();
                Variable RestArgs = new Variable();
                Variable S3 = new Variable();
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", Functor_1, Pos)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Atom.a("("), S1)))
                    {
                        foreach (bool l4 in parse(S1, 999, Arg1, S2))
                        {
                            foreach (bool l5 in read_args(S2, RestArgs, S3))
                            {
                                foreach (bool l6 in YP.univ(Term, new ListPair(Functor_1, new ListPair(Pos, new ListPair(Arg1, RestArgs)))))
                                {
                                    foreach (bool l7 in exprtl0(S3, Term, Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                                yield break;
                            }
                        }
                        yield break;
                    }
                }
            }
            {
                object S0 = arg2;
                Variable Op = new Variable();
                Variable Pos = new Variable();
                Variable Oprec = new Variable();
                Variable Aprec = new Variable();
                Variable Flag = new Variable();
                Variable Term = new Variable();
                Variable Arg = new Variable();
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", Op, Pos)))
                {
                    foreach (bool l3 in prefixop(Op, Oprec, Aprec))
                    {
                        foreach (bool l4 in possible_right_operand(S0, Flag))
                        {
                            if (YP.lessThan(Flag, 0))
                            {
                                foreach (bool l6 in YP.univ(Term, new ListPair(Op, new ListPair(Pos, Atom.NIL))))
                                {
                                    foreach (bool l7 in exprtl0(S0, Term, Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                                goto cutIf1;
                            }
                            if (YP.greaterThan(Oprec, Precedence))
                            {
                                foreach (bool l6 in syntax_error(ListPair.make(new object[] { Atom.a("prefix"), Atom.a("operator"), Op, Atom.a("in"), Atom.a("context"), Atom.a("with"), Atom.a("precedence"), Precedence }), S0))
                                {
                                    yield return false;
                                }
                                goto cutIf2;
                            }
                            if (YP.greaterThan(Flag, 0))
                            {
                                foreach (bool l6 in parse(S0, Aprec, Arg, S1))
                                {
                                    foreach (bool l7 in YP.univ(Term, ListPair.make(new object[] { Op, Pos, Arg })))
                                    {
                                        foreach (bool l8 in exprtl(S1, Oprec, Term, Precedence, Answer, S))
                                        {
                                            yield return false;
                                        }
                                    }
                                    yield break;
                                }
                                goto cutIf3;
                            }
                            foreach (bool l5 in peepop(S0, S1))
                            {
                                foreach (bool l6 in prefix_is_atom(S1, Oprec))
                                {
                                    foreach (bool l7 in exprtl(S1, Oprec, new Functor2("/", Op, Pos), Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                            }
                            foreach (bool l5 in parse(S0, Aprec, Arg, S1))
                            {
                                foreach (bool l6 in YP.univ(Term, ListPair.make(new object[] { Op, Pos, Arg })))
                                {
                                    foreach (bool l7 in exprtl(S1, Oprec, Term, Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                                yield break;
                            }
                        cutIf3:
                        cutIf2:
                        cutIf1:
                            { }
                        }
                        yield break;
                    }
                }
            }
            {
                object S0 = arg2;
                Variable Atom_1 = new Variable();
                Variable Pos = new Variable();
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", Atom_1, Pos)))
                {
                    foreach (bool l3 in YP.univ(Term, new ListPair(Atom_1, new ListPair(Pos, Atom.NIL))))
                    {
                        foreach (bool l4 in exprtl0(S0, Term, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> cannot_start(object Token, object S0)
        {
            {
                foreach (bool l2 in syntax_error(ListPair.make(new object[] { Token, Atom.a("cannot"), Atom.a("start"), Atom.a("an"), Atom.a("expression") }), S0))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> read_args(object arg1, object arg2, object arg3)
        {
            {
                object S = arg3;
                Variable S1 = new Variable();
                Variable Term = new Variable();
                Variable Rest = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a(","), S1)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(Term, Rest)))
                    {
                        foreach (bool l4 in parse(S1, 999, Term, S2))
                        {
                            foreach (bool l5 in read_args(S2, Rest, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                        yield break;
                    }
                }
            }
            {
                object S = arg3;
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a(")"), S)))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object S = arg1;
                object x2 = arg2;
                object x3 = arg3;
                foreach (bool l2 in syntax_error(ListPair.make(new object[] { Atom.a(", or )"), Atom.a("expected"), Atom.a("in"), Atom.a("arguments") }), S))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> read_list(object arg1, object arg2, object arg3)
        {
            {
                object x1 = arg2;
                object x2 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in syntax_error(ListPair.make(new object[] { Atom.a(", | or ]"), Atom.a("expected"), Atom.a("in"), Atom.a("list") }), Atom.NIL))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Rest = arg2;
                object S = arg3;
                Variable Token = new Variable();
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, S1)))
                {
                    foreach (bool l3 in read_list4(Token, S1, Rest, S))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> read_list4(object arg1, object arg2, object arg3, object arg4)
        {
            {
                object S1 = arg2;
                object S = arg4;
                Variable Term = new Variable();
                Variable Rest = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a(",")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Term, Rest)))
                    {
                        foreach (bool l4 in parse(S1, 999, Term, S2))
                        {
                            foreach (bool l5 in read_list(S2, Rest, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                        yield break;
                    }
                }
            }
            {
                object S1 = arg2;
                object Rest = arg3;
                object S = arg4;
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("|")))
                {
                    foreach (bool l3 in parse(S1, 999, Rest, S2))
                    {
                        foreach (bool l4 in expect(Atom.a("]"), S2, S))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                    yield break;
                }
            }
            {
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("]")))
                {
                    foreach (bool l3 in YP.unify(arg2, S1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.NIL))
                        {
                            foreach (bool l5 in YP.unify(arg4, S1))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object Token = arg1;
                object S1 = arg2;
                object x3 = arg3;
                object x4 = arg4;
                foreach (bool l2 in syntax_error(ListPair.make(new object[] { Atom.a(", | or ]"), Atom.a("expected"), Atom.a("in"), Atom.a("list") }), new ListPair(Token, S1)))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> possible_right_operand(object arg1, object arg2)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Flag = arg2;
                Variable H = new Variable();
                Variable T = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(H, T)))
                {
                    foreach (bool l3 in possible_right_operand3(H, Flag, T))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> possible_right_operand3(object arg1, object arg2, object arg3)
        {
            {
                object x4 = arg3;
                Variable x1 = new Variable();
                Variable x2 = new Variable();
                Variable x3 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("var", x1, x2, x3)))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x2 = arg3;
                Variable x1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("number", x1)))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x2 = arg3;
                Variable x1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("string", x1)))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a(" (")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a("(")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a(")")))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                Variable x1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("[")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        foreach (bool l4 in YP.unify(arg3, new ListPair(Atom.a("]"), x1)))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a("[")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a("]")))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                Variable x1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("{")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        foreach (bool l4 in YP.unify(arg3, new ListPair(Atom.a("}"), x1)))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a("{")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a("}")))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a(",")))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.a("|")))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x3 = arg3;
                Variable x1 = new Variable();
                Variable x2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("atom", x1, x2)))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> peepop(object arg1, object arg2)
        {
            {
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("atom", F, Pos), new ListPair(Atom.a("("), S1))))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(new Functor2("atom", F, Pos), new ListPair(Atom.a("("), S1))))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable S1 = new Variable();
                Variable L = new Variable();
                Variable P = new Variable();
                Variable R = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("atom", F, Pos), S1)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(new Functor(Atom.a("infixop", Atom.a("")), new object[] { new Functor2("/", F, Pos), L, P, R }), S1)))
                    {
                        foreach (bool l4 in infixop(F, L, P, R))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable S1 = new Variable();
                Variable L = new Variable();
                Variable P = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("atom", F, Pos), S1)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(new Functor3(Atom.a("postfixop", Atom.a("")), new Functor2("/", F, Pos), L, P), S1)))
                    {
                        foreach (bool l4 in postfixop(F, L, P))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                Variable S0 = new Variable();
                foreach (bool l2 in YP.unify(arg1, S0))
                {
                    foreach (bool l3 in YP.unify(arg2, S0))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> prefix_is_atom(object arg1, object arg2)
        {
            {
                object Precedence = arg2;
                Variable Token = new Variable();
                Variable x2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, x2)))
                {
                    foreach (bool l3 in prefix_is_atom(Token, Precedence))
                    {
                        yield return false;
                    }
                }
            }
            {
                object P = arg2;
                Variable x1 = new Variable();
                Variable L = new Variable();
                Variable x3 = new Variable();
                Variable x4 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor(Atom.a("infixop", Atom.a("")), new object[] { x1, L, x3, x4 })))
                {
                    if (YP.greaterThanOrEqual(L, P))
                    {
                        yield return false;
                    }
                }
            }
            {
                object P = arg2;
                Variable x1 = new Variable();
                Variable L = new Variable();
                Variable x3 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3(Atom.a("postfixop", Atom.a("")), x1, L, x3)))
                {
                    if (YP.greaterThanOrEqual(L, P))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a(")")))
                {
                    yield return false;
                }
            }
            {
                object x1 = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("]")))
                {
                    yield return false;
                }
            }
            {
                object x1 = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("}")))
                {
                    yield return false;
                }
            }
            {
                object P = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("|")))
                {
                    if (YP.greaterThanOrEqual(1100, P))
                    {
                        yield return false;
                    }
                }
            }
            {
                object P = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a(",")))
                {
                    if (YP.greaterThanOrEqual(1000, P))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> exprtl0(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            {
                object x2 = arg3;
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, Term))
                    {
                        foreach (bool l4 in YP.unify(arg4, Term))
                        {
                            foreach (bool l5 in YP.unify(arg5, Atom.NIL))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable Token = new Variable();
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, S1)))
                {
                    foreach (bool l3 in exprtl0_6(Token, Term, Precedence, Answer, S, S1))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> exprtl0_6(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
        {
            {
                object x2 = arg3;
                object S1 = arg6;
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("}")))
                {
                    foreach (bool l3 in YP.unify(arg2, Term))
                    {
                        foreach (bool l4 in YP.unify(arg4, Term))
                        {
                            foreach (bool l5 in YP.unify(arg5, new ListPair(Atom.a("}"), S1)))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object x2 = arg3;
                object S1 = arg6;
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("]")))
                {
                    foreach (bool l3 in YP.unify(arg2, Term))
                    {
                        foreach (bool l4 in YP.unify(arg4, Term))
                        {
                            foreach (bool l5 in YP.unify(arg5, new ListPair(Atom.a("]"), S1)))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object x2 = arg3;
                object S1 = arg6;
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a(")")))
                {
                    foreach (bool l3 in YP.unify(arg2, Term))
                    {
                        foreach (bool l4 in YP.unify(arg4, Term))
                        {
                            foreach (bool l5 in YP.unify(arg5, new ListPair(Atom.a(")"), S1)))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                object S1 = arg6;
                Variable Next = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a(",")))
                {
                    if (YP.greaterThanOrEqual(Precedence, 1000))
                    {
                        foreach (bool l4 in parse(S1, 1000, Next, S2))
                        {
                            foreach (bool l5 in exprtl(S2, 1000, new Functor2(",", Term, Next), Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in YP.unify(Answer, Term))
                    {
                        foreach (bool l4 in YP.unify(S, new ListPair(Atom.a(","), S1)))
                        {
                            yield return false;
                        }
                    }
                cutIf1:
                    { }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                object S1 = arg6;
                Variable Next = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("|")))
                {
                    if (YP.greaterThanOrEqual(Precedence, 1100))
                    {
                        foreach (bool l4 in parse(S1, 1100, Next, S2))
                        {
                            foreach (bool l5 in exprtl(S2, 1100, new Functor2(";", Term, Next), Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                        goto cutIf2;
                    }
                    foreach (bool l3 in YP.unify(Answer, Term))
                    {
                        foreach (bool l4 in YP.unify(S, new ListPair(Atom.a("|"), S1)))
                        {
                            yield return false;
                        }
                    }
                cutIf2:
                    { }
                }
            }
            {
                object x2 = arg2;
                object x3 = arg3;
                object x4 = arg4;
                object x5 = arg5;
                object S1 = arg6;
                Variable S = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("string", S)))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("chars"), new Functor1("string", S), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x2 = arg2;
                object x3 = arg3;
                object x4 = arg4;
                object x5 = arg5;
                object S1 = arg6;
                Variable N = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("number", N)))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("number"), new Functor1("number", N), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("{")))
                {
                    foreach (bool l3 in YP.unify(arg6, new ListPair(Atom.a("}"), S1)))
                    {
                        foreach (bool l4 in exprtl0_atom(Atom.a("{}"), Term, Precedence, Answer, S, S1))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object x1 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                object S1 = arg6;
                foreach (bool l2 in YP.unify(arg1, Atom.a("{")))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("brace"), Atom.a("{"), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable S1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("[")))
                {
                    foreach (bool l3 in YP.unify(arg6, new ListPair(Atom.a("]"), S1)))
                    {
                        foreach (bool l4 in exprtl0_atom(Atom.NIL, Term, Precedence, Answer, S, S1))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object x1 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                object S1 = arg6;
                foreach (bool l2 in YP.unify(arg1, Atom.a("[")))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("bracket"), Atom.a("["), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                object S1 = arg6;
                foreach (bool l2 in YP.unify(arg1, Atom.a("(")))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("parenthesis"), Atom.a("("), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x1 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                object x4 = arg5;
                object S1 = arg6;
                foreach (bool l2 in YP.unify(arg1, Atom.a(" (")))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("parenthesis"), Atom.a("("), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object x4 = arg2;
                object x5 = arg3;
                object x6 = arg4;
                object x7 = arg5;
                object S1 = arg6;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable P = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("var", A, B, P)))
                {
                    foreach (bool l3 in cannot_follow(Atom.a("variable"), new Functor3("var", A, B, P), S1))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                object S1 = arg6;
                Variable F = new Variable();
                Variable P = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("atom", F, P)))
                {
                    foreach (bool l3 in exprtl0_atom(new Functor2("/", F, P), Term, Precedence, Answer, S, S1))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> exprtl0_atom(object arg1, object arg2, object arg3, object arg4, object arg5, object S1)
        {
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable L1 = new Variable();
                Variable O1 = new Variable();
                Variable R1 = new Variable();
                Variable L2 = new Variable();
                Variable O2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", F, Pos)))
                {
                    foreach (bool l3 in ambigop(F, Precedence, L1, O1, R1, L2, O2))
                    {
                        foreach (bool l4 in prefix_is_atom(S1, Precedence))
                        {
                            foreach (bool l5 in exprtl(new ListPair(new Functor3(Atom.a("postfixop", Atom.a("")), new Functor2("/", F, Pos), L2, O2), S1), 0, Term, Precedence, Answer, S))
                            {
                                yield return false;
                            }
                            yield break;
                        }
                        foreach (bool l4 in exprtl(new ListPair(new Functor(Atom.a("infixop", Atom.a("")), new object[] { new Functor2("/", F, Pos), L1, O1, R1 }), S1), 0, Term, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                        foreach (bool l4 in exprtl(new ListPair(new Functor3(Atom.a("postfixop", Atom.a("")), new Functor2("/", F, Pos), L2, O2), S1), 0, Term, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable L1 = new Variable();
                Variable O1 = new Variable();
                Variable R1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", F, Pos)))
                {
                    foreach (bool l3 in infixop(F, L1, O1, R1))
                    {
                        foreach (bool l4 in exprtl(new ListPair(new Functor(Atom.a("infixop", Atom.a("")), new object[] { new Functor2("/", F, Pos), L1, O1, R1 }), S1), 0, Term, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object Term = arg2;
                object Precedence = arg3;
                object Answer = arg4;
                object S = arg5;
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable L2 = new Variable();
                Variable O2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("/", F, Pos)))
                {
                    foreach (bool l3 in postfixop(F, L2, O2))
                    {
                        foreach (bool l4 in exprtl(new ListPair(new Functor3(Atom.a("postfixop", Atom.a("")), new Functor2("/", F, Pos), L2, O2), S1), 0, Term, Precedence, Answer, S))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                object X = arg1;
                object x2 = arg2;
                object x3 = arg3;
                object x4 = arg4;
                object x5 = arg5;
                Variable x7 = new Variable();
                foreach (bool l2 in syntax_error(ListPair.make(new object[] { new Functor2("-", Atom.a("non"), Atom.a("operator")), X, Atom.a("follows"), Atom.a("expression") }), new ListPair(new Functor2("atom", X, x7), S1)))
                {
                    yield return false;
                }
                yield break;
            }
        }

        public static IEnumerable<bool> cannot_follow(object Type, object Token, object Tokens)
        {
            {
                foreach (bool l2 in syntax_error(ListPair.make(new object[] { Type, Atom.a("follows"), Atom.a("expression") }), new ListPair(Token, Tokens)))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> exprtl(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
        {
            {
                object x1 = arg2;
                object x3 = arg4;
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg3, Term))
                    {
                        foreach (bool l4 in YP.unify(arg5, Term))
                        {
                            foreach (bool l5 in YP.unify(arg6, Atom.NIL))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object C = arg2;
                object Term = arg3;
                object Precedence = arg4;
                object Answer = arg5;
                object S = arg6;
                Variable Token = new Variable();
                Variable Tokens = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Token, Tokens)))
                {
                    foreach (bool l3 in exprtl_7(Token, C, Term, Precedence, Answer, S, Tokens))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> exprtl_7(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
        {
            {
                object C = arg2;
                object Term = arg3;
                object Precedence = arg4;
                object Answer = arg5;
                object S = arg6;
                object S1 = arg7;
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable L = new Variable();
                Variable O = new Variable();
                Variable R = new Variable();
                Variable Other = new Variable();
                Variable S2 = new Variable();
                Variable Expr = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor(Atom.a("infixop", Atom.a("")), new object[] { new Functor2("/", F, Pos), L, O, R })))
                {
                    if (YP.greaterThanOrEqual(Precedence, O))
                    {
                        if (YP.lessThanOrEqual(C, L))
                        {
                            foreach (bool l5 in parse(S1, R, Other, S2))
                            {
                                foreach (bool l6 in YP.univ(Expr, ListPair.make(new object[] { F, Pos, Term, Other })))
                                {
                                    foreach (bool l7 in exprtl(S2, O, Expr, Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object C = arg2;
                object Term = arg3;
                object Precedence = arg4;
                object Answer = arg5;
                object S = arg6;
                object S1 = arg7;
                Variable F = new Variable();
                Variable Pos = new Variable();
                Variable L = new Variable();
                Variable O = new Variable();
                Variable Expr = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3(Atom.a("postfixop", Atom.a("")), new Functor2("/", F, Pos), L, O)))
                {
                    if (YP.greaterThanOrEqual(Precedence, O))
                    {
                        if (YP.lessThanOrEqual(C, L))
                        {
                            foreach (bool l5 in YP.univ(Expr, ListPair.make(new object[] { F, Pos, Term })))
                            {
                                foreach (bool l6 in peepop(S1, S2))
                                {
                                    foreach (bool l7 in exprtl(S2, O, Expr, Precedence, Answer, S))
                                    {
                                        yield return false;
                                    }
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object C = arg2;
                object Term = arg3;
                object Precedence = arg4;
                object Answer = arg5;
                object S = arg6;
                object S1 = arg7;
                Variable Next = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a(",")))
                {
                    if (YP.greaterThanOrEqual(Precedence, 1000))
                    {
                        if (YP.lessThan(C, 1000))
                        {
                            foreach (bool l5 in parse(S1, 1000, Next, S2))
                            {
                                foreach (bool l6 in exprtl(S2, 1000, new Functor2(",", Term, Next), Precedence, Answer, S))
                                {
                                    yield return false;
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object C = arg2;
                object Term = arg3;
                object Precedence = arg4;
                object Answer = arg5;
                object S = arg6;
                object S1 = arg7;
                Variable Next = new Variable();
                Variable S2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.a("|")))
                {
                    if (YP.greaterThanOrEqual(Precedence, 1100))
                    {
                        if (YP.lessThan(C, 1100))
                        {
                            foreach (bool l5 in parse(S1, 1100, Next, S2))
                            {
                                foreach (bool l6 in exprtl(S2, 1100, new Functor2(";", Term, Next), Precedence, Answer, S))
                                {
                                    yield return false;
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                object Token = arg1;
                object x2 = arg2;
                object x4 = arg4;
                object Tokens = arg7;
                Variable Term = new Variable();
                foreach (bool l2 in YP.unify(arg3, Term))
                {
                    foreach (bool l3 in YP.unify(arg5, Term))
                    {
                        foreach (bool l4 in YP.unify(arg6, new ListPair(Token, Tokens)))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> syntax_error(object _Message, object _List)
        {
            {
                yield break;
            }
            foreach (bool l1 in YP.fail())
            {
                yield return false;
            }
        }

        public static IEnumerable<bool> syntax_error(object _List)
        {
            {
                yield break;
            }
            foreach (bool l1 in YP.fail())
            {
                yield return false;
            }
        }

        public static IEnumerable<bool> prefixop(object F, object O, object Q)
        {
            {
                foreach (bool l2 in YP.current_op(O, Atom.a("fx"), F))
                {
                    foreach (bool l3 in YP.unify(Q, YP.subtract(O, 1)))
                    {
                        yield return false;
                    }
                    goto cutIf1;
                }
                foreach (bool l2 in YP.current_op(O, Atom.a("fy"), F))
                {
                    foreach (bool l3 in YP.unify(Q, O))
                    {
                        yield return false;
                    }
                    goto cutIf2;
                }
            cutIf2:
            cutIf1:
                { }
            }
        }

        public static IEnumerable<bool> postfixop(object F, object P, object O)
        {
            {
                foreach (bool l2 in YP.current_op(O, Atom.a("xf"), F))
                {
                    foreach (bool l3 in YP.unify(P, YP.subtract(O, 1)))
                    {
                        yield return false;
                    }
                    goto cutIf1;
                }
                foreach (bool l2 in YP.current_op(O, Atom.a("yf"), F))
                {
                    foreach (bool l3 in YP.unify(P, O))
                    {
                        yield return false;
                    }
                    goto cutIf2;
                }
            cutIf2:
            cutIf1:
                { }
            }
        }

        public static IEnumerable<bool> infixop(object F, object P, object O, object Q)
        {
            {
                foreach (bool l2 in YP.current_op(O, Atom.a("xfy"), F))
                {
                    foreach (bool l3 in YP.unify(P, YP.subtract(O, 1)))
                    {
                        foreach (bool l4 in YP.unify(Q, O))
                        {
                            yield return false;
                        }
                    }
                    goto cutIf1;
                }
                foreach (bool l2 in YP.current_op(O, Atom.a("xfx"), F))
                {
                    foreach (bool l3 in YP.unify(P, YP.subtract(O, 1)))
                    {
                        foreach (bool l4 in YP.unify(Q, P))
                        {
                            yield return false;
                        }
                    }
                    goto cutIf2;
                }
                foreach (bool l2 in YP.current_op(O, Atom.a("yfx"), F))
                {
                    foreach (bool l3 in YP.unify(Q, YP.subtract(O, 1)))
                    {
                        foreach (bool l4 in YP.unify(P, O))
                        {
                            yield return false;
                        }
                    }
                    goto cutIf3;
                }
            cutIf3:
            cutIf2:
            cutIf1:
                { }
            }
        }

        public static IEnumerable<bool> ambigop(object F, object Precedence, object L1, object O1, object R1, object L2, object O2)
        {
            {
                foreach (bool l2 in postfixop(F, L2, O2))
                {
                    if (YP.lessThanOrEqual(O2, Precedence))
                    {
                        foreach (bool l4 in infixop(F, L1, O1, R1))
                        {
                            if (YP.lessThanOrEqual(O1, Precedence))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_tokens1(object arg1)
        {
            {
                object TokenList = arg1;
                Variable C1 = new Variable();
                Variable _X = new Variable();
                Variable ListOfTokens = new Variable();
                foreach (bool l2 in YP.get_code(C1))
                {
                    foreach (bool l3 in read_tokens(C1, _X, ListOfTokens))
                    {
                        foreach (bool l4 in YP.unify(TokenList, ListOfTokens))
                        {
                            yield return false;
                        }
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("atom", Atom.a("end_of_file"), 0), Atom.NIL)))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> read_tokens2(object arg1, object arg2)
        {
            {
                object TokenList = arg1;
                object Dictionary = arg2;
                Variable C1 = new Variable();
                Variable Dict = new Variable();
                Variable ListOfTokens = new Variable();
                foreach (bool l2 in YP.get_code(C1))
                {
                    foreach (bool l3 in read_tokens(C1, Dict, ListOfTokens))
                    {
                        foreach (bool l4 in terminate_list(Dict))
                        {
                            foreach (bool l5 in YP.unify(Dictionary, Dict))
                            {
                                foreach (bool l6 in YP.unify(TokenList, ListOfTokens))
                                {
                                    yield return false;
                                }
                            }
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("atom", Atom.a("end_of_file"), 0), Atom.NIL)))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.NIL))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> terminate_list(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    yield return false;
                }
            }
            {
                Variable x1 = new Variable();
                Variable Tail = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(x1, Tail)))
                {
                    foreach (bool l3 in terminate_list(Tail))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> read_special(object arg1, object Dict, object arg3)
        {
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 95))
                {
                    foreach (bool l3 in read_variable(95, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 247))
                {
                    foreach (bool l3 in read_symbol(247, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 215))
                {
                    foreach (bool l3 in read_symbol(215, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                Variable StartPos = new Variable();
                Variable EndPos = new Variable();
                Variable Tokens = new Variable();
                Variable Ch = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 37))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("comment", StartPos, EndPos), Tokens)))
                    {
                        foreach (bool l4 in get_current_position(StartPos))
                        {
                            foreach (bool l5 in YP.repeat())
                            {
                                foreach (bool l6 in YP.get_code(Ch))
                                {
                                    if (YP.lessThan(Ch, new ListPair(32, Atom.NIL)))
                                    {
                                        if (YP.notEqual(Ch, 9))
                                        {
                                            if (YP.termNotEqual(Ch, -1))
                                            {
                                                foreach (bool l10 in get_current_position(EndPos))
                                                {
                                                    foreach (bool l11 in YP.get_code(NextCh))
                                                    {
                                                        foreach (bool l12 in read_tokens(NextCh, Dict, Tokens))
                                                        {
                                                            yield return false;
                                                        }
                                                    }
                                                }
                                            }
                                            yield break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            {
                object T = arg3;
                Variable C2 = new Variable();
                Variable StartPos = new Variable();
                Variable EndPos = new Variable();
                Variable Tokens = new Variable();
                Variable StartPos1 = new Variable();
                Variable NextCh = new Variable();
                Variable Chars = new Variable();
                foreach (bool l2 in YP.unify(arg1, 47))
                {
                    foreach (bool l3 in YP.get_code(C2))
                    {
                        if (YP.equal(C2, new ListPair(42, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.unify(T, new ListPair(new Functor2("comment", StartPos, EndPos), Tokens)))
                            {
                                foreach (bool l6 in get_current_position(StartPos1))
                                {
                                    foreach (bool l7 in YP.unify(StartPos, YP.subtract(StartPos1, 1)))
                                    {
                                        foreach (bool l8 in read_solidus(32, NextCh))
                                        {
                                            foreach (bool l9 in get_current_position(EndPos))
                                            {
                                                foreach (bool l10 in read_tokens(NextCh, Dict, Tokens))
                                                {
                                                    yield return false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            goto cutIf1;
                        }
                        foreach (bool l4 in YP.unify(T, Tokens))
                        {
                            foreach (bool l5 in rest_symbol(C2, Chars, NextCh))
                            {
                                foreach (bool l6 in read_after_atom4(NextCh, Dict, Tokens, new ListPair(47, Chars)))
                                {
                                    yield return false;
                                }
                            }
                        }
                    cutIf1:
                        { }
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 33))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("atom", Atom.a("!"), Pos), Tokens)))
                    {
                        foreach (bool l4 in get_current_position(Pos))
                        {
                            foreach (bool l5 in YP.get_code(NextCh))
                            {
                                foreach (bool l6 in read_after_atom(NextCh, Dict, Tokens))
                                {
                                    yield return false;
                                }
                            }
                        }
                    }
                }
            }
            {
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 40))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a(" ("), Tokens)))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 41))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a(")"), Tokens)))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 44))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a(","), Tokens)))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 59))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("atom", Atom.a(";"), Pos), Tokens)))
                    {
                        foreach (bool l4 in get_current_position(Pos))
                        {
                            foreach (bool l5 in YP.get_code(NextCh))
                            {
                                foreach (bool l6 in read_after_atom(NextCh, Dict, Tokens))
                                {
                                    yield return false;
                                }
                            }
                        }
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 91))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("/", Atom.a("["), Pos), Tokens)))
                    {
                        foreach (bool l4 in get_current_position(Pos))
                        {
                            foreach (bool l5 in YP.get_code(NextCh))
                            {
                                foreach (bool l6 in read_tokens(NextCh, Dict, Tokens))
                                {
                                    yield return false;
                                }
                            }
                        }
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 93))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("/", Atom.a("]"), Pos), Tokens)))
                    {
                        foreach (bool l4 in get_current_position(Pos))
                        {
                            foreach (bool l5 in YP.get_code(NextCh))
                            {
                                foreach (bool l6 in read_after_atom(NextCh, Dict, Tokens))
                                {
                                    yield return false;
                                }
                            }
                        }
                    }
                }
            }
            {
                Variable Pos = new Variable();
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 123))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("/", Atom.a("{"), Pos), Tokens)))
                    {
                        foreach (bool l4 in get_current_position(Pos))
                        {
                            foreach (bool l5 in YP.get_code(NextCh))
                            {
                                foreach (bool l6 in read_tokens(NextCh, Dict, Tokens))
                                {
                                    yield return false;
                                }
                            }
                        }
                    }
                }
            }
            {
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 124))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("|"), Tokens)))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 125))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("}"), Tokens)))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in read_after_atom(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object Tokens = arg3;
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 46))
                {
                    foreach (bool l3 in YP.get_code(NextCh))
                    {
                        foreach (bool l4 in read_fullstop(NextCh, Dict, Tokens))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                Variable Chars = new Variable();
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 34))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor1("string", Chars), Tokens)))
                    {
                        foreach (bool l4 in read_string(Chars, 34, NextCh))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
            {
                object Tokens = arg3;
                Variable Chars = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 39))
                {
                    foreach (bool l3 in read_string(Chars, 39, NextCh))
                    {
                        foreach (bool l4 in read_after_atom4(NextCh, Dict, Tokens, Chars))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 35))
                {
                    foreach (bool l3 in read_symbol(35, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 36))
                {
                    foreach (bool l3 in read_symbol(36, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 38))
                {
                    foreach (bool l3 in read_symbol(38, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 42))
                {
                    foreach (bool l3 in read_symbol(42, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 43))
                {
                    foreach (bool l3 in read_symbol(43, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 45))
                {
                    foreach (bool l3 in read_symbol(45, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 58))
                {
                    foreach (bool l3 in read_symbol(58, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 60))
                {
                    foreach (bool l3 in read_symbol(60, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 61))
                {
                    foreach (bool l3 in read_symbol(61, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 62))
                {
                    foreach (bool l3 in read_symbol(62, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 63))
                {
                    foreach (bool l3 in read_symbol(63, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 64))
                {
                    foreach (bool l3 in read_symbol(64, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 92))
                {
                    foreach (bool l3 in read_symbol(92, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 94))
                {
                    foreach (bool l3 in read_symbol(94, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 96))
                {
                    foreach (bool l3 in read_symbol(96, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Tokens = arg3;
                foreach (bool l2 in YP.unify(arg1, 126))
                {
                    foreach (bool l3 in read_symbol(126, Dict, Tokens))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> read_symbol(object C1, object Dict, object Tokens)
        {
            {
                Variable C2 = new Variable();
                Variable Chars = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.get_code(C2))
                {
                    foreach (bool l3 in rest_symbol(C2, Chars, NextCh))
                    {
                        foreach (bool l4 in read_after_atom4(NextCh, Dict, Tokens, new ListPair(C1, Chars)))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> rest_symbol(object arg1, object arg2, object arg3)
        {
            {
                object C2 = arg1;
                object LastCh = arg3;
                Variable Chars = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(C2, Chars)))
                {
                    if (YP.greaterThan(C2, 160))
                    {
                        if (YP.lessThan(C2, 192))
                        {
                            if (YP.notEqual(C2, 186))
                            {
                                if (YP.notEqual(C2, 170))
                                {
                                    foreach (bool l7 in YP.get_code(NextCh))
                                    {
                                        foreach (bool l8 in rest_symbol(NextCh, Chars, LastCh))
                                        {
                                            yield return false;
                                        }
                                    }
                                    yield break;
                                }
                            }
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in symbol_char(C2))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in rest_symbol(NextCh, Chars, LastCh))
                            {
                                yield return false;
                            }
                        }
                        yield break;
                    }
                cutIf1:
                    { }
                }
            }
            {
                Variable C2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, C2))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.NIL))
                    {
                        foreach (bool l4 in YP.unify(arg3, C2))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> symbol_char(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, 35))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 36))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 38))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 42))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 43))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 45))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 46))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 47))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 58))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 60))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 61))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 62))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 63))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 64))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 92))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 94))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 96))
                {
                    yield return false;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 126))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> get_current_position(object Pos)
        {
            {
                foreach (bool l2 in YP.unify(Pos, 0))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> read_after_atom4(object Ch, object Dict, object arg3, object Chars)
        {
            {
                Variable Atom_1 = new Variable();
                Variable Pos = new Variable();
                Variable Tokens = new Variable();
                foreach (bool l2 in YP.unify(arg3, new ListPair(new Functor2("atom", Atom_1, Pos), Tokens)))
                {
                    foreach (bool l3 in YP.unify(Pos, 0))
                    {
                        foreach (bool l4 in YP.atom_codes(Atom_1, Chars))
                        {
                            foreach (bool l5 in read_after_atom(Ch, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_after_atom(object arg1, object Dict, object arg3)
        {
            {
                Variable Tokens = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, 40))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("("), Tokens)))
                    {
                        foreach (bool l4 in YP.get_code(NextCh))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                        yield break;
                    }
                }
            }
            {
                object Ch = arg1;
                object Tokens = arg3;
                foreach (bool l2 in read_tokens(Ch, Dict, Tokens))
                {
                    yield return false;
                }
            }
        }

        public static IEnumerable<bool> read_string(object Chars, object Quote, object NextCh)
        {
            {
                Variable Ch = new Variable();
                Variable Char = new Variable();
                Variable Next = new Variable();
                foreach (bool l2 in YP.get_code(Ch))
                {
                    foreach (bool l3 in read_char(Ch, Quote, Char, Next))
                    {
                        foreach (bool l4 in rest_string5(Char, Next, Chars, Quote, NextCh))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> rest_string5(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            {
                object _X = arg4;
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg1, -1))
                {
                    foreach (bool l3 in YP.unify(arg2, NextCh))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.NIL))
                        {
                            foreach (bool l5 in YP.unify(arg5, NextCh))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object Char = arg1;
                object Next = arg2;
                object Quote = arg4;
                object NextCh = arg5;
                Variable Chars = new Variable();
                Variable Char2 = new Variable();
                Variable Next2 = new Variable();
                foreach (bool l2 in YP.unify(arg3, new ListPair(Char, Chars)))
                {
                    foreach (bool l3 in read_char(Next, Quote, Char2, Next2))
                    {
                        foreach (bool l4 in rest_string5(Char2, Next2, Chars, Quote, NextCh))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> escape_char(object arg1, object arg2)
        {
            {
                foreach (bool l2 in YP.unify(arg1, 110))
                {
                    foreach (bool l3 in YP.unify(arg2, 10))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 78))
                {
                    foreach (bool l3 in YP.unify(arg2, 10))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 116))
                {
                    foreach (bool l3 in YP.unify(arg2, 9))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 84))
                {
                    foreach (bool l3 in YP.unify(arg2, 9))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 114))
                {
                    foreach (bool l3 in YP.unify(arg2, 13))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 82))
                {
                    foreach (bool l3 in YP.unify(arg2, 13))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 118))
                {
                    foreach (bool l3 in YP.unify(arg2, 11))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 86))
                {
                    foreach (bool l3 in YP.unify(arg2, 11))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 98))
                {
                    foreach (bool l3 in YP.unify(arg2, 8))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 66))
                {
                    foreach (bool l3 in YP.unify(arg2, 8))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 102))
                {
                    foreach (bool l3 in YP.unify(arg2, 12))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 70))
                {
                    foreach (bool l3 in YP.unify(arg2, 12))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 101))
                {
                    foreach (bool l3 in YP.unify(arg2, 27))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 69))
                {
                    foreach (bool l3 in YP.unify(arg2, 27))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 100))
                {
                    foreach (bool l3 in YP.unify(arg2, 127))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 68))
                {
                    foreach (bool l3 in YP.unify(arg2, 127))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 115))
                {
                    foreach (bool l3 in YP.unify(arg2, 32))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 83))
                {
                    foreach (bool l3 in YP.unify(arg2, 32))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 122))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, 90))
                {
                    foreach (bool l3 in YP.unify(arg2, -1))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> read_variable(object C1, object Dict, object arg3)
        {
            {
                Variable Var = new Variable();
                Variable Name = new Variable();
                Variable StartPos = new Variable();
                Variable Tokens = new Variable();
                Variable Chars = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in YP.unify(arg3, new ListPair(new Functor3("var", Var, Name, StartPos), Tokens)))
                {
                    foreach (bool l3 in get_current_position(StartPos))
                    {
                        foreach (bool l4 in read_name(C1, Chars, NextCh))
                        {
                            foreach (bool l5 in YP.atom_codes(Name, Chars))
                            {
                                if (YP.termEqual(Name, Atom.a("_")))
                                {
                                    foreach (bool l7 in read_after_atom(NextCh, Dict, Tokens))
                                    {
                                        yield return false;
                                    }
                                    goto cutIf1;
                                }
                                foreach (bool l6 in read_lookup(Dict, Name, Var))
                                {
                                    foreach (bool l7 in read_after_atom(NextCh, Dict, Tokens))
                                    {
                                        yield return false;
                                    }
                                }
                            cutIf1:
                                { }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_lookup(object arg1, object Name, object Var)
        {
            {
                Variable N = new Variable();
                Variable V = new Variable();
                Variable L = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("=", N, V), L)))
                {
                    foreach (bool l3 in YP.unify(N, Name))
                    {
                        foreach (bool l4 in YP.unify(V, Var))
                        {
                            yield return false;
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in read_lookup(L, Name, Var))
                    {
                        yield return false;
                    }
                cutIf1:
                    { }
                }
            }
        }

        public static IEnumerable<bool> read_solidus(object Ch, object LastCh)
        {
            {
                Variable NextCh = new Variable();
                if (YP.equal(Ch, 42))
                {
                    foreach (bool l3 in YP.get_code(NextCh))
                    {
                        if (YP.equal(NextCh, 47))
                        {
                            foreach (bool l5 in YP.get_code(LastCh))
                            {
                                yield return false;
                            }
                            goto cutIf2;
                        }
                        foreach (bool l4 in read_solidus(NextCh, LastCh))
                        {
                            yield return false;
                        }
                    cutIf2:
                        { }
                    }
                    goto cutIf1;
                }
                if (YP.notEqual(Ch, -1))
                {
                    foreach (bool l3 in YP.get_code(NextCh))
                    {
                        foreach (bool l4 in read_solidus(NextCh, LastCh))
                        {
                            yield return false;
                        }
                    }
                    goto cutIf3;
                }
                foreach (bool l2 in YP.unify(LastCh, Ch))
                {
                    foreach (bool l3 in formatError(Atom.a("user_error"), Atom.a("~N** end of file in /*comment~n"), Atom.NIL))
                    {
                        yield return false;
                    }
                }
            cutIf3:
            cutIf1:
                { }
            }
        }

        public static IEnumerable<bool> read_identifier(object C1, object Dict, object Tokens)
        {
            {
                Variable Chars = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in read_name(C1, Chars, NextCh))
                {
                    foreach (bool l3 in read_after_atom4(NextCh, Dict, Tokens, Chars))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> read_name(object C1, object arg2, object LastCh)
        {
            {
                Variable Chars = new Variable();
                Variable C2 = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(C1, Chars)))
                {
                    foreach (bool l3 in YP.get_code(C2))
                    {
                        if (YP.greaterThanOrEqual(C2, new ListPair(97, Atom.NIL)))
                        {
                            if (YP.lessThanOrEqual(C2, new ListPair(122, Atom.NIL)))
                            {
                                foreach (bool l6 in read_name(C2, Chars, LastCh))
                                {
                                    yield return false;
                                }
                                goto cutIf2;
                            }
                            if (YP.lessThan(C2, 192))
                            {
                                if (YP.notEqual(YP.bitwiseOr(C2, 16), 186))
                                {
                                    foreach (bool l7 in YP.unify(Chars, Atom.NIL))
                                    {
                                        foreach (bool l8 in YP.unify(LastCh, C2))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf3;
                                }
                            }
                            if (YP.equal(YP.bitwiseOr(C2, 32), 247))
                            {
                                foreach (bool l6 in YP.unify(Chars, Atom.NIL))
                                {
                                    foreach (bool l7 in YP.unify(LastCh, C2))
                                    {
                                        yield return false;
                                    }
                                }
                                goto cutIf4;
                            }
                            foreach (bool l5 in read_name(C2, Chars, LastCh))
                            {
                                yield return false;
                            }
                        cutIf4:
                        cutIf3:
                        cutIf2:
                            goto cutIf1;
                        }
                        if (YP.greaterThanOrEqual(C2, new ListPair(65, Atom.NIL)))
                        {
                            if (YP.greaterThan(C2, new ListPair(90, Atom.NIL)))
                            {
                                if (YP.notEqual(C2, new ListPair(95, Atom.NIL)))
                                {
                                    foreach (bool l7 in YP.unify(Chars, Atom.NIL))
                                    {
                                        foreach (bool l8 in YP.unify(LastCh, C2))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf6;
                                }
                            }
                            foreach (bool l5 in read_name(C2, Chars, LastCh))
                            {
                                yield return false;
                            }
                        cutIf6:
                            goto cutIf5;
                        }
                        if (YP.greaterThanOrEqual(C2, new ListPair(48, Atom.NIL)))
                        {
                            if (YP.lessThanOrEqual(C2, new ListPair(57, Atom.NIL)))
                            {
                                foreach (bool l6 in read_name(C2, Chars, LastCh))
                                {
                                    yield return false;
                                }
                                goto cutIf7;
                            }
                        }
                        foreach (bool l4 in YP.unify(Chars, Atom.NIL))
                        {
                            foreach (bool l5 in YP.unify(LastCh, C2))
                            {
                                yield return false;
                            }
                        }
                    cutIf7:
                    cutIf5:
                    cutIf1:
                        { }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_fullstop(object Ch, object Dict, object Tokens)
        {
            {
                Variable Number = new Variable();
                Variable Tokens1 = new Variable();
                Variable Chars = new Variable();
                Variable NextCh = new Variable();
                if (YP.lessThanOrEqual(Ch, new ListPair(57, Atom.NIL)))
                {
                    if (YP.greaterThanOrEqual(Ch, new ListPair(48, Atom.NIL)))
                    {
                        foreach (bool l4 in YP.unify(Tokens, new ListPair(new Functor1("number", Number), Tokens1)))
                        {
                            foreach (bool l5 in read_float(Number, Dict, Tokens1, new ListPair(48, Atom.NIL), Ch))
                            {
                                yield return false;
                            }
                        }
                        goto cutIf1;
                    }
                }
                if (YP.greaterThan(Ch, new ListPair(32, Atom.NIL)))
                {
                    foreach (bool l3 in rest_symbol(Ch, Chars, NextCh))
                    {
                        foreach (bool l4 in read_after_atom4(NextCh, Dict, Tokens, new ListPair(46, Chars)))
                        {
                            yield return false;
                        }
                    }
                    goto cutIf2;
                }
                if (YP.greaterThanOrEqual(Ch, 0))
                {
                    foreach (bool l3 in YP.unify(Tokens, Atom.NIL))
                    {
                        yield return false;
                    }
                    goto cutIf3;
                }
                foreach (bool l2 in formatError(Atom.a("user_error"), Atom.a("~N** end of file just after full stop~n"), Atom.NIL))
                {
                }
            cutIf3:
            cutIf2:
            cutIf1:
                { }
            }
        }

        public static IEnumerable<bool> read_float(object Number, object Dict, object Tokens, object Digits, object Digit)
        {
            {
                Variable Chars = new Variable();
                Variable Rest = new Variable();
                Variable NextCh = new Variable();
                foreach (bool l2 in prepend(Digits, Chars, Rest))
                {
                    foreach (bool l3 in read_float4(Digit, Rest, NextCh, Chars))
                    {
                        foreach (bool l4 in YP.number_codes(Number, Chars))
                        {
                            foreach (bool l5 in read_tokens(NextCh, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> prepend(object arg1, object arg2, object arg3)
        {
            {
                object X = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(46, X)))
                    {
                        yield return false;
                    }
                }
            }
            {
                object Y = arg3;
                Variable C = new Variable();
                Variable Cs = new Variable();
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(C, Cs)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(C, X)))
                    {
                        foreach (bool l4 in prepend(Cs, X, Y))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_float4(object C1, object arg2, object NextCh, object Total)
        {
            {
                Variable Chars = new Variable();
                Variable C2 = new Variable();
                Variable C3 = new Variable();
                Variable C4 = new Variable();
                Variable More = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(C1, Chars)))
                {
                    foreach (bool l3 in YP.get_code(C2))
                    {
                        if (YP.greaterThanOrEqual(C2, new ListPair(48, Atom.NIL)))
                        {
                            if (YP.lessThanOrEqual(C2, new ListPair(57, Atom.NIL)))
                            {
                                foreach (bool l6 in read_float4(C2, Chars, NextCh, Total))
                                {
                                    yield return false;
                                }
                                goto cutIf1;
                            }
                        }
                        if (YP.equal(YP.bitwiseOr(C2, 32), new ListPair(101, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.get_code(C3))
                            {
                                if (YP.equal(C3, new ListPair(45, Atom.NIL)))
                                {
                                    foreach (bool l7 in YP.get_code(C4))
                                    {
                                        foreach (bool l8 in YP.unify(Chars, new ListPair(C2, new ListPair(45, More))))
                                        {
                                            if (YP.greaterThanOrEqual(C4, new ListPair(48, Atom.NIL)))
                                            {
                                                if (YP.lessThanOrEqual(C4, new ListPair(57, Atom.NIL)))
                                                {
                                                    foreach (bool l11 in read_exponent(C4, More, NextCh))
                                                    {
                                                        yield return false;
                                                    }
                                                    goto cutIf4;
                                                }
                                            }
                                            foreach (bool l9 in YP.unify(More, Atom.NIL))
                                            {
                                                foreach (bool l10 in formatError(Atom.a("user_error"), Atom.a("~N** Missing exponent in ~s~n"), new ListPair(Total, Atom.NIL)))
                                                {
                                                }
                                            }
                                            foreach (bool l9 in YP.unify(More, new ListPair(48, Atom.NIL)))
                                            {
                                                foreach (bool l10 in YP.unify(NextCh, C4))
                                                {
                                                    yield return false;
                                                }
                                            }
                                        cutIf4:
                                            { }
                                        }
                                    }
                                    goto cutIf3;
                                }
                                if (YP.equal(C3, new ListPair(43, Atom.NIL)))
                                {
                                    foreach (bool l7 in YP.get_code(C4))
                                    {
                                        foreach (bool l8 in YP.unify(Chars, new ListPair(C2, More)))
                                        {
                                            if (YP.greaterThanOrEqual(C4, new ListPair(48, Atom.NIL)))
                                            {
                                                if (YP.lessThanOrEqual(C4, new ListPair(57, Atom.NIL)))
                                                {
                                                    foreach (bool l11 in read_exponent(C4, More, NextCh))
                                                    {
                                                        yield return false;
                                                    }
                                                    goto cutIf6;
                                                }
                                            }
                                            foreach (bool l9 in YP.unify(More, Atom.NIL))
                                            {
                                                foreach (bool l10 in formatError(Atom.a("user_error"), Atom.a("~N** Missing exponent in ~s~n"), new ListPair(Total, Atom.NIL)))
                                                {
                                                }
                                            }
                                            foreach (bool l9 in YP.unify(More, new ListPair(48, Atom.NIL)))
                                            {
                                                foreach (bool l10 in YP.unify(NextCh, C4))
                                                {
                                                    yield return false;
                                                }
                                            }
                                        cutIf6:
                                            { }
                                        }
                                    }
                                    goto cutIf5;
                                }
                                foreach (bool l6 in YP.unify(C4, C3))
                                {
                                    foreach (bool l7 in YP.unify(Chars, new ListPair(C2, More)))
                                    {
                                        if (YP.greaterThanOrEqual(C4, new ListPair(48, Atom.NIL)))
                                        {
                                            if (YP.lessThanOrEqual(C4, new ListPair(57, Atom.NIL)))
                                            {
                                                foreach (bool l10 in read_exponent(C4, More, NextCh))
                                                {
                                                    yield return false;
                                                }
                                                goto cutIf7;
                                            }
                                        }
                                        foreach (bool l8 in YP.unify(More, Atom.NIL))
                                        {
                                            foreach (bool l9 in formatError(Atom.a("user_error"), Atom.a("~N** Missing exponent in ~s~n"), new ListPair(Total, Atom.NIL)))
                                            {
                                            }
                                        }
                                        foreach (bool l8 in YP.unify(More, new ListPair(48, Atom.NIL)))
                                        {
                                            foreach (bool l9 in YP.unify(NextCh, C4))
                                            {
                                                yield return false;
                                            }
                                        }
                                    cutIf7:
                                        { }
                                    }
                                }
                            cutIf5:
                            cutIf3:
                                { }
                            }
                            goto cutIf2;
                        }
                        foreach (bool l4 in YP.unify(Chars, Atom.NIL))
                        {
                            foreach (bool l5 in YP.unify(NextCh, C2))
                            {
                                yield return false;
                            }
                        }
                    cutIf2:
                    cutIf1:
                        { }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_exponent(object C1, object arg2, object NextCh)
        {
            {
                Variable Chars = new Variable();
                Variable C2 = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(C1, Chars)))
                {
                    foreach (bool l3 in YP.get_code(C2))
                    {
                        if (YP.greaterThanOrEqual(C2, new ListPair(48, Atom.NIL)))
                        {
                            if (YP.lessThanOrEqual(C2, new ListPair(57, Atom.NIL)))
                            {
                                foreach (bool l6 in read_exponent(C2, Chars, NextCh))
                                {
                                    yield return false;
                                }
                                goto cutIf1;
                            }
                        }
                        foreach (bool l4 in YP.unify(Chars, Atom.NIL))
                        {
                            foreach (bool l5 in YP.unify(NextCh, C2))
                            {
                                yield return false;
                            }
                        }
                    cutIf1:
                        { }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_number(object C1, object Dict, object arg3)
        {
            {
                Variable Number = new Variable();
                Variable Tokens = new Variable();
                Variable C2 = new Variable();
                Variable N = new Variable();
                Variable C = new Variable();
                Variable C3 = new Variable();
                Variable Digits = new Variable();
                foreach (bool l2 in YP.unify(arg3, new ListPair(new Functor1("number", Number), Tokens)))
                {
                    foreach (bool l3 in read_number4(C1, C2, 0, N))
                    {
                        if (YP.equal(C2, 39))
                        {
                            if (YP.greaterThanOrEqual(N, 2))
                            {
                                if (YP.lessThanOrEqual(N, 36))
                                {
                                    foreach (bool l7 in read_based(N, 0, Number, C))
                                    {
                                        foreach (bool l8 in read_tokens(C, Dict, Tokens))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf2;
                                }
                            }
                            if (YP.equal(N, 0))
                            {
                                foreach (bool l6 in YP.get_code(C3))
                                {
                                    foreach (bool l7 in read_char(C3, -1, Number, C))
                                    {
                                        foreach (bool l8 in read_tokens(C, Dict, Tokens))
                                        {
                                            yield return false;
                                        }
                                    }
                                }
                                goto cutIf3;
                            }
                            foreach (bool l5 in formatError(Atom.a("user_error"), Atom.a("~N** ~d' read as ~d '~n"), new ListPair(N, new ListPair(N, Atom.NIL))))
                            {
                                foreach (bool l6 in YP.unify(Number, N))
                                {
                                    foreach (bool l7 in YP.unify(C, C2))
                                    {
                                        foreach (bool l8 in read_tokens(C, Dict, Tokens))
                                        {
                                            yield return false;
                                        }
                                    }
                                }
                            }
                        cutIf3:
                        cutIf2:
                            goto cutIf1;
                        }
                        if (YP.equal(C2, 46))
                        {
                            foreach (bool l5 in YP.get_code(C3))
                            {
                                if (YP.greaterThanOrEqual(C3, new ListPair(48, Atom.NIL)))
                                {
                                    if (YP.lessThanOrEqual(C3, new ListPair(57, Atom.NIL)))
                                    {
                                        foreach (bool l8 in YP.number_codes(N, Digits))
                                        {
                                            foreach (bool l9 in read_float(Number, Dict, Tokens, Digits, C3))
                                            {
                                                yield return false;
                                            }
                                        }
                                        goto cutIf5;
                                    }
                                }
                                foreach (bool l6 in YP.unify(Number, N))
                                {
                                    foreach (bool l7 in read_fullstop(C3, Dict, Tokens))
                                    {
                                        yield return false;
                                    }
                                }
                            cutIf5:
                                { }
                            }
                            goto cutIf4;
                        }
                        foreach (bool l4 in YP.unify(Number, N))
                        {
                            foreach (bool l5 in read_tokens(C2, Dict, Tokens))
                            {
                                yield return false;
                            }
                        }
                    cutIf4:
                    cutIf1:
                        { }
                    }
                }
            }
        }

        public static IEnumerable<bool> read_number4(object C0, object C, object N0, object N)
        {
            {
                Variable N1 = new Variable();
                Variable C1 = new Variable();
                if (YP.greaterThanOrEqual(C0, new ListPair(48, Atom.NIL)))
                {
                    if (YP.lessThanOrEqual(C0, new ListPair(57, Atom.NIL)))
                    {
                        foreach (bool l4 in YP.unify(N1, YP.add(YP.subtract(YP.multiply(N0, 10), new ListPair(48, Atom.NIL)), C0)))
                        {
                            foreach (bool l5 in YP.get_code(C1))
                            {
                                foreach (bool l6 in read_number4(C1, C, N1, N))
                                {
                                    yield return false;
                                }
                            }
                        }
                        goto cutIf1;
                    }
                }
                if (YP.equal(C0, 95))
                {
                    foreach (bool l3 in YP.get_code(C1))
                    {
                        foreach (bool l4 in read_number4(C1, C, N0, N))
                        {
                            yield return false;
                        }
                    }
                    goto cutIf2;
                }
                foreach (bool l2 in YP.unify(C, C0))
                {
                    foreach (bool l3 in YP.unify(N, N0))
                    {
                        yield return false;
                    }
                }
            cutIf2:
            cutIf1:
                { }
            }
        }

        public static IEnumerable<bool> read_based(object Base, object N0, object N, object C)
        {
            {
                Variable C1 = new Variable();
                Variable Digit = new Variable();
                Variable N1 = new Variable();
                foreach (bool l2 in YP.get_code(C1))
                {
                    if (YP.greaterThanOrEqual(C1, new ListPair(48, Atom.NIL)))
                    {
                        if (YP.lessThanOrEqual(C1, new ListPair(57, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.unify(Digit, YP.subtract(C1, new ListPair(48, Atom.NIL))))
                            {
                                if (YP.lessThan(Digit, Base))
                                {
                                    foreach (bool l7 in YP.unify(N1, YP.add(YP.multiply(N0, Base), Digit)))
                                    {
                                        foreach (bool l8 in read_based(Base, N1, N, C))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf2;
                                }
                                if (YP.equal(C1, new ListPair(95, Atom.NIL)))
                                {
                                    foreach (bool l7 in read_based(Base, N0, N, C))
                                    {
                                        yield return false;
                                    }
                                    goto cutIf3;
                                }
                                foreach (bool l6 in YP.unify(N, N0))
                                {
                                    foreach (bool l7 in YP.unify(C, C1))
                                    {
                                        yield return false;
                                    }
                                }
                            cutIf3:
                            cutIf2:
                                { }
                            }
                            goto cutIf1;
                        }
                    }
                    if (YP.greaterThanOrEqual(C1, new ListPair(65, Atom.NIL)))
                    {
                        if (YP.lessThanOrEqual(C1, new ListPair(90, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.unify(Digit, YP.subtract(C1, YP.subtract(new ListPair(65, Atom.NIL), 10))))
                            {
                                if (YP.lessThan(Digit, Base))
                                {
                                    foreach (bool l7 in YP.unify(N1, YP.add(YP.multiply(N0, Base), Digit)))
                                    {
                                        foreach (bool l8 in read_based(Base, N1, N, C))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf5;
                                }
                                if (YP.equal(C1, new ListPair(95, Atom.NIL)))
                                {
                                    foreach (bool l7 in read_based(Base, N0, N, C))
                                    {
                                        yield return false;
                                    }
                                    goto cutIf6;
                                }
                                foreach (bool l6 in YP.unify(N, N0))
                                {
                                    foreach (bool l7 in YP.unify(C, C1))
                                    {
                                        yield return false;
                                    }
                                }
                            cutIf6:
                            cutIf5:
                                { }
                            }
                            goto cutIf4;
                        }
                    }
                    if (YP.greaterThanOrEqual(C1, new ListPair(97, Atom.NIL)))
                    {
                        if (YP.lessThanOrEqual(C1, new ListPair(122, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.unify(Digit, YP.subtract(C1, YP.subtract(new ListPair(97, Atom.NIL), 10))))
                            {
                                if (YP.lessThan(Digit, Base))
                                {
                                    foreach (bool l7 in YP.unify(N1, YP.add(YP.multiply(N0, Base), Digit)))
                                    {
                                        foreach (bool l8 in read_based(Base, N1, N, C))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf8;
                                }
                                if (YP.equal(C1, new ListPair(95, Atom.NIL)))
                                {
                                    foreach (bool l7 in read_based(Base, N0, N, C))
                                    {
                                        yield return false;
                                    }
                                    goto cutIf9;
                                }
                                foreach (bool l6 in YP.unify(N, N0))
                                {
                                    foreach (bool l7 in YP.unify(C, C1))
                                    {
                                        yield return false;
                                    }
                                }
                            cutIf9:
                            cutIf8:
                                { }
                            }
                            goto cutIf7;
                        }
                    }
                    foreach (bool l3 in YP.unify(Digit, 99))
                    {
                        if (YP.lessThan(Digit, Base))
                        {
                            foreach (bool l5 in YP.unify(N1, YP.add(YP.multiply(N0, Base), Digit)))
                            {
                                foreach (bool l6 in read_based(Base, N1, N, C))
                                {
                                    yield return false;
                                }
                            }
                            goto cutIf10;
                        }
                        if (YP.equal(C1, new ListPair(95, Atom.NIL)))
                        {
                            foreach (bool l5 in read_based(Base, N0, N, C))
                            {
                                yield return false;
                            }
                            goto cutIf11;
                        }
                        foreach (bool l4 in YP.unify(N, N0))
                        {
                            foreach (bool l5 in YP.unify(C, C1))
                            {
                                yield return false;
                            }
                        }
                    cutIf11:
                    cutIf10:
                        { }
                    }
                cutIf7:
                cutIf4:
                cutIf1:
                    { }
                }
            }
        }

        public static IEnumerable<bool> read_char(object Char, object Quote, object Result, object Next)
        {
            {
                Variable C1 = new Variable();
                Variable C2 = new Variable();
                Variable C3 = new Variable();
                Variable Ch = new Variable();
                if (YP.equal(Char, 92))
                {
                    foreach (bool l3 in YP.get_code(C1))
                    {
                        if (YP.lessThan(C1, 0))
                        {
                            foreach (bool l5 in formatError(Atom.a("user_error"), Atom.a("~N** end of file in ~cquoted~c~n"), new ListPair(Quote, new ListPair(Quote, Atom.NIL))))
                            {
                                foreach (bool l6 in YP.unify(Result, -1))
                                {
                                    foreach (bool l7 in YP.unify(Next, C1))
                                    {
                                        yield return false;
                                    }
                                }
                            }
                            goto cutIf2;
                        }
                        if (YP.lessThanOrEqual(C1, new ListPair(32, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.get_code(C2))
                            {
                                foreach (bool l6 in read_char(C2, Quote, Result, Next))
                                {
                                    yield return false;
                                }
                            }
                            goto cutIf3;
                        }
                        if (YP.equal(YP.bitwiseOr(C1, 32), new ListPair(99, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.get_code(C2))
                            {
                                foreach (bool l6 in read_char(C2, Quote, Result, Next))
                                {
                                    yield return false;
                                }
                            }
                            goto cutIf4;
                        }
                        if (YP.lessThanOrEqual(C1, new ListPair(55, Atom.NIL)))
                        {
                            if (YP.greaterThanOrEqual(C1, new ListPair(48, Atom.NIL)))
                            {
                                foreach (bool l6 in YP.get_code(C2))
                                {
                                    if (YP.lessThanOrEqual(C2, new ListPair(55, Atom.NIL)))
                                    {
                                        if (YP.greaterThanOrEqual(C2, new ListPair(48, Atom.NIL)))
                                        {
                                            foreach (bool l9 in YP.get_code(C3))
                                            {
                                                if (YP.lessThanOrEqual(C3, new ListPair(55, Atom.NIL)))
                                                {
                                                    if (YP.greaterThanOrEqual(C3, new ListPair(48, Atom.NIL)))
                                                    {
                                                        foreach (bool l12 in YP.get_code(Next))
                                                        {
                                                            foreach (bool l13 in YP.unify(Result, YP.subtract(YP.add(YP.multiply(YP.add(YP.multiply(C1, 8), C2), 8), C3), YP.multiply(73, new ListPair(48, Atom.NIL)))))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf7;
                                                    }
                                                }
                                                foreach (bool l10 in YP.unify(Next, C3))
                                                {
                                                    foreach (bool l11 in YP.unify(Result, YP.subtract(YP.add(YP.multiply(C1, 8), C2), YP.multiply(9, new ListPair(48, Atom.NIL)))))
                                                    {
                                                        yield return false;
                                                    }
                                                }
                                            cutIf7:
                                                { }
                                            }
                                            goto cutIf6;
                                        }
                                    }
                                    foreach (bool l7 in YP.unify(Next, C2))
                                    {
                                        foreach (bool l8 in YP.unify(Result, YP.subtract(C1, new ListPair(48, Atom.NIL))))
                                        {
                                            yield return false;
                                        }
                                    }
                                cutIf6:
                                    { }
                                }
                                goto cutIf5;
                            }
                        }
                        if (YP.equal(C1, new ListPair(94, Atom.NIL)))
                        {
                            foreach (bool l5 in YP.get_code(C2))
                            {
                                if (YP.lessThan(C2, 0))
                                {
                                    foreach (bool l7 in formatError(Atom.a("user_error"), Atom.a("~N** end of file in ~c..~c^..~c~n"), ListPair.make(new object[] { Quote, 92, Quote })))
                                    {
                                        foreach (bool l8 in YP.unify(Result, -1))
                                        {
                                            foreach (bool l9 in YP.unify(Next, C2))
                                            {
                                                yield return false;
                                            }
                                        }
                                    }
                                    goto cutIf9;
                                }
                                if (YP.equal(C2, new ListPair(63, Atom.NIL)))
                                {
                                    foreach (bool l7 in YP.unify(Result, 127))
                                    {
                                        foreach (bool l8 in YP.get_code(Next))
                                        {
                                            yield return false;
                                        }
                                    }
                                    goto cutIf10;
                                }
                                foreach (bool l6 in YP.unify(Result, YP.bitwiseAnd(C2, 31)))
                                {
                                    foreach (bool l7 in YP.get_code(Next))
                                    {
                                        yield return false;
                                    }
                                }
                            cutIf10:
                            cutIf9:
                                { }
                            }
                            goto cutIf8;
                        }
                        foreach (bool l4 in escape_char(C1, Result))
                        {
                            foreach (bool l5 in YP.get_code(Next))
                            {
                                yield return false;
                            }
                            goto cutIf11;
                        }
                        foreach (bool l4 in YP.unify(Result, C1))
                        {
                            foreach (bool l5 in YP.get_code(Next))
                            {
                                yield return false;
                            }
                        }
                    cutIf11:
                    cutIf8:
                    cutIf5:
                    cutIf4:
                    cutIf3:
                    cutIf2:
                        { }
                    }
                    goto cutIf1;
                }
                if (YP.equal(Char, Quote))
                {
                    foreach (bool l3 in YP.get_code(Ch))
                    {
                        if (YP.equal(Ch, Quote))
                        {
                            foreach (bool l5 in YP.unify(Result, Quote))
                            {
                                foreach (bool l6 in YP.get_code(Next))
                                {
                                    yield return false;
                                }
                            }
                            goto cutIf13;
                        }
                        foreach (bool l4 in YP.unify(Result, -1))
                        {
                            foreach (bool l5 in YP.unify(Next, Ch))
                            {
                                yield return false;
                            }
                        }
                    cutIf13:
                        { }
                    }
                    goto cutIf12;
                }
                if (YP.lessThan(Char, new ListPair(32, Atom.NIL)))
                {
                    if (YP.notEqual(Char, 9))
                    {
                        if (YP.notEqual(Char, 10))
                        {
                            if (YP.notEqual(Char, 13))
                            {
                                foreach (bool l6 in YP.unify(Result, -1))
                            {
                                    foreach (bool l7 in YP.unify(Next, Char))
                                {
                                        foreach (bool l8 in formatError(Atom.a("user_error"), Atom.a("~N** Strange character ~d ends ~ctoken~c~n"), ListPair.make(new object[] { Char, Quote, Quote })))
                                    {
                                        yield return false;
                                    }
                                }
                            }
                            goto cutIf14;
                            }
                        }
                    }
                }
                foreach (bool l2 in YP.unify(Result, Char))
                {
                    foreach (bool l3 in YP.get_code(Next))
                    {
                        yield return false;
                    }
                }
            cutIf14:
            cutIf12:
            cutIf1:
                { }
            }
        }
        #pragma warning restore 0168, 0219, 0162
    }
}
