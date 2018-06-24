// COPYRIGHT 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.
#if DEBUG
// prints details of the file as read from input
#define DEBUG_PRINT_IN

// prints details of the file as processed
// #define DEBUG_PRINT_OUT
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Orts.Formats.Msts
{
    #region SCRReadinfo
    public class SCRReadInfo
    {
        public string ReadLine { get; internal set; }
        public int LineNumber { get; internal set; }

        public SCRReadInfo(string line, int lineNumber)
        {
            ReadLine = line;
            LineNumber = lineNumber;
        }
    }
    #endregion

    static class StringExtensions
    {
        public static StringBuilder Trim(this StringBuilder sb)
        {
            if (sb == null || sb.Length == 0) return sb;

            int i = sb.Length - 1;
            for (; i >= 0; i--)
                if (!char.IsWhiteSpace(sb[i]))
                    break;

            if (i < sb.Length - 1)
                sb.Length = i + 1;

            return sb;
        }
    }

    #region Script Tokenizer and Parser
    internal enum SignalScriptTokenType
    {
        Value = 0x00,
        Tab = 0x09,             // \t
        LineEnd = 0x0a,         // \n
        Separator = 0x20,       // blank
        BracketOpen = 0x28,     // (
        BracketClose = 0x29,    // )
        StatementEnd = 0x3b,    // ;
        BlockOpen = 0x7b,       // {
        BlockClose = 0x7d,      // }
    }

    internal struct SignalScriptToken
    {
        public SignalScriptToken(SignalScriptTokenType type, string value)
        {
            Value = value;
            Type = type;
        }
        public SignalScriptToken(SignalScriptTokenType type, char value)
        {
            Value = value.ToString();
            Type = type;
        }

        public string Value { get; private set; }
        public SignalScriptTokenType Type { get; private set; }
    }

    internal enum CommentParserState
    {
        None,
        StartComment,
        OpenComment,
        EndComment,
    }

    internal class SignalScriptTokenizer : IEnumerable<SignalScriptToken>
    {
        private TextReader reader;
        private int lineNumber;

        internal int LineNumber { get { return lineNumber; } }

        public SignalScriptTokenizer(TextReader reader) : this(reader, 0)
        {
        }

        public SignalScriptTokenizer(TextReader reader, int lineNumberOffset)
        {
            this.reader = reader;
            this.lineNumber = lineNumberOffset;
        }

        public IEnumerator<SignalScriptToken> GetEnumerator()
        {
            string line;
            CommentParserState state = CommentParserState.None;
            StringBuilder value = new StringBuilder();
            bool lineContent = false;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                lineContent = false;

                foreach (char c in line)
                {
                    switch (c)
                    {
                        case '/':
                            switch (state)
                            {
                                case CommentParserState.None:
                                    value.Append(c);
                                    state = CommentParserState.StartComment;
                                    continue;
                                case CommentParserState.StartComment:
                                    state = CommentParserState.None;
                                    value.Length = value.Length - 1;
                                    goto SkipLineComment;
                                case CommentParserState.EndComment:
                                    state = CommentParserState.None;
                                    continue;
                                case CommentParserState.OpenComment:
                                    continue;
                                default:
                                    value.Append(c);
                                    continue;
                            }
                        case '*':
                            switch (state)
                            {
                                case CommentParserState.StartComment:
                                    value.Length = value.Length - 1;
                                    state = CommentParserState.OpenComment;
                                    continue;
                                case CommentParserState.OpenComment:
                                    state = CommentParserState.EndComment;
                                    continue;
                                default:
                                    value.Append(c);
                                    continue;
                            }
                        case ';':
                        case '{':
                        case '}':
                        case '(':
                        case ')':
                        case '\t':
                        case ' ':
                            switch (state)
                            {
                                case CommentParserState.OpenComment:
                                    continue;
                                default:
                                    if (value.Length > 0)
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Value, value.ToString());
                                        value.Length = 0;
                                    }
                                    lineContent = true; ;
                                    yield return new SignalScriptToken((SignalScriptTokenType)c, c);
                                    continue;
                            }
                        default:
                            switch (state)
                            {
                                case CommentParserState.OpenComment:
                                    continue;
                                default:
                                    value.Append(char.ToUpper(c));
                                    continue;
                            }
                    }
                }
                SkipLineComment:
                if (state != CommentParserState.OpenComment)
                {
                    if (value.Length > 0)
                {
                        lineContent = true; ;
                        yield return new SignalScriptToken(SignalScriptTokenType.Value, value.ToString());
                        value.Length = 0;
                    }
                    if (lineContent)
                        yield return new SignalScriptToken(SignalScriptTokenType.LineEnd, '\n');
                    state = CommentParserState.None;
                }
            }
            if (value.Length > 0)
            {
                yield return new SignalScriptToken(SignalScriptTokenType.Value, value.ToString());
                value.Length = 0;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal enum ParserTokenType
    {
        Value = 0x00,
        LineEnd = 0x0a,         // \n
        Separator = 0x20,       // blank
        BracketOpen = 0x28,     // (
        BracketClose = 0x29,    // )
        StatementEnd = 0x3b,    // ;
        BlockOpen = 0x7b,       // {
        BlockClose = 0x7d,      // }
    }

    internal struct ParserToken
    {
        public ParserToken(ParserTokenType type, string value)
        {
            Value = value;
            Type = type;
        }
        public ParserToken(ParserTokenType type, char value)
        {
            Value = value.ToString();
            Type = type;
        }

        public string Value { get; private set; }
        public ParserTokenType Type { get; private set; }
    }

        internal class SignalScriptParser : IEnumerable<string>
    {
        private SignalScriptTokenizer tokenizer;

        public int LineNumber { get { return this.tokenizer.LineNumber; } }

        public SignalScriptParser(TextReader reader)
        {
            this.tokenizer = new SignalScriptTokenizer(reader);
        }

        public IEnumerator<string> GetEnumerator()
        {
            StringBuilder result = new StringBuilder();
            Stack<SignalScriptToken> scriptTokens = new Stack<SignalScriptToken>();

            foreach (SignalScriptToken token in tokenizer)
            {
                switch (token.Type)
                {
                    case SignalScriptTokenType.StatementEnd:
                                if (result.Trim().Length > 0)
                                {
                            result.Append(token.Value);
                            yield return result.ToString();// new ParserToken(ParserTokenType.Value, result.ToString());
                                    result.Length = 0;
                                }
//                                yield return new ParserToken(ParserTokenType.StatementEnd, token.Value);
                                break;
                    case SignalScriptTokenType.LineEnd:
                                if (result.Trim().Length > 0)
                                {
                            yield return result.ToString();// new ParserToken(ParserTokenType.Value, result.ToString());
                                    result.Length = 0;
                                }
//                                yield return new ParserToken(ParserTokenType.LineEnd, result.ToString());
                        break;
                    case SignalScriptTokenType.BlockOpen:
                                if (result.Trim().Length > 0)
                                {
                            yield return result.ToString(); // new ParserToken(ParserTokenType.Value, result.ToString());
                                    result.Length = 0;
                                }
                        yield return token.Value; // new ParserToken(ParserTokenType.BlockOpen, token.Value);
                                break;
                    case SignalScriptTokenType.BlockClose:
                                if (result.Trim().Length > 0)
                                {
                            yield return result.ToString();//new ParserToken(ParserTokenType.Value, result.ToString());
                                    result.Length = 0;
                                }
                        yield return token.Value; // new ParserToken(ParserTokenType.BlockClose, token.Value);
                                break;
                    case SignalScriptTokenType.Tab:
                    case SignalScriptTokenType.Separator:
                        if (result.Length > 0)
                            result.Append(' ');
                        break;
                    case SignalScriptTokenType.BracketOpen:
                    case SignalScriptTokenType.BracketClose:
                    case SignalScriptTokenType.Value:
                                result.Append(token.Value);
                                break;
                    default:
                        throw new InvalidOperationException("Unknown token type: " + token.Type);
                }
            }
            if (result.Trim().Length > 0)
            {
                yield return result.ToString();// new ParserToken(ParserTokenType.Value, result.ToString());
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    #endregion

    public class SignalScripts
    {
        #region SCRExternalFunctions
        public enum SCRExternalFunctions
        {
            NONE,
            BLOCK_STATE,
            ROUTE_SET,
            NEXT_SIG_LR,
            NEXT_SIG_MR,
            THIS_SIG_LR,
            THIS_SIG_MR,
            OPP_SIG_LR,
            OPP_SIG_MR,
            NEXT_NSIG_LR,
            DIST_MULTI_SIG_MR,
            NEXT_SIG_ID,
            NEXT_NSIG_ID,
            OPP_SIG_ID,
            ID_SIG_ENABLED,
            ID_SIG_LR,
            SIG_FEATURE,
            DEF_DRAW_STATE,
            ALLOW_CLEAR_TO_PARTIAL_ROUTE,
            APPROACH_CONTROL_POSITION,
            APPROACH_CONTROL_POSITION_FORCED,
            APPROACH_CONTROL_SPEED,
            APPROACH_CONTROL_LOCK_CLAIM,
            APPROACH_CONTROL_NEXT_STOP,
            ACTIVATE_TIMING_TRIGGER,
            CHECK_TIMING_TRIGGER,
            TRAINHASCALLON,
            TRAINHASCALLON_RESTRICTED,
            TRAIN_REQUIRES_NEXT_SIGNAL,
            FIND_REQ_NORMAL_SIGNAL,
            ROUTE_CLEARED_TO_SIGNAL,
            ROUTE_CLEARED_TO_SIGNAL_CALLON,
            HASHEAD,
            INCREASE_SIGNALNUMCLEARAHEAD,
            DECREASE_SIGNALNUMCLEARAHEAD,
            SET_SIGNALNUMCLEARAHEAD,
            RESET_SIGNALNUMCLEARAHEAD,
            STORE_LVAR,
            THIS_SIG_LVAR,
            NEXT_SIG_LVAR,
            ID_SIG_LVAR,
            THIS_SIG_NOUPDATE,
            THIS_SIG_HASNORMALSUBTYPE,
            NEXT_SIG_HASNORMALSUBTYPE,
            ID_SIG_HASNORMALSUBTYPE,
            DEBUG_HEADER,
            DEBUG_OUT,
            RETURN,
        }
        #endregion

        #region SCRExternalFloats
        public enum SCRExternalFloats
        {
            STATE,
            DRAW_STATE,
            ENABLED,                         // read only
            BLOCK_STATE,                     // read only
            APPROACH_CONTROL_REQ_POSITION,   // read only
            APPROACH_CONTROL_REQ_SPEED,      // read only
        }
        #endregion

        #region SCRTermCondition
        public enum SCRTermCondition
        {
            GT,
            GE,
            LT,
            LE,
            EQ,
            NE,
            NONE,
        }
        #endregion

        #region SCRAndOr
        public enum SCRAndOr
        {
            AND,
            OR,
            NONE,
        }
        #endregion

        #region SCRNegate
        public enum SCRNegate
        {
            NEGATE,
        }
        #endregion

        #region SCRTermOperator
        public enum SCRTermOperator
        {
            NONE,        // used for first term
            MINUS,       // needs to come first to avoid it being interpreted as range separator
            MULTIPLY,
            PLUS,
            DIVIDE,
            MODULO,
        }
        #endregion

        #region SCRTermType
        public enum SCRTermType
        {
            ExternalFloat,
            LocalFloat,
            Sigasp,
            Sigfn,
            ORNormalSubtype,
            Sigfeat,
            Block,
            Constant,
            Invalid,
        }
        #endregion

        private static IDictionary<string, SCRTermCondition> TranslateConditions = new Dictionary<string, SCRTermCondition>
            {
                { ">", SCRTermCondition.GT },
                { ">=", SCRTermCondition.GE },
                { "<", SCRTermCondition.LT },
                { "<=", SCRTermCondition.LE },
                { "==", SCRTermCondition.EQ },
                { "!=", SCRTermCondition.NE },
                { "::", SCRTermCondition.NE }  // dummy (for no separator)
            };

        private static IDictionary<string, SCRTermOperator> TranslateOperator = new Dictionary<string, SCRTermOperator>
            {
                { "?", SCRTermOperator.NONE },
                { "-", SCRTermOperator.MINUS },  // needs to come first to avoid it being interpreted as range separator
                { "*", SCRTermOperator.MULTIPLY },
                { "+", SCRTermOperator.PLUS },
                { "/", SCRTermOperator.DIVIDE },
                { "%", SCRTermOperator.MODULO }
            };

        private static IDictionary<string, SCRAndOr> TranslateAndOr = new Dictionary<string, SCRAndOr>
            {
                { "&&", SCRAndOr.AND },
                { "||", SCRAndOr.OR },
                { "AND", SCRAndOr.AND },
                { "OR", SCRAndOr.OR },
                { "??", SCRAndOr.NONE }
            };

#if DEBUG_PRINT_IN
        public static string din_fileLoc = @"C:\temp\";     /* file path for debug files */
#endif

#if DEBUG_PRINT_OUT
        public static string dout_fileLoc = @"C:\temp\";    /* file path for debug files */
#endif

        public IDictionary<SignalType, SCRScripts> Scripts { get; private set; }

        //================================================================================================//
        //
        // Constructor
        //
        //================================================================================================//
        public SignalScripts(string routePath, IList<string> scriptFiles, IDictionary<string, SignalType> signalTypes, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
        {
            Scripts = new Dictionary<SignalType, SCRScripts>();

#if DEBUG_PRINT_PROCESS
            TDB_debug_ref = new int[5] { 7305, 7307, 7308, 7309, 7310 };   /* signal tdb ref.no selected for print-out */
#endif
#if DEBUG_PRINT_IN
            File.Delete(din_fileLoc + @"sigscr.txt");
#endif        
#if DEBUG_PRINT_OUT            
            File.Delete(dout_fileLoc + @"scriptproc.txt");
#endif
#if DEBUG_PRINT_ENABLED
            File.Delete(dpe_fileLoc + @"printproc.txt");
#endif
#if DEBUG_PRINT_PROCESS
            File.Delete(dpr_fileLoc + @"printproc.txt");
#endif

            // Process all files listed in SIGCFG
            foreach (string fileName in scriptFiles)
            {
                string fullName = Path.Combine(routePath, fileName);
                try
                {
                    using (StreamReader stream = new StreamReader(fullName, true))
                    {
#if DEBUG_PRINT_IN
                        File.AppendAllText(din_fileLoc + @"sigscr.txt", "Reading file : " + fullName + "\n\n");
#endif
                        ParseSignalScript(stream, fullName, signalTypes, orSignalTypes, orNormalSubtypes);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Error reading signal script - {fullName} : {ex.ToString()}");
                }
            }
        }// Constructor

        //================================================================================================//
        //
        // overall script file routines
        //
        //================================================================================================//

#if DEBUG_PRINT_OUT
        //================================================================================================//
        //
        // print processed script - for DEBUG purposes only
        //
        //================================================================================================//

        public void printscript(ArrayList Statements)
        {
            bool function = false;
            List<int> Sublevels = new List<int>();

            foreach (object scriptstat in Statements)
            {

                // process statement lines

                if (scriptstat is SCRScripts.SCRStatement)
                {
                    SCRScripts.SCRStatement ThisStat = (SCRScripts.SCRStatement)scriptstat;
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Statement : \n");
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt",
                                    ThisStat.AssignType.ToString() + "[" + ThisStat.AssignParameter.ToString() + "] = ");

                    foreach (SCRScripts.SCRStatTerm ThisTerm in ThisStat.StatementTerms)
                    {
                        if (ThisTerm.issublevel > 0)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt",
                                            " <SUB" + ThisTerm.issublevel.ToString() + "> ");
                        }
                        function = false;
                        if (ThisTerm.Function != SCRExternalFunctions.NONE)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt",
                                    ThisTerm.Function.ToString() + "(");
                            function = true;
                        }

                        if (ThisTerm.PartParameter != null)
                        {
                            foreach (SCRScripts.SCRParameterType ThisParam in ThisTerm.PartParameter)
                            {
                                File.AppendAllText(dout_fileLoc + @"scriptproc.txt",
                                        ThisParam.PartType + "[" + ThisParam.PartParameter + "] ,");
                            }
                        }

                        if (ThisTerm.sublevel != 0)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", " SUBTERM_" + ThisTerm.sublevel.ToString());
                        }

                        if (function)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", ")");
                        }
                        File.AppendAllText(dout_fileLoc + @"scriptproc.txt", " -" + ThisTerm.TermOperator.ToString() + "- \n");
                    }

                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n\n");
                }

                // process conditions line

                if (scriptstat is SCRScripts.SCRConditionBlock)
                {
                    SCRScripts.SCRConditionBlock CondBlock = (SCRScripts.SCRConditionBlock)scriptstat;
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nCondition : \n");

                    printConditionArray(CondBlock.Conditions);

                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nIF Block : \n");
                    printscript(CondBlock.IfBlock.Statements);

                    if (CondBlock.ElseIfBlock != null)
                    {
                        foreach (SCRScripts.SCRBlock TempBlock in CondBlock.ElseIfBlock)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nStatements in ELSEIF : " +
                                    TempBlock.Statements.Count + "\n");
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Elseif Block : \n");
                            printscript(TempBlock.Statements);
                        }
                    }

                    if (CondBlock.ElseBlock != null)
                    {
                        File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nElse Block : \n");
                        printscript(CondBlock.ElseBlock.Statements);
                    }

                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nEnd IF Block : \n");

                }
            }
        }// printscript

        //================================================================================================//
        //
        // print condition info - for DEBUG purposes only
        //
        //================================================================================================//

        public void printConditionArray(ArrayList Conditions)
        {
            foreach (object ThisCond in Conditions)
            {
                if (ThisCond is SCRScripts.SCRConditions)
                {
                    printcondition((SCRScripts.SCRConditions)ThisCond);
                }
                else if (ThisCond is SCRAndOr)
                {
                    SCRAndOr condstring = (SCRAndOr)ThisCond;
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", condstring.ToString() + "\n");
                }
                else if (ThisCond is SCRNegate)
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "NEGATED : \n");
                }
                else
                {
                    printConditionArray((ArrayList)ThisCond);
                }
            }
        }// printConditionArray

        //================================================================================================//
        //
        // print condition statement - for DEBUG purposes only
        //
        //================================================================================================//

        public void printcondition(SCRScripts.SCRConditions ThisCond)
        {

            bool function = false;
            if (ThisCond.negate1)
            {
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "NOT : ");
            }
            if (ThisCond.Term1.Function != SCRExternalFunctions.NONE)
            {
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", ThisCond.Term1.Function.ToString() + "(");
                function = true;
            }

            if (ThisCond.Term1.PartParameter != null)
            {
                foreach (SCRScripts.SCRParameterType ThisParam in ThisCond.Term1.PartParameter)
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", ThisParam.PartType + "[" + ThisParam.PartParameter + "] ,");
                }
            }
            else
            {
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", " 0 , ");
            }

            if (function)
            {
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", ")");
            }

            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", " -- " + ThisCond.Condition.ToString() + " --\n");

            if (ThisCond.Term2 != null)
            {
                function = false;
                if (ThisCond.negate2)
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "NOT : ");
                }
                if (ThisCond.Term2.Function != SCRExternalFunctions.NONE)
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", ThisCond.Term2.Function.ToString() + "(");
                    function = true;
                }

                if (ThisCond.Term2.PartParameter != null)
                {
                    foreach (SCRScripts.SCRParameterType ThisParam in ThisCond.Term2.PartParameter)
                    {
                        File.AppendAllText(dout_fileLoc + @"scriptproc.txt",
                                        ThisParam.PartType + "[" + ThisParam.PartParameter + "] ,");
                    }
                }
                else
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", " 0 , ");
                }

                if (function)
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", ")");
                }
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n");
            }
        }// printcondition
