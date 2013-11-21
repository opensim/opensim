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
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using log4net;

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
{
    /// <summary>
    /// YP has static methods for general functions in Yield Prolog such as <see cref="getValue"/>
    /// and <see cref="unify"/>.
    /// </summary>
    public class YP
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static Fail _fail = new Fail();
        private static Repeat _repeat = new Repeat();
        private static Dictionary<NameArity, List<IClause>> _predicatesStore =
            new Dictionary<NameArity, List<IClause>>();
        private static TextWriter _outputStream = System.Console.Out;
        private static TextReader _inputStream = System.Console.In;
        private static IndexedAnswers _operatorTable = null;
        private static Dictionary<string, object> _prologFlags = new Dictionary<string, object>();
        public const int MAX_ARITY = 255;

        /// <summary>
        /// An IClause is used so that dynamic predicates can call match.
        /// </summary>
        public interface IClause
        {
            IEnumerable<bool> match(object[] args);
            IEnumerable<bool> clause(object Head, object Body);
        }

        /// <summary>
        /// If value is a Variable, then return its getValue.  Otherwise, just
        /// return value.  You should call YP.getValue on any object that
        /// may be a Variable to get the value to pass to other functions in
        /// your system that are not part of Yield Prolog, such as math functions
        /// or file I/O.
        /// For more details, see http://yieldprolog.sourceforge.net/tutorial1.html
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object getValue(object value)
        {
            if (value is Variable)
                return ((Variable)value).getValue();
            else
                return value;
        }

        /// <summary>
        /// If arg1 or arg2 is an object with a unify method (such as Variable or
        /// Functor) then just call its unify with the other argument.  The object's
        /// unify method will bind the values or check for equals as needed.
        /// Otherwise, both arguments are "normal" (atomic) values so if they
        /// are equal then succeed (yield once), else fail (don't yield).
        /// For more details, see http://yieldprolog.sourceforge.net/tutorial1.html
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
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
        /// (to handle the char types like "a").
        /// If can't convert, throw a PrologException for type_error evaluable (because this is only
        ///   called from arithmetic functions).
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
            if (term is Variable)
                throw new PrologException(Atom.a("instantiation_error"),
                    "Expected a number but the argument is an unbound variable");

            try
            {
                return (int)term;
            }
            catch (InvalidCastException)
            {
                throw new PrologException
                    (new Functor2
                     ("type_error", Atom.a("evaluable"),
                      new Functor2(Atom.SLASH, getFunctorName(term), getFunctorArgs(term).Length)),
                     "Term must be an integer");
            }
        }

        /// <summary>
        /// Convert term to a double.  This may convert an int to a double, etc.
        /// If term is a single-element List, use its first element
        /// (to handle the char types like "a").
        /// If can't convert, throw a PrologException for type_error evaluable (because this is only
        ///   called from arithmetic functions).
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
            if (term is Variable)
                throw new PrologException(Atom.a("instantiation_error"),
                    "Expected a number but the argument is an unbound variable");

            try
            {
                return Convert.ToDouble(term);
            }
            catch (InvalidCastException)
            {
                throw new PrologException
                    (new Functor2
                     ("type_error", Atom.a("evaluable"),
                      new Functor2(Atom.SLASH, getFunctorName(term), getFunctorArgs(term).Length)),
                     "Term must be an integer");
            }
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

        // Use toFloat instead of float because it is a reserved keyword.
        public static object toFloat(object x)
        {
            return convertDouble(x);
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
            int z = 0;
            try
            {
                if (gotMatch)
                    yield return false;
            }
            finally
            {
                // Manually finalize all the iterators.
                for (z = 0; z < nIterators; ++z)
                    iterators[z].Dispose();
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

        // disable warning on l1, don't see how we can
        // code this differently
        #pragma warning disable 0168, 0219
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
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("list"), ArgList),
                        "Expected a list. Got: " + ArgList.getValue());
                if (args.Length == 0)
                    // Return the Name, even if it is not an Atom.
                    return YP.unify(Term, Name);
                if (args.Length > MAX_ARITY)
                    throw new PrologException
                        (new Functor1("representation_error", Atom.a("max_arity")),
                         "Functor arity " + args.Length + " may not be greater than " + MAX_ARITY);
                if (!atom(Name))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), Name),
                        "Expected an atom. Got: " + Name.getValue());

                return YP.unify(Term, Functor.make((Atom)YP.getValue(Name), args));
            }

            return YP.fail();
        }

        public static IEnumerable<bool> functor(object Term, object FunctorName, object Arity)
        {
            Term = YP.getValue(Term);
            FunctorName = YP.getValue(FunctorName);
            Arity = YP.getValue(Arity);

            if (Term is Variable)
            {
                if (FunctorName is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 2 FunctorName is an unbound variable");
                if (Arity is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 3 Arity is an unbound variable");
                if (!(Arity is int))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("integer"), Arity), "Arity is not an integer");
                if (!YP.atomic(FunctorName))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atomic"), FunctorName), "FunctorName is not atomic");

                if ((int)Arity < 0)
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("not_less_than_zero"), Arity),
                         "Arity may not be less than zero");
                else if ((int)Arity == 0)
                {
                    // Just unify Term with the atomic FunctorName.
                    foreach (bool l1 in YP.unify(Term, FunctorName))
                        yield return false;
                }
                else
                {
                    if ((int)Arity > MAX_ARITY)
                        throw new PrologException
                            (new Functor1("representation_error", Atom.a("max_arity")),
                             "Functor arity " + Arity + " may not be greater than " + MAX_ARITY);
                    if (!(FunctorName is Atom))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("atom"), FunctorName), "FunctorName is not an atom");
                    // Construct a functor with unbound variables.
                    object[] args = new object[(int)Arity];
                    for (int i = 0; i < args.Length; ++i)
                        args[i] = new Variable();
                    foreach (bool l1 in YP.unify(Term, Functor.make((Atom)FunctorName, args)))
                        yield return false;
                }
            }
            else
            {
                foreach (bool l1 in YP.unify(FunctorName, getFunctorName(Term)))
                {
                    foreach (bool l2 in YP.unify(Arity, getFunctorArgs(Term).Length))
                        yield return false;
                }
            }
        }

        public static IEnumerable<bool> arg(object ArgNumber, object Term, object Value)
        {
            if (var(ArgNumber))
                throw new PrologException(Atom.a("instantiation_error"), "Arg 1 ArgNumber is an unbound variable");
            int argNumberInt;
            if (!getInt(ArgNumber, out argNumberInt))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("integer"), ArgNumber), "Arg 1 ArgNumber must be integer");
            if (argNumberInt < 0)
                throw new PrologException
                    (new Functor2("domain_error", Atom.a("not_less_than_zero"), argNumberInt),
                    "ArgNumber may not be less than zero");

            if (YP.var(Term))
                throw new PrologException(Atom.a("instantiation_error"),
                    "Arg 2 Term is an unbound variable");
            if (!YP.compound(Term))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("compound"), Term), "Arg 2 Term must be compound");

            object[] termArgs = YP.getFunctorArgs(Term);
            // Silently fail if argNumberInt is out of range.
            if (argNumberInt >= 1 && argNumberInt <= termArgs.Length)
            {
                // The first ArgNumber is at 1, not 0.
                foreach (bool l1 in YP.unify(Value, termArgs[argNumberInt - 1]))
                    yield return false;
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
                _operatorTable = new IndexedAnswers(3);
                _operatorTable.addAnswer(new object[] { 1200, Atom.a("xfx"), Atom.a(":-") });
                _operatorTable.addAnswer(new object[] { 1200, Atom.a("xfx"), Atom.a("-->") });
                _operatorTable.addAnswer(new object[] { 1200, Atom.a("fx"), Atom.a(":-") });
                _operatorTable.addAnswer(new object[] { 1200, Atom.a("fx"), Atom.a("?-") });
                _operatorTable.addAnswer(new object[] { 1100, Atom.a("xfy"), Atom.a(";") });
                _operatorTable.addAnswer(new object[] { 1050, Atom.a("xfy"), Atom.a("->") });
                _operatorTable.addAnswer(new object[] { 1000, Atom.a("xfy"), Atom.a(",") });
                _operatorTable.addAnswer(new object[] { 900, Atom.a("fy"), Atom.a("\\+") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("=") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("\\=") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("==") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("\\==") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("@<") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("@=<") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("@>") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("@>=") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("=..") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("is") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("=:=") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("=\\=") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("<") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a("=<") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a(">") });
                _operatorTable.addAnswer(new object[] { 700, Atom.a("xfx"), Atom.a(">=") });
                _operatorTable.addAnswer(new object[] { 600, Atom.a("xfy"), Atom.a(":") });
                _operatorTable.addAnswer(new object[] { 500, Atom.a("yfx"), Atom.a("+") });
                _operatorTable.addAnswer(new object[] { 500, Atom.a("yfx"), Atom.a("-") });
                _operatorTable.addAnswer(new object[] { 500, Atom.a("yfx"), Atom.a("/\\") });
                _operatorTable.addAnswer(new object[] { 500, Atom.a("yfx"), Atom.a("\\/") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a("*") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a("/") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a("//") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a("rem") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a("mod") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a("<<") });
                _operatorTable.addAnswer(new object[] { 400, Atom.a("yfx"), Atom.a(">>") });
                _operatorTable.addAnswer(new object[] { 200, Atom.a("xfx"), Atom.a("**") });
                _operatorTable.addAnswer(new object[] { 200, Atom.a("xfy"), Atom.a("^") });
                _operatorTable.addAnswer(new object[] { 200, Atom.a("fy"), Atom.a("-") });
                _operatorTable.addAnswer(new object[] { 200, Atom.a("fy"), Atom.a("\\") });
                // Debug: This is hacked in to run the Prolog test suite until we implement op/3.
                _operatorTable.addAnswer(new object[] { 20, Atom.a("xfx"), Atom.a("<--") });
            }

            return _operatorTable.match(new object[] { Priority, Specifier, Operator });
        }

        public static IEnumerable<bool> atom_length(object atom, object Length)
        {
            atom = YP.getValue(atom);
            Length = YP.getValue(Length);
            if (atom is Variable)
                throw new PrologException(Atom.a("instantiation_error"),
                    "Expected atom(Arg1) but it is an unbound variable");
            if (!(atom is Atom))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("atom"), atom), "Arg 1 Atom is not an atom");
            if (!(Length is Variable))
            {
                if (!(Length is int))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("integer"), Length), "Length must be var or integer");
                if ((int)Length < 0)
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("not_less_than_zero"), Length),
                        "Length must not be less than zero");
            }
            return YP.unify(Length, ((Atom)atom)._name.Length);
        }

        public static IEnumerable<bool> atom_concat(object Start, object End, object Whole)
        {
            // Debug: Should we try to preserve the _declaringClass?
            Start = YP.getValue(Start);
            End = YP.getValue(End);
            Whole = YP.getValue(Whole);
            if (Whole is Variable)
            {
                if (Start is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 1 Start and arg 3 Whole are both var");
                if (End is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 2 End and arg 3 Whole are both var");
                if (!(Start is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), Start), "Arg 1 Start is not an atom");
                if (!(End is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), End), "Arg 2 End is not an atom");

                foreach (bool l1 in YP.unify(Whole, Atom.a(((Atom)Start)._name + ((Atom)End)._name)))
                    yield return false;
            }
            else
            {
                if (!(Whole is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), Whole), "Arg 3 Whole is not an atom");
                bool gotStartLength = false;
                int startLength = 0;
                if (!(Start is Variable))
                {
                    if (!(Start is Atom))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("atom"), Start), "Arg 1 Start is not var or atom");
                    startLength = ((Atom)Start)._name.Length;
                    gotStartLength = true;
                }

                bool gotEndLength = false;
                int endLength = 0;
                if (!(End is Variable))
                {
                    if (!(End is Atom))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("atom"), End), "Arg 2 End is not var or atom");
                    endLength = ((Atom)End)._name.Length;
                    gotEndLength = true;
                }

                // We are doing a search through all possible Start and End which concatenate to Whole.
                string wholeString = ((Atom)Whole)._name;
                for (int i = 0; i <= wholeString.Length; ++i)
                {
                    // If we got either startLength or endLength, we know the lengths have to match so check
                    //   the lengths instead of constructing an Atom to do it.
                    if (gotStartLength && startLength != i)
                        continue;
                    if (gotEndLength && endLength != wholeString.Length - i)
                        continue;
                    foreach (bool l1 in YP.unify(Start, Atom.a(wholeString.Substring(0, i))))
                    {
                        foreach (bool l2 in YP.unify(End, Atom.a(wholeString.Substring(i, wholeString.Length - i))))
                            yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> sub_atom
            (object atom, object Before, object Length, object After, object Sub_atom)
        {
            // Debug: Should we try to preserve the _declaringClass?
            atom = YP.getValue(atom);
            Before = YP.getValue(Before);
            Length = YP.getValue(Length);
            After = YP.getValue(After);
            Sub_atom = YP.getValue(Sub_atom);
            if (atom is Variable)
                throw new PrologException(Atom.a("instantiation_error"),
                    "Expected atom(Arg1) but it is an unbound variable");
            if (!(atom is Atom))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("atom"), atom), "Arg 1 Atom is not an atom");
            if (!(Sub_atom is Variable))
            {
                if (!(Sub_atom is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), Sub_atom), "Sub_atom is not var or atom");
            }

            bool beforeIsInt = false;
            bool lengthIsInt = false;
            bool afterIsInt = false;
            if (!(Before is Variable))
            {
                if (!(Before is int))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("integer"), Before), "Before must be var or integer");
                beforeIsInt = true;
                if ((int)Before < 0)
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("not_less_than_zero"), Before),
                        "Before must not be less than zero");
            }
            if (!(Length is Variable))
            {
                if (!(Length is int))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("integer"), Length), "Length must be var or integer");
                lengthIsInt = true;
                if ((int)Length < 0)
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("not_less_than_zero"), Length),
                        "Length must not be less than zero");
            }
            if (!(After is Variable))
            {
                if (!(After is int))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("integer"), After), "After must be var or integer");
                afterIsInt = true;
                if ((int)After < 0)
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("not_less_than_zero"), After),
                        "After must not be less than zero");
            }

            Atom atomAtom = (Atom)atom;
            int atomLength = atomAtom._name.Length;
            if (beforeIsInt && lengthIsInt)
            {
                // Special case: the caller is just trying to extract a substring, so do it quickly.
                int xAfter = atomLength - (int)Before - (int)Length;
                if (xAfter >= 0)
                {
                    foreach (bool l1 in YP.unify(After, xAfter))
                    {
                        foreach (bool l2 in YP.unify
                            (Sub_atom, Atom.a(atomAtom._name.Substring((int)Before, (int)Length))))
                            yield return false;
                    }
                }
            }
            else if (afterIsInt && lengthIsInt)
            {
                // Special case: the caller is just trying to extract a substring, so do it quickly.
                int xBefore = atomLength - (int)After - (int)Length;
                if (xBefore >= 0)
                {
                    foreach (bool l1 in YP.unify(Before, xBefore))
                    {
                        foreach (bool l2 in YP.unify
                            (Sub_atom, Atom.a(atomAtom._name.Substring(xBefore, (int)Length))))
                            yield return false;
                    }
                }
            }
            else
            {
                // We are underconstrained and doing a search, so go through all possibilities.
                for (int xBefore = 0; xBefore <= atomLength; ++xBefore)
                {
                    foreach (bool l1 in YP.unify(Before, xBefore))
                    {
                        for (int xLength = 0; xLength <= (atomLength - xBefore); ++xLength)
                        {
                            foreach (bool l2 in YP.unify(Length, xLength))
                            {
                                foreach (bool l3 in YP.unify(After, atomLength - (xBefore + xLength)))
                                {
                                    foreach (bool l4 in YP.unify
                                        (Sub_atom, Atom.a(atomAtom._name.Substring(xBefore, xLength))))
                                        yield return false;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> atom_chars(object atom, object List)
        {
            atom = YP.getValue(atom);
            List = YP.getValue(List);

            if (atom is Variable)
            {
                if (List is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 1 Atom and arg 2 List are both unbound variables");
                object[] codeArray = ListPair.toArray(List);
                if (codeArray == null)
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("list"), List), "Arg 2 List is not a list");

                char[] charArray = new char[codeArray.Length];
                for (int i = 0; i < codeArray.Length; ++i)
                {
                    object listAtom = YP.getValue(codeArray[i]);
                    if (listAtom is Variable)
                        throw new PrologException(Atom.a("instantiation_error"),
                            "Arg 2 List has an element which is an unbound variable");
                    if (!(listAtom is Atom && ((Atom)listAtom)._name.Length == 1))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("character"), listAtom),
                             "Arg 2 List has an element which is not a one character atom");
                    charArray[i] = ((Atom)listAtom)._name[0];
                }
                return YP.unify(atom, Atom.a(new String(charArray)));
            }
            else
            {
                if (!(atom is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), atom), "Arg 1 Atom is not var or atom");

                string atomString = ((Atom)atom)._name;
                object charList = Atom.NIL;
                // Start from the back to make the list.
                for (int i = atomString.Length - 1; i >= 0; --i)
                    charList = new ListPair(Atom.a(atomString.Substring(i, 1)), charList);
                return YP.unify(List, charList);
            }
        }

        public static IEnumerable<bool> atom_codes(object atom, object List)
        {
            atom = YP.getValue(atom);
            List = YP.getValue(List);

            if (atom is Variable)
            {
                if (List is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 1 Atom and arg 2 List are both unbound variables");
                object[] codeArray = ListPair.toArray(List);
                if (codeArray == null)
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("list"), List), "Arg 2 List is not a list");

                char[] charArray = new char[codeArray.Length];
                for (int i = 0; i < codeArray.Length; ++i)
                {
                    int codeInt;
                    if (!getInt(codeArray[i], out codeInt) || codeInt < 0)
                        throw new PrologException
                            (new Functor1("representation_error", Atom.a("character_code")),
                             "Element of Arg 2 List is not a character code");
                    charArray[i] = (char)codeInt;
                }
                return YP.unify(atom, Atom.a(new String(charArray)));
            }
            else
            {
                if (!(atom is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), atom), "Arg 1 Atom is not var or atom");

                string atomString = ((Atom)atom)._name;
                object codeList = Atom.NIL;
                // Start from the back to make the list.
                for (int i = atomString.Length - 1; i >= 0; --i)
                    codeList = new ListPair((int)atomString[i], codeList);
                return YP.unify(List, codeList);
            }
        }

        public static IEnumerable<bool> number_chars(object Number, object List)
        {
            Number = YP.getValue(Number);
            List = YP.getValue(List);

            if (Number is Variable)
            {
                if (List is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 1 Number and arg 2 List are both unbound variables");
                object[] codeArray = ListPair.toArray(List);
                if (codeArray == null)
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("list"), List), "Arg 2 List is not a list");

                char[] charArray = new char[codeArray.Length];
                for (int i = 0; i < codeArray.Length; ++i)
                {
                    object listAtom = YP.getValue(codeArray[i]);
                    if (listAtom is Variable)
                        throw new PrologException(Atom.a("instantiation_error"),
                            "Arg 2 List has an element which is an unbound variable");
                    if (!(listAtom is Atom && ((Atom)listAtom)._name.Length == 1))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("character"), listAtom),
                             "Arg 2 List has an element which is not a one character atom");
                    charArray[i] = ((Atom)listAtom)._name[0];
                }
                return YP.unify(Number, parseNumberString(charArray));
            }
            else
            {
                string numberString = null;
                // Try converting to an int first.
                int intNumber;
                if (YP.getInt(Number, out intNumber))
                    numberString = intNumber.ToString();
                else
                {
                    if (!YP.number(Number))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("number"), Number),
                            "Arg 1 Number is not var or number");
                    // We just checked, so convertDouble shouldn't throw an exception.
                    numberString = YP.doubleToString(YP.convertDouble(Number));
                }

                object charList = Atom.NIL;
                // Start from the back to make the list.
                for (int i = numberString.Length - 1; i >= 0; --i)
                    charList = new ListPair(Atom.a(numberString.Substring(i, 1)), charList);
                return YP.unify(List, charList);
            }
        }

        public static IEnumerable<bool> number_codes(object Number, object List)
        {
            Number = YP.getValue(Number);
            List = YP.getValue(List);

            if (Number is Variable)
            {
                if (List is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 1 Number and arg 2 List are both unbound variables");
                object[] codeArray = ListPair.toArray(List);
                if (codeArray == null)
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("list"), List), "Arg 2 List is not a list");

                char[] charArray = new char[codeArray.Length];
                for (int i = 0; i < codeArray.Length; ++i)
                {
                    int codeInt;
                    if (!getInt(codeArray[i], out codeInt) || codeInt < 0)
                        throw new PrologException
                            (new Functor1("representation_error", Atom.a("character_code")),
                             "Element of Arg 2 List is not a character code");
                    charArray[i] = (char)codeInt;
                }
                return YP.unify(Number, parseNumberString(charArray));
            }
            else
            {
                string numberString = null;
                // Try converting to an int first.
                int intNumber;
                if (YP.getInt(Number, out intNumber))
                    numberString = intNumber.ToString();
                else
                {
                    if (!YP.number(Number))
                        throw new PrologException
                            (new Functor2("type_error", Atom.a("number"), Number),
                            "Arg 1 Number is not var or number");
                    // We just checked, so convertDouble shouldn't throw an exception.
                    numberString = YP.doubleToString(YP.convertDouble(Number));
                }

                object codeList = Atom.NIL;
                // Start from the back to make the list.
                for (int i = numberString.Length - 1; i >= 0; --i)
                    codeList = new ListPair((int)numberString[i], codeList);
                return YP.unify(List, codeList);
            }
        }

        /// <summary>
        /// Used by number_chars and number_codes.  Return the number in charArray or
        /// throw an exception if can't parse.
        /// </summary>
        /// <param name="numberString"></param>
        /// <returns></returns>
        private static object parseNumberString(char[] charArray)
        {
            string numberString = new String(charArray);
            if (charArray.Length == 3 && numberString.StartsWith("0'"))
                // This is a char code.
                return (int)charArray[2];
            if (numberString.StartsWith("0x"))
            {
                try
                {
                    return Int32.Parse
                        (numberString.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
                }
                catch (FormatException)
                {
                    throw new PrologException
                        (new Functor1("syntax_error", Atom.a("number_format: " + numberString)),
                         "Arg 2 List is not a list for a hexadecimal number");
                }
            }
            // Debug: Is there a way in C# to ask if a string parses as int without throwing an exception?
            try
            {
                // Try an int first.
                return Convert.ToInt32(numberString);
            }
            catch (FormatException) { }
            try
            {
                return Convert.ToDouble(numberString);
            }
            catch (FormatException)
            {
                throw new PrologException
                    (new Functor1("syntax_error", Atom.a("number_format: " + numberString)),
                     "Arg 2 List is not a list for a number");
            }
        }

        public static IEnumerable<bool> char_code(object Char, object Code)
        {
            Char = YP.getValue(Char);
            Code = YP.getValue(Code);

            int codeInt = 0;
            if (!(Code is Variable))
            {
                // Get codeInt now so we type check it whether or not Char is Variable.
                if (!getInt(Code, out codeInt))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("integer"), Code),
                         "Arg 2 Code is not var or a character code");
                if (codeInt < 0)
                    throw new PrologException
                        (new Functor1("representation_error", Atom.a("character_code")),
                         "Arg 2 Code is not a character code");
            }

            if (Char is Variable)
            {
                if (Code is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "Arg 1 Char and arg 2 Code are both unbound variables");

                return YP.unify(Char, Atom.a(new String(new char[] {(char)codeInt})));
            }
            else
            {
                if (!(Char is Atom) || ((Atom)Char)._name.Length != 1)
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("character"), Char),
                         "Arg 1 Char is not var or one-character atom");

                if (Code is Variable)
                    return YP.unify(Code, (int)((Atom)Char)._name[0]);
                else
                    // Use codeInt to handle whether Code is supplied as, e.g., 97 or 0'a .
                    return YP.unify(codeInt, (int)((Atom)Char)._name[0]);
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

        public static bool integer(object Term)
        {
            // Debug: Should exhaustively check for all integer types.
            return getValue(Term) is int;
        }

        // Use isFloat instead of float because it is a reserved keyword.
        public static bool isFloat(object Term)
        {
            // Debug: Should exhaustively check for all float types.
            return getValue(Term) is double;
        }

        public static bool number(object Term)
        {
            return YP.integer(Term) || YP.isFloat(Term);
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

        /// <summary>
        /// If input is a TextReader, use it. If input is an Atom or String, create a StreamReader with the
        /// input as the filename.  If input is a Prolog list, then read character codes from it.
        /// </summary>
        /// <param name="input"></param>
        public static void see(object input)
        {
            input = YP.getValue(input);
            if (input is Variable)
                throw new PrologException(Atom.a("instantiation_error"), "Arg is an unbound variable");

            if (input == null)
            {
                _inputStream = null;
                return;
            }
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
            else if (input is Functor2 && ((Functor2)input)._name == Atom.DOT)
            {
                _inputStream = new CodeListReader(input);
                return;
            }
            else
                throw new PrologException
                    (new Functor2("domain_error", Atom.a("stream_or_alias"), input),
                     "Input stream specifier not recognized");
        }

        public static void seen()
        {
            if (_inputStream == null)
                return;
            if (_inputStream == Console.In)
                return;
            _inputStream.Close();
            _inputStream = Console.In;
        }

        public static IEnumerable<bool> current_input(object Stream)
        {
            return YP.unify(Stream, _inputStream);
        }

        /// <summary>
        /// If output is a TextWriter, use it.  If output is an Atom or a String, create a StreamWriter
        /// with the input as the filename.
        /// </summary>
        /// <param name="output"></param>
        public static void tell(object output)
        {
            output = YP.getValue(output);
            if (output is Variable)
                throw new PrologException(Atom.a("instantiation_error"), "Arg is an unbound variable");

            if (output == null)
            {
                _outputStream = null;
                return;
            }
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
                throw new PrologException
                    (new Functor2("domain_error", Atom.a("stream_or_alias"), output),
                     "Can't open stream for " + output);
        }

        public static void told()
        {
            if (_outputStream == null)
                return;
            if (_outputStream == Console.Out)
                return;
            _outputStream.Close();
            _outputStream = Console.Out;
        }

        public static IEnumerable<bool> current_output(object Stream)
        {
            return YP.unify(Stream, _outputStream);
        }

        public static void write(object x)
        {
            if (_outputStream == null)
                return;
            x = YP.getValue(x);
            if (x is double)
                _outputStream.Write(doubleToString((double)x));
            else
                _outputStream.Write(x.ToString());
        }

        /// <summary>
        /// Format x as a string, making sure that it won't parse as an int later.  I.e., for 1.0, don't just
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
            if (_outputStream == null)
                return;
            if (var(x))
                throw new PrologException(Atom.a("instantiation_error"), "Arg 1 is an unbound variable");
            int xInt;
            if (!getInt(x, out xInt))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("integer"), x), "Arg 1 must be integer");
            _outputStream.Write((char)xInt);
        }

        public static void nl()
        {
            if (_outputStream == null)
                return;
            _outputStream.WriteLine();
        }

        public static IEnumerable<bool> get_code(object code)
        {
            if (_inputStream == null)
                return YP.unify(code, -1);
            else
            return YP.unify(code, _inputStream.Read());
        }

        public static void asserta(object Term, Type declaringClass)
        {
            assertDynamic(Term, declaringClass, true);
        }

        public static void assertz(object Term, Type declaringClass)
        {
            assertDynamic(Term, declaringClass, false);
        }

        public static void assertDynamic(object Term, Type declaringClass, bool prepend)
        {
            Term = getValue(Term);
            if (Term is Variable)
                throw new PrologException("instantiation_error", "Term to assert is an unbound variable");

            Variable.CopyStore copyStore = new Variable.CopyStore();
            object TermCopy = makeCopy(Term, copyStore);
            object Head, Body;
            if (TermCopy is Functor2 && ((Functor2)TermCopy)._name == Atom.RULE)
            {
                Head = YP.getValue(((Functor2)TermCopy)._arg1);
                Body = YP.getValue(((Functor2)TermCopy)._arg2);
                if (Head is Variable)
                    throw new PrologException("instantiation_error", "Head to assert is an unbound variable");
                if (Body is Variable)
                    throw new PrologException("instantiation_error", "Body to assert is an unbound variable");
            }
            else
            {
                Head = TermCopy;
                Body = Atom.a("true");
            }

            Atom name = getFunctorName(Head) as Atom;
            if (name == null)
                // name is a non-Atom, such as a number.
                throw new PrologException
                    (new Functor2("type_error", Atom.a("callable"), Head), "Term to assert is not callable");
            object[] args = getFunctorArgs(Head);
            if (isSystemPredicate(name, args.Length))
                throw new PrologException
                    (new Functor3("permission_error", Atom.a("modify"), Atom.a("static_procedure"),
                                  new Functor2(Atom.SLASH, name, args.Length)),
                     "Assert cannot modify static predicate " + name + "/" + args.Length);

            if (copyStore.getNUniqueVariables() == 0 && Body == Atom.a("true"))
            {
                // This is a fact with no unbound variables
                // assertFact and prependFact use IndexedAnswers, so don't we don't need to compile.
                if (prepend)
                    prependFact(name, args);
                else
                    assertFact(name, args);

                return;
            }

            IClause clause = YPCompiler.compileAnonymousClause(Head, Body, declaringClass);
            // We expect clause to be a ClauseHeadAndBody (from Compiler.compileAnonymousFunction)
            //   so we can set the Head and Body.
            if (clause is ClauseHeadAndBody)
                ((ClauseHeadAndBody)clause).setHeadAndBody(Head, Body);

            // Add the clause to the entry in _predicatesStore.
            NameArity nameArity = new NameArity(name, args.Length);
            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(nameArity, out clauses))
                // Create an entry for the nameArity.
                _predicatesStore[nameArity] = (clauses = new List<IClause>());

            if (prepend)
                clauses.Insert(0, clause);
            else
                clauses.Add(clause);
        }

        private static bool isSystemPredicate(Atom name, int arity)
        {
            if (arity == 2 && (name == Atom.a(",") || name == Atom.a(";") || name == Atom.DOT))
                return true;
            // Use the same mapping to static predicates in YP as the compiler.
            foreach (bool l1 in YPCompiler.functorCallYPFunctionName(name, arity, new Variable()))
                return true;
            // Debug: Do we need to check if name._module is null?
            return false;
        }

        /// <summary>
        /// Assert values at the end of the set of facts for the predicate with the
        /// name and with arity values.Length.
        /// </summary>
        /// <param name="name">must be an Atom</param>
        /// <param name="values">the array of arguments to the fact predicate.
        /// It is an error if an value has an unbound variable.</param>
        public static void assertFact(Atom name, object[] values)
        {
            NameArity nameArity = new NameArity(name, values.Length);
            List<IClause> clauses;
            IndexedAnswers indexedAnswers;
            if (!_predicatesStore.TryGetValue(nameArity, out clauses))
            {
                // Create an IndexedAnswers as the only clause of the predicate.
                _predicatesStore[nameArity] = (clauses = new List<IClause>());
                clauses.Add(indexedAnswers = new IndexedAnswers(values.Length));
            }
            else
            {
                indexedAnswers = null;
                if (clauses.Count >= 1)
                    indexedAnswers = clauses[clauses.Count - 1] as IndexedAnswers;
                if (indexedAnswers == null)
                    // The latest clause is not an IndexedAnswers, so add one.
                    clauses.Add(indexedAnswers = new IndexedAnswers(values.Length));
            }

            indexedAnswers.addAnswer(values);
        }

        /// <summary>
        /// Assert values, prepending to the front of the set of facts for the predicate with the
        /// name and with arity values.Length.
        /// </summary>
        /// <param name="name">must be an Atom</param>
        /// <param name="values">the array of arguments to the fact predicate.
        /// It is an error if an value has an unbound variable.</param>
        public static void prependFact(Atom name, object[] values)
        {
            NameArity nameArity = new NameArity(name, values.Length);
            List<IClause> clauses;
            IndexedAnswers indexedAnswers;
            if (!_predicatesStore.TryGetValue(nameArity, out clauses))
            {
                // Create an IndexedAnswers as the only clause of the predicate.
                _predicatesStore[nameArity] = (clauses = new List<IClause>());
                clauses.Add(indexedAnswers = new IndexedAnswers(values.Length));
            }
            else
            {
                indexedAnswers = null;
                if (clauses.Count >= 1)
                    indexedAnswers = clauses[0] as IndexedAnswers;
                if (indexedAnswers == null)
                    // The first clause is not an IndexedAnswers, so prepend one.
                    clauses.Insert(0, indexedAnswers = new IndexedAnswers(values.Length));
            }

            indexedAnswers.prependAnswer(values);
        }

        /// <summary>
        /// Match all clauses of the dynamic predicate with the name and with arity
        /// arguments.Length.
        /// If the predicate is not defined, return the result of YP.unknownPredicate.
        /// </summary>
        /// <param name="name">must be an Atom</param>
        /// <param name="arguments">an array of arity number of arguments</param>
        /// <returns>an iterator which you can use in foreach</returns>
        public static IEnumerable<bool> matchDynamic(Atom name, object[] arguments)
        {
            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(new NameArity(name, arguments.Length), out clauses))
                return unknownPredicate(name, arguments.Length,
                     "Undefined dynamic predicate: " + name + "/" + arguments.Length);

            if (clauses.Count == 1)
                // Usually there is only one clause, so return it without needing to wrap it in an iterator.
                return clauses[0].match(arguments);
            else
                return matchAllClauses(clauses, arguments);
        }

        /// <summary>
        /// Call match(arguments) for each IClause in clauses.  We make this a separate
        /// function so that matchDynamic itself does not need to be an iterator object.
        /// </summary>
        /// <param name="clauses"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static IEnumerable<bool> matchAllClauses(List<IClause> clauses, object[] arguments)
        {
            // Debug: If the caller asserts another clause into this same predicate during yield, the iterator
            //   over clauses will be corrupted.  Should we take the time to copy clauses?
            foreach (IClause clause in clauses)
            {
                foreach (bool lastCall in clause.match(arguments))
                {
                    yield return false;
                    if (lastCall)
                        // This happens after a cut in a clause.
                        yield break;
                }
            }
        }

        /// <summary>
        /// If _prologFlags["unknown"] is fail then return fail(), else if
        ///   _prologFlags["unknown"] is warning then write the message to YP.write and
        ///   return fail(), else throw a PrologException for existence_error.  .
        /// </summary>
        /// <param name="name"></param>
        /// <param name="arity"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IEnumerable<bool> unknownPredicate(Atom name, int arity, string message)
        {
            establishPrologFlags();

            if (_prologFlags["unknown"] == Atom.a("fail"))
                return fail();
            else if (_prologFlags["unknown"] == Atom.a("warning"))
            {
                write(message);
                nl();
                return fail();
            }
            else
                throw new PrologException
                    (new Functor2
                     (Atom.a("existence_error"), Atom.a("procedure"),
                      new Functor2(Atom.SLASH, name, arity)), message);
        }

        /// <summary>
        /// This is deprecated and just calls matchDynamic. This matches all clauses,
        /// not just the ones defined with assertFact.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static IEnumerable<bool> matchFact(Atom name, object[] arguments)
        {
            return matchDynamic(name, arguments);
        }

        public static IEnumerable<bool> clause(object Head, object Body)
        {
            Head = getValue(Head);
            Body = getValue(Body);
            if (Head is Variable)
                throw new PrologException("instantiation_error", "Head is an unbound variable");

            Atom name = getFunctorName(Head) as Atom;
            if (name == null)
                // name is a non-Atom, such as a number.
                throw new PrologException
                    (new Functor2("type_error", Atom.a("callable"), Head), "Head is not callable");
            object[] args = getFunctorArgs(Head);
            if (isSystemPredicate(name, args.Length))
                throw new PrologException
                    (new Functor3("permission_error", Atom.a("access"), Atom.a("private_procedure"),
                                  new Functor2(Atom.SLASH, name, args.Length)),
                     "clause cannot access private predicate " + name + "/" + args.Length);
            if (!(Body is Variable) && !(YP.getFunctorName(Body) is Atom))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("callable"), Body), "Body is not callable");

            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(new NameArity(name, args.Length), out clauses))
                yield break;
            // The caller can assert another clause into this same predicate during yield, so we have to
            //   make a copy of the clauses.
            foreach (IClause predicateClause in clauses.ToArray())
            {
                foreach (bool l1 in predicateClause.clause(Head, Body))
                    yield return false;
            }
        }

        public static IEnumerable<bool> retract(object Term)
        {
            Term = getValue(Term);
            if (Term is Variable)
                throw new PrologException("instantiation_error", "Term to retract is an unbound variable");

            object Head, Body;
            if (Term is Functor2 && ((Functor2)Term)._name == Atom.RULE)
            {
                Head = YP.getValue(((Functor2)Term)._arg1);
                Body = YP.getValue(((Functor2)Term)._arg2);
            }
            else
            {
                Head = Term;
                Body = Atom.a("true");
            }
            if (Head is Variable)
                throw new PrologException("instantiation_error", "Head is an unbound variable");

            Atom name = getFunctorName(Head) as Atom;
            if (name == null)
                // name is a non-Atom, such as a number.
                throw new PrologException
                    (new Functor2("type_error", Atom.a("callable"), Head), "Head is not callable");
            object[] args = getFunctorArgs(Head);
            if (isSystemPredicate(name, args.Length))
                throw new PrologException
                    (new Functor3("permission_error", Atom.a("modify"), Atom.a("static_procedure"),
                        new Functor2(Atom.SLASH, name, args.Length)),
                     "clause cannot access private predicate " + name + "/" + args.Length);
            if (!(Body is Variable) && !(YP.getFunctorName(Body) is Atom))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("callable"), Body), "Body is not callable");

            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(new NameArity(name, args.Length), out clauses))
                yield break;
            // The caller can assert another clause into this same predicate during yield, so we have to
            //   make a copy of the clauses.
            foreach (IClause predicateClause in clauses.ToArray())
            {
                if (predicateClause is IndexedAnswers)
                {
                    // IndexedAnswers handles its own retract.  Even if it removes all of its
                    //   answers, it is OK to leave it empty as one of the elements in clauses.
                    foreach (bool l1 in ((IndexedAnswers)predicateClause).retract(Head, Body))
                        yield return false;
                }
                else
                {
                    foreach (bool l1 in predicateClause.clause(Head, Body))
                    {
                        clauses.Remove(predicateClause);
                        yield return false;
                    }
                }
            }
        }

        /// <summary>
        /// This is deprecated for backward compatibility.  You should use retractall.
        /// </summary>
        /// <param name="name">must be an Atom</param>
        /// <param name="arguments">an array of arity number of arguments</param>
        public static void retractFact(Atom name, object[] arguments)
        {
            retractall(Functor.make(name, arguments));
        }

        /// <summary>
        /// Retract all dynamic clauses which unify with Head.  If this matches all clauses in a predicate,
        /// the predicate is still defined.  To completely remove the predicate, see abolish.
        /// </summary>
        /// <param name="Head"></param>
        public static void retractall(object Head)
        {
            object name = YP.getFunctorName(Head);
            object[] arguments = getFunctorArgs(Head);
            if (!(name is Atom))
                return;
            NameArity nameArity = new NameArity((Atom)name, arguments.Length);
            List<IClause> clauses;
            if (!_predicatesStore.TryGetValue(nameArity, out clauses))
                // Can't find, so ignore.
                return;

            foreach (object arg in arguments)
            {
                if (!YP.var(arg))
                    throw new InvalidOperationException
                        ("Until matching retractall is supported, all arguments must be unbound to retract all clauses");
            }
            // Clear all clauses.
            _predicatesStore[nameArity] = new List<IClause>();
        }

        /// <summary>
        /// If NameSlashArity is var, match with all the dynamic predicates using the
        /// Name/Artity form.
        /// If NameSlashArity is not var, check if the Name/Arity exists as a static or
        /// dynamic predicate.
        /// </summary>
        /// <param name="NameSlashArity"></param>
        /// <param name="declaringClass">if not null, used to resolve references to the default
        /// module Atom.a("")</param>
        /// <returns></returns>
        public static IEnumerable<bool> current_predicate(object NameSlashArity, Type declaringClass)
        {
            NameSlashArity = YP.getValue(NameSlashArity);
            // First check if Name and Arity are nonvar so we can do a direct lookup.
            if (YP.ground(NameSlashArity))
            {
                Functor2 NameArityFunctor = NameSlashArity as Functor2;
                if (!(NameArityFunctor != null && NameArityFunctor._name == Atom.SLASH))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("predicate_indicator"), NameSlashArity),
                         "Must be a name/arity predicate indicator");
                object name = YP.getValue(NameArityFunctor._arg1);
                object arity = YP.getValue(NameArityFunctor._arg2);
                if (name is Variable || arity is Variable)
                    throw new PrologException
                        ("instantiation_error", "Predicate indicator name or arity is an unbound variable");
                if (!(name is Atom && arity is int))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("predicate_indicator"), NameSlashArity),
                         "Must be a name/arity predicate indicator");
                if ((int)arity < 0)
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("not_less_than_zero"), arity),
                         "Arity may not be less than zero");

                if (YPCompiler.isCurrentPredicate((Atom)name, (int)arity, declaringClass))
                    // The predicate is defined.
                    yield return false;
            }
            else
            {
                foreach (NameArity key in _predicatesStore.Keys)
                {
                    foreach (bool l1 in YP.unify
                        (new Functor2(Atom.SLASH, key._name, key._arity), NameSlashArity))
                        yield return false;
                }
            }
        }

        /// <summary>
        /// Return true if the dynamic predicate store has an entry for the predicate
        /// with name and arity.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="arity"></param>
        /// <returns></returns>
        public static bool isDynamicCurrentPredicate(Atom name, int arity)
        {
            return _predicatesStore.ContainsKey(new NameArity(name, arity));
        }

        public static void abolish(object NameSlashArity)
        {
            NameSlashArity = YP.getValue(NameSlashArity);
            if (NameSlashArity is Variable)
                throw new PrologException
                    ("instantiation_error", "Predicate indicator is an unbound variable");
            Functor2 NameArityFunctor = NameSlashArity as Functor2;
            if (!(NameArityFunctor != null && NameArityFunctor._name == Atom.SLASH))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("predicate_indicator"), NameSlashArity),
                     "Must be a name/arity predicate indicator");
            object name = YP.getValue(NameArityFunctor._arg1);
            object arity = YP.getValue(NameArityFunctor._arg2);
            if (name is Variable || arity is Variable)
                throw new PrologException
                    ("instantiation_error", "Predicate indicator name or arity is an unbound variable");
            if (!(name is Atom))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("atom"), name),
                     "Predicate indicator name must be an atom");
            if (!(arity is int))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("integer"), arity),
                     "Predicate indicator arity must be an integer");
            if ((int)arity < 0)
                throw new PrologException
                    (new Functor2("domain_error", Atom.a("not_less_than_zero"), arity),
                     "Arity may not be less than zero");
            if ((int)arity > MAX_ARITY)
                throw new PrologException
                    (new Functor1("representation_error", Atom.a("max_arity")),
                     "Arity may not be greater than " + MAX_ARITY);

            if (isSystemPredicate((Atom)name, (int)arity))
                throw new PrologException
                    (new Functor3("permission_error", Atom.a("modify"), Atom.a("static_procedure"),
                                  new Functor2(Atom.SLASH, name, arity)),
                     "Abolish cannot modify static predicate " + name + "/" + arity);
            _predicatesStore.Remove(new NameArity((Atom)name, (int)arity));
        }

        /// <summary>
        /// If Goal is a simple predicate, call YP.getFunctorName(Goal) using arguments from
        /// YP.getFunctorArgs(Goal). If not found, this throws a PrologException for existence_error.
        /// Otherwise, compile the goal as a single clause predicate and invoke it.
        /// </summary>
        /// <param name="Goal"></param>
        /// <param name="declaringClass">if not null, used to resolve references to the default
        /// module Atom.a("")</param>
        /// <returns></returns>
        public static IEnumerable<bool> getIterator(object Goal, Type declaringClass)
        {
            Atom name;
            object[] args;
            while (true)
            {
                Goal = YP.getValue(Goal);
                if (Goal is Variable)
                    throw new PrologException("instantiation_error", "Goal to call is an unbound variable");
                name = YP.getFunctorName(Goal) as Atom;
                if (name == null)
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("callable"), Goal), "Goal to call is not callable");
                args = YP.getFunctorArgs(Goal);
                if (name == Atom.HAT && args.Length == 2)
                    // Assume this is called from a bagof operation.  Skip the leading qualifiers.
                    Goal = YP.getValue(((Functor2)Goal)._arg2);
                else
                    break;
            }

            IEnumerable<bool> simpleIterator = YPCompiler.getSimpleIterator(name, args, declaringClass);
            if (simpleIterator != null)
                // We don't need to compile since the goal is a simple predicate which we call directly.
                return simpleIterator;

            // Compile the goal as a clause.
            List<Variable> variableSetList = new List<Variable>();
            addUniqueVariables(Goal, variableSetList);
            Variable[] variableSet = variableSetList.ToArray();

            // Use Atom.F since it is ignored.
            return YPCompiler.compileAnonymousClause
                (Functor.make(Atom.F, variableSet), Goal, declaringClass).match(variableSet);
        }

        public static void throwException(object Term)
        {
            throw new PrologException(Term);
        }
        /// <summary>
        /// This must be called by any function that uses YP._prologFlags to make sure
        /// the initial defaults are loaded.
        /// </summary>
        private static void establishPrologFlags()
        {
            if (_prologFlags.Count > 0)
                // Already established.
                return;

            // List these in the order they appear in the ISO standard.
            _prologFlags["bounded"] = Atom.a("true");
            _prologFlags["max_integer"] = Int32.MaxValue;
            _prologFlags["min_integer"] = Int32.MinValue;
            _prologFlags["integer_rounding_function"] = Atom.a("toward_zero");
            _prologFlags["char_conversion"] = Atom.a("off");
            _prologFlags["debug"] = Atom.a("off");
            _prologFlags["max_arity"] = MAX_ARITY;
            _prologFlags["unknown"] = Atom.a("error");
            _prologFlags["double_quotes"] = Atom.a("codes");
        }

        public static IEnumerable<bool> current_prolog_flag(object Key, object Value)
        {
            establishPrologFlags();

            Key = YP.getValue(Key);
            Value = YP.getValue(Value);

            if (Key is Variable)
            {
                // Bind all key values.
                foreach (string key in _prologFlags.Keys)
                {
                    foreach (bool l1 in YP.unify(Key, Atom.a(key)))
                    {
                        foreach (bool l2 in YP.unify(Value, _prologFlags[key]))
                            yield return false;
                    }
                }
            }
            else
            {
                if (!(Key is Atom))
                    throw new PrologException
                        (new Functor2("type_error", Atom.a("atom"), Key), "Arg 1 Key is not an atom");
                if (!_prologFlags.ContainsKey(((Atom)Key)._name))
                    throw new PrologException
                        (new Functor2("domain_error", Atom.a("prolog_flag"), Key),
                        "Arg 1 Key is not a recognized flag");

                foreach (bool l1 in YP.unify(Value, _prologFlags[((Atom)Key)._name]))
                    yield return false;
            }
        }

        public static void set_prolog_flag(object Key, object Value)
        {
            establishPrologFlags();

            Key = YP.getValue(Key);
            Value = YP.getValue(Value);

            if (Key is Variable)
                throw new PrologException(Atom.a("instantiation_error"),
                    "Arg 1 Key is an unbound variable");
            if (Value is Variable)
                throw new PrologException(Atom.a("instantiation_error"),
                    "Arg 1 Key is an unbound variable");
            if (!(Key is Atom))
                throw new PrologException
                    (new Functor2("type_error", Atom.a("atom"), Key), "Arg 1 Key is not an atom");

            string keyName = ((Atom)Key)._name;
            if (!_prologFlags.ContainsKey(keyName))
                throw new PrologException
                    (new Functor2("domain_error", Atom.a("prolog_flag"), Key),
                    "Arg 1 Key " + Key + " is not a recognized flag");

            bool valueIsOK = false;
            if (keyName == "char_conversion")
                valueIsOK = (Value == _prologFlags[keyName]);
            else if (keyName == "debug")
                valueIsOK = (Value == _prologFlags[keyName]);
            else if (keyName == "unknown")
                valueIsOK = (Value == Atom.a("fail") || Value == Atom.a("warning") ||
                    Value == Atom.a("error"));
            else if (keyName == "double_quotes")
                valueIsOK = (Value == Atom.a("codes") || Value == Atom.a("chars") ||
                    Value == Atom.a("atom"));
            else
                throw new PrologException
                    (new Functor3("permission_error", Atom.a("modify"), Atom.a("flag"), Key),
                     "May not modify Prolog flag " + Key);

            if (!valueIsOK)
                throw new PrologException
                    (new Functor2("domain_error", Atom.a("flag_value"), new Functor2("+", Key, Value)),
                    "May not set arg 1 Key " + Key + " to arg 2 Value " + Value);

            _prologFlags[keyName] = Value;
        }
        /// <summary>
        /// script_event calls hosting script with events as a callback method.
        /// </summary>
        /// <param name="script_event"></param>
        /// <param name="script_params"></param>
        /// <returns></returns>
        public static IEnumerable<bool> script_event(object script_event, object script_params)
        {
            // string function = ((Atom)YP.getValue(script_event))._name;
            object[] array = ListPair.toArray(script_params);
            if (array == null)
                yield return false;  // return; // YP.fail();
            if (array.Length > 1)
            {
                //m_CmdManager.m_ScriptEngine.m_EventQueManager.AddToScriptQueue
                //(localID, itemID, function, array);
                // sortArray(array);
            }
            //return YP.unify(Sorted, ListPair.makeWithoutRepeatedTerms(array));
            yield return false;
        }

        /* Non-prolog-ish functions for inline coding */
        public static string regexString(string inData, string inPattern, string presep,string postsep)
        {
            //string str=cycMessage;
            //string strMatch = @"\. \#\$(.*)\)";
            string results = "";
            for (Match m = Regex.Match(inData,inPattern); m.Success; m=m.NextMatch())
            {
                //m_log.Debug(m);
                results += presep+ m + postsep;
            }
            return results;
        }

        public static string cycComm(object msgobj)
        {
            string cycInputString = msgobj.ToString();
            string cycOutputString="";
            TcpClient socketForServer;

            try
            {
                socketForServer = new TcpClient("localHost", 3601);
            }
            catch
            {
                m_log.Error("Failed to connect to server at localhost:999");
                return "";
            }

            NetworkStream networkStream = socketForServer.GetStream();

            System.IO.StreamReader streamReader = new System.IO.StreamReader(networkStream);

            System.IO.StreamWriter streamWriter = new System.IO.StreamWriter(networkStream);

            try
            {
                // read the data from the host and display it

                {

                    streamWriter.WriteLine(cycInputString);
                    streamWriter.Flush();

                    cycOutputString = streamReader.ReadLine();
                    m_log.Debug("Cycoutput:" + cycOutputString);
                    //streamWriter.WriteLine("Client Message");
                    //m_log.Debug("Client Message");
                    streamWriter.Flush();
                }

            }
            catch
            {
                m_log.Error("Exception reading from Server");
                return "";
            }
            // tidy up
            networkStream.Close();
            return cycOutputString;

        }
        //public static void throwException(object Term)
        //{
        //    throw new PrologException(Term);
        //}
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

        /// <summary>
        /// An enumerator that wraps another enumerator in order to catch a PrologException.
        /// </summary>
        public class Catch : IEnumerator<bool>, IEnumerable<bool>
        {
            private IEnumerator<bool> _enumerator;
            private PrologException _exception = null;

            /// <summary>
            /// Call YP.getIterator(Goal, declaringClass) and save the returned iterator.
            /// If getIterator throws an exception, save it the same as MoveNext().
            /// </summary>
            /// <param name="Goal"></param>
            /// <param name="declaringClass"></param>
            public Catch(object Goal, Type declaringClass)
            {
                try
            {
                    _enumerator = getIterator(Goal, declaringClass).GetEnumerator();
                }
                catch (PrologException exception)
                {
                    // MoveNext() will check this.
                    _exception = exception;
                }
            }

            /// <summary>
            /// Call _enumerator.MoveNext().  If it throws a PrologException, set _exception
            /// and return false.  After this returns false, call unifyExceptionOrThrow.
            /// </summary>
            /// <returns></returns>
            public bool MoveNext()
            {
                if (_exception != null)
                    return false;

                try
                {
                    return _enumerator.MoveNext();
                }
                catch (PrologException exception)
                {
                    _exception = exception;
                    return false;
                }
            }

            /// <summary>
            /// Call this after MoveNext() returns false to check for an exception.  If
            /// MoveNext did not get a PrologException, don't yield.
            /// Otherwise, unify the exception with Catcher and yield so the caller can
            /// do the handler code.  However, if can't unify with Catcher then throw the exception.
            /// </summary>
            /// <param name="Catcher"></param>
            /// <returns></returns>
            public IEnumerable<bool> unifyExceptionOrThrow(object Catcher)
            {
                if (_exception != null)
                {
                    bool didUnify = false;
                    foreach (bool l1 in YP.unify(_exception._term, Catcher))
                    {
                        didUnify = true;
                        yield return false;
                    }
                    if (!didUnify)
                        throw _exception;
                }
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
                get { return _enumerator.Current; }
            }

            object IEnumerator.Current
            {
                get { return _enumerator.Current; }
            }

            public void Dispose()
            {
                if (_enumerator != null)
                _enumerator.Dispose();
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
        #pragma warning restore 0168, 0219
        /// <summary>
        /// A ClauseHeadAndBody is used in Compiler.compileAnonymousFunction as a base class
        /// in order to implement YP.IClause.  After creating the object, you must call setHeadAndBody.
        /// </summary>
        public class ClauseHeadAndBody
        {
            private object _Head;
            private object _Body;

            public void setHeadAndBody(object Head, object Body)
            {
                _Head = Head;
                _Body = Body;
            }

            public IEnumerable<bool> clause(object Head, object Body)
            {
                if (_Head == null || _Body == null)
                    yield break;

                #pragma warning disable 0168, 0219
                foreach (bool l1 in YP.unify(Head, _Head))
                {
                    foreach (bool l2 in YP.unify(Body, _Body))
                        yield return false;
                }
                #pragma warning restore 0168, 0219
            }
        }

        /// <summary>
        /// CodeListReader extends TextReader and overrides Read to read the next code from
        /// the CodeList which is a Prolog list of integer character codes.
        /// </summary>
        public class CodeListReader : TextReader
        {
            private object _CodeList;

            public CodeListReader(object CodeList)
            {
                _CodeList = YP.getValue(CodeList);
            }

            /// <summary>
            /// If the head of _CodeList is an integer, return it and advance the list.  Otherwise,
            /// return -1 for end of file.
            /// </summary>
            /// <returns></returns>
            public override int Read()
            {
                Functor2 CodeListPair = _CodeList as Functor2;
                int code;
                if (!(CodeListPair != null && CodeListPair._name == Atom.DOT &&
                    getInt(CodeListPair._arg1, out code)))
                {
                    _CodeList = Atom.NIL;
                    return -1;
                }

                // Advance.
                _CodeList = YP.getValue(CodeListPair._arg2);
                return code;
            }
        }
    }
}
