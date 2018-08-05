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
 #define DEBUG_PRINT_OUT
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Orts.Formats.Msts
{
    #region Script Tokenizer and Parser
    internal enum SignalScriptTokenType
    {
        Value = 0x00,
        Operator,               // ! & | ^ + - * / % #
        Tab = 0x09,             // \t
        LineEnd = 0x0a,         // \n
        Separator = 0x20,       // blank
        BracketOpen = 0x28,     // (
        BracketClose = 0x29,    // )
        Comma = 0x2c,           // ,
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
        OpenComment,
        EndComment,
        Operator,
    }

    internal static class OperatorTokenExtension
    {
        public static bool ValidateOperator(string value, char c)
        {
            if (value.Length > 3)
                return false;
            switch (value)
            {
                case "|":
                    return (c == '|');
                case "&":
                    return (c == '&');
                case "^":
                    return false;
                case "!": case "*": case "%": case "=": case "/#":
                    return (c == '=');
                case "+": case "-":
                    return (c == '=' || c == value[0]);
                case "/": case "<": case ">":
                    return (c == '=' || c == '#');
                case "#":
                    return false;
                case "==": case "!=": case "<=": case ">=":
                    return (c == '#');
            }
            return false;
        }
    }

    internal class SignalScriptTokenizer : IEnumerable<SignalScriptToken>
    {
        private TextReader reader;

        internal int LineNumber { get; private set; }

        public SignalScriptTokenizer(TextReader reader) : this(reader, 0)
        {
        }

        public SignalScriptTokenizer(TextReader reader, int lineNumberOffset)
        {
            this.reader = reader;
            this.LineNumber = lineNumberOffset;
        }

        public IEnumerator<SignalScriptToken> GetEnumerator()
        {
            string line;
            CommentParserState state = CommentParserState.None;
            StringBuilder value = new StringBuilder();
            bool lineContent = false;

            while ((line = reader.ReadLine()) != null)
            {
                LineNumber++;
                lineContent = false;

                foreach (char c in line)
                {
                    switch (c)
                    {
                        case '/':
                            switch (state)
                            {
                                case CommentParserState.None:
                                    if (value.Length > 0)
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Value, value.ToString());
                                        value.Length = 0;
                                    }
                                    value.Append(c);
                                    state = CommentParserState.Operator;
                                    continue;
                                case CommentParserState.Operator:
                                    if (value.Length == 1 && value.ToString() == "/")
                                    {
                                        state = CommentParserState.None;
                                        value.Length = value.Length - 1;
                                        goto SkipLineComment;
                                    }
                                    else
                                    {
                                        if (!OperatorTokenExtension.ValidateOperator(value.ToString(), c))
                                        {
                                            yield return new SignalScriptToken(SignalScriptTokenType.Operator, value.ToString());
                                            value.Length = 0;
                                        }
                                        value.Append(c);
                                        continue;
                                    }
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
                                case CommentParserState.OpenComment:
                                    state = CommentParserState.EndComment;
                                    continue;
                                case CommentParserState.Operator:
                                    if (value.Length == 1 && value.ToString() == "/")
                                    {
                                        value.Length = value.Length - 1;
                                        state = CommentParserState.OpenComment;
                                        continue;
                                    }
                                    if (!OperatorTokenExtension.ValidateOperator(value.ToString(), c))
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Operator, value.ToString());
                                        value.Length = 0;
                                    }
                                    value.Append(c);
                                    continue;
                                default:
                                    if (value.Length > 0)
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Value, value.ToString());
                                        value.Length = 0;
                                    }
                                    value.Append(c);
                                    state = CommentParserState.Operator;
                                    continue;
                            }
                        case ';': case '{': case '}': case '(': case ')': case '\t': case ' ': case ',':
                            switch (state)
                            {
                                case CommentParserState.OpenComment:
                                    continue;
                                default:
                                    if (value.Length > 0)
                                    {
                                        yield return new SignalScriptToken((state == CommentParserState.Operator ? SignalScriptTokenType.Operator : SignalScriptTokenType.Value), value.ToString());
                                        value.Length = 0;
                                    }
                                    lineContent = true;
                                    state = CommentParserState.None;
                                    yield return new SignalScriptToken((SignalScriptTokenType)c, c);
                                    continue;
                            }
                        case '|': case '&': case '^': case '!': case '+': case '-': case '%': case '#': case '<': case '>': case '=':
                            switch (state)
                            {
                                case CommentParserState.OpenComment:
                                    continue;
                                case CommentParserState.Operator:
                                    if (!OperatorTokenExtension.ValidateOperator(value.ToString(), c))
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Operator, value.ToString());
                                        value.Length = 0;
                                    }
                                    value.Append(c);
                                    continue;
                                default:
                                    if (value.Length > 0)
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Value, value.ToString());
                                        value.Length = 0;
                                    }
                                    value.Append(c);
                                    state = CommentParserState.Operator;
                                    continue;
                            }
                        default:
                            switch (state)
                            {
                                case CommentParserState.OpenComment:
                                    continue;
                                case CommentParserState.Operator:
                                    if (value.Length > 0)
                                    {
                                        yield return new SignalScriptToken(SignalScriptTokenType.Operator, value.ToString());
                                        value.Length = 0;
                                    }
                                    state = CommentParserState.None;
                                    value.Append(char.ToUpper(c));
                                    continue;
                                default:
                                    state = CommentParserState.None;
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
                        lineContent = true;
                        yield return new SignalScriptToken((state == CommentParserState.Operator ? SignalScriptTokenType.Operator : SignalScriptTokenType.Value), value.ToString());
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

    #region Script Tokens
    internal class ScriptToken
    {
        public virtual string Token { get; set; }

        public override string ToString()
        {
            return Token;
        }
    }

    internal enum OperatorType
    {
        Negator,
        Logical,
        Equality,
        Assignment,
        Operation,
        Other,
    }

    internal class OperatorToken : ScriptToken
    {
        public OperatorToken(string token)
        {
            Token = token;

            switch(token)
            {
                case "NOT":
                case "!":
                    OperatorType = OperatorType.Negator;
                    break;
                case "AND": case "OR": case "||": case "&&": case "EOR": case "^":
                    OperatorType = OperatorType.Logical;
                    break;
                case "=": case "#=": case "+=": case "-=": case "*=": case "/=": case "/#=": case "%=":
                    OperatorType = OperatorType.Assignment;
                    break;
                case "-": case "*": case "+": case "/": case "/#": case "%": case "DIV": case "MOD":
                    OperatorType = OperatorType.Operation;
                    break;
                case ">": case ">#": case ">=": case ">=#": case "<": case "<#": case "<=": case "<=#": case "==": case "==#": case "!=": case "!=#":
                    OperatorType = OperatorType.Equality;
                    break;
                default:
                    OperatorType = OperatorType.Other;
                    Trace.TraceWarning($"sigscr-file : Invalid operator token {token}");
                    break;
            }
        }

        public OperatorType OperatorType { get; private set; }
    }

    internal class ScriptStatement : ScriptToken
    {
        public int LineNumber { get; set; }

        public List<ScriptToken> Tokens { get; private set; } = new List<ScriptToken>();

        public virtual void Add(ScriptToken token)
        {
            Tokens.Add(token);
        }

        public override string Token
        {
            get { return ToString(); }
        }

        public override string ToString()
        {
            if (Tokens.Count == 0)
                return string.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (ScriptToken item in Tokens)
            {
                if (item is BlockToken)
                {
                    builder.Append("\r\n");
                    builder.Append(item.ToString());
                    builder.Append("\r\n");
                }
                else
                {
                    builder.Append(item.ToString());
                    builder.Append(' ');
                }
            }
            builder.Length -= builder[builder.Length - 1] == '\n' ? 2 : 1;
            return builder.ToString();
        }
    }

    internal class ConditionalStatementTerm : ScriptStatement
    {
        private static readonly ScriptToken ifToken = new ScriptToken() { Token = "IF" };
        private static readonly ScriptToken elseifToken = new ScriptToken() { Token = "ELSEIF" };
        private static readonly ScriptToken elseToken = new ScriptToken() { Token = "ELSE" };

        public static ScriptToken IF { get; } = ifToken;
        public static ScriptToken ELSEIF { get; } = elseifToken;
        public static ScriptToken ELSE { get; } = elseToken;

        public ScriptToken ConditionalToken { get { return Tokens.Count > 0 ? Tokens[0] : null; } }
        public BracketToken Condition { get { return (this.Else ? null : (Tokens.Count > 1 ? Tokens[1] as BracketToken : null)); } }
        public ScriptStatement Statement { get { return (Else ? Tokens.Count > 1 ? Tokens[1] : null : Tokens.Count > 2 ? Tokens[2] : null) as ScriptStatement; } }

        public override void Add(ScriptToken token)
        {
            //If this is a simple token, not a block, it might be a statement which is not encapsulated, so we will create a statement for the token
            if (!(token is ScriptStatement) && (Else || Condition != null))
            {
                if (Statement == null)
                    base.Add(new ScriptStatement());
                Statement.Tokens.Add(token);
            }
            else
            {
                base.Add(token);
            }
        }

        internal bool Else { get { return (Tokens.Count > 0 && Tokens[0] == elseToken); } }
    }

    internal abstract class ScriptBlockBase : ScriptToken
    {
        public List<ScriptStatement> Statements { get; } = new List<ScriptStatement>();

        public ScriptStatement CurrentStatement { get; private set; } = new ScriptStatement();

        private ConditionalBlockToken conditionalBlock;

        public virtual void CompleteCurrentStatement(int lineNumber)
        {
            if (CurrentStatement.Tokens.Count > 0)
            {
                if (((CurrentStatement is ConditionalStatementTerm) && (CurrentStatement as ConditionalStatementTerm).Statement == null))
                {
                }
                else
                {
                    if (null != conditionalBlock)
                    {
                        CurrentStatement.LineNumber = lineNumber;
                        Statements.Add(new ScriptStatement() { Tokens = { conditionalBlock } });
                        CurrentStatement = new ScriptStatement();
                        conditionalBlock = null;
                    }
                    else
                    {
                        CurrentStatement.LineNumber = lineNumber;
                        Statements.Add(CurrentStatement);
                        CurrentStatement = new ScriptStatement();
                    }
                }
            }
        }
        public void StartConditionalStatement()
        {
            if ((CurrentStatement is ConditionalStatementTerm)) // this is the If Token for the Else Token before, so replace as ElseIf
            {
                CurrentStatement.Tokens[0] = ConditionalStatementTerm.ELSEIF;
            }
            else
            {
                conditionalBlock = new ConditionalBlockToken();
                CurrentStatement = conditionalBlock.Statements[0];  //new If statement
            }
        }

        public void StartAlternateStatement(int lineNumber)
        {
            //check if there is a prior If or ElseIf
            conditionalBlock = Statements[Statements.Count - 1].Tokens[0] as ConditionalBlockToken;
            Statements.RemoveAt(Statements.Count - 1);
            if (null == conditionalBlock)
                throw new InvalidDataException($"Missing If or Else If statement before Else in Line {lineNumber}");

            CurrentStatement = new ConditionalStatementTerm() { Tokens = { ConditionalStatementTerm.ELSE } };
            conditionalBlock.Statements.Add(CurrentStatement as ConditionalStatementTerm);
        }

        public override string Token { get { return ToString(); } }

        public override string ToString()
        {
            if (Statements.Count == 0)
                return string.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (ScriptToken statement in Statements)
            {
                builder.Append(statement.ToString());
                builder.Append("\r\n");
            }
            if (builder.Length > 0)
                builder.Length -= 2;
            return builder.ToString();
        }
    }

    internal class ScriptBlock : ScriptBlockBase
    {
        public string ScriptName { get; set; } = string.Empty;

        public override string ToString()
        {
            return base.ToString();
        }
    }

    internal class BlockToken : ScriptBlockBase
    {
        public override string ToString()
        {
            return $"{{\r\n{base.ToString()}\r\n}}";
        }
    }

    internal class BracketToken : ScriptBlockBase
    {
        public override string ToString()
        {
            return $"({base.ToString()})";
        }
    }

    internal class ConditionalBlockToken : ScriptBlockBase
    {
        public ConditionalBlockToken()
        {
            Statements.Add(new ConditionalStatementTerm() { Tokens = { ConditionalStatementTerm.IF } });
        }
    }

    #endregion

    internal class ScriptStatementParser : IEnumerable<ScriptBlock>
    {
        private readonly SignalScriptTokenizer tokenizer;

        public int LineNumber { get { return tokenizer.LineNumber; } }

        public ScriptStatementParser(TextReader reader)
        {
            this.tokenizer = new SignalScriptTokenizer(reader);
        }

        internal enum ScriptParserState
        {
            None,
            ScriptName,
            Remark,
        }

        public IEnumerator<ScriptBlock> GetEnumerator()
        {
            bool inScript = false;
            ScriptParserState parserState = ScriptParserState.None;
            Stack<ScriptBlockBase> blockStack = new Stack<ScriptBlockBase>();
            ScriptBlockBase currentBlock = null;

            foreach (SignalScriptToken token in tokenizer)
            {
                if (inScript)
                {
                    switch (token.Type)
                    {
                        case SignalScriptTokenType.StatementEnd:
                            currentBlock.CompleteCurrentStatement(tokenizer.LineNumber);
                            parserState = ScriptParserState.None;
                            continue;
                        case SignalScriptTokenType.LineEnd:
                            if (parserState == ScriptParserState.ScriptName)
                                parserState = ScriptParserState.None;
                            continue;
                        case SignalScriptTokenType.BlockOpen:
                            blockStack.Push(currentBlock);
                            currentBlock = new BlockToken();
                            continue;
                        case SignalScriptTokenType.BracketOpen:
                            blockStack.Push(currentBlock);
                            currentBlock = new BracketToken();
                            continue;
                        case SignalScriptTokenType.BlockClose:
                        case SignalScriptTokenType.BracketClose:
                            if ((token.Type == SignalScriptTokenType.BracketClose && currentBlock is BracketToken) ||
                            (token.Type == SignalScriptTokenType.BlockClose && currentBlock is BlockToken))
                            {
                                currentBlock.CompleteCurrentStatement(tokenizer.LineNumber);
                                ScriptBlockBase outer = blockStack.Pop();
                                outer.CurrentStatement.Add(currentBlock);
                                if (outer.CurrentStatement is ConditionalStatementTerm)
                                {
                                    outer.CompleteCurrentStatement(tokenizer.LineNumber);
                                }
                                currentBlock = outer;
                                continue;
                            }
                            else //something wrong here
                                throw new InvalidDataException($"Error in signal script data, matching element not found in line {tokenizer.LineNumber}.");
                        case SignalScriptTokenType.Value:
                            if (parserState == ScriptParserState.ScriptName)        //script names may include any value token or operator, only ended by line end
                            {
                                (currentBlock as ScriptBlock).ScriptName += token.Value;
                                continue;
                            }

                            switch (token.Value)
                            {
                                case "REM":
                                    if (blockStack.Count > 0) //something wrong here
                                        throw new InvalidDataException($"Error in signal script, matching element not found before new script in line {tokenizer.LineNumber}.");
                                    //end current script
                                    currentBlock.CompleteCurrentStatement(tokenizer.LineNumber);
                                    yield return currentBlock as ScriptBlock;
                                    inScript = false;
                                    parserState = ScriptParserState.Remark;
                                    continue;
                                case "SCRIPT":
                                    if (blockStack.Count > 0) //something wrong here
                                        throw new InvalidDataException($"Error in signal script, matching element not found before new script in line {tokenizer.LineNumber}.");
                                    currentBlock.CompleteCurrentStatement(tokenizer.LineNumber);
                                    yield return currentBlock as ScriptBlock;
                                    if (parserState == ScriptParserState.Remark)
                                        parserState = ScriptParserState.None;
                                    else
                                    {
                                        currentBlock = new ScriptBlock();
                                        parserState = ScriptParserState.ScriptName;
                                        inScript = true;
                                    }
                                    continue;
                                case "IF":
                                    currentBlock.StartConditionalStatement();
                                    continue;
                                case "ELSE":
                                    currentBlock.StartAlternateStatement(tokenizer.LineNumber);
                                    continue;
                                case "AND":
                                case "OR":
                                case "NOT":
                                case "MOD":
                                case "DIV":
                                    currentBlock.CurrentStatement.Add(new OperatorToken(token.Value));
                                    continue;
                                default:
                                    currentBlock.CurrentStatement.Add(new ScriptToken() { Token = token.Value });
                                    continue;
                            }
                        case SignalScriptTokenType.Separator:
                        case SignalScriptTokenType.Tab:
                        case SignalScriptTokenType.Comma:
                            continue;
                        case SignalScriptTokenType.Operator:
                            if (parserState == ScriptParserState.ScriptName)
                            {
                                (currentBlock as ScriptBlock).ScriptName += token.Value;
                            }
                            else
                            {
                                currentBlock.CurrentStatement.Add(new OperatorToken(token.Value));
                            }
                            continue;
                        default:
                            throw new InvalidOperationException($"Unknown token type {token.Type} containing '{token.Value}' in line {tokenizer.LineNumber}");
                    }
                }
                else if (token.Type == SignalScriptTokenType.Value)
                {
                    switch (token.Value)
                    {
                        case "REM":
                            parserState = ScriptParserState.Remark;
                            continue;
                        case "SCRIPT":
                            if (parserState == ScriptParserState.Remark)
                                parserState = ScriptParserState.None;
                            else // start new script
                            {
                                currentBlock = new ScriptBlock();
                                parserState = ScriptParserState.ScriptName;
                                inScript = true;
                            }
                            continue;
                    }
                }
            }
            currentBlock.CompleteCurrentStatement(tokenizer.LineNumber);
            yield return currentBlock as ScriptBlock;
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

        private static readonly IDictionary<string, SCRTermCondition> TranslateConditions = new Dictionary<string, SCRTermCondition>
            {
                { ">", SCRTermCondition.GT },
                { ">#", SCRTermCondition.GT },
                { ">=", SCRTermCondition.GE },
                { ">=#", SCRTermCondition.GE },
                { "<", SCRTermCondition.LT },
                { "<#", SCRTermCondition.LT },
                { "<=", SCRTermCondition.LE },
                { "<=#", SCRTermCondition.LE },
                { "==", SCRTermCondition.EQ },
                { "==#", SCRTermCondition.EQ },
                { "!=", SCRTermCondition.NE },
                { "!=#", SCRTermCondition.NE },
            };

        private static readonly IDictionary<string, SCRTermOperator> TranslateOperator = new Dictionary<string, SCRTermOperator>
            {
                { "?", SCRTermOperator.NONE },
                { "-", SCRTermOperator.MINUS },  // needs to come first to avoid it being interpreted as range separator
                { "*", SCRTermOperator.MULTIPLY },
                { "+", SCRTermOperator.PLUS },
                { "/", SCRTermOperator.DIVIDE },
                { "%", SCRTermOperator.MODULO }
            };

        private static readonly IDictionary<string, SCRAndOr> TranslateAndOr = new Dictionary<string, SCRAndOr>
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

                        ScriptStatementParser scriptParser = new ScriptStatementParser(stream);
                        foreach (ScriptBlock script in scriptParser)
                        {
                            #region DEBUG
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "\n===============================\n");
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", "\nNew Script : " + script.ScriptName + "\n");
#endif
#if DEBUG_PRINT_OUT
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n===============================\n");
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\nNew Script : " + script.ScriptName + "\n");
#endif
                            #endregion
                            AssignScriptToSignalType(new SCRScripts(script, orSignalTypes, orNormalSubtypes),
                                signalTypes, scriptParser.LineNumber, fileName);
                            Trace.Write("s");
                        }
                        #region DEBUG
#if DEBUG_PRINT_OUT
                        // print processed details 
                        foreach (KeyValuePair<SignalType, SCRScripts> item in Scripts)
                        {
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Script : " + item.Value.ScriptName + "\n\n");
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", PrintScript(item.Value.Statements));
                            File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n=====================\n");
                        }
#endif
                        #endregion
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
        #region DEBUG_PRINT_OUT
#if DEBUG_PRINT_OUT
        //================================================================================================//
        //
        // print processed script - for DEBUG purposes only
        //
        //================================================================================================//

        private string PrintScript(ArrayList statements)
        {
            bool function = false;
            List<int> Sublevels = new List<int>();
            StringBuilder builder = new StringBuilder();

            foreach (object statement in statements)
            {

                // process statement lines

                if (statement is SCRScripts.SCRStatement scrStatement)
                {
                    builder.Append("Statement : \n");
                    builder.Append(scrStatement.AssignType.ToString() + "[" + scrStatement.AssignParameter.ToString() + "] = ");

                    foreach (SCRScripts.SCRStatTerm scrTerm in scrStatement.StatementTerms)
                    {
                        if (scrTerm.TermLevel > 0)
                        {
                            builder.Append(" <SUB" + scrTerm.TermLevel.ToString() + "> ");
                        }
                        function = false;
                        if (scrTerm.Function != SCRExternalFunctions.NONE)
                        {
                            builder.Append(scrTerm.Function.ToString() + "(");
                            function = true;
                        }

                        if (scrTerm.PartParameter != null)
                        {
                            foreach (SCRScripts.SCRParameterType scrParam in scrTerm.PartParameter)
                            {
                                builder.Append(scrParam.PartType + "[" + scrParam.PartParameter + "] ,");
                            }
                        }

                        if (scrTerm.TermNumber != 0)
                        {
                            builder.Append(" SUBTERM_" + scrTerm.TermNumber.ToString());
                        }

                        if (function)
                        {
                            builder.Append(")");
                        }
                        builder.Append(" -" + scrTerm.TermOperator.ToString() + "- \n");
                    }

                    builder.Append("\n\n");
                }

                // process conditions line

                if (statement is SCRScripts.SCRConditionBlock scrCondBlock)
                {
                    builder.Append("\nCondition : \n");

                    builder.Append(PrintConditionArray(scrCondBlock.Conditions));

                    builder.Append("\nIF Block : \n");
                    builder.Append(PrintScript(scrCondBlock.IfBlock.Statements));

                    if (scrCondBlock.ElseIfBlock != null)
                    {
                        foreach (SCRScripts.SCRBlock tempBlock in scrCondBlock.ElseIfBlock)
                        {
                            builder.Append("\nStatements in ELSEIF : " + tempBlock.Statements.Count + "\n");
                            builder.Append("Elseif Block : \n");
                            builder.Append(PrintScript(tempBlock.Statements));
                        }
                    }
                    if (scrCondBlock.ElseBlock != null)
                    {
                        builder.Append("\nElse Block : \n");
                        builder.Append(PrintScript(scrCondBlock.ElseBlock.Statements));
                    }
                    builder.Append("\nEnd IF Block : \n");
                }
            }
            return builder.ToString();
        }// printscript

        //================================================================================================//
        //
        // print condition info - for DEBUG purposes only
        //
        //================================================================================================//

        private string PrintConditionArray(ArrayList Conditions)
        {
            StringBuilder builder = new StringBuilder();

            foreach (object condition in Conditions)
            {
                if (condition is SCRScripts.SCRConditions)
                {
                    builder.Append(PrintCondition((SCRScripts.SCRConditions)condition));
                }
                else if (condition is SCRAndOr andor)
                {
                    builder.Append(andor.ToString() + "\n");
                }
                else if (condition is SCRNegate)
                {
                    builder.Append("NEGATED : \n");
                }
                else
                {
                    builder.Append(PrintConditionArray((ArrayList)condition));
                }
            }
            return builder.ToString();
        }// printConditionArray

        //================================================================================================//
        //
        // print condition statement - for DEBUG purposes only
        //
        //================================================================================================//

        private string PrintCondition(SCRScripts.SCRConditions condition)
        {
            StringBuilder builder = new StringBuilder();

            bool function = false;
            if (condition.Term1.Negated)
            {
                builder.Append("NOT : ");
            }
            if (condition.Term1.Function != SCRExternalFunctions.NONE)
            {
                builder.Append(condition.Term1.Function.ToString() + "(");
                function = true;
            }

            if (condition.Term1.PartParameter != null)
            {
                foreach (SCRScripts.SCRParameterType scrParam in condition.Term1.PartParameter)
                {
                    builder.Append(scrParam.PartType + "[" + scrParam.PartParameter + "] ,");
                }
            }
            else
            {
                builder.Append(" 0 , ");
            }

            if (function)
            {
                builder.Append(")");
            }

            builder.Append(" -- " + condition.Condition.ToString() + " --\n");

            if (condition.Term2 != null)
            {
                function = false;
                if (condition.Term2.Negated)
                {
                    builder.Append("NOT : ");
                }
                if (condition.Term2.Function != SCRExternalFunctions.NONE)
                {
                    builder.Append(condition.Term2.Function.ToString() + "(");
                    function = true;
                }

                if (condition.Term2.PartParameter != null)
                {
                    foreach (SCRScripts.SCRParameterType scrParam in condition.Term2.PartParameter)
                    {
                        builder.Append(scrParam.PartType + "[" + scrParam.PartParameter + "] ,");
                    }
                }
                else
                {
                    builder.Append(" 0 , ");
                }

                if (function)
                {
                    builder.Append(")");
                }
                builder.Append("\n");
            }
            return builder.ToString();
        }// printcondition
#endif
        #endregion

        /// <summary>
        /// Links the script to the required signal type
        /// </summary>
        private void AssignScriptToSignalType(SCRScripts script, IDictionary<string, SignalType> signalTypes, int currentLine, string fileName)
        {
            bool isValid = false;
            string scriptName = script.ScriptName;
            // try and find signal type with same name as script
            if (signalTypes.TryGetValue(script.ScriptName.ToLower(), out SignalType signalType))
            {
                if (Scripts.ContainsKey(signalType))
                {
                    Trace.TraceWarning($"Ignored duplicate SignalType script {scriptName} in {0} {fileName} before {currentLine}");
                }
                else
                {
                    #region DEBUG
#if DEBUG_PRINT_IN
                    File.AppendAllText(din_fileLoc + @"sigscr.txt", "Adding script : " + signalType.Name + "\n");
#endif
                    #endregion
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
                        #region DEBUG
#if DEBUG_PRINT_IN
                        File.AppendAllText(din_fileLoc + @"sigscr.txt", "Adding script : " + currentSignal.Value.Script + " to " + currentSignal.Value.Name + "\n");
#endif
                        #endregion
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

        public class SCRScripts
        {
            private IDictionary<string, int> localFloats;

            public int TotalLocalFloats { get { return localFloats.Count; } }

            public ArrayList Statements { get; private set; }
            //public List<Statements> { get; private set; }

            public string ScriptName { get; private set; }

            internal SCRScripts(ScriptBlock script, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
            {
                localFloats = new Dictionary<string, int>();
                Statements = new ArrayList();
                ScriptName = script.ScriptName;
                int statementLine = 0;
                int maxCount = script.Statements.Count;
                #region DEBUG_PRINT_IN
#if DEBUG_PRINT_IN
                // print inputlines
                File.AppendAllText(din_fileLoc + @"sigscr.txt", script.ToString() + '\n');
                File.AppendAllText(din_fileLoc + @"sigscr.txt", "\n+++++++++++++++++++++++++++++++++++\n\n");

#endif
                #endregion
                // Skip external floats (exist automatically)
                while (script.Statements[statementLine].Tokens[0].Token == "EXTERN" && script.Statements[statementLine].Tokens[1].Token == "FLOAT" && statementLine++ < maxCount) ;

                //// Process floats : build list with internal floats
                while ((script.Statements[statementLine].Tokens[0].Token == "FLOAT") && statementLine < maxCount)
                {
                    string floatString = script.Statements[statementLine].Tokens[1].Token;
                    if (!localFloats.ContainsKey(floatString))
                    {
                        localFloats.Add(floatString, localFloats.Count);
                    }
                    statementLine++;
                }

                #region DEBUG_PRINT_OUT
#if DEBUG_PRINT_OUT
                // print details of internal floats

                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "\n\nFloats : \n");
                foreach (KeyValuePair<string, int> item in localFloats)
                {
                    File.AppendAllText(dout_fileLoc + @"scriptproc.txt", $"Float : {item.Key} = {item.Value}\n");
                }
                File.AppendAllText(dout_fileLoc + @"scriptproc.txt", "Total : " + localFloats.Count.ToString() + "\n\n\n");
#endif
                #endregion
                script.Statements.RemoveRange(0, statementLine);

                foreach (ScriptStatement statement in script.Statements)
                {
                    if (statement.Tokens[0] is ConditionalBlockToken)
                    {
                        SCRConditionBlock condition = new SCRConditionBlock(statement.Tokens[0] as ConditionalBlockToken, localFloats, orSignalTypes, orNormalSubtypes);
                        Statements.Add(condition);
                    }
                    else
                    {
                        SCRStatement scrStatement = new SCRStatement(statement, localFloats, orSignalTypes, orNormalSubtypes);
                        Statements.Add(scrStatement);
                    }
                }
            }// constructor

            internal static SCRParameterType ParameterFromToken(ScriptToken token, int lineNumber, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
            {

                int index;

                // try constant
                if (int.TryParse(token.Token, out int constantInt))
                {
                    return new SCRParameterType(SCRTermType.Constant, constantInt);
                }
                // try external float
                else if (Enum.TryParse(token.Token, true, out SCRExternalFloats externalFloat))
                {
                    return new SCRParameterType(SCRTermType.ExternalFloat, (int)externalFloat);
                }
                // try local float
                else if (localFloats.TryGetValue(token.Token, out int localFloat))
                {
                    return new SCRParameterType(SCRTermType.LocalFloat, localFloat);
                }
                string[] definitions = token.Token.Split(new char[] { '_' }, 2);
                if (definitions.Length == 2)
                    switch (definitions[0])
                    {
                        // try blockstate
                        case "BLOCK":
                            if (Enum.TryParse(definitions[1], out MstsBlockState blockstate))
                            {
                                return new SCRParameterType(SCRTermType.Block, (int)blockstate);
                            }
                            else
                            {
                                Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown Blockstate : {definitions[1]} \n");
#if DEBUG_PRINT_IN
                                File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Unknown Blockstate : {token.Token} \n");
#endif
                            }
                            break;
                        // try SIGASP definition
                        case "SIGASP":
                            if (Enum.TryParse(definitions[1], out MstsSignalAspect aspect))
                            {
                                return new SCRParameterType(SCRTermType.Sigasp, (int)aspect);
                            }
                            else
                            {
                                Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown Aspect : {definitions[1]} \n");
#if DEBUG_PRINT_IN
                                File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Unknown Aspect : {token.Token} \n");
#endif
                            }
                            break;
                        // try SIGFN definition
                        case "SIGFN":
                            index = orSignalTypes.IndexOf(definitions[1]);
                            if (index != -1)
                            {
                                return new SCRParameterType(SCRTermType.Sigfn, index);
                            }
                            else
                            {
                                Trace.TraceWarning($"sigscr-file line {lineNumber.ToString()} : Unknown SIGFN Type : {definitions[1]} \n");
#if DEBUG_PRINT_IN
                                File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Unknown Type : {token.Token} \n");
#endif
                            }
                            break;
                        // try ORSubtype definition
                        case "ORSUBTYPE":
                            index = orNormalSubtypes.IndexOf(definitions[1]);
                            if (index != -1)
                            {
                                return new SCRParameterType(SCRTermType.ORNormalSubtype, index);
                            }
                            else
                            {
                                Trace.TraceWarning($"sigscr-file line {lineNumber} : Unknown ORSUBTYPE : {definitions[1]} \n");
#if DEBUG_PRINT_IN
                                File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Unknown Type : {token.Token} \n");
#endif
                            }
                            break;
                        // try SIGFEAT definition
                        case "SIGFEAT":
                            index = SignalShape.SignalSubObj.SignalSubTypes.IndexOf(definitions[1]);
                            if (index != -1)
                            {
                                return new SCRParameterType(SCRTermType.Sigfeat, index);
                            }
                            else
                            {
                                Trace.TraceWarning($"sigscr-file line {lineNumber} : Unknown SubType : {definitions[1]} \n");
#if DEBUG_PRINT_IN
                                File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Unknown SubType : {token.Token} \n");
#endif
                            }
                            break;
                        default:
                            // nothing found - set error
                            Trace.TraceWarning($"sigscr-file line {lineNumber} : Unknown parameter in statement : {token.Token}");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Unknown parameter : {token.Token} \n");
#endif
                            break;
                    }
                return new SCRParameterType(SCRTermType.Constant, 0);
            }//process_TermPart

            //================================================================================================//
            //
            // process IF condition line - split into logic parts
            //
            //================================================================================================//
            internal static ArrayList ParseConditions(ScriptStatement statement, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
            {
                ArrayList result = new ArrayList();
                SCRAndOr logicalOperator = SCRAndOr.NONE;
                while (statement.Tokens.Count > 0)
                {
                    if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Logical)
                    {
                        if (TranslateAndOr.TryGetValue(statement.Tokens[0].Token, out logicalOperator))
                        {
                            result.Add(logicalOperator);
                        }
                        else
                        {
                            Trace.TraceWarning($"sigscr-file line {statement.LineNumber} : Invalid logical operator in : {statement.Token[0]}");
                        }
                        statement.Tokens.RemoveAt(0);
                    }
                    else if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Negator && statement.Tokens[1] is ScriptBlockBase)
                    {
                        result.Add(SCRNegate.NEGATE);
                        statement.Tokens.RemoveAt(0);
                    }
                    //Conditions are dedicated blocks, but always separated by logical operators
                    else if (statement.Tokens[0] is ScriptBlockBase) //process sub block
                    {
                        result.AddRange(ParseConditions((statement.Tokens[0] as ScriptBlockBase).Statements[0], localFloats, orSignalTypes, orNormalSubtypes));
                        //recurse in the block
                        statement.Tokens.RemoveAt(0);
                    }
                    else //single term
                    {
                        SCRConditions condition = new SCRConditions(statement, localFloats, orSignalTypes, orNormalSubtypes);
                        result.Add(condition);
                    }
                }
                // TODO: This can be removed, only for debug output compatibility
                if (logicalOperator != SCRAndOr.NONE)
                    result.Add(logicalOperator);
                return result;
            }

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
                public List<SCRStatTerm> StatementTerms { get; private set; } = new List<SCRStatTerm>();

                public SCRTermType AssignType { get; private set; }

                public int AssignParameter { get; private set; }

                internal SCRStatement(ScriptStatement statement, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    AssignType = SCRTermType.Invalid;

                    //TODO: may want to process other Assignment Operations (+=, -= etc)
                    if (statement.Tokens.Count > 1 && (statement.Tokens[1] as OperatorToken).OperatorType == OperatorType.Assignment)
                    {
                        if (Enum.TryParse(statement.Tokens[0].Token, out SCRExternalFloats result))
                        {
                            AssignParameter = (int)result;
                            AssignType = SCRTermType.ExternalFloat;
                        }
                        else if (localFloats.TryGetValue(statement.Tokens[0].Token, out int value))
                        {
                            AssignParameter = value;
                            AssignType = SCRTermType.LocalFloat;
                        }
                        // Assignment term
                        statement.Tokens.RemoveRange(0, 2);
                    }
                    ProcessScriptStatement(statement, 0, localFloats, orSignalTypes, orNormalSubtypes);
                }

                private void ProcessScriptStatement(ScriptStatement statement, int level, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    string operatorString = string.Empty;
                    int termNumber = level;
                    bool negated = false;

                    while (statement.Tokens.Count > 0)
                    {
                        negated = false;
                        if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Operation)
                        {
                            operatorString = statement.Tokens[0].Token;
                            statement.Tokens.RemoveAt(0);
                            if (statement.Tokens.Count == 0)
                            {
                                Trace.TraceWarning($"sigscr-file line {statement.LineNumber} : Invalid statement syntax : {statement.ToString()}");
                                StatementTerms.Clear();
                                return;
                            }
                        }
                        else
                            operatorString = string.Empty;

                        if (statement.Tokens[0] is ScriptBlockBase)
                        {
                            termNumber++;
                            //recurse through inner statemement
                            SCRStatTerm term = new SCRStatTerm(termNumber, level, operatorString);
                            StatementTerms.Add(term);

                            foreach (ScriptStatement subStatement in (statement.Tokens[0] as ScriptBlockBase).Statements)
                            {
                                // recursive call to process at sublevel
                                ProcessScriptStatement(subStatement, termNumber, localFloats, orSignalTypes, orNormalSubtypes);
                            }
                        }
                        else
                        {
                            if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Negator)
                            {
                                statement.Tokens.RemoveAt(0);
                                negated = true;
                            }
                            if (statement.Tokens.Count > 1 && Enum.TryParse(statement.Tokens[0].Token, out SCRExternalFunctions externalFunctionsResult) && statement.Tokens[1] is ScriptBlockBase)   //check if it is a Sub Function ()
                            {
                                StatementTerms.Add(
                                    new SCRStatTerm(externalFunctionsResult, statement.Tokens[1] as ScriptBlockBase, termNumber, operatorString, negated, localFloats, orSignalTypes, orNormalSubtypes));
                                statement.Tokens.RemoveAt(0);
                            }
                            else
                            {
                                StatementTerms.Add(
                                    new SCRStatTerm(statement.Tokens[0], termNumber, operatorString, statement.LineNumber, negated, localFloats, orSignalTypes, orNormalSubtypes));
                            }
                        }
                        statement.Tokens.RemoveAt(0);
                    }
                }
            }


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

                public bool Negated { get; private set; } 

                public int TermNumber { get; private set; }

                public int TermLevel { get; private set; }

                // SubLevel term
                internal SCRStatTerm(int termNumber, int level, string operatorTerm)
                {
                    // sublevel definition
                    this.TermNumber = termNumber;
                    this.TermLevel = level;

                    TermOperator = TranslateOperator.TryGetValue(operatorTerm, out SCRTermOperator termOperator) ? termOperator : SCRTermOperator.NONE;
                } // constructor

                // Function term
                internal SCRStatTerm(SCRExternalFunctions externalFunction, ScriptBlockBase block, int subLevel, string operatorTerm, bool negated, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    Negated = negated;
                    TermLevel = subLevel;
                    Function = externalFunction;
                    TermOperator = TranslateOperator.TryGetValue(operatorTerm, out SCRTermOperator tempOperator) ? tempOperator : SCRTermOperator.NONE;

                    List<SCRParameterType> result = new List<SCRParameterType>();
                    ScriptStatement statement = block.Statements.Count > 0 ? block.Statements[0] : null;

                    while (statement?.Tokens.Count > 0)
                    {
                        if (statement.Tokens.Count > 1 && Enum.TryParse(statement.Tokens[0].Token, out SCRExternalFunctions externalFunctionsResult) && statement.Tokens[1] is ScriptBlockBase)   //check if it is a Function ()
                        {
                            // TODO Nested Function Call in Parameter not supported
                            throw new NotImplementedException($"Nested function call in parameter {statement.Token} not supported at line {statement.LineNumber}");
                            //SCRParameterType parameter = ParameterFromToken(statement.Tokens[0], lineNumber, localFloats, orSignalTypes, orNormalSubtypes);
                            //StatementTerms.Add(
                            //    new SCRStatTerm(externalFunctionsResult, statement.Tokens[1] as ScriptBlockBase, termNumber, operatorString, statement.LineNumber, localFloats, orSignalTypes, orNormalSubtypes));
                            //statement.Tokens.RemoveAt(0);
                        }
                        else
                        {
                            SCRParameterType parameter = ParameterFromToken(statement.Tokens[0], statement.LineNumber, localFloats, orSignalTypes, orNormalSubtypes);
                            result.Add(parameter);
                        }
                        statement.Tokens.RemoveAt(0);
                    }
                    PartParameter = result.Count > 0 ? result.ToArray() : null;
                }

                internal SCRStatTerm(ScriptToken token, int subLevel, string operatorTerm, int lineNumber, bool negated, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    TermLevel = subLevel;
                    Negated = negated;

                    if (token.Token == "RETURN")
                    {
                        Function = SCRExternalFunctions.RETURN;
                    }
                    else
                    {
                        Function = SCRExternalFunctions.NONE;
                        PartParameter = new SCRParameterType[1];
                        PartParameter[0] = ParameterFromToken(token, lineNumber, localFloats, orSignalTypes, orNormalSubtypes);
                        TermOperator = TranslateOperator.TryGetValue(operatorTerm, out SCRTermOperator tempOperator) ? tempOperator : SCRTermOperator.NONE;
                    }
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

                public SCRParameterType(SCRTermType type, int value)
                {
                    PartType = type;
                    PartParameter = value;
                }
            }

            //================================================================================================//
            //
            // class SCRConditionBlock
            //
            //================================================================================================//
            public class SCRConditionBlock
            {
                public ArrayList Conditions { get; private set; }

                public SCRBlock IfBlock { get; private set; }

                public List<SCRBlock> ElseIfBlock { get; private set; }

                public SCRBlock ElseBlock { get; private set; }

                internal SCRConditionBlock(ConditionalBlockToken conditionalBlock, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    ConditionalStatementTerm term = conditionalBlock.Statements[0] as ConditionalStatementTerm;
                    //IF-Term
                    Conditions = ParseConditions(term.Condition.Statements[0], localFloats, orSignalTypes, orNormalSubtypes);
                    IfBlock = new SCRBlock(term.Statement, localFloats, orSignalTypes, orNormalSubtypes);
                    conditionalBlock.Statements.RemoveAt(0);

                    //ElseIf-Term
                    while ((conditionalBlock.Statements.Count > 0) && (term = conditionalBlock.Statements[0] as ConditionalStatementTerm).ConditionalToken == ConditionalStatementTerm.ELSEIF)
                    {
                        if (ElseIfBlock == null)
                            ElseIfBlock = new List<SCRBlock>();
                        ElseIfBlock.Add(new SCRBlock(term, localFloats, orSignalTypes, orNormalSubtypes));
                        conditionalBlock.Statements.RemoveAt(0);
                    }

                    // Else-Block
                    if ((conditionalBlock.Statements.Count > 0 && (term = conditionalBlock.Statements[0] as ConditionalStatementTerm).Else))
                    {
                        ElseBlock = new SCRBlock(term.Statement, localFloats, orSignalTypes, orNormalSubtypes);
                        conditionalBlock.Statements.RemoveAt(0);
                    }
                }
                internal SCRConditionBlock(ConditionalStatementTerm conditionalStatement, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    Conditions = ParseConditions(conditionalStatement.Condition.Statements[0], localFloats, orSignalTypes, orNormalSubtypes);
                    IfBlock = new SCRBlock(conditionalStatement.Statement, localFloats, orSignalTypes, orNormalSubtypes);
                }

            } // class SCRConditionBlock

            //================================================================================================//
            //
            // class SCRConditions
            //
            //================================================================================================//
            public class SCRConditions
            {
                public SCRStatTerm Term1 { get; private set; }

                public SCRStatTerm Term2 { get; private set; }

                public SCRTermCondition Condition { get; private set; }

                internal SCRConditions(ScriptStatement statement, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    bool negated = false;

                    if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Negator)
                    {
                        statement.Tokens.RemoveAt(0);
                        negated = true;
                    }

                    if (statement.Tokens.Count > 1 && Enum.TryParse(statement.Tokens[0].Token, out SCRExternalFunctions externalFunctionsResult) && statement.Tokens[1] is ScriptBlockBase)   //check if it is a Sub Function ()
                    {
                        Term1 = new SCRStatTerm(externalFunctionsResult, statement.Tokens[1] as ScriptBlockBase, 0, string.Empty, negated, localFloats, orSignalTypes, orNormalSubtypes);
                        statement.Tokens.RemoveAt(0);
                    }
                    else
                    {
                        Term1 = new SCRStatTerm(statement.Tokens[0], 0, string.Empty, statement.LineNumber, negated, localFloats, orSignalTypes, orNormalSubtypes);
                    }
                    statement.Tokens.RemoveAt(0);

                    if (statement.Tokens.Count > 0)
                    {
                        if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Logical)
                        {
                            // if this is a unary (boolean)comparison
                            return;
                        }
                        //Comparison Operator
                        else if (TranslateConditions.TryGetValue(statement.Tokens[0].Token, out SCRTermCondition comparison))
                        {
                            Condition = comparison;
                        }
                        else
                        {
                            Trace.TraceWarning($"sigscr-file line {statement.LineNumber} : Invalid comparison operator in : {statement}");
#if DEBUG_PRINT_IN
                            File.AppendAllText(din_fileLoc + @"sigscr.txt", $"Invalid comparison operator in : {statement}\n"); ;
#endif
                        }
                        statement.Tokens.RemoveAt(0);

                        //Term 2
                        if ((statement.Tokens[0] as OperatorToken)?.OperatorType == OperatorType.Negator)
                        {
                            statement.Tokens.RemoveAt(0);
                            negated = true;
                        }

                        if (statement.Tokens.Count > 1 && Enum.TryParse(statement.Tokens[0].Token, out SCRExternalFunctions externalFunctionsResult2) && statement.Tokens[1] is ScriptBlockBase)   //check if it is a Sub Function ()
                        {
                            Term2 = new SCRStatTerm(externalFunctionsResult2, statement.Tokens[1] as ScriptBlockBase, 0, string.Empty, negated, localFloats, orSignalTypes, orNormalSubtypes);
                            statement.Tokens.RemoveAt(0);
                        }
                        else
                        {
                            Term2 = new SCRStatTerm(statement.Tokens[0], 0, string.Empty, statement.LineNumber, negated, localFloats, orSignalTypes, orNormalSubtypes);
                        }
                        statement.Tokens.RemoveAt(0);
                    }
                }
            } // class SCRConditions

            //================================================================================================//
            //
            // class SCRBlock
            //
            //================================================================================================//
            public class SCRBlock
            {
                public ArrayList Statements { get; private set; }

                internal SCRBlock(ScriptStatement statement, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    List<ScriptStatement> statements;

                    ScriptBlockBase block;
                    block = statement.Tokens[0] as BlockToken ?? new BlockToken() { Statements = { statement } };

                    while ((statements = block.Statements)?.Count == 1 && statements[0].Tokens[0] is BlockToken)    //remove nested empty blocks, primarily for legacy compatiblity
                        block = statements[0].Tokens[0] as BlockToken;

                    Statements = new ArrayList();

                    foreach (ScriptStatement item in statements)
                    {
                        if (item.Tokens[0] is ConditionalBlockToken)
                        {
                            SCRConditionBlock conditionBlock = new SCRConditionBlock(item.Tokens[0] as ConditionalBlockToken, localFloats, orSignalTypes, orNormalSubtypes);
                            Statements.Add(conditionBlock);
                        }
                        else
                        {
                            SCRStatement statementBlock = new SCRStatement(item, localFloats, orSignalTypes, orNormalSubtypes);
                            Statements.Add(statementBlock);
                        }
                    }
                }

                internal SCRBlock(ConditionalStatementTerm conditionalStatement, IDictionary<string, int> localFloats, IList<string> orSignalTypes, IList<string> orNormalSubtypes)
                {
                    Statements = new ArrayList
                    {
                        new SCRConditionBlock(conditionalStatement, localFloats, orSignalTypes, orNormalSubtypes)
                    };
                }
            } // class SCRBlock
        } // class Scripts
    }
}