#endif

        //================================================================================================//
        //
        // allocate script to required signal type
        //
        //================================================================================================//

        private void AssignScriptToSignalType(SCRScripts script, IDictionary<string, SignalType> signalTypes, string scriptName, int currentLine, string fileName)
        {
            bool isValid = false;

            // try and find signal type with same name as script
            if (signalTypes.TryGetValue(scriptName.ToLower(), out SignalType signalType))
            {
                if (Scripts.ContainsKey(signalType))
                {
                    Trace.TraceWarning($"Ignored duplicate SignalType script {scriptName} in {0} {fileName} before {currentLine}");
                }
                else
                {
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "Adding script : " + signalType.Name + "\n");
#endif
                    Scripts.Add(signalType, script);
                    isValid = true;
                }
            }

            // try and find any other signal types which reference this script
            foreach (KeyValuePair<string, SignalType> currentSignal in signalTypes)
            {

                if (scriptName.Equals(currentSignal.Value.Script, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (Scripts.ContainsKey(currentSignal.Value))
                    {
                        Trace.TraceWarning($"Ignored duplicate SignalType script {scriptName} in {fileName} before {currentLine}");
                    }
                    else
                    {
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "Adding script : " + currentSignal.Value.Script + " to " + currentSignal.Value.Name + "\n");
#endif
                        Scripts.Add(currentSignal.Value, script);
                        isValid = true;
                    }
                }
            }
            #region DEBUG
