using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LibLSLCC.CodeValidator.Components;
using LibLSLCC.CodeValidator.Components.Interfaces;
using LibLSLCC.CodeValidator.ValidatorNodes.Interfaces;
using LibLSLCC.Compilers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;

namespace OpenSim.Region.ScriptEngine.Shared.LibLSLCCCompiler
{
    // ReSharper disable once InconsistentNaming
    public class LibLSLCCCodeGenerator : ICodeConverter
    {
        private readonly ILSLMainLibraryDataProvider _libraryData;

        public LibLSLCCCodeGenerator()
        {

        }

        public LibLSLCCCodeGenerator(ILSLMainLibraryDataProvider libraryData, IScriptModuleComms comms, bool mInsertCoopTerminationCalls)
        {
            _libraryData = libraryData;
        }


        public Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> PositionMap
        {
            get { return new Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>(); }
        }

        private class ErrorListener : LSLDefaultSyntaxErrorListener
        {
            public override void OnError(LibLSLCC.CodeValidator.Primitives.LSLSourceCodeRange location, string message)
            {
                int line = location.LineStart == 0 ? 0 : location.LineStart - 1;
                throw new Exception(string.Format("({0},{1}): ERROR, {2}", line, location.ColumnStart, message));
            }
        }

        private class WarningListener : LSLDefaultSyntaxWarningListener
        {
            public readonly List<string> Warnings = new List<string>();
            public override void OnWarning(LibLSLCC.CodeValidator.Primitives.LSLSourceCodeRange location, string message)
            {
                int line = location.LineStart == 0 ? 0 : location.LineStart - 1;

                Warnings.Add(string.Format("({0},{1}): WARNING, {2}", line, location.ColumnStart, message));
            }
        }

        private static readonly ILSLExpressionValidator ExpressionValidator = new LSLDefaultExpressionValidator();



        public string Convert(string script)
        {
            var validatorServices = new LSLCustomValidatorServiceProvider();


            var errorListener = new ErrorListener();
            var warningListener = new WarningListener();

            validatorServices.ExpressionValidator = ExpressionValidator;
            validatorServices.MainLibraryDataProvider = _libraryData;
            validatorServices.StringLiteralPreProcessor = new LSLDefaultStringPreProcessor();
            validatorServices.SyntaxErrorListener = errorListener;
            validatorServices.SyntaxWarningListener = warningListener;


            var validator = new LibLSLCC.CodeValidator.LSLCodeValidator(validatorServices);

            ILSLCompilationUnitNode syntaxTree;

            using (var m = new MemoryStream(Encoding.UTF8.GetBytes(script)))
            {

                syntaxTree = validator.Validate(new StreamReader(m, Encoding.UTF8));
            }

            _warnings = warningListener.Warnings;

            var outStream = new MemoryStream();

            if (!validator.HasSyntaxErrors)
            {
                var compiler = new LSLOpenSimCSCompiler(LSLOpenSimCSCompilerSettings.OpenSimClientUploadable(_libraryData));

                compiler.Compile(syntaxTree, new StreamWriter(outStream, Encoding.UTF8));
            }

            return Encoding.UTF8.GetString(outStream.ToArray());
        }



        private List<string> _warnings;

        public string[] GetWarnings()
        {
            return _warnings.ToArray();
        }
    }
}