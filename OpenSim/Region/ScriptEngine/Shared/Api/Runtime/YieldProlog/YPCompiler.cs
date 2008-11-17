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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.CodeDom.Compiler;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
{
    public class YPCompiler
    {
        private class CompilerState
        {
            public IndexedAnswers _pred = new IndexedAnswers(4);
            public Dictionary<YP.NameArity, Atom> _moduleForNameArity = new Dictionary<YP.NameArity, Atom>();
            public int _gensymCounter;
            public bool _useFinalCutCode;
            public Variable _finalCutCode;
            public bool _codeUsesYield;
            public Atom _determinism;
            // a list of '='(Name, Variable)
            public List<object> _variableNames;

            // Make these static functions that explicitly take the State so Prolog can call it.

            /// <summary>
            /// Make a new CompilerState and bind it to State.
            /// </summary>
            /// <param name="State"></param>
            /// <returns></returns>
            public static IEnumerable<bool> make(object State)
            {
                return YP.unify(State, new CompilerState());
            }

            public static void assertPred(object State, object Pred, object Determinism)
            {
                State = YP.getValue(State);
                object functorName = YP.getFunctorName(Pred);
                object[] functorArgs = YP.getFunctorArgs(Pred);
                // Debug: Should check if it's already asserted and is the same.
                ((CompilerState)State)._pred.addAnswer
                    (new object[] { functorName, functorArgs.Length, Pred, YP.getValue(Determinism) });
            }

            public static void assertModuleForNameArity(object State, object Name, object Arity, object Module)
            {
                State = YP.getValue(State);
                Name = YP.getValue(Name);
                Arity = YP.getValue(Arity);
                Module = YP.getValue(Module);
                // If the Module Atom comes from the parser, it always has null _declaringClass.
                if (Module is Atom && ((Atom)Module)._module == null && Name is Atom && Arity is int)
                    // Replace a previous entry if it exists.
                    ((CompilerState)State)._moduleForNameArity[new YP.NameArity((Atom)Name, (int)Arity)] =
                        (Atom)Module;
            }

            public static void startFunction(object State, object Head)
            {
                State = YP.getValue(State);
                ((CompilerState)State)._gensymCounter = 0;
                ((CompilerState)State)._useFinalCutCode = false;
                ((CompilerState)State)._finalCutCode = new Variable();
                ((CompilerState)State)._codeUsesYield = false;
                if (CompilerState.isDetNoneOut(State, Head))
                    ((CompilerState)State)._determinism = Atom.a("detNoneOut");
                else if (CompilerState.isSemidetNoneOut(State, Head))
                    ((CompilerState)State)._determinism = Atom.a("semidetNoneOut");
                else
                    ((CompilerState)State)._determinism = Atom.a("nondet");
            }

            public static void setCodeUsesYield(object State)
            {
                State = YP.getValue(State);
                ((CompilerState)State)._codeUsesYield = true;
            }

            public static bool codeUsesYield(object State)
            {
                State = YP.getValue(State);
                return ((CompilerState)State)._codeUsesYield;
            }

            public static bool determinismEquals(object State, object Term)
            {
                State = YP.getValue(State);
                return YP.termEqual(((CompilerState)State)._determinism, Term);
            }

            /// <summary>
            /// Set _variableNames to a new list of (Name = Variable) for each unique variable in rule.
            /// If the variable is in variableNameSuggestions, use it, otherwise use x1, x2, etc.
            /// </summary>
            /// <param name="State"></param>
            /// <param name="rule"></param>
            /// <param name="variableNameSuggestions"></param>
            public static void newVariableNames(object State, object Rule, object VariableNameSuggestions)
            {
                State = YP.getValue(State);
                List<Variable> variablesSet = new List<Variable>();
                YP.addUniqueVariables(Rule, variablesSet);

                ((CompilerState)State)._variableNames = new List<object>();
                int xCounter = 0;
                foreach (Variable variable in variablesSet)
                    ((CompilerState)State)._variableNames.Add
                        (new Functor2(Atom.a("="), makeVariableName(variable, VariableNameSuggestions, ++xCounter),
                                      variable));
            }

            private static object makeVariableName(object variable, object variableNameSuggestions, int xCounter)
            {
                // Debug: should require named variables to start with _ or capital. Should
                //   check for duplicates and clashes with keywords.
                for (object element = YP.getValue(variableNameSuggestions);
                     element is Functor2 && ((Functor2)element)._name == Atom.DOT;
                     element = YP.getValue(((Functor2)element)._arg2))
                {
                    object suggestionPair = YP.getValue(((Functor2)element)._arg1);
                    if (sameVariable(variable, ((Functor2)suggestionPair)._arg2))
                    {
                        Atom suggestion = (Atom)YP.getValue(((Functor2)suggestionPair)._arg1);
                        if (suggestion.Equals(Atom.a("Atom")))
                            suggestion = Atom.a("Atom_1");
                        if (suggestion.Equals(Atom.a("Variable")))
                            suggestion = Atom.a("Variable_1");
                        if (suggestion.Equals(Atom.a("Functor")))
                            suggestion = Atom.a("Functor_1");
                        return suggestion;
                    }
                }

                return Atom.a("x" + xCounter);
            }

            /// <summary>
            /// Unify Result with the name assigned by CompilerState.newVariableNames in State._variableNames
            ///   for variable.
            /// </summary>
            /// <param name="variable">a Variable</param>
            /// <param name="State"></param>
            /// <param name="Result">the assigned Name</param>
            public static IEnumerable<bool> getVariableName(object State, object variable, object Result)
            {
                State = YP.getValue(State);
                foreach (object variableInfo in ((CompilerState)State)._variableNames)
                {
                    if (variableInfo is Functor2 && ((Functor2)variableInfo)._name.Equals(Atom.a("=")))
                    {
                        if (sameVariable(variable, ((Functor2)variableInfo)._arg2))
                            return YP.unify(Result, ((Functor2)variableInfo)._arg1);
                    }
                }

                // We set up names for all unique variables, so this should never happen.
                throw new PrologException(Atom.a("Can't find entry in _variableNames"));
            }

            public static IEnumerable<bool> variableNamesList(object State, object VariableNamesList)
            {
                State = YP.getValue(State);
                return YP.unify(VariableNamesList, ListPair.make(((CompilerState)State)._variableNames));
            }

            public static IEnumerable<bool> gensym(object State, object Base, object Symbol)
            {
                State = YP.getValue(State);
                return YP.unify(Symbol, Atom.a(Base.ToString() + ++((CompilerState)State)._gensymCounter));
            }

            // disable warning on l1, don't see how we can
            // code this differently
            #pragma warning disable 0168, 0164, 0162, 0219
            public static bool isDetNoneOut(object State, object Term)
            {
                State = YP.getValue(State);
                object functorName = YP.getFunctorName(Term);
                object[] functorArgs = YP.getFunctorArgs(Term);

                Variable pred = new Variable();
                foreach (bool l1 in ((CompilerState)State)._pred.match
                    (new object[] { functorName, functorArgs.Length, pred, Atom.a("det") }))
                {
                    if (CompilerState.isNoneOut(YP.getFunctorArgs(pred.getValue())))
                    {
                        return true;
                    }
                }

                return false;
            }

            public static bool isSemidetNoneOut(object State, object Term)
            {
                State = YP.getValue(State);
                object functorName = YP.getFunctorName(Term);
                object[] functorArgs = YP.getFunctorArgs(Term);

                Variable pred = new Variable();
                foreach (bool l1 in ((CompilerState)State)._pred.match
                    (new object[] { functorName, functorArgs.Length, pred, Atom.a("semidet") }))
                {
                    if (CompilerState.isNoneOut(YP.getFunctorArgs(pred.getValue())))
                    {
                        return true;
                    }
                }

                return false;
            }
            #pragma warning restore 0168, 0164, 0162, 0219

            /// <summary>
            /// Return false if any of args is out, otherwise true.
            /// args is an array of ::(Type,Mode) where Mode is in or out.
            /// </summary>
            /// <param name="args"></param>
            /// <returns></returns>
            private static bool isNoneOut(object[] args)
            {
                foreach (object arg in args)
                {
                    if (arg is Functor2 && ((Functor2)arg)._name == Atom.a("::") &&
                        ((Functor2)arg)._arg2 == Atom.a("out"))
                        return false;
                }
                return true;
            }

            public static bool nameArityHasModule(object State, object Name, object Arity, object Module)
            {
                State = YP.getValue(State);
                Name = YP.getValue(Name);
                Arity = YP.getValue(Arity);
                Module = YP.getValue(Module);
                if (Name is Atom && Arity is int)
                {
                    Atom FoundModule;
                    if (!((CompilerState)State)._moduleForNameArity.TryGetValue
                         (new YP.NameArity((Atom)Name, (int)Arity), out FoundModule))
                        return false;
                    return FoundModule == Module;
                }
                return false;
            }
        }

        // disable warning on l1, don't see how we can
        // code this differently
        #pragma warning disable 0168, 0219,0164,0162

        /// <summary>
        /// Use makeFunctionPseudoCode, convertFunctionCSharp and compileAnonymousFunction
        /// to return an anonymous YP.IClause for the Head and Body of a rule clause.
        /// </summary>
        /// <param name="Head">a prolog term such as new Functor2("test1", X, Y).
        /// Note that the name of the head is ignored.
        /// </param>
        /// <param name="Body">a prolog term such as 
        /// new Functor2(",", new Functor1(Atom.a("test2", Atom.a("")), X), 
        ///              new Functor2("=", Y, X)).
        /// This may not be null.  (For a head-only clause, set the Body to Atom.a("true").
        /// </param>
        /// <param name="declaringClass">if not null, the code is compiled as a subclass of this class
        /// to resolve references to the default module Atom.a("")</param>
        /// <returns>a new YP.IClause object on which you can call match(object[] args) where
        /// args length is the arity of the Head</returns>
        public static YP.IClause compileAnonymousClause(object Head, object Body, Type declaringClass)
        {
            object[] args = YP.getFunctorArgs(Head);
            // compileAnonymousFunction wants "function".
            object Rule = new Functor2(Atom.RULE, Functor.make("function", args), Body);
            object RuleList = ListPair.make(new Functor2(Atom.F, Rule, Atom.NIL));

            StringWriter functionCode = new StringWriter();
            Variable SaveOutputStream = new Variable();
            foreach (bool l1 in YP.current_output(SaveOutputStream))
            {
                try
                {
                    YP.tell(functionCode);
                    Variable PseudoCode = new Variable();
                    foreach (bool l2 in makeFunctionPseudoCode(RuleList, PseudoCode))
                    {
                        if (YP.termEqual(PseudoCode, Atom.a("getDeclaringClass")))
                            // Ignore getDeclaringClass since we have access to the one passed in.
                            continue;

                        convertFunctionCSharp(PseudoCode);
                    }
                    YP.told();
                }
                finally
                {
                    // Restore after calling tell.
                    YP.tell(SaveOutputStream.getValue());
                }
            }
            return YPCompiler.compileAnonymousFunction
                (functionCode.ToString(), args.Length, declaringClass);
        }

        /// <summary>
        /// Use CodeDomProvider to compile the functionCode and return a YP.ClauseHeadAndBody
        ///   which implements YP.IClause.
        /// The function name must be "function" and have nArgs arguments.
        /// </summary>
        /// <param name="functionCode">the code for the iterator, such as
        /// "public static IEnumerable<bool> function() { yield return false; }"
        /// </param>
        /// <param name="nArgs">the number of args in the function</param>
        /// <param name="declaringClass">if not null, then use the functionCode inside a class which
        /// inherits from contextClass, so that references in functionCode to methods in declaringClass don't
        /// have to be qualified</param>
        /// <returns>a new YP.IClause object on which you can call match(object[] args) where
        /// args length is nArgs</returns>
        public static YP.IClause compileAnonymousFunction(string functionCode, int nArgs, Type declaringClass)
        {
            CompilerParameters parameters = new CompilerParameters();
            // This gets the location of the System assembly.
            parameters.ReferencedAssemblies.Add(typeof(System.Int32).Assembly.Location);
            // This gets the location of this assembly which also has YieldProlog.YP, etc.
            parameters.ReferencedAssemblies.Add(typeof(YPCompiler).Assembly.Location);
            if (declaringClass != null)
                parameters.ReferencedAssemblies.Add(declaringClass.Assembly.Location);
            parameters.GenerateInMemory = true;

            StringBuilder sourceCode = new StringBuilder();
            sourceCode.Append(@"
using System;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Shared.YieldProlog;

namespace Temporary {
  public class Temporary : YP.ClauseHeadAndBody, YP.IClause {");
            if (declaringClass == null)
                // We don't extend a class with getDeclaringClass, so define it.
                sourceCode.Append(@"
    public class Inner {
      public static System.Type getDeclaringClass() { return null; }
");
            else
                sourceCode.Append(@"
    public class Inner : " + declaringClass.FullName + @" {
");
            sourceCode.Append(functionCode);
            // Basically, match applies the args to function.
            sourceCode.Append(@"
    }
    public IEnumerable<bool> match(object[] args) {
      return Inner.function(");
            if (nArgs >= 1)
                sourceCode.Append("args[0]");
            for (int i = 1; i < nArgs; ++i)
                sourceCode.Append(", args[" + i + "]");
            sourceCode.Append(@");
    }
  }
}
");

            CompilerResults results = CodeDomProvider.CreateProvider
                ("CSharp").CompileAssemblyFromSource(parameters, sourceCode.ToString());
            if (results.Errors.Count > 0)
                throw new Exception("Error evaluating code: " + results.Errors[0]);

            // Return a new Temporary.Temporary object.
            return (YP.IClause)results.CompiledAssembly.GetType
                ("Temporary.Temporary").GetConstructor(Type.EmptyTypes).Invoke(null);
        }

        /// <summary>
        /// If the functor with name and args can be called directly as determined by
        ///   functorCallFunctionName, then call it and return its iterator.  If the predicate is
        ///   dynamic and undefined, or if static and the method cannot be found, return
        ///   the result of YP.unknownPredicate.
        /// This returns null if the functor has a special form than needs to be compiled 
        ///   (including ,/2 and ;/2).
        /// </summary>
        /// <param name="name"></param>
        /// <param name="args"></param>
        /// <param name="declaringClass">used to resolve references to the default 
        /// module Atom.a(""). If a declaringClass is needed to resolve the reference but it is
        ///   null, this throws a PrologException for existence_error</param>
        /// <returns></returns>
        public static IEnumerable<bool> getSimpleIterator(Atom name, object[] args, Type declaringClass)
        {
            CompilerState state = new CompilerState();
            Variable FunctionName = new Variable();
            foreach (bool l1 in functorCallFunctionName(state, name, args.Length, FunctionName))
            {
                Atom functionNameAtom = ((Atom)FunctionName.getValue());
                if (functionNameAtom == Atom.NIL)
                    // name is for a dynamic predicate.
                    return YP.matchDynamic(name, args);

                string methodName = functionNameAtom._name;
                // Set the default for the method to call.
                Type methodClass = declaringClass;

                bool checkMode = false;
                if (methodName.StartsWith("YP."))
                {
                    // Assume we only check mode in calls to standard Prolog predicates in YP.
                    checkMode = true;

                    // Use the method in class YP.
                    methodName = methodName.Substring(3);
                    methodClass = typeof(YP);
                }
                if (methodName.Contains("."))
                    // We don't support calling inner classes, etc.
                    return null;

                if (methodClass == null)
                    return YP.unknownPredicate
                        (name, args.Length,
                         "Cannot find predicate function for: " + name + "/" + args.Length + 
                         " because declaringClass is null.  Set declaringClass to the class containing " +
                         methodName);
                try
                {
                    if (checkMode)
                    {
                        assertYPPred(state);
                        object functor = Functor.make(name, args);
                        if (CompilerState.isDetNoneOut(state, functor))
                        {
                            methodClass.InvokeMember
                                (methodName, BindingFlags.InvokeMethod, null, null, args);
                            return YP.succeed();
                        }
                        if (CompilerState.isSemidetNoneOut(state, functor))
                        {
                            if ((bool)methodClass.InvokeMember
                                 (methodName, BindingFlags.InvokeMethod, null, null, args))
                                return YP.succeed();
                            else
                                return YP.fail();
                        }

                    }
                    return (IEnumerable<bool>)methodClass.InvokeMember
                      (methodName, BindingFlags.InvokeMethod, null, null, args);
                }
                catch (TargetInvocationException exception)
                {
                    throw exception.InnerException;
                }
                catch (MissingMethodException)
                {
                    return YP.unknownPredicate
                        (name, args.Length,
                         "Cannot find predicate function " + methodName + " for " + name + "/" + args.Length + 
                         " in " + methodClass.FullName);
                }
            }

            return null;
        }

        /// <summary>
        /// Return true if there is a dynamic or static predicate with name and arity.
        /// This returns false for built-in predicates.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="arity"></param>
        /// <param name="declaringClass">used to resolve references to the default 
        /// module Atom.a(""). If a declaringClass is needed to resolve the reference but it is
        ///   null, return false</param>
        /// <returns></returns>
        public static bool isCurrentPredicate(Atom name, int arity, Type declaringClass)
        {
            CompilerState state = new CompilerState();
            Variable FunctionName = new Variable();
            foreach (bool l1 in functorCallFunctionName(state, name, arity, FunctionName))
            {
                Atom functionNameAtom = ((Atom)FunctionName.getValue());
                if (functionNameAtom == Atom.NIL)
                    // name is for a dynamic predicate.
                    return YP.isDynamicCurrentPredicate(name, arity);

                string methodName = functionNameAtom._name;

                if (methodName.StartsWith("YP."))
                    // current_predicate/1 should fail for built-ins.
                    return false;
                if (methodName.Contains("."))
                    // We don't support calling inner classes, etc.
                    return false;
                if (declaringClass == null)
                    return false;

                foreach (MemberInfo member in declaringClass.GetMember(methodName))
                {
                    MethodInfo method = member as MethodInfo;
                    if (method == null)
                        continue;
                    if ((method.Attributes | MethodAttributes.Static) == 0)
                        // Not a static method.
                        continue;
                    if (method.GetParameters().Length == arity)
                        return true;
                }
            }

            return false;
        }

        // Compiler output follows.

        public class YPInnerClass { }
        public static System.Type getDeclaringClass() { return typeof(YPInnerClass).DeclaringType; }

        public static void repeatWrite(object arg1, object N)
        {
            {
                object _Value = arg1;
                if (YP.termEqual(N, 0))
                {
                    return;
                }
            }
            {
                object Value = arg1;
                Variable NextN = new Variable();
                YP.write(Value);
                foreach (bool l2 in YP.unify(NextN, YP.subtract(N, 1)))
                {
                    repeatWrite(Value, NextN);
                    return;
                }
            }
        }

        public static bool sameVariable(object Variable1, object Variable2)
        {
            {
                if (YP.var(Variable1))
                {
                    if (YP.var(Variable2))
                    {
                        if (YP.termEqual(Variable1, Variable2))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static IEnumerable<bool> makeFunctionPseudoCode(object RuleList, object FunctionCode)
        {
            {
                Variable State = new Variable();
                foreach (bool l2 in CompilerState.make(State))
                {
                    assertYPPred(State);
                    processCompilerDirectives(RuleList, State);
                    foreach (bool l3 in YP.unify(FunctionCode, Atom.a("getDeclaringClass")))
                    {
                        yield return false;
                    }
                    foreach (bool l3 in makeFunctionPseudoCode3(RuleList, State, FunctionCode))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static void assertYPPred(object State)
        {
            {
                CompilerState.assertPred(State, Atom.a("nl"), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("write", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("put_code", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("see", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, Atom.a("seen"), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("tell", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, Atom.a("told"), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("throw", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("abolish", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("retractall", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, new Functor2("set_prolog_flag", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("det"));
                CompilerState.assertPred(State, new Functor1("var", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("nonvar", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("atom", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("integer", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("float", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("number", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("atomic", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor1("compound", new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor2("==", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor2("\\==", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor2("@<", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor2("@=<", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor2("@>", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                CompilerState.assertPred(State, new Functor2("@>=", new Functor2("::", Atom.a("univ"), Atom.a("in")), new Functor2("::", Atom.a("univ"), Atom.a("in"))), Atom.a("semidet"));
                return;
            }
        }

        public static void processCompilerDirectives(object arg1, object arg2)
        {
            {
                object _State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                object State = arg2;
                Variable Pred = new Variable();
                Variable Determinism = new Variable();
                Variable x3 = new Variable();
                Variable RestRules = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", new Functor1(":-", new Functor1("pred", new Functor2("is", Pred, Determinism))), x3), RestRules)))
                {
                    CompilerState.assertPred(State, Pred, Determinism);
                    processCompilerDirectives(RestRules, State);
                    return;
                }
            }
            {
                object State = arg2;
                Variable Module = new Variable();
                Variable PredicateList = new Variable();
                Variable x3 = new Variable();
                Variable RestRules = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", new Functor1(":-", new Functor2("import", Module, PredicateList)), x3), RestRules)))
                {
                    foreach (bool l3 in importPredicateList(State, Module, PredicateList))
                    {
                        processCompilerDirectives(RestRules, State);
                        return;
                    }
                }
            }
            {
                object State = arg2;
                Variable x1 = new Variable();
                Variable x2 = new Variable();
                Variable RestRules = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", new Functor1(":-", x1), x2), RestRules)))
                {
                    processCompilerDirectives(RestRules, State);
                    return;
                }
            }
            {
                object State = arg2;
                Variable Head = new Variable();
                Variable _Body = new Variable();
                Variable x3 = new Variable();
                Variable RestRules = new Variable();
                Variable Name = new Variable();
                Variable Arity = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", new Functor2(":-", Head, _Body), x3), RestRules)))
                {
                    foreach (bool l3 in YP.functor(Head, Name, Arity))
                    {
                        CompilerState.assertModuleForNameArity(State, Name, Arity, Atom.a(""));
                        processCompilerDirectives(RestRules, State);
                        return;
                    }
                }
            }
            {
                object State = arg2;
                Variable Fact = new Variable();
                Variable x2 = new Variable();
                Variable RestRules = new Variable();
                Variable Name = new Variable();
                Variable Arity = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", Fact, x2), RestRules)))
                {
                    foreach (bool l3 in YP.functor(Fact, Name, Arity))
                    {
                        CompilerState.assertModuleForNameArity(State, Name, Arity, Atom.a(""));
                        processCompilerDirectives(RestRules, State);
                        return;
                    }
                }
            }
            {
                object State = arg2;
                Variable x1 = new Variable();
                Variable RestRules = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(x1, RestRules)))
                {
                    processCompilerDirectives(RestRules, State);
                    return;
                }
            }
        }

        public static IEnumerable<bool> importPredicateList(object arg1, object arg2, object arg3)
        {
            {
                object _State = arg1;
                object _Module = arg2;
                foreach (bool l2 in YP.unify(arg3, Atom.NIL))
                {
                    yield return true;
                    yield break;
                }
            }
            {
                object State = arg1;
                object Module = arg2;
                Variable Name = new Variable();
                Variable Arity = new Variable();
                Variable Rest = new Variable();
                foreach (bool l2 in YP.unify(arg3, new ListPair(new Functor2("/", Name, Arity), Rest)))
                {
                    CompilerState.assertModuleForNameArity(State, Name, Arity, Module);
                    foreach (bool l3 in importPredicateList(State, Module, Rest))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg1;
                object Module = arg2;
                Variable x3 = new Variable();
                Variable Rest = new Variable();
                foreach (bool l2 in YP.unify(arg3, new ListPair(x3, Rest)))
                {
                    foreach (bool l3 in importPredicateList(State, Module, Rest))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
        }

        public static IEnumerable<bool> makeFunctionPseudoCode3(object RuleList, object State, object FunctionCode)
        {
            {
                Variable SamePredicateRuleList = new Variable();
                Variable RestRules = new Variable();
                foreach (bool l2 in samePredicateRuleList(RuleList, SamePredicateRuleList, RestRules))
                {
                    if (YP.termNotEqual(SamePredicateRuleList, Atom.NIL))
                    {
                        foreach (bool l4 in compileSamePredicateFunction(SamePredicateRuleList, State, FunctionCode))
                        {
                            yield return false;
                        }
                        foreach (bool l4 in makeFunctionPseudoCode3(RestRules, State, FunctionCode))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> compileSamePredicateFunction(object SamePredicateRuleList, object State, object FunctionCode)
        {
            {
                Variable FirstRule = new Variable();
                Variable x5 = new Variable();
                Variable x6 = new Variable();
                Variable x7 = new Variable();
                Variable Head = new Variable();
                Variable x9 = new Variable();
                Variable ArgAssignments = new Variable();
                Variable Calls = new Variable();
                Variable Rule = new Variable();
                Variable VariableNameSuggestions = new Variable();
                Variable ClauseBag = new Variable();
                Variable Name = new Variable();
                Variable ArgsList = new Variable();
                Variable FunctionArgNames = new Variable();
                Variable MergedArgName = new Variable();
                Variable ArgName = new Variable();
                Variable MergedArgNames = new Variable();
                Variable FunctionArgs = new Variable();
                Variable BodyCode = new Variable();
                Variable ReturnType = new Variable();
                Variable BodyWithReturn = new Variable();
                foreach (bool l2 in YP.unify(new ListPair(new Functor2("f", FirstRule, x5), x6), SamePredicateRuleList))
                {
                    foreach (bool l3 in YP.unify(FirstRule, new Functor1(":-", x7)))
                    {
                        goto cutIf1;
                    }
                    foreach (bool l3 in YP.unify(new Functor2(":-", Head, x9), FirstRule))
                    {
                        CompilerState.startFunction(State, Head);
                        FindallAnswers findallAnswers3 = new FindallAnswers(new Functor2("f", ArgAssignments, Calls));
                        foreach (bool l4 in member(new Functor2("f", Rule, VariableNameSuggestions), SamePredicateRuleList))
                        {
                            foreach (bool l5 in compileBodyWithHeadBindings(Rule, VariableNameSuggestions, State, ArgAssignments, Calls))
                            {
                                findallAnswers3.add();
                            }
                        }
                        foreach (bool l4 in findallAnswers3.result(ClauseBag))
                        {
                            foreach (bool l5 in YP.univ(Head, new ListPair(Name, ArgsList)))
                            {
                                foreach (bool l6 in getFunctionArgNames(ArgsList, 1, FunctionArgNames))
                                {
                                    FindallAnswers findallAnswers4 = new FindallAnswers(MergedArgName);
                                    foreach (bool l7 in member(ArgName, FunctionArgNames))
                                    {
                                        foreach (bool l8 in argAssignedAll(ArgName, ClauseBag, MergedArgName))
                                        {
                                            findallAnswers4.add();
                                            goto cutIf5;
                                        }
                                        foreach (bool l8 in YP.unify(MergedArgName, ArgName))
                                        {
                                            findallAnswers4.add();
                                        }
                                    cutIf5:
                                        { }
                                    }
                                    foreach (bool l7 in findallAnswers4.result(MergedArgNames))
                                    {
                                        foreach (bool l8 in maplist_arg(MergedArgNames, FunctionArgs))
                                        {
                                            foreach (bool l9 in maplist_compileClause(ClauseBag, MergedArgNames, BodyCode))
                                            {
                                                if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                {
                                                    foreach (bool l11 in YP.unify(ReturnType, Atom.a("void")))
                                                    {
                                                        if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                        {
                                                            foreach (bool l13 in append(BodyCode, new ListPair(Atom.a("returnfalse"), Atom.NIL), BodyWithReturn))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf7;
                                                        }
                                                        if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf8;
                                                        }
                                                        if (CompilerState.codeUsesYield(State))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf9;
                                                        }
                                                        foreach (bool l12 in append(BodyCode, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.fail"), Atom.NIL), new ListPair(Atom.a("yieldfalse"), Atom.NIL)), Atom.NIL), BodyWithReturn))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                    cutIf9:
                                                    cutIf8:
                                                    cutIf7:
                                                        { }
                                                    }
                                                    goto cutIf6;
                                                }
                                                if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                {
                                                    foreach (bool l11 in YP.unify(ReturnType, Atom.a("bool")))
                                                    {
                                                        if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                        {
                                                            foreach (bool l13 in append(BodyCode, new ListPair(Atom.a("returnfalse"), Atom.NIL), BodyWithReturn))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf11;
                                                        }
                                                        if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf12;
                                                        }
                                                        if (CompilerState.codeUsesYield(State))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf13;
                                                        }
                                                        foreach (bool l12 in append(BodyCode, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.fail"), Atom.NIL), new ListPair(Atom.a("yieldfalse"), Atom.NIL)), Atom.NIL), BodyWithReturn))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                    cutIf13:
                                                    cutIf12:
                                                    cutIf11:
                                                        { }
                                                    }
                                                    goto cutIf10;
                                                }
                                                foreach (bool l10 in YP.unify(ReturnType, Atom.a("IEnumerable<bool>")))
                                                {
                                                    if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                    {
                                                        foreach (bool l12 in append(BodyCode, new ListPair(Atom.a("returnfalse"), Atom.NIL), BodyWithReturn))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf14;
                                                    }
                                                    if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                    {
                                                        foreach (bool l12 in YP.unify(BodyWithReturn, BodyCode))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf15;
                                                    }
                                                    if (CompilerState.codeUsesYield(State))
                                                    {
                                                        foreach (bool l12 in YP.unify(BodyWithReturn, BodyCode))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf16;
                                                    }
                                                    foreach (bool l11 in append(BodyCode, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.fail"), Atom.NIL), new ListPair(Atom.a("yieldfalse"), Atom.NIL)), Atom.NIL), BodyWithReturn))
                                                    {
                                                        foreach (bool l12 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                        {
                                                            yield return false;
                                                        }
                                                    }
                                                cutIf16:
                                                cutIf15:
                                                cutIf14:
                                                    { }
                                                }
                                            cutIf10:
                                            cutIf6:
                                                { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        goto cutIf2;
                    }
                    foreach (bool l3 in YP.unify(Head, FirstRule))
                    {
                        CompilerState.startFunction(State, Head);
                        FindallAnswers findallAnswers17 = new FindallAnswers(new Functor2("f", ArgAssignments, Calls));
                        foreach (bool l4 in member(new Functor2("f", Rule, VariableNameSuggestions), SamePredicateRuleList))
                        {
                            foreach (bool l5 in compileBodyWithHeadBindings(Rule, VariableNameSuggestions, State, ArgAssignments, Calls))
                            {
                                findallAnswers17.add();
                            }
                        }
                        foreach (bool l4 in findallAnswers17.result(ClauseBag))
                        {
                            foreach (bool l5 in YP.univ(Head, new ListPair(Name, ArgsList)))
                            {
                                foreach (bool l6 in getFunctionArgNames(ArgsList, 1, FunctionArgNames))
                                {
                                    FindallAnswers findallAnswers18 = new FindallAnswers(MergedArgName);
                                    foreach (bool l7 in member(ArgName, FunctionArgNames))
                                    {
                                        foreach (bool l8 in argAssignedAll(ArgName, ClauseBag, MergedArgName))
                                        {
                                            findallAnswers18.add();
                                            goto cutIf19;
                                        }
                                        foreach (bool l8 in YP.unify(MergedArgName, ArgName))
                                        {
                                            findallAnswers18.add();
                                        }
                                    cutIf19:
                                        { }
                                    }
                                    foreach (bool l7 in findallAnswers18.result(MergedArgNames))
                                    {
                                        foreach (bool l8 in maplist_arg(MergedArgNames, FunctionArgs))
                                        {
                                            foreach (bool l9 in maplist_compileClause(ClauseBag, MergedArgNames, BodyCode))
                                            {
                                                if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                {
                                                    foreach (bool l11 in YP.unify(ReturnType, Atom.a("void")))
                                                    {
                                                        if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                        {
                                                            foreach (bool l13 in append(BodyCode, new ListPair(Atom.a("returnfalse"), Atom.NIL), BodyWithReturn))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf21;
                                                        }
                                                        if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf22;
                                                        }
                                                        if (CompilerState.codeUsesYield(State))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf23;
                                                        }
                                                        foreach (bool l12 in append(BodyCode, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.fail"), Atom.NIL), new ListPair(Atom.a("yieldfalse"), Atom.NIL)), Atom.NIL), BodyWithReturn))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                    cutIf23:
                                                    cutIf22:
                                                    cutIf21:
                                                        { }
                                                    }
                                                    goto cutIf20;
                                                }
                                                if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                {
                                                    foreach (bool l11 in YP.unify(ReturnType, Atom.a("bool")))
                                                    {
                                                        if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                        {
                                                            foreach (bool l13 in append(BodyCode, new ListPair(Atom.a("returnfalse"), Atom.NIL), BodyWithReturn))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf25;
                                                        }
                                                        if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf26;
                                                        }
                                                        if (CompilerState.codeUsesYield(State))
                                                        {
                                                            foreach (bool l13 in YP.unify(BodyWithReturn, BodyCode))
                                                            {
                                                                foreach (bool l14 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                                {
                                                                    yield return false;
                                                                }
                                                            }
                                                            goto cutIf27;
                                                        }
                                                        foreach (bool l12 in append(BodyCode, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.fail"), Atom.NIL), new ListPair(Atom.a("yieldfalse"), Atom.NIL)), Atom.NIL), BodyWithReturn))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                    cutIf27:
                                                    cutIf26:
                                                    cutIf25:
                                                        { }
                                                    }
                                                    goto cutIf24;
                                                }
                                                foreach (bool l10 in YP.unify(ReturnType, Atom.a("IEnumerable<bool>")))
                                                {
                                                    if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                                                    {
                                                        foreach (bool l12 in append(BodyCode, new ListPair(Atom.a("returnfalse"), Atom.NIL), BodyWithReturn))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf28;
                                                    }
                                                    if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                                                    {
                                                        foreach (bool l12 in YP.unify(BodyWithReturn, BodyCode))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf29;
                                                    }
                                                    if (CompilerState.codeUsesYield(State))
                                                    {
                                                        foreach (bool l12 in YP.unify(BodyWithReturn, BodyCode))
                                                        {
                                                            foreach (bool l13 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                            {
                                                                yield return false;
                                                            }
                                                        }
                                                        goto cutIf30;
                                                    }
                                                    foreach (bool l11 in append(BodyCode, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.fail"), Atom.NIL), new ListPair(Atom.a("yieldfalse"), Atom.NIL)), Atom.NIL), BodyWithReturn))
                                                    {
                                                        foreach (bool l12 in YP.unify(FunctionCode, new Functor("function", new object[] { ReturnType, Name, FunctionArgs, BodyWithReturn })))
                                                        {
                                                            yield return false;
                                                        }
                                                    }
                                                cutIf30:
                                                cutIf29:
                                                cutIf28:
                                                    { }
                                                }
                                            cutIf24:
                                            cutIf20:
                                                { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                cutIf2:
                cutIf1:
                    { }
                }
            }
        }

        public static IEnumerable<bool> samePredicateRuleList(object arg1, object arg2, object arg3)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.NIL))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.NIL))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                Variable First = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(First, Atom.NIL)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(First, Atom.NIL)))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.NIL))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object SamePredicateRuleList = arg2;
                object RestRules = arg3;
                Variable First = new Variable();
                Variable Rest = new Variable();
                Variable FirstRule = new Variable();
                Variable x6 = new Variable();
                Variable SecondRule = new Variable();
                Variable x8 = new Variable();
                Variable x9 = new Variable();
                Variable FirstHead = new Variable();
                Variable x11 = new Variable();
                Variable SecondHead = new Variable();
                Variable x13 = new Variable();
                Variable Name = new Variable();
                Variable Arity = new Variable();
                Variable RestSamePredicates = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(First, Rest)))
                {
                    foreach (bool l3 in YP.unify(new Functor2("f", FirstRule, x6), First))
                    {
                        foreach (bool l4 in YP.unify(new ListPair(new Functor2("f", SecondRule, x8), x9), Rest))
                        {
                            foreach (bool l5 in YP.unify(new Functor2(":-", FirstHead, x11), FirstRule))
                            {
                                foreach (bool l6 in YP.unify(new Functor2(":-", SecondHead, x13), SecondRule))
                                {
                                    foreach (bool l7 in YP.functor(FirstHead, Name, Arity))
                                    {
                                        foreach (bool l8 in YP.functor(SecondHead, Name, Arity))
                                        {
                                            foreach (bool l9 in samePredicateRuleList(Rest, RestSamePredicates, RestRules))
                                            {
                                                foreach (bool l10 in YP.unify(SamePredicateRuleList, new ListPair(First, RestSamePredicates)))
                                                {
                                                    yield return true;
                                                    yield break;
                                                }
                                            }
                                            goto cutIf3;
                                        }
                                        foreach (bool l8 in YP.unify(SamePredicateRuleList, new ListPair(First, Atom.NIL)))
                                        {
                                            foreach (bool l9 in YP.unify(RestRules, Rest))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    cutIf3:
                                        { }
                                    }
                                    goto cutIf2;
                                }
                                foreach (bool l6 in YP.unify(SecondHead, SecondRule))
                                {
                                    foreach (bool l7 in YP.functor(FirstHead, Name, Arity))
                                    {
                                        foreach (bool l8 in YP.functor(SecondHead, Name, Arity))
                                        {
                                            foreach (bool l9 in samePredicateRuleList(Rest, RestSamePredicates, RestRules))
                                            {
                                                foreach (bool l10 in YP.unify(SamePredicateRuleList, new ListPair(First, RestSamePredicates)))
                                                {
                                                    yield return true;
                                                    yield break;
                                                }
                                            }
                                            goto cutIf4;
                                        }
                                        foreach (bool l8 in YP.unify(SamePredicateRuleList, new ListPair(First, Atom.NIL)))
                                        {
                                            foreach (bool l9 in YP.unify(RestRules, Rest))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    cutIf4:
                                        { }
                                    }
                                }
                            cutIf2:
                                goto cutIf1;
                            }
                            foreach (bool l5 in YP.unify(FirstHead, FirstRule))
                            {
                                foreach (bool l6 in YP.unify(new Functor2(":-", SecondHead, x13), SecondRule))
                                {
                                    foreach (bool l7 in YP.functor(FirstHead, Name, Arity))
                                    {
                                        foreach (bool l8 in YP.functor(SecondHead, Name, Arity))
                                        {
                                            foreach (bool l9 in samePredicateRuleList(Rest, RestSamePredicates, RestRules))
                                            {
                                                foreach (bool l10 in YP.unify(SamePredicateRuleList, new ListPair(First, RestSamePredicates)))
                                                {
                                                    yield return true;
                                                    yield break;
                                                }
                                            }
                                            goto cutIf6;
                                        }
                                        foreach (bool l8 in YP.unify(SamePredicateRuleList, new ListPair(First, Atom.NIL)))
                                        {
                                            foreach (bool l9 in YP.unify(RestRules, Rest))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    cutIf6:
                                        { }
                                    }
                                    goto cutIf5;
                                }
                                foreach (bool l6 in YP.unify(SecondHead, SecondRule))
                                {
                                    foreach (bool l7 in YP.functor(FirstHead, Name, Arity))
                                    {
                                        foreach (bool l8 in YP.functor(SecondHead, Name, Arity))
                                        {
                                            foreach (bool l9 in samePredicateRuleList(Rest, RestSamePredicates, RestRules))
                                            {
                                                foreach (bool l10 in YP.unify(SamePredicateRuleList, new ListPair(First, RestSamePredicates)))
                                                {
                                                    yield return true;
                                                    yield break;
                                                }
                                            }
                                            goto cutIf7;
                                        }
                                        foreach (bool l8 in YP.unify(SamePredicateRuleList, new ListPair(First, Atom.NIL)))
                                        {
                                            foreach (bool l9 in YP.unify(RestRules, Rest))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    cutIf7:
                                        { }
                                    }
                                }
                            cutIf5:
                                { }
                            }
                        cutIf1:
                            { }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> maplist_compileClause(object arg1, object arg2, object arg3)
        {
            {
                object _MergedArgNames = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg3, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object MergedArgNames = arg2;
                Variable ArgAssignments = new Variable();
                Variable Calls = new Variable();
                Variable Rest = new Variable();
                Variable ClauseCode = new Variable();
                Variable RestResults = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", ArgAssignments, Calls), Rest)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor1("blockScope", ClauseCode), RestResults)))
                    {
                        foreach (bool l4 in prependArgAssignments(ArgAssignments, Calls, MergedArgNames, ClauseCode))
                        {
                            foreach (bool l5 in maplist_compileClause(Rest, MergedArgNames, RestResults))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> prependArgAssignments(object arg1, object arg2, object arg3, object arg4)
        {
            {
                object _MergedArgNames = arg3;
                Variable In = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, In))
                    {
                        foreach (bool l4 in YP.unify(arg4, In))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object In = arg2;
                object MergedArgNames = arg3;
                object ClauseCode = arg4;
                Variable VariableName = new Variable();
                Variable ArgName = new Variable();
                Variable RestArgAssignments = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("f", VariableName, ArgName), RestArgAssignments)))
                {
                    foreach (bool l3 in member(VariableName, MergedArgNames))
                    {
                        foreach (bool l4 in prependArgAssignments(RestArgAssignments, In, MergedArgNames, ClauseCode))
                        {
                            yield return true;
                            yield break;
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in prependArgAssignments(RestArgAssignments, new ListPair(new Functor3("declare", Atom.a("object"), VariableName, new Functor1("var", ArgName)), In), MergedArgNames, ClauseCode))
                    {
                        yield return true;
                        yield break;
                    }
                cutIf1:
                    { }
                }
            }
        }

        public static IEnumerable<bool> argAssignedAll(object arg1, object arg2, object VariableName)
        {
            {
                object _ArgName = arg1;
                foreach (bool l2 in YP.unify(arg2, Atom.NIL))
                {
                    if (YP.nonvar(VariableName))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object ArgName = arg1;
                Variable ArgAssignments = new Variable();
                Variable _Calls = new Variable();
                Variable RestClauseBag = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(new Functor2("f", ArgAssignments, _Calls), RestClauseBag)))
                {
                    foreach (bool l3 in member(new Functor2("f", VariableName, ArgName), ArgAssignments))
                    {
                        foreach (bool l4 in argAssignedAll(ArgName, RestClauseBag, VariableName))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> maplist_arg(object arg1, object arg2)
        {
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
                Variable First = new Variable();
                Variable Rest = new Variable();
                Variable RestResults = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(First, Rest)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(new Functor1("arg", First), RestResults)))
                    {
                        foreach (bool l4 in maplist_arg(Rest, RestResults))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> getFunctionArgNames(object arg1, object arg2, object arg3)
        {
            {
                object _StartArgNumber = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg3, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object StartArgNumber = arg2;
                Variable x1 = new Variable();
                Variable Rest = new Variable();
                Variable ArgName = new Variable();
                Variable RestFunctionArgs = new Variable();
                Variable NumberCodes = new Variable();
                Variable NumberAtom = new Variable();
                Variable NextArgNumber = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(x1, Rest)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(ArgName, RestFunctionArgs)))
                    {
                        foreach (bool l4 in YP.number_codes(StartArgNumber, NumberCodes))
                        {
                            foreach (bool l5 in YP.atom_codes(NumberAtom, NumberCodes))
                            {
                                foreach (bool l6 in YP.atom_concat(Atom.a("arg"), NumberAtom, ArgName))
                                {
                                    foreach (bool l7 in YP.unify(NextArgNumber, YP.add(StartArgNumber, 1)))
                                    {
                                        foreach (bool l8 in getFunctionArgNames(Rest, NextArgNumber, RestFunctionArgs))
                                        {
                                            yield return true;
                                            yield break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> compileBodyWithHeadBindings(object Rule, object VariableNameSuggestions, object State, object ArgAssignments, object Calls)
        {
            {
                Variable Head = new Variable();
                Variable Body = new Variable();
                Variable x8 = new Variable();
                Variable HeadArgs = new Variable();
                Variable CompiledHeadArgs = new Variable();
                Variable BodyCode = new Variable();
                Variable VariableNamesList = new Variable();
                Variable ArgUnifications = new Variable();
                foreach (bool l2 in YP.unify(new Functor2(":-", Head, Body), Rule))
                {
                    CompilerState.newVariableNames(State, Rule, VariableNameSuggestions);
                    foreach (bool l3 in YP.univ(Head, new ListPair(x8, HeadArgs)))
                    {
                        foreach (bool l4 in maplist_compileTerm(HeadArgs, State, CompiledHeadArgs))
                        {
                            foreach (bool l5 in compileRuleBody(Body, State, BodyCode))
                            {
                                foreach (bool l6 in CompilerState.variableNamesList(State, VariableNamesList))
                                {
                                    foreach (bool l7 in compileArgUnifications(HeadArgs, CompiledHeadArgs, 1, HeadArgs, BodyCode, ArgUnifications))
                                    {
                                        foreach (bool l8 in compileDeclarations(VariableNamesList, HeadArgs, Atom.NIL, ArgAssignments, ArgUnifications, Calls))
                                        {
                                            yield return true;
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
                foreach (bool l2 in compileBodyWithHeadBindings(new Functor2(":-", Rule, Atom.a("true")), VariableNameSuggestions, State, ArgAssignments, Calls))
                {
                    yield return true;
                    yield break;
                }
            }
        }

        public static IEnumerable<bool> compileArgUnifications(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
        {
            {
                object x1 = arg2;
                object x2 = arg3;
                object x3 = arg4;
                Variable BodyCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg5, BodyCode))
                    {
                        foreach (bool l4 in YP.unify(arg6, BodyCode))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object Index = arg3;
                object AllHeadArgs = arg4;
                object BodyCode = arg5;
                object ArgUnifications = arg6;
                Variable HeadArg = new Variable();
                Variable RestHeadArgs = new Variable();
                Variable x3 = new Variable();
                Variable RestCompiledHeadArgs = new Variable();
                Variable _ArgIndex1 = new Variable();
                Variable NextIndex = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(HeadArg, RestHeadArgs)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(x3, RestCompiledHeadArgs)))
                    {
                        foreach (bool l4 in getVariableArgIndex1(HeadArg, AllHeadArgs, _ArgIndex1))
                        {
                            foreach (bool l5 in YP.unify(NextIndex, YP.add(Index, 1)))
                            {
                                foreach (bool l6 in compileArgUnifications(RestHeadArgs, RestCompiledHeadArgs, NextIndex, AllHeadArgs, BodyCode, ArgUnifications))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }
            {
                object Index = arg3;
                object AllHeadArgs = arg4;
                object BodyCode = arg5;
                Variable _HeadArg = new Variable();
                Variable RestHeadArgs = new Variable();
                Variable CompiledHeadArg = new Variable();
                Variable RestCompiledHeadArgs = new Variable();
                Variable ArgName = new Variable();
                Variable RestArgUnifications = new Variable();
                Variable NumberCodes = new Variable();
                Variable NumberAtom = new Variable();
                Variable NextIndex = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(_HeadArg, RestHeadArgs)))
                {
                    foreach (bool l3 in YP.unify(arg2, new ListPair(CompiledHeadArg, RestCompiledHeadArgs)))
                    {
                        foreach (bool l4 in YP.unify(arg6, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.unify"), new ListPair(new Functor1("var", ArgName), new ListPair(CompiledHeadArg, Atom.NIL))), RestArgUnifications), Atom.NIL)))
                        {
                            foreach (bool l5 in YP.number_codes(Index, NumberCodes))
                            {
                                foreach (bool l6 in YP.atom_codes(NumberAtom, NumberCodes))
                                {
                                    foreach (bool l7 in YP.atom_concat(Atom.a("arg"), NumberAtom, ArgName))
                                    {
                                        foreach (bool l8 in YP.unify(NextIndex, YP.add(Index, 1)))
                                        {
                                            foreach (bool l9 in compileArgUnifications(RestHeadArgs, RestCompiledHeadArgs, NextIndex, AllHeadArgs, BodyCode, RestArgUnifications))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> compileDeclarations(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
        {
            {
                object _HeadArgs = arg2;
                Variable ArgAssignmentsIn = new Variable();
                Variable DeclarationsIn = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg3, ArgAssignmentsIn))
                    {
                        foreach (bool l4 in YP.unify(arg4, ArgAssignmentsIn))
                        {
                            foreach (bool l5 in YP.unify(arg5, DeclarationsIn))
                            {
                                foreach (bool l6 in YP.unify(arg6, DeclarationsIn))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }
            {
                object HeadArgs = arg2;
                object ArgAssignmentsIn = arg3;
                object ArgAssignmentsOut = arg4;
                object DeclarationsIn = arg5;
                object DeclarationsOut = arg6;
                Variable VariableName = new Variable();
                Variable Var = new Variable();
                Variable RestVariableNames = new Variable();
                Variable ArgIndex1 = new Variable();
                Variable NumberCodes = new Variable();
                Variable NumberAtom = new Variable();
                Variable ArgName = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("=", VariableName, Var), RestVariableNames)))
                {
                    foreach (bool l3 in getVariableArgIndex1(Var, HeadArgs, ArgIndex1))
                    {
                        foreach (bool l4 in YP.number_codes(ArgIndex1, NumberCodes))
                        {
                            foreach (bool l5 in YP.atom_codes(NumberAtom, NumberCodes))
                            {
                                foreach (bool l6 in YP.atom_concat(Atom.a("arg"), NumberAtom, ArgName))
                                {
                                    foreach (bool l7 in compileDeclarations(RestVariableNames, HeadArgs, new ListPair(new Functor2("f", VariableName, ArgName), ArgAssignmentsIn), ArgAssignmentsOut, DeclarationsIn, DeclarationsOut))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            {
                object HeadArgs = arg2;
                object ArgAssignmentsIn = arg3;
                object ArgAssignmentsOut = arg4;
                object DeclarationsIn = arg5;
                Variable VariableName = new Variable();
                Variable _Var = new Variable();
                Variable RestVariableNames = new Variable();
                Variable DeclarationsOut = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("=", VariableName, _Var), RestVariableNames)))
                {
                    foreach (bool l3 in YP.unify(arg6, new ListPair(new Functor3("declare", Atom.a("Variable"), VariableName, new Functor2("new", Atom.a("Variable"), Atom.NIL)), DeclarationsOut)))
                    {
                        foreach (bool l4 in compileDeclarations(RestVariableNames, HeadArgs, ArgAssignmentsIn, ArgAssignmentsOut, DeclarationsIn, DeclarationsOut))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> getVariableArgIndex1(object Var, object arg2, object arg3)
        {
            {
                Variable FirstHeadArgs = new Variable();
                Variable RestHeadArgs = new Variable();
                Variable x4 = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(FirstHeadArgs, RestHeadArgs)))
                {
                    foreach (bool l3 in YP.unify(arg3, 1))
                    {
                        if (sameVariable(Var, FirstHeadArgs))
                        {
                            foreach (bool l5 in getVariableArgIndex1(Var, RestHeadArgs, x4))
                            {
                                goto cutIf1;
                            }
                            yield return false;
                        cutIf1:
                            yield break;
                        }
                    }
                }
            }
            {
                object Index = arg3;
                Variable x2 = new Variable();
                Variable RestHeadArgs = new Variable();
                Variable RestIndex = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(x2, RestHeadArgs)))
                {
                    foreach (bool l3 in getVariableArgIndex1(Var, RestHeadArgs, RestIndex))
                    {
                        foreach (bool l4 in YP.unify(Index, YP.add(1, RestIndex)))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> compileRuleBody(object arg1, object arg2, object arg3)
        {
            {
                object A = arg1;
                object State = arg2;
                object PseudoCode = arg3;
                if (YP.var(A))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", new Functor1("call", A), Atom.a("true")), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", A, B)))
                {
                    if (YP.var(A))
                    {
                        foreach (bool l4 in compileRuleBody(new Functor2(",", new Functor1("call", A), B), State, PseudoCode))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ACode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", A, B)))
                {
                    foreach (bool l3 in compileFunctorCall(A, State, ACode))
                    {
                        if (CompilerState.isDetNoneOut(State, A))
                        {
                            foreach (bool l5 in compileRuleBody(B, State, BCode))
                            {
                                foreach (bool l6 in YP.unify(PseudoCode, new ListPair(ACode, BCode)))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        }
                        if (CompilerState.isSemidetNoneOut(State, A))
                        {
                            foreach (bool l5 in compileRuleBody(B, State, BCode))
                            {
                                foreach (bool l6 in YP.unify(PseudoCode, new ListPair(new Functor2("if", ACode, BCode), Atom.NIL)))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        }
                        foreach (bool l4 in compileRuleBody(B, State, BCode))
                        {
                            foreach (bool l5 in YP.unify(PseudoCode, new ListPair(new Functor2("foreach", ACode, BCode), Atom.NIL)))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable T = new Variable();
                Variable B = new Variable();
                Variable C = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor2(";", new Functor2("->", A, T), B), C)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(";", new Functor2("->", A, new Functor2(",", T, C)), new Functor2(",", B, C)), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable C = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor2(";", A, B), C)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(";", new Functor2(",", A, C), new Functor2(",", B, C)), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ACode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("\\+", A), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("if", new Functor1("not", ACode), BCode), Atom.NIL)))
                    {
                        if (CompilerState.isSemidetNoneOut(State, A))
                        {
                            foreach (bool l5 in compileFunctorCall(A, State, ACode))
                            {
                                foreach (bool l6 in compileRuleBody(B, State, BCode))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("\\+", A), B)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", new Functor2(";", new Functor2("->", A, Atom.a("fail")), Atom.a("true")), B), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("once", A), B)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", new Functor2(";", new Functor2("->", A, Atom.a("true")), Atom.a("fail")), B), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable T = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor2("->", A, T), B)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", new Functor2(";", new Functor2("->", A, T), Atom.a("fail")), B), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable C = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor2("\\=", A, B), C)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", new Functor1("\\+", new Functor2("=", A, B)), C), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable ACode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", Atom.a("!"), A)))
                {
                    foreach (bool l3 in compileRuleBody(A, State, ACode))
                    {
                        foreach (bool l4 in append(ACode, new ListPair(Atom.a("yieldbreak"), Atom.NIL), PseudoCode))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable Name = new Variable();
                Variable A = new Variable();
                Variable ACode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("$CUTIF", Name), A)))
                {
                    foreach (bool l3 in compileRuleBody(A, State, ACode))
                    {
                        foreach (bool l4 in append(ACode, new ListPair(new Functor1("breakBlock", Name), Atom.NIL), PseudoCode))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object _State = arg2;
                Variable x1 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", Atom.a("fail"), x1)))
                {
                    foreach (bool l3 in YP.unify(arg3, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", Atom.a("true"), A)))
                {
                    foreach (bool l3 in compileRuleBody(A, State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable Term = new Variable();
                Variable B = new Variable();
                Variable ACode = new Variable();
                Variable TermCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor2("is", A, Term), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.unify"), new ListPair(ACode, new ListPair(TermCode, Atom.NIL))), BCode), Atom.NIL)))
                    {
                        foreach (bool l4 in compileTerm(A, State, ACode))
                        {
                            foreach (bool l5 in compileExpression(Term, State, TermCode))
                            {
                                foreach (bool l6 in compileRuleBody(B, State, BCode))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable ACode = new Variable();
                Variable B = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("$DET_NONE_OUT", ACode), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(ACode, BCode)))
                    {
                        foreach (bool l4 in compileRuleBody(B, State, BCode))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable FunctionName = new Variable();
                Variable X1Code = new Variable();
                Variable X2Code = new Variable();
                Variable BCode = new Variable();
                Variable Name = new Variable();
                Variable X1 = new Variable();
                Variable X2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", A, B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("if", new Functor2("call", FunctionName, new ListPair(X1Code, new ListPair(X2Code, Atom.NIL))), BCode), Atom.NIL)))
                    {
                        foreach (bool l4 in YP.univ(A, ListPair.make(new object[] { Name, X1, X2 })))
                        {
                            foreach (bool l5 in binaryExpressionConditional(Name, FunctionName))
                            {
                                foreach (bool l6 in compileExpression(X1, State, X1Code))
                                {
                                    foreach (bool l7 in compileExpression(X2, State, X2Code))
                                    {
                                        foreach (bool l8 in compileRuleBody(B, State, BCode))
                                        {
                                            yield return true;
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
                object State = arg2;
                object PseudoCode = arg3;
                Variable Template = new Variable();
                Variable Goal = new Variable();
                Variable Bag = new Variable();
                Variable B = new Variable();
                Variable TemplateCode = new Variable();
                Variable FindallAnswers = new Variable();
                Variable GoalAndAddCode = new Variable();
                Variable BagCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor3("findall", Template, Goal, Bag), B)))
                {
                    foreach (bool l3 in compileTerm(Template, State, TemplateCode))
                    {
                        foreach (bool l4 in CompilerState.gensym(State, Atom.a("findallAnswers"), FindallAnswers))
                        {
                            foreach (bool l5 in compileRuleBody(new Functor2(",", Goal, new Functor2(",", new Functor1("$DET_NONE_OUT", new Functor3("callMember", new Functor1("var", FindallAnswers), Atom.a("add"), Atom.NIL)), Atom.a("fail"))), State, GoalAndAddCode))
                            {
                                foreach (bool l6 in compileTerm(Bag, State, BagCode))
                                {
                                    foreach (bool l7 in compileRuleBody(B, State, BCode))
                                    {
                                        foreach (bool l8 in append(new ListPair(new Functor3("declare", Atom.a("FindallAnswers"), FindallAnswers, new Functor2("new", Atom.a("FindallAnswers"), new ListPair(TemplateCode, Atom.NIL))), GoalAndAddCode), new ListPair(new Functor2("foreach", new Functor3("callMember", new Functor1("var", FindallAnswers), Atom.a("result"), new ListPair(BagCode, Atom.NIL)), BCode), Atom.NIL), PseudoCode))
                                        {
                                            yield return true;
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
                object State = arg2;
                object PseudoCode = arg3;
                Variable Template = new Variable();
                Variable Goal = new Variable();
                Variable Bag = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor3("bagof", Template, Goal, Bag), B)))
                {
                    foreach (bool l3 in compileBagof(Atom.a("result"), Template, Goal, Bag, B, State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable Template = new Variable();
                Variable Goal = new Variable();
                Variable Bag = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor3("setof", Template, Goal, Bag), B)))
                {
                    foreach (bool l3 in compileBagof(Atom.a("resultSet"), Template, Goal, Bag, B, State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ATermCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("call", A), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.getIterator"), new ListPair(ATermCode, new ListPair(new Functor2("call", Atom.a("getDeclaringClass"), Atom.NIL), Atom.NIL))), BCode), Atom.NIL)))
                    {
                        foreach (bool l4 in compileTerm(A, State, ATermCode))
                        {
                            foreach (bool l5 in compileRuleBody(B, State, BCode))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ATermCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("current_predicate", A), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("foreach", new Functor2("call", Atom.a("YP.current_predicate"), new ListPair(ATermCode, new ListPair(new Functor2("call", Atom.a("getDeclaringClass"), Atom.NIL), Atom.NIL))), BCode), Atom.NIL)))
                    {
                        foreach (bool l4 in compileTerm(A, State, ATermCode))
                        {
                            foreach (bool l5 in compileRuleBody(B, State, BCode))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ATermCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("asserta", A), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("call", Atom.a("YP.asserta"), new ListPair(ATermCode, new ListPair(new Functor2("call", Atom.a("getDeclaringClass"), Atom.NIL), Atom.NIL))), BCode)))
                    {
                        foreach (bool l4 in compileTerm(A, State, ATermCode))
                        {
                            foreach (bool l5 in compileRuleBody(B, State, BCode))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ATermCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("assertz", A), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("call", Atom.a("YP.assertz"), new ListPair(ATermCode, new ListPair(new Functor2("call", Atom.a("getDeclaringClass"), Atom.NIL), Atom.NIL))), BCode)))
                    {
                        foreach (bool l4 in compileTerm(A, State, ATermCode))
                        {
                            foreach (bool l5 in compileRuleBody(B, State, BCode))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor1("assert", A), B)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", new Functor1("assertz", A), B), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                Variable Goal = new Variable();
                Variable Catcher = new Variable();
                Variable Handler = new Variable();
                Variable B = new Variable();
                Variable CatchGoal = new Variable();
                Variable GoalTermCode = new Variable();
                Variable BCode = new Variable();
                Variable CatcherTermCode = new Variable();
                Variable HandlerAndBCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor3("catch", Goal, Catcher, Handler), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, ListPair.make(new object[] { new Functor3("declare", Atom.a("YP.Catch"), CatchGoal, new Functor2("new", Atom.a("YP.Catch"), new ListPair(GoalTermCode, new ListPair(new Functor2("call", Atom.a("getDeclaringClass"), Atom.NIL), Atom.NIL)))), new Functor2("foreach", new Functor1("var", CatchGoal), BCode), new Functor2("foreach", new Functor3("callMember", new Functor1("var", CatchGoal), Atom.a("unifyExceptionOrThrow"), new ListPair(CatcherTermCode, Atom.NIL)), HandlerAndBCode) })))
                    {
                        foreach (bool l4 in CompilerState.gensym(State, Atom.a("catchGoal"), CatchGoal))
                        {
                            foreach (bool l5 in compileTerm(Goal, State, GoalTermCode))
                            {
                                foreach (bool l6 in compileTerm(Catcher, State, CatcherTermCode))
                                {
                                    foreach (bool l7 in compileRuleBody(B, State, BCode))
                                    {
                                        foreach (bool l8 in compileRuleBody(new Functor2(",", Handler, B), State, HandlerAndBCode))
                                        {
                                            yield return true;
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
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable C = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(",", new Functor2(",", A, B), C)))
                {
                    foreach (bool l3 in compileRuleBody(new Functor2(",", A, new Functor2(",", B, C)), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(";", A, B)))
                {
                    if (YP.var(A))
                    {
                        foreach (bool l4 in compileRuleBody(new Functor2(";", new Functor1("call", A), B), State, PseudoCode))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable A = new Variable();
                Variable T = new Variable();
                Variable B = new Variable();
                Variable CutIfLabel = new Variable();
                Variable Code = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(";", new Functor2("->", A, T), B)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor2("breakableBlock", CutIfLabel, Code), Atom.NIL)))
                    {
                        foreach (bool l4 in CompilerState.gensym(State, Atom.a("cutIf"), CutIfLabel))
                        {
                            foreach (bool l5 in compileRuleBody(new Functor2(";", new Functor2(",", A, new Functor2(",", new Functor1("$CUTIF", CutIfLabel), T)), B), State, Code))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable _B = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(";", Atom.a("!"), _B)))
                {
                    foreach (bool l3 in compileRuleBody(Atom.a("!"), State, PseudoCode))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                object PseudoCode = arg3;
                Variable A = new Variable();
                Variable B = new Variable();
                Variable ACode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2(";", A, B)))
                {
                    foreach (bool l3 in compileRuleBody(A, State, ACode))
                    {
                        foreach (bool l4 in compileRuleBody(B, State, BCode))
                        {
                            foreach (bool l5 in append(ACode, BCode, PseudoCode))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("!")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("return"), Atom.NIL)))
                    {
                        if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("!")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("returntrue"), Atom.NIL)))
                    {
                        if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("!")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("yieldtrue"), new ListPair(Atom.a("yieldbreak"), Atom.NIL))))
                    {
                        CompilerState.setCodeUsesYield(State);
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object _State = arg2;
                Variable Name = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("$CUTIF", Name)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(new Functor1("breakBlock", Name), Atom.NIL)))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("true")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("return"), Atom.NIL)))
                    {
                        if (CompilerState.determinismEquals(State, Atom.a("detNoneOut")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("true")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("returntrue"), Atom.NIL)))
                    {
                        if (CompilerState.determinismEquals(State, Atom.a("semidetNoneOut")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.a("true")))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(Atom.a("yieldfalse"), Atom.NIL)))
                    {
                        CompilerState.setCodeUsesYield(State);
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object A = arg1;
                object State = arg2;
                object PseudoCode = arg3;
                foreach (bool l2 in compileRuleBody(new Functor2(",", A, Atom.a("true")), State, PseudoCode))
                {
                    yield return true;
                    yield break;
                }
            }
        }

        public static IEnumerable<bool> compileBagof(object ResultMethod, object Template, object Goal, object Bag, object B, object State, object PseudoCode)
        {
            {
                Variable TemplateCode = new Variable();
                Variable GoalTermCode = new Variable();
                Variable UnqualifiedGoal = new Variable();
                Variable BagofAnswers = new Variable();
                Variable GoalAndAddCode = new Variable();
                Variable BagCode = new Variable();
                Variable BCode = new Variable();
                foreach (bool l2 in compileTerm(Template, State, TemplateCode))
                {
                    foreach (bool l3 in compileTerm(Goal, State, GoalTermCode))
                    {
                        foreach (bool l4 in unqualifiedGoal(Goal, UnqualifiedGoal))
                        {
                            foreach (bool l5 in CompilerState.gensym(State, Atom.a("bagofAnswers"), BagofAnswers))
                            {
                                foreach (bool l6 in compileRuleBody(new Functor2(",", UnqualifiedGoal, new Functor2(",", new Functor1("$DET_NONE_OUT", new Functor3("callMember", new Functor1("var", BagofAnswers), Atom.a("add"), Atom.NIL)), Atom.a("fail"))), State, GoalAndAddCode))
                                {
                                    foreach (bool l7 in compileTerm(Bag, State, BagCode))
                                    {
                                        foreach (bool l8 in compileRuleBody(B, State, BCode))
                                        {
                                            foreach (bool l9 in append(new ListPair(new Functor3("declare", Atom.a("BagofAnswers"), BagofAnswers, new Functor2("new", Atom.a("BagofAnswers"), new ListPair(TemplateCode, new ListPair(GoalTermCode, Atom.NIL)))), GoalAndAddCode), new ListPair(new Functor2("foreach", new Functor3("callMember", new Functor1("var", BagofAnswers), ResultMethod, new ListPair(BagCode, Atom.NIL)), BCode), Atom.NIL), PseudoCode))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> unqualifiedGoal(object arg1, object arg2)
        {
            {
                object Goal = arg1;
                foreach (bool l2 in YP.unify(arg2, new Functor1("call", Goal)))
                {
                    if (YP.var(Goal))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object UnqualifiedGoal = arg2;
                Variable x1 = new Variable();
                Variable Goal = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("^", x1, Goal)))
                {
                    foreach (bool l3 in unqualifiedGoal(Goal, UnqualifiedGoal))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                Variable UnqualifiedGoal = new Variable();
                foreach (bool l2 in YP.unify(arg1, UnqualifiedGoal))
                {
                    foreach (bool l3 in YP.unify(arg2, UnqualifiedGoal))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
        }

        public static IEnumerable<bool> binaryExpressionConditional(object arg1, object arg2)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("=:=")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.equal")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("=\\=")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.notEqual")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a(">")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.greaterThan")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("<")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.lessThan")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a(">=")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.greaterThanOrEqual")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("=<")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.lessThanOrEqual")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
        }

        public static IEnumerable<bool> compileFunctorCall(object Functor_1, object State, object PseudoCode)
        {
            {
                Variable FunctorName = new Variable();
                Variable FunctorArgs = new Variable();
                Variable x6 = new Variable();
                Variable Arity = new Variable();
                Variable FunctionName = new Variable();
                Variable CompiledArgs = new Variable();
                foreach (bool l2 in YP.univ(Functor_1, new ListPair(FunctorName, FunctorArgs)))
                {
                    foreach (bool l3 in YP.functor(Functor_1, x6, Arity))
                    {
                        foreach (bool l4 in functorCallFunctionName(State, FunctorName, Arity, FunctionName))
                        {
                            foreach (bool l5 in maplist_compileTerm(FunctorArgs, State, CompiledArgs))
                            {
                                if (YP.termEqual(FunctionName, Atom.NIL))
                                {
                                    foreach (bool l7 in YP.unify(PseudoCode, new Functor2("call", Atom.a("YP.matchDynamic"), new ListPair(new Functor2("call", Atom.a("Atom.a"), new ListPair(new Functor1("object", FunctorName), Atom.NIL)), new ListPair(new Functor1("objectArray", CompiledArgs), Atom.NIL)))))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                    goto cutIf1;
                                }
                                foreach (bool l6 in YP.unify(PseudoCode, new Functor3("functorCall", FunctionName, FunctorArgs, CompiledArgs)))
                                {
                                    yield return true;
                                    yield break;
                                }
                            cutIf1:
                                { }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> functorCallFunctionName(object arg1, object arg2, object arg3, object arg4)
        {
            {
                object _State = arg1;
                object Name = arg2;
                object Arity = arg3;
                object x4 = arg4;
                if (functorCallIsSpecialForm(Name, Arity))
                {
                    yield break;
                }
            }
            {
                object x1 = arg1;
                object Name = arg2;
                object Arity = arg3;
                object FunctionName = arg4;
                foreach (bool l2 in functorCallYPFunctionName(Name, Arity, FunctionName))
                {
                    yield return true;
                    yield break;
                }
            }
            {
                object State = arg1;
                object Arity = arg3;
                Variable Name = new Variable();
                foreach (bool l2 in YP.unify(arg2, Name))
                {
                    foreach (bool l3 in YP.unify(arg4, Name))
                    {
                        if (CompilerState.nameArityHasModule(State, Name, Arity, Atom.a("")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object _State = arg1;
                object _Arity = arg3;
                Variable Name = new Variable();
                foreach (bool l2 in YP.unify(arg2, Name))
                {
                    foreach (bool l3 in YP.unify(arg4, Name))
                    {
                        foreach (bool l4 in Atom.module(Name, Atom.a("")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object _State = arg1;
                object Name = arg2;
                object _Arity = arg3;
                foreach (bool l2 in YP.unify(arg4, Atom.NIL))
                {
                    foreach (bool l3 in Atom.module(Name, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object _State = arg1;
                object Name = arg2;
                object Arity = arg3;
                object x4 = arg4;
                Variable Module = new Variable();
                Variable Message = new Variable();
                foreach (bool l2 in Atom.module(Name, Module))
                {
                    foreach (bool l3 in YP.atom_concat(Atom.a("Not supporting calls to external module: "), Module, Message))
                    {
                        YP.throwException(new Functor2("error", new Functor2("type_error", Atom.a("callable"), new Functor2("/", Name, Arity)), Message));
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object _State = arg1;
                object Name = arg2;
                object _Arity = arg3;
                object x4 = arg4;
                YP.throwException(new Functor2("error", new Functor2("type_error", Atom.a("callable"), Name), Atom.a("Term is not callable")));
                yield return true;
                yield break;
            }
        }

        public static bool functorCallIsSpecialForm(object Name, object Arity)
        {
            {
                Variable x3 = new Variable();
                if (YP.termEqual(Arity, 0))
                {
                    if (YP.termEqual(Name, Atom.a("!")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("fail")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("true")))
                    {
                        return true;
                    }
                }
                if (YP.termEqual(Arity, 1))
                {
                    if (YP.termEqual(Name, Atom.a("\\+")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("once")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("$CUTIF")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("$DET_NONE_OUT")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("call")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("current_predicate")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("asserta")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("assertz")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("assert")))
                    {
                        return true;
                    }
                }
                if (YP.termEqual(Arity, 2))
                {
                    if (YP.termEqual(Name, Atom.a(";")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a(",")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("->")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("\\=")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("is")))
                    {
                        return true;
                    }
                    foreach (bool l3 in binaryExpressionConditional(Name, x3))
                    {
                        return true;
                    }
                }
                if (YP.termEqual(Arity, 3))
                {
                    if (YP.termEqual(Name, Atom.a("findall")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("bagof")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("setof")))
                    {
                        return true;
                    }
                    if (YP.termEqual(Name, Atom.a("catch")))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static IEnumerable<bool> functorCallYPFunctionName(object arg1, object arg2, object arg3)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("=")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.unify")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("=..")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.univ")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("var")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.var")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("nonvar")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.nonvar")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("arg")))
                {
                    foreach (bool l3 in YP.unify(arg2, 3))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.arg")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("functor")))
                {
                    foreach (bool l3 in YP.unify(arg2, 3))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.functor")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("repeat")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.repeat")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("get_code")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.get_code")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("current_op")))
                {
                    foreach (bool l3 in YP.unify(arg2, 3))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.current_op")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atom_length")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.atom_length")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atom_concat")))
                {
                    foreach (bool l3 in YP.unify(arg2, 3))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.atom_concat")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("sub_atom")))
                {
                    foreach (bool l3 in YP.unify(arg2, 5))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.sub_atom")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atom_chars")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.atom_chars")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atom_codes")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.atom_codes")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("char_code")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.char_code")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("number_chars")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.number_chars")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("number_codes")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.number_codes")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("copy_term")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.copy_term")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("sort")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.sort")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
            // Manually included : script_event for callback to LSL/C#

                //object x1 = arg1;
                foreach (bool l2 in YP.unify(arg1, Atom.a(@"script_event")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a(@"YP.script_event")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("nl")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.nl")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("write")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.write")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("put_code")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.put_code")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("see")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.see")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("seen")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.seen")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("tell")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.tell")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("told")))
                {
                    foreach (bool l3 in YP.unify(arg2, 0))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.told")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("clause")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.clause")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("retract")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.retract")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("abolish")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.abolish")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("retractall")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.retractall")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atom")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.atom")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("integer")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.integer")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("float")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.isFloat")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("number")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.number")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atomic")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.atomic")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("compound")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.compound")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("==")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.termEqual")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("\\==")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.termNotEqual")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("@<")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.termLessThan")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("@=<")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.termLessThanOrEqual")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("@>")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.termGreaterThan")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("@>=")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.termGreaterThanOrEqual")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("throw")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.throwException")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("current_prolog_flag")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.current_prolog_flag")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("set_prolog_flag")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.set_prolog_flag")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("current_input")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.current_input")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("current_output")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("YP.current_output")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("read_term")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("Parser.read_term2")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("read_term")))
                {
                    foreach (bool l3 in YP.unify(arg2, 3))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("Parser.read_term3")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("read")))
                {
                    foreach (bool l3 in YP.unify(arg2, 1))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("Parser.read1")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("read")))
                {
                    foreach (bool l3 in YP.unify(arg2, 2))
                    {
                        foreach (bool l4 in YP.unify(arg3, Atom.a("Parser.read2")))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> compileTerm(object arg1, object arg2, object arg3)
        {
            {
                object Term = arg1;
                object State = arg2;
                Variable VariableName = new Variable();
                foreach (bool l2 in YP.unify(arg3, new Functor1("var", VariableName)))
                {
                    if (YP.var(Term))
                    {
                        foreach (bool l4 in CompilerState.getVariableName(State, Term, VariableName))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
            {
                object _State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg3, new Functor1("var", Atom.a("Atom.NIL"))))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Term = arg1;
                object State = arg2;
                object Code = arg3;
                Variable ModuleCode = new Variable();
                if (YP.atom(Term))
                {
                    foreach (bool l3 in compileAtomModule(Term, 0, State, ModuleCode))
                    {
                        foreach (bool l4 in YP.unify(Code, new Functor2("call", Atom.a("Atom.a"), new ListPair(new Functor1("object", Term), new ListPair(ModuleCode, Atom.NIL)))))
                        {
                            yield return true;
                            yield break;
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in YP.unify(Code, new Functor2("call", Atom.a("Atom.a"), new ListPair(new Functor1("object", Term), Atom.NIL))))
                    {
                        yield return true;
                        yield break;
                    }
                cutIf1:
                    { }
                }
            }
            {
                object State = arg2;
                Variable First = new Variable();
                Variable Rest = new Variable();
                Variable CompiledList = new Variable();
                Variable x5 = new Variable();
                Variable Rest2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(First, Rest)))
                {
                    foreach (bool l3 in YP.unify(arg3, new Functor2("call", Atom.a("ListPair.make"), new ListPair(new Functor1("objectArray", CompiledList), Atom.NIL))))
                    {
                        if (YP.nonvar(Rest))
                        {
                            foreach (bool l5 in YP.unify(Rest, new ListPair(x5, Rest2)))
                            {
                                if (YP.termNotEqual(Rest2, Atom.NIL))
                                {
                                    foreach (bool l7 in maplist_compileTerm(new ListPair(First, Rest), State, CompiledList))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            {
                object State = arg2;
                Variable First = new Variable();
                Variable Rest = new Variable();
                Variable Arg1 = new Variable();
                Variable Arg2 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(First, Rest)))
                {
                    foreach (bool l3 in YP.unify(arg3, new Functor2("new", Atom.a("ListPair"), new ListPair(Arg1, new ListPair(Arg2, Atom.NIL)))))
                    {
                        foreach (bool l4 in compileTerm(First, State, Arg1))
                        {
                            foreach (bool l5 in compileTerm(Rest, State, Arg2))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object Term = arg1;
                object State = arg2;
                object Result = arg3;
                Variable Name = new Variable();
                Variable TermArgs = new Variable();
                Variable x6 = new Variable();
                Variable Arity = new Variable();
                Variable ModuleCode = new Variable();
                Variable NameCode = new Variable();
                Variable X1 = new Variable();
                Variable Arg1 = new Variable();
                Variable X2 = new Variable();
                Variable Arg2 = new Variable();
                Variable X3 = new Variable();
                Variable Arg3 = new Variable();
                Variable Args = new Variable();
                foreach (bool l2 in YP.univ(Term, new ListPair(Name, TermArgs)))
                {
                    if (YP.termEqual(TermArgs, Atom.NIL))
                    {
                        foreach (bool l4 in YP.unify(Result, new Functor1("object", Name)))
                        {
                            yield return true;
                            yield break;
                        }
                        goto cutIf2;
                    }
                    foreach (bool l3 in YP.functor(Term, x6, Arity))
                    {
                        foreach (bool l4 in compileAtomModule(Name, Arity, State, ModuleCode))
                        {
                            foreach (bool l5 in YP.unify(NameCode, new Functor2("call", Atom.a("Atom.a"), new ListPair(new Functor1("object", Name), new ListPair(ModuleCode, Atom.NIL)))))
                            {
                                foreach (bool l6 in YP.unify(TermArgs, new ListPair(X1, Atom.NIL)))
                                {
                                    foreach (bool l7 in compileTerm(X1, State, Arg1))
                                    {
                                        foreach (bool l8 in YP.unify(Result, new Functor2("new", Atom.a("Functor1"), new ListPair(NameCode, new ListPair(Arg1, Atom.NIL)))))
                                        {
                                            yield return true;
                                            yield break;
                                        }
                                    }
                                    goto cutIf4;
                                }
                                foreach (bool l6 in YP.unify(TermArgs, new ListPair(X1, new ListPair(X2, Atom.NIL))))
                                {
                                    foreach (bool l7 in compileTerm(X1, State, Arg1))
                                    {
                                        foreach (bool l8 in compileTerm(X2, State, Arg2))
                                        {
                                            foreach (bool l9 in YP.unify(Result, new Functor2("new", Atom.a("Functor2"), ListPair.make(new object[] { NameCode, Arg1, Arg2 }))))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    }
                                    goto cutIf5;
                                }
                                foreach (bool l6 in YP.unify(TermArgs, ListPair.make(new object[] { X1, X2, X3 })))
                                {
                                    foreach (bool l7 in compileTerm(X1, State, Arg1))
                                    {
                                        foreach (bool l8 in compileTerm(X2, State, Arg2))
                                        {
                                            foreach (bool l9 in compileTerm(X3, State, Arg3))
                                            {
                                                foreach (bool l10 in YP.unify(Result, new Functor2("new", Atom.a("Functor3"), ListPair.make(new object[] { NameCode, Arg1, Arg2, Arg3 }))))
                                                {
                                                    yield return true;
                                                    yield break;
                                                }
                                            }
                                        }
                                    }
                                }
                                foreach (bool l6 in maplist_compileTerm(TermArgs, State, Args))
                                {
                                    foreach (bool l7 in YP.unify(Result, new Functor2("new", Atom.a("Functor"), new ListPair(NameCode, new ListPair(new Functor1("objectArray", Args), Atom.NIL)))))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                }
                            cutIf5:
                            cutIf4:
                                { }
                            }
                            goto cutIf3;
                        }
                        foreach (bool l4 in YP.unify(NameCode, new Functor1("object", Name)))
                        {
                            foreach (bool l5 in YP.unify(TermArgs, new ListPair(X1, Atom.NIL)))
                            {
                                foreach (bool l6 in compileTerm(X1, State, Arg1))
                                {
                                    foreach (bool l7 in YP.unify(Result, new Functor2("new", Atom.a("Functor1"), new ListPair(NameCode, new ListPair(Arg1, Atom.NIL)))))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                }
                                goto cutIf6;
                            }
                            foreach (bool l5 in YP.unify(TermArgs, new ListPair(X1, new ListPair(X2, Atom.NIL))))
                            {
                                foreach (bool l6 in compileTerm(X1, State, Arg1))
                                {
                                    foreach (bool l7 in compileTerm(X2, State, Arg2))
                                    {
                                        foreach (bool l8 in YP.unify(Result, new Functor2("new", Atom.a("Functor2"), ListPair.make(new object[] { NameCode, Arg1, Arg2 }))))
                                        {
                                            yield return true;
                                            yield break;
                                        }
                                    }
                                }
                                goto cutIf7;
                            }
                            foreach (bool l5 in YP.unify(TermArgs, ListPair.make(new object[] { X1, X2, X3 })))
                            {
                                foreach (bool l6 in compileTerm(X1, State, Arg1))
                                {
                                    foreach (bool l7 in compileTerm(X2, State, Arg2))
                                    {
                                        foreach (bool l8 in compileTerm(X3, State, Arg3))
                                        {
                                            foreach (bool l9 in YP.unify(Result, new Functor2("new", Atom.a("Functor3"), ListPair.make(new object[] { NameCode, Arg1, Arg2, Arg3 }))))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    }
                                }
                            }
                            foreach (bool l5 in maplist_compileTerm(TermArgs, State, Args))
                            {
                                foreach (bool l6 in YP.unify(Result, new Functor2("new", Atom.a("Functor"), new ListPair(NameCode, new ListPair(new Functor1("objectArray", Args), Atom.NIL)))))
                                {
                                    yield return true;
                                    yield break;
                                }
                            }
                        cutIf7:
                        cutIf6:
                            { }
                        }
                    cutIf3:
                        { }
                    }
                cutIf2:
                    { }
                }
            }
        }

        public static IEnumerable<bool> compileAtomModule(object Name, object arg2, object arg3, object ModuleCode)
        {
            {
                object Arity = arg2;
                object State = arg3;
                if (CompilerState.nameArityHasModule(State, Name, Arity, Atom.a("")))
                {
                    foreach (bool l3 in YP.unify(ModuleCode, new Functor2("call", Atom.a("Atom.a"), new ListPair(new Functor1("object", Atom.a("")), Atom.NIL))))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object _Arity = arg2;
                object _State = arg3;
                Variable Module = new Variable();
                foreach (bool l2 in Atom.module(Name, Module))
                {
                    if (YP.termNotEqual(Module, Atom.NIL))
                    {
                        foreach (bool l4 in YP.unify(ModuleCode, new Functor2("call", Atom.a("Atom.a"), new ListPair(new Functor1("object", Module), Atom.NIL))))
                        {
                            yield return true;
                            yield break;
                        }
                    }
                }
            }
        }

        public static IEnumerable<bool> maplist_compileTerm(object arg1, object arg2, object arg3)
        {
            {
                object _State = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg3, Atom.NIL))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object State = arg2;
                Variable First = new Variable();
                Variable Rest = new Variable();
                Variable FirstResult = new Variable();
                Variable RestResults = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(First, Rest)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(FirstResult, RestResults)))
                    {
                        if (YP.nonvar(Rest))
                        {
                            foreach (bool l5 in compileTerm(First, State, FirstResult))
                        {
                                foreach (bool l6 in maplist_compileTerm(Rest, State, RestResults))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
        }
        }

        public static IEnumerable<bool> compileExpression(object Term, object State, object Result)
        {
            {
                Variable Name = new Variable();
                Variable TermArgs = new Variable();
                Variable X1 = new Variable();
                Variable FunctionName = new Variable();
                Variable Arg1 = new Variable();
                Variable x9 = new Variable();
                Variable X2 = new Variable();
                Variable Arg2 = new Variable();
                Variable x12 = new Variable();
                Variable Arity = new Variable();
                if (YP.nonvar(Term))
                {
                    foreach (bool l3 in YP.univ(Term, new ListPair(Name, TermArgs)))
                    {
                        if (YP.atom(Name))
                        {
                            foreach (bool l5 in YP.unify(TermArgs, new ListPair(X1, Atom.NIL)))
                            {
                                foreach (bool l6 in unaryFunction(Name, FunctionName))
                                {
                                    foreach (bool l7 in compileExpression(X1, State, Arg1))
                                    {
                                        foreach (bool l8 in YP.unify(Result, new Functor2("call", FunctionName, new ListPair(Arg1, Atom.NIL))))
                                        {
                                            yield return true;
                                            yield break;
                                        }
                                    }
                                    goto cutIf1;
                                }
                            }
                            foreach (bool l5 in YP.unify(Term, new ListPair(x9, Atom.NIL)))
                            {
                                foreach (bool l6 in compileTerm(Term, State, Result))
                                {
                                    yield return true;
                                    yield break;
                                }
                                goto cutIf2;
                            }
                            foreach (bool l5 in YP.unify(TermArgs, new ListPair(X1, new ListPair(X2, Atom.NIL))))
                            {
                                foreach (bool l6 in binaryFunction(Name, FunctionName))
                                {
                                    foreach (bool l7 in compileExpression(X1, State, Arg1))
                                    {
                                        foreach (bool l8 in compileExpression(X2, State, Arg2))
                                        {
                                            foreach (bool l9 in YP.unify(Result, new Functor2("call", FunctionName, new ListPair(Arg1, new ListPair(Arg2, Atom.NIL)))))
                                            {
                                                yield return true;
                                                yield break;
                                            }
                                        }
                                    }
                                    goto cutIf3;
                                }
                            }
                            foreach (bool l5 in YP.functor(Term, x12, Arity))
                            {
                                YP.throwException(new Functor2("error", new Functor2("type_error", Atom.a("evaluable"), new Functor2("/", Name, Arity)), Atom.a("Not an expression function")));
                                yield return false;
                            }
                        cutIf3:
                        cutIf2:
                        cutIf1:
                            { }
                        }
                    }
                }
            }
            {
                foreach (bool l2 in compileTerm(Term, State, Result))
                {
                    yield return true;
                    yield break;
                }
            }
        }

        public static IEnumerable<bool> unaryFunction(object arg1, object arg2)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("-")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.negate")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("abs")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.abs")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("sign")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.sign")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("float")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.toFloat")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("floor")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.floor")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("truncate")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.truncate")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("round")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.round")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("ceiling")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.ceiling")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("sin")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.sin")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("cos")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.cos")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("atan")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.atan")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("exp")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.exp")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("log")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.log")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("sqrt")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.sqrt")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("\\")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.bitwiseComplement")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
        }

        public static IEnumerable<bool> binaryFunction(object arg1, object arg2)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("+")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.add")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("-")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.subtract")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("*")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.multiply")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("/")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.divide")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("//")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.intDivide")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("mod")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.mod")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("**")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.pow")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a(">>")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.bitwiseShiftRight")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("<<")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.bitwiseShiftLeft")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("/\\")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.bitwiseAnd")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("\\/")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.bitwiseOr")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("min")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.min")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("max")))
                {
                    foreach (bool l3 in YP.unify(arg2, Atom.a("YP.max")))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
        }

        public static void convertFunctionCSharp(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("getDeclaringClass")))
                {
                    YP.write(Atom.a("public class YPInnerClass {}"));
                    YP.nl();
                    YP.write(Atom.a("public static System.Type getDeclaringClass() { return typeof(YPInnerClass).DeclaringType; }"));
                    YP.nl();
                    YP.nl();
                    return;
                }
            }
            {
                Variable ReturnType = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable Body = new Variable();
                Variable Level = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor("function", new object[] { ReturnType, Name, ArgList, Body })))
                {
                    YP.write(Atom.a("public static "));
                    YP.write(ReturnType);
                    YP.write(Atom.a(" "));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a(") {"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(Level, 1))
                    {
                        convertStatementListCSharp(Body, Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        YP.nl();
                        return;
                    }
                }
            }
        }

        public static IEnumerable<bool> convertStatementListCSharp(object arg1, object x1, object x2)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    yield return true;
                    yield break;
                }
            }
        }

        public static void convertStatementListCSharp(object arg1, object Level)
        {
            {
                Variable Name = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NewStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("breakableBlock", Name, Body), RestStatements)))
                {
                    foreach (bool l3 in append(Body, new ListPair(new Functor1("label", Name), RestStatements), NewStatements))
                    {
                        convertStatementListCSharp(NewStatements, Level);
                        return;
                    }
                }
            }
            {
                Variable Type = new Variable();
                Variable Name = new Variable();
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("declare", Type, Name, Expression), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Type);
                    YP.write(Atom.a(" "));
                    YP.write(Name);
                    YP.write(Atom.a(" = "));
                    convertExpressionCSharp(Expression);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("assign", Name, Expression), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Name);
                    YP.write(Atom.a(" = "));
                    convertExpressionCSharp(Expression);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldtrue"), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("yield return true;"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldfalse"), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("yield return false;"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldbreak"), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("yield break;"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("return"), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("return;"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("returntrue"), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("return true;"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("returnfalse"), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("return false;"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("label", Name), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Name);
                    YP.write(Atom.a(":"));
                    YP.nl();
                    if (YP.termEqual(RestStatements, Atom.NIL))
                    {
                        convertIndentationCSharp(Level);
                        YP.write(Atom.a("{}"));
                        YP.nl();
                        convertStatementListCSharp(RestStatements, Level);
                        return;
                        goto cutIf1;
                    }
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                cutIf1:
                    { }
                }
            }
            {
                Variable Name = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("breakBlock", Name), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("goto "));
                    YP.write(Name);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("call", Name, ArgList), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a(");"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable _FunctorArgs = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("functorCall", Name, _FunctorArgs, ArgList), RestStatements)))
                {
                    convertStatementListCSharp(new ListPair(new Functor2("call", Name, ArgList), RestStatements), Level);
                    return;
                }
            }
            {
                Variable Obj = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("callMember", new Functor1("var", Obj), Name, ArgList), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Obj);
                    YP.write(Atom.a("."));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a(");"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
            {
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("blockScope", Body), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("{"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListCSharp(Body, NextLevel);
                        convertIndentationCSharp(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListCSharp(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                Variable Expression = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("if", Expression, Body), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("if ("));
                    convertExpressionCSharp(Expression);
                    YP.write(Atom.a(") {"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListCSharp(Body, NextLevel);
                        convertIndentationCSharp(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListCSharp(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                Variable Expression = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("foreach", Expression, Body), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("foreach (bool l"));
                    YP.write(Level);
                    YP.write(Atom.a(" in "));
                    convertExpressionCSharp(Expression);
                    YP.write(Atom.a(") {"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListCSharp(Body, NextLevel);
                        convertIndentationCSharp(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListCSharp(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("throw", Expression), RestStatements)))
                {
                    convertIndentationCSharp(Level);
                    YP.write(Atom.a("throw "));
                    convertExpressionCSharp(Expression);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListCSharp(RestStatements, Level);
                    return;
                }
            }
        }

        public static void convertIndentationCSharp(object Level)
        {
            {
                Variable N = new Variable();
                foreach (bool l2 in YP.unify(N, YP.multiply(Level, 2)))
                {
                    repeatWrite(Atom.a(" "), N);
                    return;
                }
            }
        }

        public static void convertArgListCSharp(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                Variable Head = new Variable();
                Variable Tail = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Head, Tail)))
                {
                    convertExpressionCSharp(Head);
                    if (YP.termNotEqual(Tail, Atom.NIL))
                    {
                        YP.write(Atom.a(", "));
                        convertArgListCSharp(Tail);
                        return;
                        goto cutIf1;
                    }
                    convertArgListCSharp(Tail);
                    return;
                cutIf1:
                    { }
                }
            }
        }

        public static void convertExpressionCSharp(object arg1)
        {
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("arg", X)))
                {
                    YP.write(Atom.a("object "));
                    YP.write(X);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("call", Name, ArgList)))
                {
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable _FunctorArgs = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("functorCall", Name, _FunctorArgs, ArgList)))
                {
                    convertExpressionCSharp(new Functor2("call", Name, ArgList));
                    return;
                }
            }
            {
                Variable Obj = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("callMember", new Functor1("var", Obj), Name, ArgList)))
                {
                    YP.write(Obj);
                    YP.write(Atom.a("."));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("new", Name, ArgList)))
                {
                    YP.write(Atom.a("new "));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("var", Name)))
                {
                    YP.write(Name);
                    return;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("null")))
                {
                    YP.write(Atom.a("null"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("not", X)))
                {
                    YP.write(Atom.a("!("));
                    convertExpressionCSharp(X);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                Variable Y = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("and", X, Y)))
                {
                    YP.write(Atom.a("("));
                    convertExpressionCSharp(X);
                    YP.write(Atom.a(") && ("));
                    convertExpressionCSharp(Y);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("objectArray", ArgList)))
                {
                    YP.write(Atom.a("new object[] {"));
                    convertArgListCSharp(ArgList);
                    YP.write(Atom.a("}"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                Variable Codes = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("object", X)))
                {
                    if (YP.atom(X))
                    {
                        YP.write(Atom.a("\""));
                        foreach (bool l4 in YP.atom_codes(X, Codes))
                        {
                            convertStringCodesCSharp(Codes);
                            YP.write(Atom.a("\""));
                            return;
                        }
                    }
                }
            }
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("object", X)))
                {
                    YP.write(X);
                    return;
                }
            }
        }

        public static void convertStringCodesCSharp(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                Variable Code = new Variable();
                Variable RestCodes = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Code, RestCodes)))
                {
                    foreach (bool l3 in putCStringCode(Code))
                    {
                        convertStringCodesCSharp(RestCodes);
                        return;
                    }
                }
            }
        }

        public static void convertFunctionJavascript(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("getDeclaringClass")))
                {
                    YP.write(Atom.a("function getDeclaringClass() { return null; }"));
                    YP.nl();
                    YP.nl();
                    return;
                }
            }
            {
                Variable x1 = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable Body = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor("function", new object[] { x1, Name, ArgList, Body })))
                {
                    YP.write(Atom.a("function "));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a(") {"));
                    YP.nl();
                    convertStatementListJavascript(Body, 1);
                    YP.write(Atom.a("}"));
                    YP.nl();
                    YP.nl();
                    return;
                }
            }
        }

        public static void convertStatementListJavascript(object arg1, object arg2)
        {
            {
                object x1 = arg2;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Name = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("breakableBlock", Name, Body), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Name);
                    YP.write(Atom.a(":"));
                    YP.nl();
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("{"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListJavascript(Body, NextLevel);
                        convertIndentationJavascript(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListJavascript(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                object Level = arg2;
                Variable _Type = new Variable();
                Variable Name = new Variable();
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("declare", _Type, Name, Expression), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("var "));
                    YP.write(Name);
                    YP.write(Atom.a(" = "));
                    convertExpressionJavascript(Expression);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Name = new Variable();
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("assign", Name, Expression), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Name);
                    YP.write(Atom.a(" = "));
                    convertExpressionJavascript(Expression);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldtrue"), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("yield true;"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldfalse"), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("yield false;"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldbreak"), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("return;"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("return"), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("return;"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("returntrue"), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("return true;"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("returnfalse"), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("return false;"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Name = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("breakBlock", Name), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("break "));
                    YP.write(Name);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("call", Name, ArgList), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a(");"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Name = new Variable();
                Variable _FunctorArgs = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("functorCall", Name, _FunctorArgs, ArgList), RestStatements)))
                {
                    convertStatementListJavascript(new ListPair(new Functor2("call", Name, ArgList), RestStatements), Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Obj = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("callMember", new Functor1("var", Obj), Name, ArgList), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Obj);
                    YP.write(Atom.a("."));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a(");"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
            {
                object Level = arg2;
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("blockScope", Body), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("{"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListJavascript(Body, NextLevel);
                        convertIndentationJavascript(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListJavascript(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                object Level = arg2;
                Variable Expression = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("if", Expression, Body), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("if ("));
                    convertExpressionJavascript(Expression);
                    YP.write(Atom.a(") {"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListJavascript(Body, NextLevel);
                        convertIndentationJavascript(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListJavascript(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                object Level = arg2;
                Variable Expression = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("foreach", Expression, Body), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("for each (var l"));
                    YP.write(Level);
                    YP.write(Atom.a(" in "));
                    convertExpressionJavascript(Expression);
                    YP.write(Atom.a(") {"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        convertStatementListJavascript(Body, NextLevel);
                        convertIndentationJavascript(Level);
                        YP.write(Atom.a("}"));
                        YP.nl();
                        convertStatementListJavascript(RestStatements, Level);
                        return;
                    }
                }
            }
            {
                object Level = arg2;
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("throw", Expression), RestStatements)))
                {
                    convertIndentationJavascript(Level);
                    YP.write(Atom.a("throw "));
                    convertExpressionJavascript(Expression);
                    YP.write(Atom.a(";"));
                    YP.nl();
                    convertStatementListJavascript(RestStatements, Level);
                    return;
                }
            }
        }

        public static void convertIndentationJavascript(object Level)
        {
            {
                Variable N = new Variable();
                foreach (bool l2 in YP.unify(N, YP.multiply(Level, 2)))
                {
                    repeatWrite(Atom.a(" "), N);
                    return;
                }
            }
        }

        public static void convertArgListJavascript(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                Variable Head = new Variable();
                Variable Tail = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Head, Tail)))
                {
                    convertExpressionJavascript(Head);
                    if (YP.termNotEqual(Tail, Atom.NIL))
                    {
                        YP.write(Atom.a(", "));
                        convertArgListJavascript(Tail);
                        return;
                        goto cutIf1;
                    }
                    convertArgListJavascript(Tail);
                    return;
                cutIf1:
                    { }
                }
            }
        }

        public static void convertExpressionJavascript(object arg1)
        {
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("arg", X)))
                {
                    YP.write(X);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("call", Name, ArgList)))
                {
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable _FunctorArgs = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("functorCall", Name, _FunctorArgs, ArgList)))
                {
                    convertExpressionJavascript(new Functor2("call", Name, ArgList));
                    return;
                }
            }
            {
                Variable Obj = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("callMember", new Functor1("var", Obj), Name, ArgList)))
                {
                    YP.write(Obj);
                    YP.write(Atom.a("."));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("new", Name, ArgList)))
                {
                    YP.write(Atom.a("new "));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("var", Name)))
                {
                    YP.write(Name);
                    return;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("null")))
                {
                    YP.write(Atom.a("null"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("not", X)))
                {
                    YP.write(Atom.a("!("));
                    convertExpressionJavascript(X);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                Variable Y = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("and", X, Y)))
                {
                    YP.write(Atom.a("("));
                    convertExpressionJavascript(X);
                    YP.write(Atom.a(") && ("));
                    convertExpressionJavascript(Y);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("objectArray", ArgList)))
                {
                    YP.write(Atom.a("["));
                    convertArgListJavascript(ArgList);
                    YP.write(Atom.a("]"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                Variable Codes = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("object", X)))
                {
                    if (YP.atom(X))
                    {
                        YP.write(Atom.a("\""));
                        foreach (bool l4 in YP.atom_codes(X, Codes))
                        {
                            convertStringCodesJavascript(Codes);
                            YP.write(Atom.a("\""));
                            return;
                        }
                    }
                }
            }
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("object", X)))
                {
                    YP.write(X);
                    return;
                }
            }
        }

        public static void convertStringCodesJavascript(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                Variable Code = new Variable();
                Variable RestCodes = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Code, RestCodes)))
                {
                    foreach (bool l3 in putCStringCode(Code))
                    {
                        convertStringCodesJavascript(RestCodes);
                        return;
                    }
                }
            }
        }

        public static void convertFunctionPython(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("getDeclaringClass")))
                {
                    YP.write(Atom.a("def getDeclaringClass():"));
                    YP.nl();
                    YP.write(Atom.a("  return None"));
                    YP.nl();
                    YP.nl();
                    return;
                }
            }
            {
                Variable x1 = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable Body = new Variable();
                Variable Level = new Variable();
                Variable HasBreakableBlock = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor("function", new object[] { x1, Name, ArgList, Body })))
                {
                    YP.write(Atom.a("def "));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a("):"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(Level, 1))
                    {
                        if (hasBreakableBlockPython(Body))
                        {
                            foreach (bool l5 in YP.unify(HasBreakableBlock, 1))
                            {
                                if (YP.termEqual(HasBreakableBlock, 1))
                                {
                                    convertIndentationPython(Level);
                                    YP.write(Atom.a("doBreak = False"));
                                    YP.nl();
                                    foreach (bool l7 in convertStatementListPython(Body, Level, HasBreakableBlock))
                                    {
                                        YP.nl();
                                        return;
                                    }
                                    goto cutIf2;
                                }
                                foreach (bool l6 in convertStatementListPython(Body, Level, HasBreakableBlock))
                                {
                                    YP.nl();
                                    return;
                                }
                            cutIf2:
                                { }
                            }
                            goto cutIf1;
                        }
                        foreach (bool l4 in YP.unify(HasBreakableBlock, 0))
                        {
                            if (YP.termEqual(HasBreakableBlock, 1))
                            {
                                convertIndentationPython(Level);
                                YP.write(Atom.a("doBreak = False"));
                                YP.nl();
                                foreach (bool l6 in convertStatementListPython(Body, Level, HasBreakableBlock))
                                {
                                    YP.nl();
                                    return;
                                }
                                goto cutIf3;
                            }
                            foreach (bool l5 in convertStatementListPython(Body, Level, HasBreakableBlock))
                            {
                                YP.nl();
                                return;
                            }
                        cutIf3:
                            { }
                        }
                    cutIf1:
                        { }
                    }
                }
            }
        }

        public static bool hasBreakableBlockPython(object arg1)
        {
            {
                Variable _Name = new Variable();
                Variable _Body = new Variable();
                Variable _RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("breakableBlock", _Name, _Body), _RestStatements)))
                {
                    return true;
                }
            }
            {
                Variable Body = new Variable();
                Variable _RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("blockScope", Body), _RestStatements)))
                {
                    if (hasBreakableBlockPython(Body))
                    {
                        return true;
                    }
                }
            }
            {
                Variable _Expression = new Variable();
                Variable Body = new Variable();
                Variable _RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("if", _Expression, Body), _RestStatements)))
                {
                    if (hasBreakableBlockPython(Body))
                    {
                        return true;
                    }
                }
            }
            {
                Variable _Expression = new Variable();
                Variable Body = new Variable();
                Variable _RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("foreach", _Expression, Body), _RestStatements)))
                {
                    if (hasBreakableBlockPython(Body))
                    {
                        return true;
                    }
                }
            }
            {
                Variable x1 = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(x1, RestStatements)))
                {
                    if (hasBreakableBlockPython(RestStatements))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static IEnumerable<bool> convertStatementListPython(object arg1, object arg2, object arg3)
        {
            {
                object x1 = arg2;
                object x2 = arg3;
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    yield return true;
                    yield break;
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Name = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("breakableBlock", Name, Body), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Name);
                    YP.write(Atom.a(" = False"));
                    YP.nl();
                    convertIndentationPython(Level);
                    YP.write(Atom.a("for _ in [1]:"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        foreach (bool l4 in convertStatementListPython(Body, NextLevel, HasBreakableBlock))
                        {
                            convertIndentationPython(Level);
                            YP.write(Atom.a("if "));
                            YP.write(Name);
                            YP.write(Atom.a(":"));
                            YP.nl();
                            convertIndentationPython(NextLevel);
                            YP.write(Atom.a("doBreak = False"));
                            YP.nl();
                            convertIndentationPython(Level);
                            YP.write(Atom.a("if doBreak:"));
                            YP.nl();
                            convertIndentationPython(NextLevel);
                            YP.write(Atom.a("break"));
                            YP.nl();
                            foreach (bool l5 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable _Type = new Variable();
                Variable Name = new Variable();
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("declare", _Type, Name, Expression), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Name);
                    YP.write(Atom.a(" = "));
                    convertExpressionPython(Expression);
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Name = new Variable();
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("assign", Name, Expression), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Name);
                    YP.write(Atom.a(" = "));
                    convertExpressionPython(Expression);
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldtrue"), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("yield True"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldfalse"), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("yield False"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("yieldbreak"), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("return"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("return"), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("return"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("returntrue"), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("return True"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Atom.a("returnfalse"), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("return False"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Name = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("breakBlock", Name), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Name);
                    YP.write(Atom.a(" = True"));
                    YP.nl();
                    convertIndentationPython(Level);
                    YP.write(Atom.a("doBreak = True"));
                    YP.nl();
                    convertIndentationPython(Level);
                    YP.write(Atom.a("break"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("call", Name, ArgList), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a(")"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Name = new Variable();
                Variable _FunctorArgs = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("functorCall", Name, _FunctorArgs, ArgList), RestStatements)))
                {
                    foreach (bool l3 in convertStatementListPython(new ListPair(new Functor2("call", Name, ArgList), RestStatements), Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Obj = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor3("callMember", new Functor1("var", Obj), Name, ArgList), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Obj);
                    YP.write(Atom.a("."));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a(")"));
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("blockScope", Body), RestStatements)))
                {
                    if (YP.termEqual(HasBreakableBlock, 1))
                    {
                        convertIndentationPython(Level);
                        YP.write(Atom.a("for _ in [1]:"));
                        YP.nl();
                        foreach (bool l4 in YP.unify(NextLevel, YP.add(Level, 1)))
                        {
                            foreach (bool l5 in convertStatementListPython(Body, NextLevel, HasBreakableBlock))
                            {
                                if (YP.termEqual(HasBreakableBlock, 1))
                                {
                                    if (YP.greaterThan(Level, 1))
                                    {
                                        convertIndentationPython(Level);
                                        YP.write(Atom.a("if doBreak:"));
                                        YP.nl();
                                        convertIndentationPython(NextLevel);
                                        YP.write(Atom.a("break"));
                                        YP.nl();
                                        foreach (bool l8 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                                        {
                                            yield return true;
                                            yield break;
                                        }
                                        goto cutIf3;
                                    }
                                    foreach (bool l7 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                cutIf3:
                                    goto cutIf2;
                                }
                                foreach (bool l6 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                                {
                                    yield return true;
                                    yield break;
                                }
                            cutIf2:
                                { }
                            }
                        }
                        goto cutIf1;
                    }
                    foreach (bool l3 in YP.unify(NextLevel, Level))
                    {
                        foreach (bool l4 in convertStatementListPython(Body, NextLevel, HasBreakableBlock))
                        {
                            if (YP.termEqual(HasBreakableBlock, 1))
                            {
                                if (YP.greaterThan(Level, 1))
                                {
                                    convertIndentationPython(Level);
                                    YP.write(Atom.a("if doBreak:"));
                                    YP.nl();
                                    convertIndentationPython(NextLevel);
                                    YP.write(Atom.a("break"));
                                    YP.nl();
                                    foreach (bool l7 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                                    {
                                        yield return true;
                                        yield break;
                                    }
                                    goto cutIf5;
                                }
                                foreach (bool l6 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                                {
                                    yield return true;
                                    yield break;
                                }
                            cutIf5:
                                goto cutIf4;
                            }
                            foreach (bool l5 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                            {
                                yield return true;
                                yield break;
                            }
                        cutIf4:
                            { }
                        }
                    }
                cutIf1:
                    { }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Expression = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("if", Expression, Body), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("if "));
                    convertExpressionPython(Expression);
                    YP.write(Atom.a(":"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        foreach (bool l4 in convertStatementListPython(Body, NextLevel, HasBreakableBlock))
                        {
                            foreach (bool l5 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                            {
                                yield return true;
                                yield break;
                            }
                        }
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Expression = new Variable();
                Variable Body = new Variable();
                Variable RestStatements = new Variable();
                Variable NextLevel = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor2("foreach", Expression, Body), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("for l"));
                    YP.write(Level);
                    YP.write(Atom.a(" in "));
                    convertExpressionPython(Expression);
                    YP.write(Atom.a(":"));
                    YP.nl();
                    foreach (bool l3 in YP.unify(NextLevel, YP.add(Level, 1)))
                    {
                        foreach (bool l4 in convertStatementListPython(Body, NextLevel, HasBreakableBlock))
                        {
                            if (YP.termEqual(HasBreakableBlock, 1))
                            {
                                convertIndentationPython(Level);
                                YP.write(Atom.a("if doBreak:"));
                                YP.nl();
                                convertIndentationPython(NextLevel);
                                YP.write(Atom.a("break"));
                                YP.nl();
                                foreach (bool l6 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                                {
                                    yield return true;
                                    yield break;
                                }
                                goto cutIf6;
                            }
                            foreach (bool l5 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                            {
                                yield return true;
                                yield break;
                            }
                        cutIf6:
                            { }
                        }
                    }
                }
            }
            {
                object Level = arg2;
                object HasBreakableBlock = arg3;
                Variable Expression = new Variable();
                Variable RestStatements = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(new Functor1("throw", Expression), RestStatements)))
                {
                    convertIndentationPython(Level);
                    YP.write(Atom.a("raise "));
                    convertExpressionPython(Expression);
                    YP.nl();
                    foreach (bool l3 in convertStatementListPython(RestStatements, Level, HasBreakableBlock))
                    {
                        yield return true;
                        yield break;
                    }
                }
            }
        }

        public static void convertIndentationPython(object Level)
        {
            {
                Variable N = new Variable();
                foreach (bool l2 in YP.unify(N, YP.multiply(Level, 2)))
                {
                    repeatWrite(Atom.a(" "), N);
                    return;
                }
            }
        }

        public static void convertArgListPython(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                Variable Head = new Variable();
                Variable Tail = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Head, Tail)))
                {
                    convertExpressionPython(Head);
                    if (YP.termNotEqual(Tail, Atom.NIL))
                    {
                        YP.write(Atom.a(", "));
                        convertArgListPython(Tail);
                        return;
                        goto cutIf1;
                    }
                    convertArgListPython(Tail);
                    return;
                cutIf1:
                    { }
                }
            }
        }

        public static void convertExpressionPython(object arg1)
        {
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("arg", X)))
                {
                    YP.write(X);
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("call", Name, ArgList)))
                {
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable _FunctorArgs = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("functorCall", Name, _FunctorArgs, ArgList)))
                {
                    convertExpressionPython(new Functor2("call", Name, ArgList));
                    return;
                }
            }
            {
                Variable Obj = new Variable();
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor3("callMember", new Functor1("var", Obj), Name, ArgList)))
                {
                    YP.write(Obj);
                    YP.write(Atom.a("."));
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("new", Name, ArgList)))
                {
                    YP.write(Name);
                    YP.write(Atom.a("("));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable Name = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("var", Name)))
                {
                    YP.write(Name);
                    return;
                }
            }
            {
                foreach (bool l2 in YP.unify(arg1, Atom.a("null")))
                {
                    YP.write(Atom.a("None"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("not", X)))
                {
                    YP.write(Atom.a("not ("));
                    convertExpressionPython(X);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                Variable Y = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor2("and", X, Y)))
                {
                    YP.write(Atom.a("("));
                    convertExpressionPython(X);
                    YP.write(Atom.a(") and ("));
                    convertExpressionPython(Y);
                    YP.write(Atom.a(")"));
                    return;
                }
            }
            {
                Variable ArgList = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("objectArray", ArgList)))
                {
                    YP.write(Atom.a("["));
                    convertArgListPython(ArgList);
                    YP.write(Atom.a("]"));
                    return;
                }
            }
            {
                Variable X = new Variable();
                Variable Codes = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("object", X)))
                {
                    if (YP.atom(X))
                    {
                        YP.write(Atom.a("\""));
                        foreach (bool l4 in YP.atom_codes(X, Codes))
                        {
                            convertStringCodesPython(Codes);
                            YP.write(Atom.a("\""));
                            return;
                        }
                    }
                }
            }
            {
                Variable X = new Variable();
                foreach (bool l2 in YP.unify(arg1, new Functor1("object", X)))
                {
                    YP.write(X);
                    return;
                }
            }
        }

        public static void convertStringCodesPython(object arg1)
        {
            {
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    return;
                }
            }
            {
                Variable Code = new Variable();
                Variable RestCodes = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(Code, RestCodes)))
                {
                    if (YP.termEqual(Code, 34))
                    {
                        YP.put_code(92);
                        YP.put_code(Code);
                        convertStringCodesPython(RestCodes);
                        return;
                        goto cutIf1;
                    }
                    if (YP.termEqual(Code, 92))
                    {
                        YP.put_code(92);
                        YP.put_code(Code);
                        convertStringCodesPython(RestCodes);
                        return;
                        goto cutIf1;
                    }
                    YP.put_code(Code);
                    convertStringCodesPython(RestCodes);
                    return;
                cutIf1:
                    { }
                }
            }
        }

        public static IEnumerable<bool> putCStringCode(object Code)
        {
            {
                Variable HexDigit = new Variable();
                Variable HexChar = new Variable();
                if (YP.lessThanOrEqual(Code, 31))
                {
                    if (YP.lessThanOrEqual(Code, 15))
                    {
                        YP.write(Atom.a("\\u000"));
                        foreach (bool l4 in YP.unify(HexDigit, Code))
                        {
                            if (YP.lessThanOrEqual(HexDigit, 9))
                            {
                                foreach (bool l6 in YP.unify(HexChar, YP.add(HexDigit, 48)))
                                {
                                    YP.put_code(HexChar);
                                    yield return true;
                                    yield break;
                                }
                                goto cutIf2;
                            }
                            foreach (bool l5 in YP.unify(HexChar, YP.add(HexDigit, 55)))
                            {
                                YP.put_code(HexChar);
                                yield return true;
                                yield break;
                            }
                        cutIf2:
                            { }
                        }
                        goto cutIf1;
                    }
                    YP.write(Atom.a("\\u001"));
                    foreach (bool l3 in YP.unify(HexDigit, YP.subtract(Code, 16)))
                    {
                        if (YP.lessThanOrEqual(HexDigit, 9))
                        {
                            foreach (bool l5 in YP.unify(HexChar, YP.add(HexDigit, 48)))
                            {
                                YP.put_code(HexChar);
                                yield return true;
                                yield break;
                            }
                            goto cutIf3;
                        }
                        foreach (bool l4 in YP.unify(HexChar, YP.add(HexDigit, 55)))
                        {
                            YP.put_code(HexChar);
                            yield return true;
                            yield break;
                        }
                    cutIf3:
                        { }
                    }
                cutIf1:
                    { }
                }
            }
            {
                if (YP.termEqual(Code, 34))
                {
                    YP.put_code(92);
                    YP.put_code(34);
                    yield return true;
                    yield break;
                }
            }
            {
                if (YP.termEqual(Code, 92))
                {
                    YP.put_code(92);
                    YP.put_code(92);
                    yield return true;
                    yield break;
                }
            }
            {
                YP.put_code(Code);
                yield return true;
                yield break;
            }
        }

        public static IEnumerable<bool> member(object X, object arg2)
        {
            {
                Variable x2 = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(X, x2)))
                {
                    yield return false;
                }
            }
            {
                Variable x2 = new Variable();
                Variable Rest = new Variable();
                foreach (bool l2 in YP.unify(arg2, new ListPair(x2, Rest)))
                {
                    foreach (bool l3 in member(X, Rest))
                    {
                        yield return false;
                    }
                }
            }
        }

        public static IEnumerable<bool> append(object arg1, object arg2, object arg3)
        {
            {
                Variable List = new Variable();
                foreach (bool l2 in YP.unify(arg1, Atom.NIL))
                {
                    foreach (bool l3 in YP.unify(arg2, List))
                    {
                        foreach (bool l4 in YP.unify(arg3, List))
                        {
                            yield return false;
                        }
                    }
                }
            }
            {
                object List2 = arg2;
                Variable X = new Variable();
                Variable List1 = new Variable();
                Variable List12 = new Variable();
                foreach (bool l2 in YP.unify(arg1, new ListPair(X, List1)))
                {
                    foreach (bool l3 in YP.unify(arg3, new ListPair(X, List12)))
                    {
                        foreach (bool l4 in append(List1, List2, List12))
                        {
                            yield return false;
                        }
                    }
                }
            }
        }
        #pragma warning restore 0168, 0219, 0164,0162
    }
}