#if DEBUG_PRINT_OUT
                        if (!isValid)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", $"\nUnknown signal type : {scriptName}\n\n");
                        }
#endif
#if DEBUG_PRINT_IN
                        if (!isValid)
                        {
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", $"\nUnknown signal type : {scriptName}\n\n");
                        }
#endif
            #endregion

        }

        private void ParseSignalScript(StreamReader reader, string fileName, IDictionary<string, SignalType> signalTypes, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
        {
            List<SCRReadInfo> scriptLines = new List<SCRReadInfo>();
            string scriptName = string.Empty;
            bool inScript = false;
            SignalScriptParser parser = new SignalScriptParser(reader);
            StringBuilder builder = new StringBuilder();

            foreach (string line in parser)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.StartsWith("SCRIPT "))
                {
                    if (scriptLines.Count > 0)
                    {
                        #region DEBUG
#if DEBUG_PRINT_IN
                        File.AppendAllText(din_fileLoc + @"sigscr.txt", "\n===============================\n");
                        File.AppendAllText(din_fileLoc + @"sigscr.txt", "\nNew Script : " + scriptName + "\n");
#endif
#if DEBUG_PRINT_OUT
                        File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n===============================\n");
                        File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nNew Script : " + scriptName + "\n");
#endif
                        #endregion
                        AssignScriptToSignalType(new SCRScripts(scriptLines, scriptName, orSignalTypes, orNormalSubtypes)
                        , signalTypes, scriptName, parser.LineNumber, fileName);
                    }
                    //finish existing script, drop following lines until new script
                    scriptName = line.Substring(7);
                    inScript = true;
                }
                else if (line.StartsWith("REM SCRIPT "))
                {
                    //finish existing script, drop following lines until new script
                    AssignScriptToSignalType(new SCRScripts(scriptLines, scriptName, orSignalTypes, orNormalSubtypes)
                        , signalTypes, scriptName, parser.LineNumber, fileName);
                    inScript = false;
                    scriptName = string.Empty;
                }
                else
                {
                    if (inScript)
                    {
                        //add line to current script
                        scriptLines.Add(new SCRReadInfo(line, parser.LineNumber));
                    }
                }
                if (parser.LineNumber % 100 == 0)
                {
                    Trace.Write("s");
                }
            }

            if (scriptLines.Count > 0)
            {
#if DEBUG_PRINT_IN
                File.AppendAllText(din_fileLoc + @"sigscr.txt", "\n===============================\n");
                File.AppendAllText(din_fileLoc + @"sigscr.txt", "\nNew Script : " + scriptName + "\n");
#endif
                AssignScriptToSignalType(new SCRScripts(scriptLines, scriptName, orSignalTypes, orNormalSubtypes)
                    , signalTypes, scriptName, parser.LineNumber, fileName);
            }

#if DEBUG_PRINT_OUT
            // print processed details 
            foreach (KeyValuePair<SignalType, SCRScripts> item in Scripts)
            {
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Script : " + item.Value.ScriptName + "\n\n");
                printscript(item.Value.Statements);
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n=====================\n");
            }
