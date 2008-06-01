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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.YieldProlog
{
    /// <summary>
    /// YP has static methods for general functions in Yield Prolog such as <see cref="getValue"/>
    /// and <see cref="unify"/>.
    /// </summary>
    public class YP
    {
        private static Fail _fail = new Fail();
        private static Repeat _repeat = new Repeat();
        private static Dictionary<NameArity, List<IClause>> _predicatesStore =
            new Dictionary<NameArity, List<IClause>>();
        private static TextWriter _outputStream = System.Console.Out;
        private static TextReader _inputStream = System.Console.In;
        private static List<object[]> _operatorTable = null;

        /// <summary>
        /// An IClause is used so that dynamic predicates can call match.
        /// </summary>
        public interface IClause
        {
            IEnumerable<bool> match(object[] args);
        }

        public static object getValue(object value)
        {
            if (value is Variable)
                return ((Variable)value).getValue();
            else
                return value;
        }

        public static IEnumerable<bool> unify(object arg1, object arg2)
        {
            arg1 = getValue(arg1);
            arg2 = getValue(arg2);
            if (arg1 is IUnifiable)
                return ((IUnifiable)arg1).unify(arg2);
            else if (arg2 is IUnifiable)
                return ((IUnifiable)arg2).unify(arg1);
            else
            {
                // Arguments are "normal" types.
                if (arg1.Equals(arg2))
                    return new Succeed();
                else
                    return _fail;
            }
        }

        /// <summary>
        /// This is used for the lookup key in _factStore.
        /// </summary>
        public struct NameArity
        {
            public readonly Atom _name;
            public readonly int _arity;

            public NameArity(Atom name, int arity)
            {
                _name = name;
                _arity = arity;
            }

            public override bool Equals(object obj)
            {
                if (obj is NameArity)
                {
                    NameArity nameArity = (NameArity)obj;
                    return nameArity._name.Equals(_name) && nameArity._arity.Equals(_arity);
                }
                else
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                return _name.GetHashCode() ^ _arity.GetHashCode();
            }
        }

        /// <summary>
        /// Convert term to an int.
        /// If term is a single-element List, use its first element
        /// (to handle the char types like "a").  If can't convert, throw an exception.
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        public static int convertInt(object term)
        {
            term = YP.getValue(term);
            if (term is Functor2 && ((Functor2)term)._name == Atom.DOT &&
                YP.getValue(((Functor2)term)._arg2) == Atom.NIL)
                // Assume it is a char type like "a".
                term = YP.getValue(((Functor2)term)._arg1);

            return (int)term;
        }

        /// <summary>
        /// Convert term to a double.  This may convert an int to a double, etc.
        /// If term is a single-element List, use its first element
        /// (to handle the char types like "a").  If can't convert, throw an exception.
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        public static double convertDouble(object term)
        {
            term = YP.getValue(term);
            if (term is Functor2 && ((Functor2)term)._name == Atom.DOT &&
                YP.getValue(((Functor2)term)._arg2) == Atom.NIL)
                // Assume it is a char type like "a".
                term = YP.getValue(((Functor2)term)._arg1);

            return Convert.ToDouble(term);
        }

        /// <summary>
        /// If term is an integer, set intTerm.
        /// If term is a single-element List, use its first element
        /// (to handle the char types like "a").  Return true for success, false if can't convert.
        /// We use a success return value because throwing an exception is inefficient.
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        public static bool getInt(object term, out int intTerm)
        {
            term = YP.getValue(term);
            if (term is Functor2 && ((Functor2)term)._name == Atom.DOT &&
                YP.getValue(((Functor2)term)._arg2) == Atom.NIL)
                // Assume it is a char type like "a".
                term = YP.getValue(((Functor2)term)._arg1);

            if (term is int)
            {
                intTerm = (int)term;
                return true;
            }

            intTerm = 0;
            return false;
        }

        public static bool equal(object x, object y)
        {
            x = YP.getValue(x);
            if (x is DateTime)
                return (DateTime)x == (DateTime)YP.getValue(y);
            // Assume convertDouble converts an int to a double perfectly.
            return YP.convertDouble(x) == YP.convertDouble(y);
        }

        public static bool notEqual(object x, object y)
        {
            x = YP.getValue(x);
            if (x is DateTime)
                return (DateTime)x != (DateTime)YP.getValue(y);
            // Assume convertDouble converts an int to a double perfectly.
            return YP.convertDouble(x) != YP.convertDouble(y);
        }

        public static bool greaterThan(object x, object y)
        {
            x = YP.getValue(x);
            if (x is DateTime)
                return (DateTime)x > (DateTime)YP.getValue(y);
            // Assume convertDouble converts an int to a double perfectly.
            return YP.convertDouble(x) > YP.convertDouble(y);
        }

        public static bool lessThan(object x, object y)
        {
            x = YP.getValue(x);
            if (x is DateTime)
                return (DateTime)x < (DateTime)YP.getValue(y);
            // Assume convertDouble converts an int to a double perfectly.
            return YP.convertDouble(x) < YP.convertDouble(y);
        }

        public static bool greaterThanOrEqual(object x, object y)
        {
            x = YP.getValue(x);
            if (x is DateTime)
                return (DateTime)x >= (DateTime)YP.getValue(y);
            // Assume convertDouble converts an int to a double perfectly.
            return YP.convertDouble(x) >= YP.convertDouble(y);
        }

        public static bool lessThanOrEqual(object x, object y)
        {
            x = YP.getValue(x);
            if (x is DateTime)
                return (DateTime)x <= (DateTime)YP.getValue(y);
            // Assume convertDouble converts an int to a double perfectly.
            return YP.convertDouble(x) <= YP.convertDouble(y);
        }

        public static object negate(object x)
        {
            int intX;
            if (getInt(x, out intX))
                return -intX;
            return -convertDouble(x);
        }

        public static object abs(object x)
        {
            int intX;
            if (getInt(x, out intX))
                return Math.Abs(intX);
            return Math.Abs(convertDouble(x));
        }

        public static object sign(object x)
        {
            int intX;
            if (getInt(x, out intX))
                return Math.Sign(intX);
            return Math.Sign(convertDouble(x));
        }

        /// <summary>
        /// The ISO standard returns an int.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static object floor(object x)
        {
            return (int)Math.Floor(convertDouble(x));
        }

        /// <summary>
        /// The ISO standard returns an int.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static object truncate(object x)
        {
            return (int)Math.Truncate(convertDouble(x));
        }

        /// <summary>
        /// The ISO standard returns an int.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static object round(object x)
        {
            return (int)Math.Round(convertDouble(x));
        }

        /// <summary>
        /// The ISO standard returns an int.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static object ceiling(object x)
        {
            return (int)Math.Ceiling(convertDouble(x));
        }

        public static object sin(object x)
        {
            return Math.Sin(YP.convertDouble(x));
        }

        public static object cos(object x)
        {
            return Math.Cos(YP.convertDouble(x));
        }

        public static object atan(object x)
        {
            return Math.Atan(YP.convertDouble(x));
        }

        public static object exp(object x)
        {
            return Math.Exp(YP.convertDouble(x));
        }

        public static object log(object x)
        {
            return Math.Log(YP.convertDouble(x));
        }

        public static object sqrt(object x)
        {
            return Math.Sqrt(convertDouble(x));
        }

        public static object bitwiseComplement(object x)
        {
            return ~YP.convertInt(x);
        }

        public static object add(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return intX + intY;
            return convertDouble(x) + convertDouble(y);
        }

        public static object subtract(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return intX - intY;
            return convertDouble(x) - convertDouble(y);
        }

        public static object multiply(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return intX * intY;
            return convertDouble(x) * convertDouble(y);
        }

        /// <summary>
        /// Return floating point, even if both arguments are integer.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static object divide(object x, object y)
        {
            return convertDouble(x) / convertDouble(y);
        }

        public static object intDivide(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return intX / intY;
            // Still allow passing a double, but treat as an int.
            return (int)convertDouble(x) / (int)convertDouble(y);
        }

        public static object mod(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return intX % intY;
            // Still allow passing a double, but treat as an int.
            return (int)convertDouble(x) % (int)convertDouble(y);
        }

        public static object pow(object x, object y)
        {
            return Math.Pow(YP.convertDouble(x), YP.convertDouble(y));
        }

        public static object bitwiseShiftRight(object x, object y)
        {
            return YP.convertInt(x) >> YP.convertInt(y);
        }

        public static object bitwiseShiftLeft(object x, object y)
        {
            return YP.convertInt(x) << YP.convertInt(y);
        }

        public static object bitwiseAnd(object x, object y)
        {
            return YP.convertInt(x) & YP.convertInt(y);
        }

        public static object bitwiseOr(object x, object y)
        {
            return YP.convertInt(x) | YP.convertInt(y);
        }

        public static object min(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return Math.Min(intX, intY);
            return Math.Min(convertDouble(x), convertDouble(y));
        }

        public static object max(object x, object y)
        {
            int intX, intY;
            if (getInt(x, out intX) && getInt(y, out intY))
                return Math.Max(intX, intY);
            return Math.Max(convertDouble(x), convertDouble(y));
        }

        public static IEnumerable<bool> copy_term(object inTerm, object outTerm)
        {
            return YP.unify(outTerm, YP.makeCopy(inTerm, new Variable.CopyStore()));
        }

        public static void addUniqueVariables(object term, List<Variable> variableSet)
        {
            term = YP.getValue(term);
            if (term is IUnifiable)
                ((IUnifiable)term).addUniqueVariables(variableSet);
        }

        public static object makeCopy(object term, Variable.CopyStore copyStore)
        {
            term = YP.getValue(term);
            if (term is IUnifiable)
                return ((IUnifiable)term).makeCopy(copyStore);
            else
                // term is a "normal" type. Assume it is ground.
                return term;
        }

        /// <summary>
        /// Sort the array in place according to termLessThan.  This does not remove duplicates
        /// </summary>
        /// <param name="array"></param>
        public static void sortArray(object[] array)
        {
            Array.Sort(array, YP.compareTerms);
        }

        /// <summary>
        /// Sort the array in place according to termLessThan.  This does not remove duplicates
        /// </summary>
        /// <param name="array"></param>
        public static void sortArray(List<object> array)
        {
            array.Sort(YP.compareTerms);
        }

        /// <summary>
        /// Sort List according to termLessThan, remove duplicates and unify with Sorted.
        /// </summary>
        /// <param name="List"></param>
        /// <param name="Sorted"></param>
        /// <returns></returns>
        public static IEnumerable<bool> sort(object List, object Sorted)
        {
            object[] array = ListPair.toArray(List);
            if (array == null)
                return YP.fail();
            if (array.Length > 1)
                sortArray(array);
            return YP.unify(Sorted, ListPair.makeWithoutRepeatedTerms(array));
        }



        /// <summary>
        /// Use YP.unify to unify each of the elements of the two arrays, and yield
        /// once if they all unify.
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns></returns>
        public static IEnumerable<bool> unifyArrays(object[] array1, object[] array2)
        {
            if (array1.Length != array2.Length)
                yield break;

            IEnumerator<bool>[] iterators = new IEnumerator<bool>[array1.Length];
            bool gotMatch = true;
            int nIterators = 0;
            // Try to bind all the arguments.
            for (int i = 0; i < array1.Length; ++i)
            {
                IEnumerator<bool> iterator = YP.unify(array1[i], array2[i]).GetEnumerator();
                iterators[nIterators++] = iterator;
                // MoveNext() is true if YP.unify succeeds.
                if (!iterator.MoveNext())
                {
                    gotMatch = false;
                    break;
                }
            }

            try
            {
                if (gotMatch)
                    yield return false;
            }
            finally
            {
                // Manually finalize all the iterators.
                for (int i = 0; i < nIterators; ++i)
                    iterators[i].Dispose();
            }
        }

        /// <summary>
        /// Return an iterator (which you can use in a for-in loop) which does
        /// zero iterations.  This returns a pre-existing iterator which is
        /// more efficient than letting the compiler generate a new one.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<bool> fail()
        {
            return _fail;
        }

        /// <summary>
        /// Return an iterator (which you can use in a for-in loop) which does
        /// one iteration.  This returns a pre-existing iterator which is
        /// more efficient than letting the compiler generate a new one.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<bool> succeed()
        {
            return new Succeed();
        }

        /// <summary>
        /// Return an iterator (which you can use in a for-in loop) which repeats
        /// indefinitely.  This returns a pre-existing iterator which is
        /// more efficient than letting the compiler generate a new one.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<bool> repeat()
        {
            return _repeat;
        }

        public static IEnumerable<bool> univ(object Term, object List)
        {
            Term = YP.getValue(Term);
            List = YP.getValue(List);

            if (nonvar(Term))
                return YP.unify(new ListPair
                    (getFunctorName(Term), ListPair.make(getFunctorArgs(Term))), List);

            Variable Name = new Variable();
            Variable ArgList = new Variable();
            foreach (bool l1 in new ListPair(Name, ArgList).unify(List))
            {
                object[] args = ListPair.toArray(ArgList);
                if (args == null)
                    throw new Exception("Expected a list. Got: " + ArgList.getValue());
                if (args.Length == 0)
                    // Return the Name, even if it is not an Atom.
                    return YP.unify(Term, Name);
                if (!atom(Name))
                    throw new Exception("Expected an atom. Got: " + Name.getValue());

                return YP.unify(Term, Functor.make((Atom)YP.getValue(Name), args));
            }

            return YP.fail();
        }

        public static IEnumerable<bool> functor(object Term, object FunctorName, object Arity)
        {
            Term = YP.getValue(Term);
            FunctorName = YP.getValue(FunctorName);
            Arity = YP.getValue(Arity);

            if (!(Term is Variable))
            {
                foreach (bool l1 in YP.unify(FunctorName, getFunctorName(Term)))
                {
                    foreach (bool l2 in YP.unify(Arity, getFunctorArgs(Term).Length))
                        yield return false;
                }
            }
            else
                throw new NotImplementedException("Debug: must finish functor/3");
        }

        public static IEnumerable<bool> arg(object ArgNumber, object Term, object Value)
        {
            if (YP.var(ArgNumber))
                throw new NotImplementedException("Debug: must finish arg/3");
            else
            {
                int argNumberInt = convertInt(ArgNumber);
                if (argNumberInt < 0)
                    throw new Exception("ArgNumber must be non-negative");
                object[] termArgs = YP.getFunctorArgs(Term);
                // Silently fail if argNumberInt is out of range.
                if (argNumberInt >= 1 && argNumberInt <= termArgs.Length)
                {
                    // The first ArgNumber is at 1, not 0.
                    foreach (bool l1 in YP.unify(Value, termArgs[argNumberInt - 1]))
                        yield return false;
                }
            }
        }

        public static bool termEqual(object Term1, object Term2)
        {
            Term1 = YP.getValue(Term1);
            if (Term1 is IUnifiable)
                return ((IUnifiable)Term1).termEqual(Term2);
            return Term1.Equals(YP.getValue(Term2));
        }

        public static bool termNotEqual(object Term1, object Term2)
        {
            return !termEqual(Term1, Term2);
        }

        public static bool termLessThan(object Term1, object Term2)
        {
            Term1 = YP.getValue(Term1);
            Term2 = YP.getValue(Term2);
            int term1TypeCode = getTypeCode(Term1);
            int term2TypeCode = getTypeCode(Term2);
            if (term1TypeCode != term2TypeCode)
                return term1TypeCode < term2TypeCode;

            // The terms are the same type code.
            if (term1TypeCode == -2)
            {
                // Variable.
                // We always check for equality first because we want to be sure 
                //   that less than returns false if the terms are equal, in 
                //   case that the less than check really behaves like less than or equal.
                if ((Variable)Term1 != (Variable)Term2)
                    // The hash code should be unique to a Variable object.
                    return Term1.GetHashCode() < Term2.GetHashCode();
                return false;
            }
            if (term1TypeCode == 0)
                return ((Atom)Term1)._name.CompareTo(((Atom)Term2)._name) < 0;
            if (term1TypeCode == 1)
                return ((Functor1)Term1).lessThan((Functor1)Term2);
            if (term1TypeCode == 2)
                return ((Functor2)Term1).lessThan((Functor2)Term2);
            if (term1TypeCode == 3)
                return ((Functor3)Term1).lessThan((Functor3)Term2);
            if (term1TypeCode == 4)
                return ((Functor)Term1).lessThan((Functor)Term2);

            // Type code is -1 for general objects.  First compare their type names.
            // Note that this puts Double before Int32 as required by ISO Prolog.
            string term1TypeName = Term1.GetType().ToString();
            string term2TypeName = Term2.GetType().ToString();
            if (term1TypeName != term2TypeName)
                return term1TypeName.CompareTo(term2TypeName) < 0;

            // The terms are the same type name.
            if (Term1 is int)
                return (int)Term1 < (int)Term2;
            else if (Term1 is double)
                return (double)Term1 < (double)Term2;
            else if (Term1 is DateTime)
                return (DateTime)Term1 < (DateTime)Term2;
            else if (Term1 is String)
                return ((String)Term1).CompareTo((String)Term2) < 0;
            // Debug: Should we try arrays, etc.?

            if (!Term1.Equals(Term2))
                // Could be equal or greater than.
                return Term1.GetHashCode() < Term2.GetHashCode();
            return false;
        }

        /// <summary>
        /// Type code is -2 if term is a Variable, 0 if it is an Atom, 
        /// 1 if it is a Functor1, 2 if it is a Functor2, 3 if it is a Functor3, 
        /// 4 if it is Functor.
        /// Otherwise, type code is -1.
        /// This does not call YP.getValue(term).
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        private static int getTypeCode(object term)
        {
            if (term is Variable)
                return -2;
            else if (term is Atom)
                return 0;
            else if (term is Functor1)
                return 1;
            else if (term is Functor2)
                return 2;
            else if (term is Functor3)
                return 3;
            else if (term is Functor)
                return 4;
            else
                return -1;
        }

        public static bool termLessThanOrEqual(object Term1, object Term2)
        {
            if (YP.termEqual(Term1, Term2))
                return true;
            return YP.termLessThan(Term1, Term2);
        }

        public static bool termGreaterThan(object Term1, object Term2)
        {
            return !YP.termLessThanOrEqual(Term1, Term2);
        }

        public static bool termGreaterThanOrEqual(object Term1, object Term2)
        {
            // termLessThan should ensure that it returns false if terms are equal,
            //   so that this would return true.
            return !YP.termLessThan(Term1, Term2);
        }

        public static int compareTerms(object Term1, object Term2)
        {
            if (YP.termEqual(Term1, Term2))
                return 0;
            else if (YP.termLessThan(Term1, Term2))
                return -1;
            else
                return 1;
        }

        public static bool ground(object Term)
        {
            Term = YP.getValue(Term);
            if (Term is IUnifiable)
                return ((IUnifiable)Term).ground();
            return true;
        }

        public static IEnumerable<bool> current_op
            (object Priority, object Specifier, object Operator)
        {
            if (_operatorTable == null)
            {
                // Initialize.
                _operatorTable = new List<object[]>();
                _operatorTable.Add(new object[] { 1200, Atom.a("xfx"), Atom.a(":-") });
                _operatorTable.Add(new object[] { 1200, Atom.a("xfx"), Atom.a("-->") });
                _operatorTable.Add(new object[] { 1200, Atom.a("fx"), Atom.a(":-") });
                _operatorTable.Add(new object[] { 1200, Atom.a("fx"), Atom.a("?-") });
                _operatorTable.Add(new object[] { 1100, Atom.a("xfy"), Atom.a(";") });
                _operatorTable.Add(new object[] { 1050, Atom.a("xfy"), Atom.a("->") });
                _operatorTable.Add(new object[] { 1000, Atom.a("xfy"), Atom.a(",") });
                _operatorTable.Add(new object[] { 900, Atom.a("fy"), Atom.a("\\+") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("=") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("\\=") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("==") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("\\==") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("@<") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("@=<") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("@>") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("@>=") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("=..") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("is") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("=:=") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("=\\=") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("<") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a("=<") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a(">") });
                _operatorTable.Add(new object[] { 700, Atom.a("xfx"), Atom.a(">=") });
                _operatorTable.Add(new object[] { 600, Atom.a("xfy"), Atom.a(":") });
                _operatorTable.Add(new object[] { 500, Atom.a("yfx"), Atom.a("+") });
                _operatorTable.Add(new object[] { 500, Atom.a("yfx"), Atom.a("-") });
                _operatorTable.Add(new object[] { 500, Atom.a("yfx"), Atom.a("/\\") });
                _operatorTable.Add(new object[] { 500, Atom.a("yfx"), Atom.a("\\/") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a("*") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a("/") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a("//") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a("rem") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a("mod") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a("<<") });
                _operatorTable.Add(new object[] { 400, Atom.a("yfx"), Atom.a(">>") });
                _operatorTable.Add(new object[] { 200, Atom.a("xfx"), Atom.a("**") });
                _operatorTable.Add(new object[] { 200, Atom.a("xfy"), Atom.a("^") });
                _operatorTable.Add(new object[] { 200, Atom.a("fy"), Atom.a("-") });
                _operatorTable.Add(new object[] { 200, Atom.a("fy"), Atom.a("\\") });
                // Debug: This is hacked in to run the Prolog test suite until we implement op/3.
                _operatorTable.Add(new object[] { 20, Atom.a("xfx"), Atom.a("<--") });
            }

            object[] args = new object[] { Priority, Specifier, Operator };
            foreach (object[] answer in _operatorTable)
            {
                foreach (bool l1 in YP.unifyArrays(args, answer))
                    yield return false;
            }
        }

        public static IEnumerable<bool> atom_length(object atom, object Length)
        {
            return YP.unify(Length, ((Atom)YP.getValue(atom))._name.Length);
        }

        public static IEnumerable<bool> atom_concat(object Start, object End, object Whole)
        {
            // Debug: Should implement for var(Start) which is a kind of search.
            // Debug: Should we try to preserve the _declaringClass?
            return YP.unify(Whole, Atom.a(((Atom)YP.getValue(Start))._name +
                ((Atom)YP.getValue(End))._name));
        }

        public static IEnumerable<bool> sub_atom
            (object atom, object Before, object Length, object After, object Sub_atom)
        {
            // Debug: Should implement for var(atom) which is a kind of search.
            // Debug: Should we try to preserve the _declaringClass?
            Atom atomAtom = (Atom)YP.getValue(atom);
            int beforeInt = YP.convertInt(Before);
            int lengthInt = YP.convertInt(Length);
            if (beforeInt < 0)
                throw new Exception("Before must be non-negative");
            if (lengthInt < 0)
                throw new Exception("Length must be non-negative");
            int afterInt = atomAtom._name.Length - (beforeInt + lengthInt);
            if (afterInt >= 0)
            {
                foreach (bool l1 in YP.unify(After, afterInt))
                {
                    foreach (bool l2 in YP.unify
                        (Sub_atom, Atom.a(atomAtom._name.Substring(beforeInt, lengthInt))))
                        yield return false;
                }
            }
        }

        public static IEnumerable<bool> atom_codes(object atom, object List)
        {
            atom = YP.getValue(atom);
            List = YP.getValue(List);

            if (nonvar(atom))
            {
                string name = ((Atom)atom)._name;
                object codeList = Atom.NIL;
                // Start from the back to make the list.
                for (int i = name.Length - 1; i >= 0; --i)
                    codeList = new ListPair((int)name[i], codeList);
                return YP.unify(List, codeList);
            }
            {
                object[] codeArray = ListPair.toArray(List);
                char[] charArray = new char[codeArray.Length];
                for (int i = 0; i < codeArray.Length; ++i)
                    charArray[i] = (char)YP.convertInt(codeArray[i]);
                return YP.unify(atom, Atom.a(new String(charArray)));
            }
        }

        public static IEnumerable<bool> number_codes(object number, object List)
        {
            number = YP.getValue(number);
            List = YP.getValue(List);

            if (nonvar(number))
            {
                string numberString = null;
                // Try converting to an int first.
                int intNumber;
                if (YP.getInt(number, out intNumber))
                    numberString = intNumber.ToString();
                else
                    numberString = YP.doubleToString(YP.convertDouble(number));

                object codeList = Atom.NIL;
                // Start from the back to make the list.
                for (int i = numberString.Length - 1; i >= 0; --i)
                    codeList = new ListPair((int)numberString[i], codeList);
                return YP.unify(List, codeList);
            }
            {
                object[] codeArray = ListPair.toArray(List);
                char[] charArray = new char[codeArray.Length];
                for (int i = 0; i < codeArray.Length; ++i)
                    charArray[i] = (char)YP.convertInt(codeArray[i]);
                String numberString = new String(charArray);
                // Debug: Is there a way in C# to ask if a string parses as int without throwing an exception?
                try
                {
                    // Try an int first.
                    return YP.unify(number, Convert.ToInt32(numberString));
                }
                catch (FormatException) { }
                return YP.unify(number, Convert.ToDouble(numberString));
            }
        }

        /// <summary>
        /// If term is an Atom or functor type, return its name.
        /// Otherwise, return term.
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        public static object getFunctorName(object term)
        {
            term = YP.getValue(term);
            if (term is Functor1)
                return ((Functor1)term)._name;
            else if (term is Functor2)
                return ((Functor2)term)._name;
            else if (term is Functor3)
                return ((Functor3)term)._name;
            else if (term is Functor)
                return ((Functor)term)._name;
            else
                return term;
        }

        /// <summary>
        /// If term is an Atom or functor type, return an array of its args.
        /// Otherwise, return an empty array.
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        public static object[] getFunctorArgs(object term)
        {
            term = YP.getValue(term);
            if (term is Functor1)
            {
                Functor1 functor = (Functor1)term;
                return new object[] { functor._arg1 };
            }
            else if (term is Functor2)
            {
                Functor2 functor = (Functor2)term;
                return new object[] { functor._arg1, functor._arg2 };
            }
            else if (term is Functor3)
            {
                Functor3 functor = (Functor3)term;
                return new object[] { functor._arg1, functor._arg2, functor._arg3 };
            }
            else if (term is Functor) {
                Functor functor = (Functor)term;
                return functor._args;
            }
            else
                return new object[0];
        }

        public static bool var(object Term)
        {
            return YP.getValue(Term) is Variable;
        }

        public static bool nonvar(object Term)
        {
            return !YP.var(Term);
        }

        public static bool atom(object Term)
        {
            return YP.getValue(Term) is Atom;
        }

        public static bool number(object Term)
        {
            Term = getValue(Term);
            // Debug: Should exhaustively check for all number types.
            return Term is int || Term is double;
        }

        public static bool atomic(object Term)
        {
            return YP.atom(Term) || YP.number(Term);
        }

        public static bool compound(object Term)
        {
            Term = getValue(Term);
            return Term is Functor1 || Term is Functor2 || Term is Functor3 || Term is Functor;
        }

        public static void see(object input)
        {
            input = YP.getValue(input);
            if (input is TextReader)
            {
                _inputStream = (TextReader)input;
                return;
            }
            else if (input is Atom)
            {
                _inputStream = new StreamReader(((Atom)input)._name);
                return;
            }
            else if (input is String)
            {
                _inputStream = new StreamReader((String)input);
                return;
            }
            else
                throw new InvalidOperationException("Can't open stream for " + input);
        }

        public static void seen()
        {
            if (_inputStream == Console.In)
                return;
            _inputStream.Close();
            _inputStream = Console.In;
        }

        public static void tell(object output)
        {
            output = YP.getValue(output);
            if (output is TextWriter)
            {
                _outputStream = (TextWriter)output;
                return;
            }
            else if (output is Atom)
            {
                _outputStream = new StreamWriter(((Atom)output)._name);
                return;
            }
            else if (output is String)
            {
                _outputStream = new StreamWriter((String)output);
                return;
            }
            else
                throw new InvalidOperationException("Can't open stream for " + output);
        }

        public static void told()
        {
            if (_outputStream == Console.Out)
                return;
            _outputStream.Close();
            _outputStream = Console.Out;
        }

        public static void write(object x)
        {
            x = YP.getValue(x);
            if (x is double)
                _outputStream.Write(doubleToString((double)x));
            else
                _outputStream.Write(x.ToString());
        }

        /// <summary>
        /// Format x as a string, making sure that it will parse as an int later.  I.e., for 1.0, don't just
        /// use "1" which will parse as an int.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private static string doubleToString(double x)
        {
            string xString = x.ToString();
            // Debug: Is there a way in C# to ask if a string parses as int without throwing an exception?
            try
            {
                Convert.ToInt32(xString);
                // The string will parse as an int, not a double, so re-format so that it does.
                // Use float if possible, else exponential if it would be too big.
                return x.ToString(x >= 100000.0 ? "E1" : "f1");
            }
            catch (FormatException)
            {
                // Assume it will parse as a double.
            }
            return xString;
        }

        public static void put_code(object x)
        {
            _outputStream.Write((char)YP.convertInt(x));
        }

        public static void nl()
        {
            _outputStream.WriteLine();
        }

        public static IEnumerable<bool> get_code(object code)
        {
            return YP.unify(code, _inputStream.Read());
        }

        public static void assertFact(Atom name, object[] values)
        {
            NameArity nameArity = new NameArity(name, values.Length);
            List<IClause> clauses;
            IndexedAnswers indexedAnswers;
            if (!_predicatesStore.TryGetValue(nameArity, out clauses))
            {
                // Create an IndexedAnswers as the first clause of the predicate.                
                _predicatesStore[nameArity] = (clauses = new List<IClause>());
                clauses.Add(indexedAnswers = new IndexedAnswers());
            }
            else
            {
                indexedAnswers = clauses[clauses.Count - 1] as IndexedAnswers;
                if (indexedAnswers == null)
                    // The latest clause is not an IndexedAnswers, so add one.
                    clauses.Add(indexedAnswers = new IndexedAnswers());
            }

            indexedAnswers.addAnswer(values);
        }

        public static IEnumerable<bool> matchFact(Atom name, object[] arguments)
        {
            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(new NameArity(name, arguments.Length), out clauses))
                throw new UndefinedPredicateException
                    ("Undefined fact: " + name + "/" + arguments.Length, name, 
                    arguments.Length);

            if (clauses.Count == 1)
                // Usually there is only one clause, so return it without needing to wrap it in an iterator.
                return clauses[0].match(arguments);
            else
                return matchAllClauses(clauses, arguments);
        }

        /// <summary>
        /// Call match(arguments) for each IClause in clauses.  We make this a separate
        /// function so that matchFact itself does not need to be an iterator object.
        /// </summary>
        /// <param name="clauses"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static IEnumerable<bool> matchAllClauses(List<IClause> clauses, object[] arguments)
        {
            foreach (IClause clause in clauses)
            {
                foreach (bool lastCall in clause.match(arguments))
                    yield return false;
            }
        }

        public static void retractFact(Atom name, object[] arguments)
        {
            NameArity nameArity = new NameArity(name, arguments.Length);
            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(nameArity, out clauses))
                // Can't find, so ignore.
                return;

            foreach (object arg in arguments)
            {
                if (!YP.var(arg))
                    throw new InvalidOperationException("All arguments must be unbound");
            }
            // Set to a fresh empty IndexedAnswers.
            _predicatesStore[nameArity] = (clauses = new List<IClause>());
            clauses.Add(new IndexedAnswers());
        }

        public static IEnumerable<bool> current_predicate(object NameSlashArity)
        {
            NameSlashArity = YP.getValue(NameSlashArity);
            // First check if Name and Arity are nonvar so we can do a direct lookup.
            if (YP.ground(NameSlashArity))
            {
                if (NameSlashArity is Functor2)
                {
                    Functor2 NameArityFunctor = (Functor2)NameSlashArity;
                    if (NameArityFunctor._name == Atom.SLASH)
                    {
                        if (_predicatesStore.ContainsKey(new NameArity
                             ((Atom)YP.getValue(NameArityFunctor._arg1),
                              (int)YP.getValue(NameArityFunctor._arg2))))
                            // The predicate is defined.
                            yield return false;
                    }
                }
                yield break;
            }

            foreach (NameArity key in _predicatesStore.Keys)
            {
                foreach (bool l1 in YP.unify
                    (new Functor2(Atom.SLASH, key._name, key._arity), NameSlashArity))
                    yield return false;
            }
        }

        /// <summary>
        /// Use YP.getFunctorName(Goal) and invoke the static method of this name in the 
        /// declaringClass, using arguments from YP.getFunctorArgs(Goal).
        /// Note that Goal must be a simple functor, not a complex expression.
        /// If not found, this throws UndefinedPredicateException.
        /// </summary>
        /// <param name="Goal"></param>
        /// <param name="contextClass">the class for looking up default function references</param>
        /// <returns></returns>
        public static IEnumerable<bool> getIterator(object Goal, Type declaringClass)
        {
#if true
            List<Variable> variableSetList = new List<Variable>();
            addUniqueVariables(Goal, variableSetList);
            Variable[] variableSet = variableSetList.ToArray();
            object Head = Functor.make("function", variableSet);

            object Rule = new Functor2(Atom.RULE, Head, Goal);
            object RuleList = ListPair.make(new Functor2(Atom.F, Rule, Atom.NIL));
            StringWriter functionCode = new StringWriter();
            TextWriter saveOutputStream = _outputStream;
            try
            {
                tell(functionCode);
                Variable FunctionCode = new Variable();
                foreach (bool l1 in YPCompiler.makeFunctionPseudoCode(RuleList, FunctionCode))
                {
                    if (YP.termEqual(FunctionCode, Atom.a("getDeclaringClass")))
                        // Ignore getDeclaringClass since we have access to the one passed in.
                        continue;

                    // Debug: should check if FunctionCode is a single call.
                    YPCompiler.convertFunctionCSharp(FunctionCode);
                }
                told();
            }
            finally
            {
                // Restore after calling tell.
                _outputStream = saveOutputStream;
            }
            return YPCompiler.compileAnonymousFunction
                (functionCode.ToString(), variableSet.Length, declaringClass).match(variableSet);
#else
            Goal = YP.getValue(Goal);
            Atom name;
            object[] args;
            while (true)
            {
                name = (Atom)YP.getFunctorName(Goal);
                args = YP.getFunctorArgs(Goal);
                if (name == Atom.HAT && args.Length == 2)
                    // Assume this is called from a bagof operation.  Skip the leading qualifiers.
                    Goal = YP.getValue(((Functor2)Goal)._arg2);
                else
                    break;
            }
            try
            {
                return (IEnumerable<bool>)declaringClass.InvokeMember
                  (name._name, BindingFlags.InvokeMethod, null, null, args);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
            catch (MissingMethodException)
            {
                throw new UndefinedPredicateException
                    ("Cannot find predicate function: " + name + "/" + args.Length + " in " +
                     declaringClass.FullName, name, args.Length);
            }
#endif
        }

        public static void throwException(object Term)
        {
            throw new PrologException(Term);
        }

        /// <summary>
        /// script_event calls hosting script with events as a callback method.
        /// </summary>
        /// <param name="script_event"></param>
        /// <param name="script_params"></param>
        /// <returns></returns>
        public static void script_event(object script_event, object script_params)
        {
            string function = ((Atom)YP.getValue(script_event))._name;
            object[] array = ListPair.toArray(script_params);
            if (array == null)
                return; // YP.fail();
            if (array.Length > 1)
            {
                //m_CmdManager.m_ScriptEngine.m_EventQueManager.AddToScriptQueue
                //(localID, itemID, function, array);
                // sortArray(array);
            }
            //return YP.unify(Sorted, ListPair.makeWithoutRepeatedTerms(array));
        }

        /// <summary>
        /// An enumerator that does zero loops.
        /// </summary>
        private class Fail : IEnumerator<bool>, IEnumerable<bool>
        {
            public bool MoveNext()
            {
                return false;
            }

            public IEnumerator<bool> GetEnumerator()
            {
                return (IEnumerator<bool>)this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool Current
            {
                get { return true; }
            }

            object IEnumerator.Current
            {
                get { return true; }
            }

            public void Dispose()
            {
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// An enumerator that does one iteration.
        /// </summary>
        private class Succeed : IEnumerator<bool>, IEnumerable<bool>
        {
            private bool _didIteration = false;

            public bool MoveNext()
            {
                if (!_didIteration)
                {
                    _didIteration = true;
                    return true;
                }
                else
                    return false;
            }

            public IEnumerator<bool> GetEnumerator()
            {
                return (IEnumerator<bool>)this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool Current
            {
                get { return false; }
            }

            object IEnumerator.Current
            {
                get { return false; }
            }

            public void Dispose()
            {
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// An enumerator that repeats forever.
        /// </summary>
        private class Repeat : IEnumerator<bool>, IEnumerable<bool>
        {
            public bool MoveNext()
            {
                return true;
            }

            public IEnumerator<bool> GetEnumerator()
            {
                return (IEnumerator<bool>)this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool Current
            {
                get { return false; }
            }

            object IEnumerator.Current
            {
                get { return false; }
            }

            public void Dispose()
            {
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