#endif
        }

        public class SCRScripts
        {

            private IDictionary<string, uint> localFloats;
            public uint totalLocalFloats { get; private set; }
            public ArrayList Statements { get; private set; }
            public string ScriptName { get; private set; }

            //================================================================================================//
            //
            // Constructor
            // Input is list with all lines for one signal script
            //
            public SCRScripts(List<SCRReadInfo> scriptLines, string scriptName, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
            {
                localFloats = new Dictionary<string, uint>();
                totalLocalFloats = 0;
                Statements = new ArrayList();
                ScriptName = scriptName;

                int line = 0;
                int maxcount = scriptLines.Count;

#if DEBUG_PRINT_IN
                // print inputlines

                foreach (SCRReadInfo InfoLine in scriptLines)
                {
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", InfoLine.ReadLine + "\n");
                }
                File.AppendAllText(din_fileLoc + @"sigscr.txt", "\n+++++++++++++++++++++++++++++++++++\n\n");

#endif

                // Skip external floats (exist automatically)
                while (scriptLines[line].ReadLine.StartsWith("EXTERN FLOAT ") && line++ < maxcount) ;

                //// Process floats : build list with internal floats
                string floatString;
                while ((floatString = scriptLines[line].ReadLine).StartsWith("FLOAT ") && line++ < maxcount)
                {
                    floatString = floatString.Substring(6, floatString.Length - 7);
                    if (!localFloats.ContainsKey(floatString))
                    {
                        localFloats.Add(floatString, totalLocalFloats++);
                    }
                }

#if DEBUG_PRINT_OUT
                // print details of internal floats

                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n\nFloats : \n");
                foreach (KeyValuePair<string, uint> deffloat in localFloats)
                {
                    string defstring = deffloat.Key;
                    uint defindex = deffloat.Value;

                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Float : " + defstring + " = " + defindex.ToString() + "\n");
                }
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Total : " + totalLocalFloats.ToString() + "\n\n\n");
#endif
                scriptLines.RemoveRange(0, line);
                Statements = ParseStatements(scriptLines, localFloats, orSignalTypes, orNormalSubtypes);
                scriptLines.Clear();
            }// constructor

            private static ArrayList ParseStatements(List<SCRReadInfo> scriptLines, IDictionary<string, uint> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
            {
                ArrayList result = new ArrayList();
                List<int> ifblockcount;
                int index = 0;

                while (index<scriptLines.Count)
                {
                    SCRReadInfo lineItem = scriptLines[index];
                    if (lineItem.ReadLine.Length == 1 && (lineItem.ReadLine == "{" || lineItem.ReadLine == "}"))
                    {
                        index++;
                        continue;
                    }
                    else if (lineItem.ReadLine.StartsWith("IF") && ((lineItem.ReadLine[2] == ' ') || (lineItem.ReadLine[2]) == '('))    //matches "IF " as well "IF("
                    {
                        ifblockcount = findEndIfBlock(scriptLines, index);
                        SCRConditionBlock thisCondition = new SCRConditionBlock(scriptLines, index, ifblockcount, localFloats, orSignalTypes, orNormalSubtypes);
                        index = ifblockcount[ifblockcount.Count - 1];
                        result.Add(thisCondition);
                    }
                    // process statement
                    else
                    {
                        Debug.Assert(!string.IsNullOrWhiteSpace(lineItem.ReadLine));
                        SCRStatement statement = new SCRStatement(lineItem, localFloats, orSignalTypes, orNormalSubtypes);
                        if (statement.IsValid)
                            result.Add(statement);
                        index++;
                    }
                }
                return result;
            }
            //================================================================================================//
            //
            // parsing routines
            //
            //================================================================================================//

            //================================================================================================//
            //
            // Find end of IF condition statement
            // returns index to next line
            //
            //================================================================================================//

            public static int FindEndStatement(List<SCRReadInfo> FESScriptLines, int index)
            {
                string presentstring, addline;
                int endpos;
                int actindex;

                //================================================================================================//

                SCRReadInfo presentInfo = FESScriptLines[index];
                presentstring = presentInfo.ReadLine.Trim();
                FESScriptLines.RemoveAt(index);
                endpos = presentstring.IndexOf(";");
                actindex = index;

                // empty string - exit and set index

                if (presentstring.Length < 1)
                {
                    return actindex;
                }

                // search for ; - keep reading until found

                while (endpos <= 0 && actindex < FESScriptLines.Count)
                {
                    addline = FESScriptLines[actindex].ReadLine;
                    FESScriptLines.RemoveAt(actindex);
                    presentstring = String.Concat(presentstring, addline);
                    endpos = presentstring.IndexOf(";");
                }

                // Illegal statement - no ;

                if (endpos <= 0)
                {
                    Trace.TraceWarning("sigscr-file line {1} : Missing ; in statement starting with {0}", presentstring, presentInfo.LineNumber.ToString());
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "Missing ; in statement starting with " + presentstring + " (" +
                                    presentInfo.LineNumber.ToString() + ")\n");
#endif

                }

                // split string at ; if anything follows after

                if (presentstring.Length > (endpos + 1) && endpos > 0)
                {
                    SCRReadInfo splitInfo = new SCRReadInfo(presentstring.Substring(endpos + 1).Trim(), presentInfo.LineNumber);
                    FESScriptLines.Insert(index, splitInfo);
                    presentstring = presentstring.Substring(0, endpos + 1).Trim();
                }

                SCRReadInfo newInfo = new SCRReadInfo(presentstring.Trim(), presentInfo.LineNumber);
                FESScriptLines.Insert(index, newInfo);
                actindex = index + 1;
                return actindex;
            }// FindEndStatement

            //================================================================================================//
            //
            // find end of full IF blocks
            // returns indices to lines following IF part, all (if any) ELSEIF part and (if available) last ELSE part
            // final index is next line after full IF - ELSEIF - ELSE sequence
            // any nested IF blocks are included but not indexed
            //
            // this function is call recursively for nester IF blocks
            //
            //================================================================================================//

            public static List<int> findEndIfBlock(List<SCRReadInfo> FEIScriptLines, int index)
            {

                List<int> nextcount = new List<int>();
                SCRReadInfo nextinfo;
                SCRReadInfo tempinfo;
                string nextline;
                int tempnumber;
                int endIfcount, endElsecount;

                int linecount = FindEndCondition(FEIScriptLines, index);

                nextinfo = FEIScriptLines[linecount];
                nextline = nextinfo.ReadLine;

                // full block : search for matching parenthesis in next lines
                // set end after related closing }

                endIfcount = linecount;

                if (nextline.Length > 0 && String.Compare(nextline.Substring(0, 1), "{") == 0)
                {
                    endIfcount = findEndBlock(FEIScriptLines, linecount);
                }

                // next statement is another if : insert { and } to ease processing

                else if (String.Compare(nextline.Substring(0, Math.Min(3, nextline.Length)), "IF ") == 0)
                {
                    List<int> fullcount = findEndIfBlock(FEIScriptLines, linecount);
                    int lastline = fullcount[fullcount.Count - 1];
                    string templine = FEIScriptLines[linecount].ReadLine;
                    FEIScriptLines.RemoveAt(linecount);
                    templine = String.Concat("{ ", templine);
                    tempinfo = new SCRReadInfo(templine, nextinfo.LineNumber);
                    FEIScriptLines.Insert(linecount, tempinfo);
                    templine = FEIScriptLines[lastline - 1].ReadLine;
                    tempnumber = FEIScriptLines[lastline - 1].LineNumber;
                    FEIScriptLines.RemoveAt(lastline - 1);
                    templine = String.Concat(templine, " }");
                    tempinfo = new SCRReadInfo(templine, tempnumber);
                    FEIScriptLines.Insert(lastline - 1, tempinfo);
                    endIfcount = lastline;
                }

                // single statement - set end after statement

                else
                {
                    endIfcount = FindEndStatement(FEIScriptLines, linecount);
                }
                nextcount.Add(endIfcount);

                endElsecount = endIfcount;

                // check if next line starts with ELSE or any form of ELSEIF

                nextline = endElsecount < FEIScriptLines.Count ? FEIScriptLines[endElsecount].ReadLine.Trim() : String.Empty;
                bool endelse = false;

                while (!endelse && endElsecount < FEIScriptLines.Count)
                {
                    bool elsepart = false;

                    // line contains ELSE only

                    if (nextline.Length <= 4)
                    {
                        if (String.Compare(nextline, "ELSE") == 0)
                        {
                            elsepart = true;
                            nextinfo = FEIScriptLines[endElsecount + 1];
                            nextline = nextinfo.ReadLine;

                            // check if next line start with IF - then this is an ELSEIF

                            if (nextline.StartsWith("IF "))
                            {
                                nextline = String.Concat("ELSEIF ", nextline.Substring(3).Trim());
                                FEIScriptLines.RemoveAt(endElsecount + 1);
                                FEIScriptLines.RemoveAt(endElsecount);
                                tempinfo = new SCRReadInfo(nextline, nextinfo.LineNumber);
                                FEIScriptLines.Insert(endElsecount, tempinfo);
                                endElsecount = FindEndCondition(FEIScriptLines, endElsecount);
                                nextinfo = FEIScriptLines[endElsecount];
                                nextline = nextinfo.ReadLine;
                            }
                            else
                            {
                                endelse = true;
                                endElsecount++;
                            }

                        }
                    }

                    // line starts with ELSE - check if followed by IF
                    // if ELSEIF, store with rest of line
                    // if ELSE, store on separate new line

                    else if (String.Compare(nextline.Substring(0, Math.Min(5, nextline.Length)), "ELSE ") == 0)
                    {
                        elsepart = true;
                        nextline = nextline.Substring(5).Trim();
                        if (nextline.StartsWith("IF "))
                        {
                            nextline = String.Concat("ELSEIF ", nextline.Substring(3).Trim());
                            FEIScriptLines.RemoveAt(endElsecount);
                            tempinfo = new SCRReadInfo(nextline, nextinfo.LineNumber);
                            FEIScriptLines.Insert(endElsecount, tempinfo);
                            endElsecount = FindEndCondition(FEIScriptLines, endElsecount);
                            nextinfo = FEIScriptLines[endElsecount];
                            nextline = nextinfo.ReadLine;
                        }
                        else
                        {
                            endelse = true;

                            FEIScriptLines.RemoveAt(endElsecount);
                            tempinfo = new SCRReadInfo(nextline.Trim(), nextinfo.LineNumber);
                            FEIScriptLines.Insert(endElsecount, tempinfo);
                            tempinfo = new SCRReadInfo("ELSE", nextinfo.LineNumber);
                            FEIScriptLines.Insert(endElsecount, tempinfo);
                            endElsecount++;
                        }
                    }

                    // line starts with ELSEIF 

                    else if (String.Compare(nextline.Substring(0, Math.Min(7, nextline.Length)), "ELSEIF ") == 0)
                    {
                        elsepart = true;
                        endElsecount = FindEndCondition(FEIScriptLines, endElsecount);
                        nextinfo = FEIScriptLines[endElsecount];
                        nextline = nextinfo.ReadLine;
                    }

                    // line starts with ELSE{
                    // store ELSE on separate new line

                    else if (String.Compare(nextline.Substring(0, Math.Min(5, nextline.Length)), "ELSE{") == 0)
                    {
                        elsepart = true;
                        endelse = true;
                        nextline = nextline.Substring(5).Trim();
                        FEIScriptLines.RemoveAt(endElsecount);
                        tempinfo = new SCRReadInfo(nextline, nextinfo.LineNumber);
                        FEIScriptLines.Insert(endElsecount, tempinfo);
                        nextline = "{";
                        tempinfo = new SCRReadInfo("{", nextinfo.LineNumber);
                        FEIScriptLines.Insert(endElsecount, tempinfo);
                        tempinfo = new SCRReadInfo("ELSE", nextinfo.LineNumber);
                        FEIScriptLines.Insert(endElsecount, tempinfo);
                    }

                    // if an ELSE or ELSEIF part is found - find end 

                    if (elsepart)
                    {
                        if (String.Compare(nextline.Substring(0, 1), "{") == 0)
                        {
                            endElsecount = findEndBlock(FEIScriptLines, endElsecount);
                        }
                        else if (String.Compare(nextline.Substring(0, Math.Min(3, nextline.Length)), "IF ") == 0)
                        {
                            List<int> fullcount = findEndIfBlock(FEIScriptLines, endElsecount);
                            int lastline = fullcount[fullcount.Count - 1];
                            string templine = FEIScriptLines[endElsecount].ReadLine;
                            FEIScriptLines.RemoveAt(endElsecount);
                            templine = String.Concat("{ ", templine);
                            tempinfo = new SCRReadInfo(templine, nextinfo.LineNumber);
                            FEIScriptLines.Insert(endElsecount, tempinfo);
                            templine = FEIScriptLines[lastline - 1].ReadLine;
                            tempnumber = FEIScriptLines[lastline - 1].LineNumber;
                            FEIScriptLines.RemoveAt(lastline - 1);
                            templine = String.Concat(templine, " }");
                            tempinfo = new SCRReadInfo(templine, tempnumber);
                            FEIScriptLines.Insert(lastline - 1, tempinfo);
                            endElsecount = lastline;
                        }
                        else
                        {
                            endElsecount = FindEndStatement(FEIScriptLines, endElsecount);
                        }
                        nextline = endElsecount < FEIScriptLines.Count ? FEIScriptLines[endElsecount].ReadLine.Trim() : String.Empty;
                        nextcount.Add(endElsecount);
                    }
                    else
                    {
                        endelse = true;
                    }
                }

                return nextcount;
            }// findEndIfBlock

            //================================================================================================//
            //
            // find end of IF block enclosed by { and }
            //
            //================================================================================================//

            public static int findEndBlock(List<SCRReadInfo> FEBScriptLines, int index)
            {

                SCRReadInfo firstinfo, thisinfo, tempinfo;

                // Use regular expression to find all occurences of { and }
                // Keep searching through next lines until match is found

                int openparent = 0;
                int closeparent = 0;

                int openindex = 0;
                int closeindex = 0;

                Regex openparstr = new Regex("{");
                Regex closeparstr = new Regex("}");

                firstinfo = FEBScriptLines[index];
                string presentline = firstinfo.ReadLine;

                bool blockEnd = false;
                int splitpoint = -1;
                int checkcount = index;

                // get positions in present line

                MatchCollection opencount = openparstr.Matches(presentline);
                MatchCollection closecount = closeparstr.Matches(presentline);

                // convert to ARRAY

                int totalopen = opencount.Count;
                int totalclose = closecount.Count;

                Match[] closearray = new Match[totalclose];
                closecount.CopyTo(closearray, 0);
                Match[] openarray = new Match[totalopen];
                opencount.CopyTo(openarray, 0);

                // search until match found
                while (!blockEnd)
                {

                    // get next position (continue from previous index)

                    int openpos = openindex < openarray.Length ? openarray[openindex].Index : presentline.Length;
                    int closepos = closeindex < closearray.Length ? closearray[closeindex].Index : presentline.Length;

                    // next is open {

                    if (openpos < closepos)
                    {
                        openparent++;
                        openindex++;
                        openpos = openindex < openarray.Length ? openarray[openindex].Index : presentline.Length;
                    }

                    // next is close }

                    else if (closepos < openpos)
                    {
                        closeparent++;

                        // check for match - if found, end of block is found

                        if (closeparent == openparent)
                        {
                            blockEnd = true;
                            splitpoint = closepos;
                        }
                        else
                        {
                            closeindex++;
                            closepos = closeindex < closearray.Length ? closearray[closeindex].Index : presentline.Length;
                        }
                    }

                    // openpos and closepos equal - both have reached end of line - get next line

                    else
                    {
                        checkcount++;
                        if (checkcount >= FEBScriptLines.Count)
                        {
                            Trace.TraceWarning("sigscr-file line {0} : unbalanced curly brackets : ", index.ToString());
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt",
                                            "unbalanced curly brackets at " + index.ToString() + "\n");
#endif
                            return (FEBScriptLines.Count - 1);
                        }

                        thisinfo = FEBScriptLines[checkcount];
                        presentline = thisinfo.ReadLine;

                        // get positions

                        opencount = openparstr.Matches(presentline);
                        closecount = closeparstr.Matches(presentline);

                        totalopen = opencount.Count;
                        totalclose = closecount.Count;

                        // convert to array

                        closearray = new Match[totalclose];
                        closecount.CopyTo(closearray, 0);
                        openarray = new Match[totalopen];
                        opencount.CopyTo(openarray, 0);

                        openindex = 0;
                        closeindex = 0;

                        // get next positions

                        openpos = openindex < openarray.Length ? openarray[openindex].Index : presentline.Length;
                        closepos = closeindex < closearray.Length ? closearray[closeindex].Index : presentline.Length;
                    }
                }

                // end found - check if anything follows final }

                int nextcount = checkcount + 1;
                thisinfo = FEBScriptLines[checkcount];
                presentline = thisinfo.ReadLine.Trim();

                if (splitpoint >= 0 && splitpoint < presentline.Length - 1)
                {
                    thisinfo = FEBScriptLines[checkcount];
                    presentline = thisinfo.ReadLine;
                    FEBScriptLines.RemoveAt(checkcount);

                    tempinfo = new SCRReadInfo(presentline.Substring(splitpoint + 1).Trim(), firstinfo.LineNumber);
                    FEBScriptLines.Insert(checkcount, tempinfo);
                    tempinfo = new SCRReadInfo(presentline.Substring(0, splitpoint + 1).Trim(), firstinfo.LineNumber);
                    FEBScriptLines.Insert(checkcount, tempinfo);
                }

                return nextcount;
            }//findEndBlock

            //================================================================================================//
            //
            // find end of IF condition statement
            //
            //================================================================================================//

            public static int FindEndCondition(List<SCRReadInfo> FECScriptLines, int index)
            {
                string presentstring, addline;
                int totalopen, totalclose;
                int actindex;

                SCRReadInfo thisinfo, addinfo, tempinfo;

                //================================================================================================//

                actindex = index;

                thisinfo = FECScriptLines[index];
                presentstring = thisinfo.ReadLine;
                FECScriptLines.RemoveAt(index);

                // use regular expression to search for open and close bracket

                Regex openbrack = new Regex(@"\(");
                Regex closebrack = new Regex(@"\)");

                // search for open bracket

                MatchCollection opencount = openbrack.Matches(presentstring);
                totalopen = opencount.Count;

                // add lines until open bracket found

                while (totalopen <= 0 && actindex < FECScriptLines.Count)
                {
                    addinfo = FECScriptLines[actindex];
                    addline = addinfo.ReadLine;
                    FECScriptLines.RemoveAt(actindex);
                    presentstring = String.Concat(presentstring, addline);
                    opencount = openbrack.Matches(presentstring);
                    totalopen = opencount.Count;
                }

                if (totalopen <= 0)
                {
                    Trace.TraceWarning("sigscr-file line {1} : If statement without ( ; starting with {0}", presentstring, thisinfo.LineNumber.ToString());
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt",
                                    "If statement without ( ; starting with {0}" + presentstring + " (" + thisinfo.LineNumber.ToString() + ")\n");
#endif
                }

                // in total string, search for close brackets

                MatchCollection closecount = closebrack.Matches(presentstring);
                totalclose = closecount.Count;

                // keep adding lines until open and close brackets match

                while (totalclose < totalopen && actindex < FECScriptLines.Count)
                {
                    addinfo = FECScriptLines[actindex];
                    addline = addinfo.ReadLine;
                    FECScriptLines.RemoveAt(actindex);
                    presentstring = String.Concat(presentstring, addline);

                    opencount = openbrack.Matches(presentstring);
                    totalopen = opencount.Count;
                    closecount = closebrack.Matches(presentstring);
                    totalclose = closecount.Count;
                }

                actindex = index;

                if (totalclose < totalopen)
                {

                    // locate first "{" - assume this to be the end of the IF statement

                    int possibleEnd = presentstring.IndexOf("{");

                    Trace.TraceWarning("sigscr-file line {1} : Missing ) in IF statement ; starting with {0}",
                    presentstring, thisinfo.LineNumber.ToString());

                    string reportString = String.Copy(presentstring);
                    if (possibleEnd > 0)
                    {
                        reportString = presentstring.Substring(0, possibleEnd);

                        Trace.TraceWarning("IF statement set to : {0}", reportString + ")");

                        tempinfo = new SCRReadInfo(presentstring.Substring(possibleEnd).Trim(),
                            thisinfo.LineNumber);
                        FECScriptLines.Insert(index, tempinfo);
                        presentstring = String.Concat(presentstring.Substring(0, possibleEnd), ")");
                        actindex = index + 1;
                    }

#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "If statement without ) ; starting with " + reportString +
                                   " (" + thisinfo.LineNumber.ToString() + ")\n");
#endif
                }
                else
                {

                    // get position of final close bracket - end of condition statement

                    Match[] closearray = new Match[totalclose];
                    closecount.CopyTo(closearray, 0);
                    Match[] openarray = new Match[totalopen];
                    opencount.CopyTo(openarray, 0);

                    // match open and close brackets - when matched, that is end of condition

                    int actbracks = 1;

                    int actopen = 1;
                    int openpos = actopen < openarray.Length ? openarray[actopen].Index : presentstring.Length + 1;

                    int actclose = 0;
                    int closepos = closearray[actclose].Index;

                    while (actbracks > 0)
                    {
                        if (openpos < closepos)
                        {
                            actbracks++;
                            actopen++;
                            openpos = actopen < openarray.Length ? openarray[actopen].Index : presentstring.Length + 1;
                        }
                        else
                        {
                            actbracks--;
                            if (actbracks > 0)
                            {
                                actclose++;
                                closepos = actclose < closearray.Length ? closearray[actclose].Index : presentstring.Length + 1;
                            }
                        }
                    }

                    // split on end of condition

                    if (closepos < (presentstring.Length - 1))
                    {
                        tempinfo = new SCRReadInfo(presentstring.Substring(closepos + 1).Trim(), thisinfo.LineNumber);
                        FECScriptLines.Insert(index, tempinfo);
                        presentstring = presentstring.Substring(0, closepos + 1);
                    }
                    actindex = index + 1;
                }

                tempinfo = new SCRReadInfo(presentstring.Trim(), thisinfo.LineNumber);
                FECScriptLines.Insert(index, tempinfo);
                return actindex;
            }//findEndCondition

            //================================================================================================//
            //
            // process function call (in statement or in IF condition)
            //
            //================================================================================================//
            static public ArrayList Process_FunctionCall(string functionStatement, IDictionary<string, uint> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes, int lineNumber)
            {
                ArrayList FunctionParts = new ArrayList();
                bool valid_func = true;

                // split in function and parameter parts

                string[] statementParts = functionStatement.Split('(');
                if (statementParts.Length > 2)
                {
                    valid_func = false;
                    Trace.TraceWarning($"sigscr-file line {lineNumber} : Unexpected number of ( in function call : {functionStatement}");
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unexpected number of ( in function call : " + functionStatement + "\n");
#endif
                }

                // process function part
                if (Enum.TryParse<SCRExternalFunctions>(statementParts[0], true, out SCRExternalFunctions scrExternalFunctionsResult))
                {
                    FunctionParts.Add(scrExternalFunctionsResult);
                }
                else
                {
                    valid_func = false;
                    Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown function call : {functionStatement}\nDetails : {statementParts[0]}");
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown function call : " + functionStatement + "\n");
#endif
                }

                // remove closing bracket

                string ParameterPart = statementParts[1].Replace(")", String.Empty).Trim();

                // process first parameters in case of multiple parameters

                int sepindex = ParameterPart.IndexOf(",");
                while (sepindex > 0 && valid_func)
                {
                    string parmPart = ParameterPart.Substring(0, sepindex).Trim();
                    SCRParameterType TempParm = Process_TermPart(parmPart, localFloats, orSignalTypes, orNormalSubtypes, lineNumber);
                    FunctionParts.Add(TempParm);

                    ParameterPart = ParameterPart.Substring(sepindex + 1).Trim();
                    sepindex = ParameterPart.IndexOf(",");
                }

                // process last or only parameter if set

                if (!String.IsNullOrEmpty(ParameterPart) && valid_func)
                {
                    SCRParameterType TempParm = Process_TermPart(ParameterPart, localFloats, orSignalTypes, orNormalSubtypes, lineNumber);
                    FunctionParts.Add(TempParm);
                }

                // return null in case of error

                if (!valid_func)
                {
                    FunctionParts = null;
                }
                return FunctionParts;
            }//process_FunctionCall

            //================================================================================================//
            //
            // process term part of statement (right-hand side)
            //
            //================================================================================================//
            static public SCRParameterType Process_TermPart(string termString, IDictionary<string, uint> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes, int lineNumber)
            {

                SCRParameterType result = new SCRParameterType(SCRTermType.Constant, 0);
                // check for use of #
                if (termString[0] == '#')
                {
                    termString = termString.Substring(1).Trim();
                }

                // try constant
                if (int.TryParse(termString, out int tmpInt))
                {
                    result = new SCRParameterType(SCRTermType.Constant, tmpInt);
                }
                // try external float
                else if (Enum.TryParse<SCRExternalFloats>(termString, true, out SCRExternalFloats exFloat))
                {
                    result = new SCRParameterType(SCRTermType.ExternalFloat, (int)exFloat);
                }
                // try local float
                else if (localFloats.TryGetValue(termString, out uint localFloat))
                {
                    result = new SCRParameterType(SCRTermType.LocalFloat, (int)localFloat);
                }
                // try blockstate
                else if (termString.StartsWith("BLOCK_"))
                {
                    if (Enum.TryParse<MstsBlockState>(termString.Substring(6), true, out MstsBlockState blockstate))
                    {
                        result = new SCRParameterType(SCRTermType.Block, (int)blockstate);
                    }
                    else
                    {
                        Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown Blockstate : {termString.Substring(6)} \n");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown Blockstate : " + termString + "\n");
#endif
                    }
                }
                // try SIGASP definition
                else if (termString.StartsWith("SIGASP_"))
                {
                    if (Enum.TryParse<MstsSignalAspect>(termString.Substring(7), true, out MstsSignalAspect aspect))
                    {
                        result = new SCRParameterType(SCRTermType.Sigasp, (int)aspect);
                    }
                    else
                    {
                        Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown Aspect : {termString.Substring(7)} \n");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown Aspect : " + termString + "\n");
#endif
                    }
                }
                // try SIGFN definition
                else if (termString.StartsWith("SIGFN_"))
                {
                    int index = orSignalTypes.IndexOf(termString.Substring(6).ToUpper());
                    if (index != -1)
                    {
                        result = new SCRParameterType(SCRTermType.Sigfn, index);
                    }
                    else
                    {
                        Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown SIGFN Type : {termString.Substring(6)} \n");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown Type : " + termString + "\n");
#endif
                    }
                }
                // try ORSubtype definition
                else if (termString.StartsWith("ORSUBTYPE_"))
                {
                    int index = orNormalSubtypes.IndexOf(termString.Substring(10).ToUpper());
                    if (index != -1)
                    {
                        result = new SCRParameterType(SCRTermType.ORNormalSubtype, index);
                    }
                    else
                    {
                        Trace.TraceWarning($"sigscr-file line {lineNumber} : Unknown ORSUBTYPE : {termString.Substring(10)} \n");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown Type : " + termString + "\n");
#endif
                    }
                }
                // try SIGFEAT definition
                else if (termString.StartsWith("SIGFEAT_"))
                {
                    int index = SignalShape.SignalSubObj.SignalSubTypes.IndexOf(termString.Substring(8).ToUpper());
                    if (index != -1)
                    {
                        result = new SCRParameterType(SCRTermType.Sigfeat, index);
                    }
                    else
                    {
                        Trace.TraceWarning($"sigscr-file line {lineNumber} : Unknown SubType : {termString.Substring(8)} \n");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown SubType : " + termString + "\n");
#endif
                    }
                }
                // nothing found - set error
                else
                {
                    Trace.TraceWarning($"sigscr-file line {lineNumber} : Unknown parameter in statement : {termString}");
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "Unknown parameter : " + termString + "\n");
#endif
                }
                return result;
            }//process_TermPart

            //================================================================================================//
            //
            // process IF condition line - split into logic parts
            //
            //================================================================================================//

            public static ArrayList getIfConditions(SCRReadInfo GICInfo, IDictionary<string, uint> LocalFloats, IList<string> ORSignalTypes, IList<string> ORNormalSubtypes)
            {
                SCRConditions ThisCondition;
                ArrayList SCRConditionList = new ArrayList();

                SCRAndOr condAndOr;
                List<string> sublist = new List<string>();

                string GICString = GICInfo.ReadLine;

                // extract condition between first ( and last )

                int startpos = GICString.IndexOf("(");
                int endpos = GICString.LastIndexOf(")");
                string presentline = GICString.Substring(startpos + 1, endpos - startpos - 1).Trim();

                // search for substrings
                // search for matching brackets

                Regex openparstr = new Regex("[(]");
                Regex closeparstr = new Regex("[)]");

                // get all brackets in string

                MatchCollection opencount = openparstr.Matches(presentline);
                MatchCollection closecount = closeparstr.Matches(presentline);

                int totalopen = opencount.Count;
                int totalclose = closecount.Count;

                if (totalopen > 0)
                {

                    // convert matches to array

                    Match[] closearray = new Match[totalclose];
                    closecount.CopyTo(closearray, 0);
                    Match[] openarray = new Match[totalopen];
                    opencount.CopyTo(openarray, 0);

                    // get positions, find ) which matches first (

                    bool blockEnd = false;
                    int bracklevel = 0;

                    int openindex = 0;
                    int closeindex = 0;

                    int openpos = openindex < openarray.Length ? openarray[openindex].Index : presentline.Length;
                    int closepos = closeindex < closearray.Length ? closearray[closeindex].Index : presentline.Length;

                    int firstopen = 0;

                    while (!blockEnd)
                    {
                        if (bracklevel == 0)
                        {
                            firstopen = openpos;
                        }

                        if (openpos < closepos)
                        {
                            bracklevel++;
                            openindex++;
                            openpos = openindex < openarray.Length ? openarray[openindex].Index : presentline.Length;
                        }
                        else
                        {
                            bracklevel--;

                            // match found, check if any | or & in between
                            // if so, condition is enclosed and must be processed separately
                            // store string in special array
                            // replace string with substitute pointer reference stored string position

                            if (bracklevel == 0)
                            {
                                string substring = presentline.Substring(firstopen, closepos - firstopen + 1);
                                if (CheckCondition(substring) > 0)
                                {
                                    sublist.Add(substring);
                                    string replacestring = "[" + sublist.Count.ToString() + "]";
                                    replacestring = replacestring.PadRight(substring.Length, '*');

                                    presentline = presentline.Remove(firstopen, closepos - firstopen + 1);
                                    presentline = presentline.Insert(firstopen, replacestring);
                                }
                            }

                            closeindex++;
                            if (closeindex < closearray.Length)
                            {
                                closepos = closearray[closeindex].Index;
                            }
                            else
                            {
                                blockEnd = true;
                            }

                        }
                    }
                }

                // process main string
                // check for separators (OR or AND)

                string reststring = presentline;
                string condstring = String.Empty;
                string procstring = String.Empty;
                string tempstring = String.Empty;

                int seppos = CheckCondition(reststring);

                // process each part

                while (seppos > 0)
                {
                    procstring = reststring.Substring(0, seppos).Trim();
                    condstring = reststring.Substring(seppos, 2);
                    tempstring = reststring.Substring(seppos + 2);

                    bool validCondition = false;
                    while (!validCondition && tempstring.Length > 0 && tempstring[0] != ' ')
                    {
                        if (TranslateAndOr.ContainsKey(condstring))
                        {
                            validCondition = true;
                        }
                        else
                        {
                            condstring = String.Concat(condstring, tempstring.Substring(0, 1));
                            tempstring = tempstring.Substring(1);
                        }
                    }

                    reststring = tempstring.Trim();

                    // process separate !

                    if (procstring.Length > 0 && String.Compare(procstring.Substring(0, 1), "!") == 0)
                    {
                        SCRNegate negated = SCRNegate.NEGATE;
                        SCRConditionList.Add(negated);
                        procstring = procstring.Substring(1).Trim();
                    }

                    // process separate NOT

                    if (procstring.Length > 4 && String.Compare(procstring.Substring(0, 4), "NOT ") == 0)
                    {
                        SCRNegate negated = SCRNegate.NEGATE;
                        SCRConditionList.Add(negated);
                        procstring = procstring.Substring(4).Trim();
                    }

                    // previous separated substring - process as new full IF condition

                    if (procstring.StartsWith("["))
                    {
                        int entnum = procstring.IndexOf("]");
                        int subindex = Convert.ToInt32(procstring.Substring(1, entnum - 1));
                        SCRReadInfo subinfo = new SCRReadInfo(sublist[subindex - 1], GICInfo.LineNumber);
                        ArrayList SubCondition = getIfConditions(subinfo, LocalFloats, ORSignalTypes, ORNormalSubtypes);
                        SCRConditionList.Add(SubCondition);
                    }

                    // single condition

                    else
                    {
                        // remove any superflouos brackets ()
                        while (procstring.StartsWith("(") && procstring.EndsWith(")"))
                        {
                            procstring = procstring.Substring(1, procstring.Length - 2);
                        }

                        ThisCondition = new SCRConditions(procstring, LocalFloats, ORSignalTypes, ORNormalSubtypes, GICInfo.LineNumber);
                        SCRConditionList.Add(ThisCondition);
                    }

                    // translate logical operator

                    if (TranslateAndOr.TryGetValue(condstring, out condAndOr))
                    {
                        SCRConditionList.Add(condAndOr);
                    }
                    else
                    {
                        Trace.TraceWarning("sigscr-file line {1} : Invalid condition operator in : {0}", GICString, GICInfo.LineNumber.ToString());
                    }

                    seppos = CheckCondition(reststring);
                }

                // process last part or full part if no separators

                procstring = reststring;

                // process separate !

                if (procstring.Length > 0 && String.Compare(procstring.Substring(0, 1), "!") == 0)
                {
                    SCRNegate negated = SCRNegate.NEGATE;
                    SCRConditionList.Add(negated);
                    procstring = procstring.Substring(1).Trim();
                }

                if (procstring.Length > 4 && String.Compare(procstring.Substring(0, 4), "NOT ") == 0)
                {
                    SCRNegate negated = SCRNegate.NEGATE;
                    SCRConditionList.Add(negated);
                    procstring = procstring.Substring(4).Trim();
                }

                // previous separated substring - process as new full IF condition

                if (procstring.StartsWith("["))
                {
                    int entnum = procstring.IndexOf("]");
                    int subindex = Convert.ToInt32(procstring.Substring(1, entnum - 1));
                    SCRReadInfo subinfo = new SCRReadInfo(sublist[subindex - 1], GICInfo.LineNumber);
                    ArrayList SubCondition = getIfConditions(subinfo, LocalFloats, ORSignalTypes, ORNormalSubtypes);
                    SCRConditionList.Add(SubCondition);
                }

                // single condition

                else
                {

                    // remove any enclosing ()

                    if (procstring.StartsWith("("))
                    {
                        procstring = procstring.Substring(1, procstring.Length - 2).Trim();
                    }
                    ThisCondition = new SCRConditions(procstring, LocalFloats, ORSignalTypes, ORNormalSubtypes, GICInfo.LineNumber);
                    SCRConditionList.Add(ThisCondition);
                }

                // process logical operator if set

                if (!String.IsNullOrEmpty(condstring))
                {
                    if (TranslateAndOr.TryGetValue(condstring, out condAndOr))
                    {
                        SCRConditionList.Add(condAndOr);
                    }
                    else
                    {
                        Trace.TraceWarning("sigscr-file line {1} : Invalid condition operator in : {0}", GICString, GICInfo.LineNumber.ToString());
                    }
                }

                return SCRConditionList;
            }//getIfConditions

            //================================================================================================//
            //
            // check for condition in statement
            //
            //================================================================================================//

            public static int CheckCondition(String teststring)
            {
                char[] AndOrCheck = "|&".ToCharArray();
                int returnvalue = 0;

                returnvalue = teststring.IndexOfAny(AndOrCheck);
                if (returnvalue > 0)
                    return returnvalue;

                returnvalue = teststring.IndexOf(" AND ");
                if (returnvalue > 0)
                    return returnvalue + 1;

                returnvalue = teststring.IndexOf(" OR ");
                if (returnvalue > 0)
                    return returnvalue + 1;

                return returnvalue;
            }// CheckCondition

            //================================================================================================//
            //
            // sub classes
            //
            //================================================================================================//
            //
            // class SCRStatement
            //
            //================================================================================================//

            public class SCRStatement
            {
                public bool IsValid { get; private set; } = true;
                public List<SCRStatTerm> StatementTerms { get; private set; } = new List<SCRStatTerm>();
                public SCRTermType AssignType { get; private set; }
                public int AssignParameter { get; private set; }

                private SCRReadInfo statementInfo;

                //================================================================================================//
                //
                //  Constructor
                //

                public SCRStatement(SCRReadInfo statement, IDictionary<string, uint> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    statementInfo = statement;
                    AssignType = SCRTermType.Invalid;
                    string[] statementParts;
                    string currentLine = statement.ReadLine;

                    // check for improper use of =#, ==# or ==
                    currentLine = currentLine.Replace("=#", "=").Replace("==", "=");

                    string term = null;
                    currentLine = currentLine.Replace(";", String.Empty);
                    //split on =, should be only 2 parts
                    statementParts = currentLine.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                    // Assignment part - search external and local floats
                    // if only 1 part, it is a single function call without assignment
                    switch (statementParts.Length)
                    {
                        case 1:
                            // Term part
                            // get positions of allowed operators
                            term = statementParts[0].Trim();
                            break;
                        case 2:
                            string assignPart = statementParts[0].Trim();
                            if (Enum.TryParse<SCRExternalFloats>(assignPart, true, out SCRExternalFloats result))
                            {
                                AssignParameter = (int)result;
                                AssignType = SCRTermType.ExternalFloat;
                            }
                            else if (localFloats.TryGetValue(assignPart, out uint value))
                            {
                                AssignParameter = (int)value;
                                AssignType = SCRTermType.LocalFloat;
                            }
                            // Term part
                            // get positions of allowed operators
                            term = statementParts[1].Trim();
                            break;
                        default:
                            IsValid = false;
                            Trace.TraceWarning("sigscr-file line {1} : Unexpected number of = in string : {0}", currentLine, statementInfo.LineNumber.ToString());
#if DEBUG_PRINT_IN
                        File.AppendAllText(din_fileLoc + @"sigscr.txt",
                                        "Unexpected number of = in string " + statementInfo.ReadLine + " (" + statementInfo.LineNumber.ToString() + ")\n");
#endif
                            break;
                    }

                    // process term string
                    int sublevel = 0;
                    if (IsValid)
                        SCRProcess_TermPartLine(term, ref sublevel, 0, localFloats, orSignalTypes, orNormalSubtypes, statementInfo.LineNumber);
                }

                //================================================================================================//
                //
                //  Process Term part line
                //  May be called recursive to process substrings
                //

                public void SCRProcess_TermPartLine(string TermLinePart, ref int sublevel, int issublevel,
                            IDictionary<string, uint> LocalFloats, IList<string> ORSignalTypes, IList<string> ORNormalSubtypes, int linenumber)
                {

                    string keepString = String.Copy(TermLinePart);
                    string procString;
                    string operString;
                    bool syntaxerror = false;

                    string AllowedOperators = "[";

                    foreach (KeyValuePair<string, SCRTermOperator> PosOperator in TranslateOperator)
                    {
                        string ActOperator = PosOperator.Key;
                        if (String.Compare(ActOperator, "?") != 0)
                        {
                            AllowedOperators = String.Concat(AllowedOperators, ActOperator);
                        }
                    }

                    AllowedOperators = String.Concat(AllowedOperators, "]");
                    Regex operators = new Regex(AllowedOperators);
                    MatchCollection opertotal = operators.Matches(keepString);

                    int totalOper = opertotal.Count;
                    Match[] operPos = new Match[totalOper];
                    opertotal.CopyTo(operPos, 0);

                    // get position of closing and opening brackets

                    Regex openbrack = new Regex("[(]");
                    MatchCollection openbrackmatch = openbrack.Matches(keepString);
                    int totalOpenbrack = openbrackmatch.Count;
                    Match[] openbrackpos;

                    Regex closebrack = new Regex("[)]");
                    MatchCollection closebrackmatch = closebrack.Matches(keepString);
                    int totalClosebrack = closebrackmatch.Count;
                    Match[] closebrackpos;

                    if (totalClosebrack != totalOpenbrack)
                    {
                        Trace.TraceWarning("sigscr-file line {1} : Unmatching brackets in : {0}", keepString, statementInfo.LineNumber.ToString());
                        keepString = String.Empty;
#if DEBUG_PRINT_IN
                        File.AppendAllText(din_fileLoc + @"sigscr.txt",
                                        "Unmatching brackets in : " + keepString + " (" + statementInfo.LineNumber.ToString() + "\n");
#endif
                    }


                    // process each part - part is either separated by operator or enclosed within brackets

                    int nextoper = 0;
                    int nextopenbrack = 0;
                    int nextoperpos;
                    int nextbrackpos;

                    while (!String.IsNullOrEmpty(keepString) && !syntaxerror)
                    {

                        // if first chars is operator, copy it to operator string
                        // redetermine position of next operator

                        opertotal = operators.Matches(keepString);
                        totalOper = opertotal.Count;
                        operPos = new Match[totalOper];
                        opertotal.CopyTo(operPos, 0);
                        nextoper = 0;
                        nextoperpos = nextoper < operPos.Length ? operPos[nextoper].Index : keepString.Length + 1;

                        if (nextoperpos == 0)
                        {
                            operString = keepString.Substring(0, 1);
                            keepString = keepString.Substring(1).Trim();

                            opertotal = operators.Matches(keepString);
                            totalOper = opertotal.Count;
                            operPos = new Match[totalOper];
                            opertotal.CopyTo(operPos, 0);
                            nextoper = 0;
                            nextoperpos = nextoper < operPos.Length ? operPos[nextoper].Index : keepString.Length + 1;
                        }
                        else
                        {
                            operString = String.Empty;
                        }

                        // redetermine positions of operators and brackets

                        openbrackmatch = openbrack.Matches(keepString);
                        totalOpenbrack = openbrackmatch.Count;
                        openbrackpos = new Match[totalOpenbrack];
                        openbrackmatch.CopyTo(openbrackpos, 0);
                        nextopenbrack = 0;
                        nextbrackpos = nextopenbrack < openbrackpos.Length ?
                                openbrackpos[nextopenbrack].Index : keepString.Length + 1;

                        closebrackmatch = closebrack.Matches(keepString);
                        totalClosebrack = closebrackmatch.Count;
                        closebrackpos = new Match[totalClosebrack];
                        closebrackmatch.CopyTo(closebrackpos, 0);

                        // first is bracket, but not at start so is part of function call - ignore
                        // first is operator
                        // operator and bracket are equal - so neither are found
                        // normal term, so process

                        if ((nextbrackpos < nextoperpos && nextbrackpos > 0) || nextbrackpos >= nextoperpos)
                        {
                            if (nextoperpos < keepString.Length)
                            {
                                procString = keepString.Substring(0, nextoperpos).Trim();
                                keepString = keepString.Substring(nextoperpos).Trim();
                            }
                            else
                            {
                                procString = String.Copy(keepString);
                                keepString = String.Empty;
                            }

                            if (procString.IndexOf(")") > 0)
                            {
                                procString = procString.Replace(")", String.Empty).Trim();
                            }

                            if (String.IsNullOrEmpty(procString))
                            {
                                Trace.TraceWarning("sigscr-file line {1} : Invalid statement syntax : {0}", TermLinePart, linenumber.ToString());
                                syntaxerror = true;
                                StatementTerms.Clear();
                            }
                            else
                            {
                                SCRStatTerm thisTerm =
                                        new SCRStatTerm(procString, operString, sublevel, issublevel, LocalFloats, ORSignalTypes, ORNormalSubtypes, statementInfo.LineNumber);
                                StatementTerms.Add(thisTerm);
                            }
                        }

                        // enclosed term - process as substring

                        else
                        {

                            // find matching end bracket

                            nextopenbrack++;
                            int brackcount = 1;
                            int nextclosebrack = 0;

                            int nextclosepos = closebrackpos[nextclosebrack].Index;
                            while (nextclosepos < nextbrackpos)
                            {
                                nextclosebrack++;
                                nextclosepos = closebrackpos[nextclosebrack].Index;
                            }
                            int lastclosepos = nextclosepos;

                            nextbrackpos =
                                  nextopenbrack < openbrackpos.Length ?
                                  openbrackpos[nextopenbrack].Index : keepString.Length + 1;

                            while (brackcount > 0)
                            {
                                if (nextbrackpos < nextclosepos)
                                {
                                    brackcount++;
                                    nextopenbrack++;
                                    nextbrackpos =
                                        nextopenbrack < openbrackpos.Length ?
                                        openbrackpos[nextopenbrack].Index : keepString.Length + 1;
                                }
                                else
                                {
                                    lastclosepos = nextclosepos;
                                    brackcount--;
                                    nextclosebrack++;
                                    nextclosepos =
                                        nextclosebrack < closebrackpos.Length ?
                                        closebrackpos[nextclosebrack].Index : keepString.Length + 1;
                                }
                            }

                            procString = keepString.Substring(1, lastclosepos - 1).Trim();
                            keepString = keepString.Substring(lastclosepos + 1).Trim();

                            // increase sublevel, set sublevel entry in statements

                            sublevel++;
                            SCRStatTerm thisTerm =
                                    new SCRStatTerm("*S*", operString, sublevel, issublevel, LocalFloats, ORSignalTypes, ORNormalSubtypes, statementInfo.LineNumber);
                            StatementTerms.Add(thisTerm);

                            // process string as sublevel

                            int nextsublevel = sublevel;
                            SCRProcess_TermPartLine(procString, ref sublevel, nextsublevel, LocalFloats, ORSignalTypes, ORNormalSubtypes, linenumber);
                        }
                    }
                }//SCRProcess_TermPartLine

                //================================================================================================//

            }// class SCRStatement


            //================================================================================================//
            //
            // class SCRStatTerm
            //
            //================================================================================================//

            public class SCRStatTerm
            {
                public SCRExternalFunctions Function { get; private set; }
                public SCRParameterType[] PartParameter { get; private set; }
                public SCRTermOperator TermOperator { get; private set; }
                public bool negate { get; private set; }
                public int sublevel { get; private set; }
                public int issublevel { get; private set; }
                public int linenumber { get; private set; }

                //================================================================================================//
                //
                // Constructor
                //

                public SCRStatTerm(string StatementString, string StatementOperator, int sublevelIn, int issublevelIn,
                            IDictionary<string, uint> LocalFloats, IList<string> ORSignalTypes, IList<string> ORNormalSubtypes, int thisLine)
                {

                    linenumber = thisLine;

                    // check if statement starts with ! - if so , set negate

                    if (String.Compare(StatementString.Substring(0, 1), "!") == 0)
                    {
                        negate = true;
                        StatementString = StatementString.Substring(1).Trim();
                    }
                    else if (StatementString.Length >= 5 && String.Compare(StatementString.Substring(0, 4), "NOT ") == 0)
                    {
                        negate = true;
                        StatementString = StatementString.Substring(4).Trim();
                    }
                    else
                    {
                        negate = false;
                    }

                    sublevel = 0;

                    List<SCRParameterType> TempParameter = new List<SCRParameterType>();

                    // empty string - no parameter (can occur incase of allocation to negative number)

                    if (String.IsNullOrEmpty(StatementString))
                    {
                        TempParameter.Add(null);
                    }

                    // sublevel definition

                    else if (String.Compare(StatementString, "*S*") == 0)
                    {
                        sublevel = sublevelIn;
                    }

                    // if contains no brackets it is a fixed parameter

                    else if (StatementString.IndexOf("(") < 0)
                    {
                        if (String.Compare(StatementString, "RETURN") == 0)
                        {
                            Function = SCRExternalFunctions.RETURN;
                        }
                        else
                        {
                            Function = SCRExternalFunctions.NONE;

                            PartParameter = new SCRParameterType[1];
                            PartParameter[0] = Process_TermPart(StatementString.Trim(), LocalFloats, ORSignalTypes, ORNormalSubtypes, linenumber);
                            TermOperator = TranslateOperator.TryGetValue(StatementOperator, out SCRTermOperator tempOperator) ? tempOperator : SCRTermOperator.NONE;
                        }
                    }


                    // function

                    else
                    {

                        ArrayList FunctionParts = Process_FunctionCall(StatementString, LocalFloats, ORSignalTypes, ORNormalSubtypes, linenumber);

                        if (FunctionParts == null)
                        {
                            Function = SCRExternalFunctions.NONE;
                        }
                        else
                        {
                            Function = (SCRExternalFunctions)FunctionParts[0];

                            if (FunctionParts.Count > 1)
                            {
                                PartParameter = new SCRParameterType[FunctionParts.Count - 1];
                                for (int iparm = 1; iparm < FunctionParts.Count; iparm++)
                                {
                                    PartParameter[iparm - 1] = (SCRParameterType)FunctionParts[iparm];
                                }
                            }
                            else
                            {
                                PartParameter = null;
                            }
                        }
                    }

                    // process operator

                    TermOperator = TranslateOperator.TryGetValue(StatementOperator, out SCRTermOperator termOperator) ? termOperator : SCRTermOperator.NONE;

                    // issublevel

                    issublevel = issublevelIn;

                } // constructor
            } // class SCRStatTerm

            //================================================================================================//
            //
            // class SCRParameterType
            //
            //================================================================================================//

            public class SCRParameterType
            {
                public SCRTermType PartType { get; private set; }
                public int PartParameter { get; private set; }

                //================================================================================================//
                //
                // Constructor
                //

                public SCRParameterType(SCRTermType type, int value)
                {
                    PartType = type;
                    PartParameter = value;
                } // constructor
            } // class SCRParameterType

            //================================================================================================//
            //
            // class SCRConditionBlock
            //
            //================================================================================================//

            public class SCRConditionBlock
            {
                public ArrayList Conditions;
                public SCRBlock IfBlock;
                public List<SCRBlock> ElseIfBlock;
                public SCRBlock ElseBlock;

                //================================================================================================//
                //
                // Constructor
                // Input is the array of indices pointing to the lines following the IF - ELSEIF - IF blocks
                //

                public SCRConditionBlock(List<SCRReadInfo> CBLScriptLines, int index, List<int> endindex, IDictionary<string, uint> LocalFloats, IList<string> ORSignalTypes, IList<string> ORNormalSubtypes)
                {

                    SCRReadInfo thisinfo, tempinfo;

                    // process conditions

                    Conditions = getIfConditions(CBLScriptLines[index], LocalFloats, ORSignalTypes, ORNormalSubtypes);

                    // process IF block

                    int iflines = endindex[0] - index - 1;
                    List<SCRReadInfo> IfSubBlock = new List<SCRReadInfo>();

                    for (int iline = 0; iline < iflines; iline++)
                    {
                        IfSubBlock.Add(CBLScriptLines[iline + index + 1]);
                    }

                    IfBlock = new SCRBlock(IfSubBlock, LocalFloats, ORSignalTypes, ORNormalSubtypes);
                    ElseIfBlock = null;
                    ElseBlock = null;

                    // process all ELSE blocks if available

                    int blockindex = 0;
                    int elseindex = endindex[blockindex];
                    blockindex++;

                    while (blockindex < endindex.Count)
                    {
                        int elselines = endindex[blockindex] - elseindex;

                        List<SCRReadInfo> ElseSubBlock = new List<SCRReadInfo>();

                        // process ELSEIF block
                        // delete ELSE to process as IF block

                        if (CBLScriptLines[elseindex].ReadLine.StartsWith("ELSEIF"))
                        {
                            thisinfo = CBLScriptLines[elseindex];
                            tempinfo = new SCRReadInfo(thisinfo.ReadLine.Substring(4), thisinfo.LineNumber); // set start of line to IF
                            ElseSubBlock.Add(tempinfo);

                            for (int iline = 1; iline < elselines; iline++)
                            {
                                ElseSubBlock.Add(CBLScriptLines[iline + elseindex]);
                            }
                            SCRBlock TempBlock = new SCRBlock(ElseSubBlock, LocalFloats, ORSignalTypes, ORNormalSubtypes);
                            if (ElseIfBlock == null)
                            {
                                ElseIfBlock = new List<SCRBlock>();
                            }
                            ElseIfBlock.Add(TempBlock);
                            elseindex = endindex[blockindex];
                            blockindex++;
                        }

                        // process ELSE block

                        else
                        {
                            for (int iline = 1; iline < elselines; iline++)
                            {
                                ElseSubBlock.Add(CBLScriptLines[iline + elseindex]);
                            }
                            ElseBlock = new SCRBlock(ElseSubBlock, LocalFloats, ORSignalTypes, ORNormalSubtypes);
                            blockindex++;
                        }

                        ElseSubBlock.Clear();

                    }
                } // constructor
            } // class SCRConditionBlock

            //================================================================================================//
            //
            // class SCRConditions
            //
            //================================================================================================//

            public class SCRConditions
            {
                public SCRStatTerm Term1;
                public bool negate1;
                public SCRStatTerm Term2;
                public bool negate2;
                public SCRTermCondition Condition;
                int linenumber;

                //================================================================================================//
                //
                //  Constructor
                //

                public SCRConditions(string TermString, IDictionary<string, uint> LocalFloats, IList<string> ORSignalTypes, IList<string> ORNormalSubtypes, int thisLine)
                {

                    string firststring, secondstring;
                    string separator;
                    string TempString = TermString;

                    linenumber = thisLine;

                    // check on !, if not followed by = then it is a NOT, replace by ^ to ease processing

                    Regex NotSeps = new Regex("!");

                    MatchCollection NotSepCount = NotSeps.Matches(TempString);
                    int totalNot = NotSepCount.Count;
                    Match[] NotSeparray = new Match[totalNot];
                    NotSepCount.CopyTo(NotSeparray, 0);

                    for (int inot = 0; inot < totalNot; inot++)
                    {
                        int notpos = NotSeparray[inot].Index;
                        if (String.Compare(TempString.Substring(notpos, 2), "!=") != 0)
                        {
                            TempString = String.Concat(TempString.Substring(0, notpos), "^", TempString.Substring(notpos + 1));
                        }
                    }

                    // search for separators

                    Regex CondSeps = new Regex("[<>!=]");

                    MatchCollection CondSepCount = CondSeps.Matches(TempString);
                    int totalSeps = CondSepCount.Count;
                    Match[] CondSeparray = new Match[totalSeps];
                    CondSepCount.CopyTo(CondSeparray, 0);

                    // split on separator

                    if (totalSeps == 0)
                    {
                        firststring = TempString.Trim();
                        secondstring = String.Empty;
                        separator = String.Empty;
                    }
                    else
                    {
                        firststring = TempString.Substring(0, CondSeparray[0].Index).Trim();
                        secondstring = TempString.Substring(CondSeparray[0].Index + 1).Trim();
                        separator = TempString.Substring(CondSeparray[0].Index, 1);
                    }

                    // first string
                    // check for ^ (as replacement for !) as starting character

                    negate1 = false;
                    if (firststring.StartsWith("^"))
                    {
                        negate1 = true;
                        firststring = firststring.Substring(1).Trim();
                    }

                    Term1 = new SCRStatTerm(firststring, String.Empty, 0, 0, LocalFloats, ORSignalTypes, ORNormalSubtypes, linenumber);

                    // second string (if it exists)
                    // check of first char, if =, add this to separator
                    // check on next char, if #, remove
                    // check for ^ (as replacement for !) as next character

                    negate2 = false;
                    if (String.IsNullOrEmpty(secondstring))
                    {
                        Term2 = null;
                    }
                    else
                    {
                        if (secondstring.StartsWith("="))
                        {
                            separator = String.Concat(separator, "=");
                            secondstring = secondstring.Substring(1).Trim();
                        }

                        if (secondstring.StartsWith("#"))
                        {
                            secondstring = secondstring.Substring(1).Trim();
                        }

                        if (secondstring.StartsWith("^"))
                        {
                            negate2 = true;
                            secondstring = secondstring.Substring(1).Trim();
                        }

                        Term2 = new SCRStatTerm(secondstring, String.Empty, 0, 0, LocalFloats, ORSignalTypes, ORNormalSubtypes, linenumber);
                    }

                    if (!String.IsNullOrEmpty(separator))
                    {
                        SCRTermCondition setcond;
                        if (TranslateConditions.TryGetValue(separator, out setcond))
                        {
                            Condition = setcond;
                        }
                        else
                        {
                            Trace.TraceWarning("sigscr-file line {1} : Invalid condition operator in : {0}", TermString, linenumber.ToString());
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt",
                                            "Invalid condition operator in : " + TermString + "\n"); ;
#endif
                        }
                    }
                } // constructor
            } // class SCRConditions

            //================================================================================================//
            //
            // class SCRBlock
            //
            //================================================================================================//

            public class SCRBlock
            {
                public ArrayList Statements { get; private set; }

                //================================================================================================//
                //
                //  Constructor
                //

                public SCRBlock(List<SCRReadInfo> blockStrings, IDictionary<string, uint> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    Statements = ParseStatements(blockStrings, localFloats, orSignalTypes, orNormalSubtypes);
                } // constructor
            } // class SCRBlock
        } // class Scripts
    }
}
