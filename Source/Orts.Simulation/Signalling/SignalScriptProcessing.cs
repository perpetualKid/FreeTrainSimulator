// COPYRIGHT 2013 by the Open Rails project.
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

// This file processes the MSTS SIGSCR.dat file, which contains the signal logic.
// The information is stored in a series of classes.
// This file also contains the functions to process the information when running, and as such is linked with signals.cs

using System;
using System.Collections;
using System.Collections.Generic;

using Orts.Formats.Msts;

namespace Orts.Simulation.Signalling
{

    internal static class SignalScriptProcessing
    {

        public static SignalScripts SignalScripts { get; private set; }

        public static void Initialize(SignalScripts scripts)
        {
            SignalScripts = scripts;
        }

        //================================================================================================//
        //
        // processing routines
        //
        //================================================================================================//
        // main update routine
        //================================================================================================//
        internal static void SignalHeadUpdate(SignalHead head, SignalScripts.SCRScripts signalScript)
        {
            if (head.SignalType == null)
                return;
            if (signalScript != null)
            {
                ProcessScript(head, signalScript);
            }
            else
            {
                UpdateBasic(head);
            }
        }

        //================================================================================================//
        // update_basic : update signal without script
        //================================================================================================//
        private static void UpdateBasic(SignalHead head)
        {
            if (head.MainSignal.BlockState() == SignalBlockState.Clear)
            {
                head.RequestLeastRestrictiveAspect();
            }
            else
            {
                head.RequestMostRestrictiveAspect();
            }
        }

        //================================================================================================//
        // process script
        //================================================================================================//
        private static void ProcessScript(SignalHead head, SignalScripts.SCRScripts signalScript)
        {

            int[] localFloats = new int[signalScript.TotalLocalFloats];
            // process script
            if (!ProcessStatementBlock(head, signalScript.Statements, localFloats))
                return;
        }

        //================================================================================================//
        // process statement block
        // called for full script as well as for IF and ELSE blocks
        // if returns false : abort further processing
        //================================================================================================//
        private static bool ProcessStatementBlock(SignalHead head, ArrayList statements, int[] localFloats)
        {
            // loop through all lines
            foreach (object scriptstat in statements)
            {
                // process statement lines
                if (scriptstat is SignalScripts.SCRScripts.SCRStatement statement)
                {
                    if (statement.StatementTerms[0].Function == SignalScripts.SCRExternalFunctions.RETURN)
                    {
                        return false;
                    }
                    ProcessAssignStatement(head, statement, localFloats);
                }
                else if (scriptstat is SignalScripts.SCRScripts.SCRConditionBlock conditionalBlock)
                {
                    if (!ProcessIfCondition(head, conditionalBlock, localFloats))
                        return false;
                }
            }
            return true;
        }

        //================================================================================================//
        // process assign statement
        //================================================================================================//
        private static void ProcessAssignStatement(SignalHead head, SignalScripts.SCRScripts.SCRStatement statement, int[] localFloats)
        {
            // get term value
            int tempvalue = ProcessSubTerm(head, statement.StatementTerms, 0, localFloats);

            // assign value
            switch (statement.AssignType)
            {
                // assign value to external float
                // Possible floats :
                //                        STATE
                //                        DRAW_STATE
                //                        ENABLED     (not allowed for write)
                //                        BLOCK_STATE (not allowed for write)
                case (SignalScripts.SCRTermType.ExternalFloat):
                    SignalScripts.SCRExternalFloats floatType = (SignalScripts.SCRExternalFloats)statement.AssignParameter;

                    switch (floatType)
                    {
                        case SignalScripts.SCRExternalFloats.STATE:
                            head.SignalIndicationState = (SignalAspectState)tempvalue;
                            break;

                        case SignalScripts.SCRExternalFloats.DRAW_STATE:
                            head.DrawState = tempvalue;
                            break;
                        default:
                            break;
                    }
                    break;

                // Local float
                case (SignalScripts.SCRTermType.LocalFloat):
                    localFloats[statement.AssignParameter] = tempvalue;
                    break;
                default:
                    break;
            }
        }

        //================================================================================================//
        // get value of single term
        //================================================================================================//
        private static int ProcessAssignTerm(SignalHead head, List<SignalScripts.SCRScripts.SCRStatTerm> statementTerms, SignalScripts.SCRScripts.SCRStatTerm term, int[] localFloats)
        {
            int termvalue = 0;

            if (term.Function != SignalScripts.SCRExternalFunctions.NONE)
            {
                termvalue = FunctionValue(head, term, localFloats);
            }
            else if (term.PartParameter != null)
            {
                // for non-function terms only first entry is valid
                SignalScripts.SCRScripts.SCRParameterType parameter = term.PartParameter[0];
                termvalue = TermValue(head, parameter, localFloats);
            }
            else if (term.TermNumber > 0)
            {
                termvalue = ProcessSubTerm(head, statementTerms, term.TermNumber, localFloats);
            }
            return termvalue;
        }

        //================================================================================================//
        // process subterm
        //================================================================================================//
        private static int ProcessSubTerm(SignalHead head, List<SignalScripts.SCRScripts.SCRStatTerm> statementTerms, int sublevel, int[] localFloats)
        {
            int tempValue = 0;
            int termValue;

            foreach (SignalScripts.SCRScripts.SCRStatTerm term in statementTerms)
            {
                if (term.Function == SignalScripts.SCRExternalFunctions.RETURN)
                {
                    break;
                }

                SignalScripts.SCRTermOperator thisOperator = term.TermOperator;
                if (term.TermLevel == sublevel)
                {
                    termValue = ProcessAssignTerm(head, statementTerms, term, localFloats);
                    if (term.Negated)
                    {
                        termValue = termValue == 0 ? 1 : 0;
                    }

                    switch (thisOperator)
                    {
                        case (SignalScripts.SCRTermOperator.MULTIPLY):
                            tempValue *= termValue;
                            break;

                        case (SignalScripts.SCRTermOperator.PLUS):
                            tempValue += termValue;
                            break;

                        case (SignalScripts.SCRTermOperator.MINUS):
                            tempValue -= termValue;
                            break;

                        case (SignalScripts.SCRTermOperator.DIVIDE):
                            if (termValue == 0)
                            {
                                tempValue = 0;
                            }
                            else
                            {
                                tempValue /= termValue;
                            }
                            break;

                        case (SignalScripts.SCRTermOperator.MODULO):
                            tempValue %= termValue;
                            break;

                        default:
                            tempValue = termValue;
                            break;
                    }
                }
            }
            return tempValue;
        }

        //================================================================================================//
        // get parameter term value
        //================================================================================================//
        private static int TermValue(SignalHead head, SignalScripts.SCRScripts.SCRParameterType parameter, int[] localFloats)
        {

            int result = 0;

            // for non-function terms only first entry is valid
            switch (parameter.PartType)
            {

                // assign value to external float
                // Possible floats :
                //                        STATE
                //                        DRAW_STATE
                //                        ENABLED     
                //                        BLOCK_STATE

                case (SignalScripts.SCRTermType.ExternalFloat):
                    SignalScripts.SCRExternalFloats floatType = (SignalScripts.SCRExternalFloats)parameter.PartParameter;

                    switch (floatType)
                    {
                        case SignalScripts.SCRExternalFloats.STATE:
                            result = (int)head.SignalIndicationState;
                            break;

                        case SignalScripts.SCRExternalFloats.DRAW_STATE:
                            result = head.DrawState;
                            break;

                        case SignalScripts.SCRExternalFloats.ENABLED:
                            result = Convert.ToInt32(head.MainSignal.Enabled);
                            break;

                        case SignalScripts.SCRExternalFloats.BLOCK_STATE:
                            result = (int)head.MainSignal.BlockState();
                            break;

                        case SignalScripts.SCRExternalFloats.APPROACH_CONTROL_REQ_POSITION:
                            result = head.ApproachControlLimitPositionM.HasValue ? Convert.ToInt32(head.ApproachControlLimitPositionM.Value) : -1;
                            break;

                        case SignalScripts.SCRExternalFloats.APPROACH_CONTROL_REQ_SPEED:
                            result = head.ApproachControlLimitSpeedMpS.HasValue ? Convert.ToInt32(head.ApproachControlLimitSpeedMpS.Value) : -1;
                            break;

                        default:
                            break;
                    }
                    break;

                // Local float
                case (SignalScripts.SCRTermType.LocalFloat):
                    result = localFloats[parameter.PartParameter];
                    break;

                // all others : constants
                default:
                    result = parameter.PartParameter;
                    break;
            }

            return result;
        }

        //================================================================================================//
        // return function value
        // Possible functions : see enum SCRExternalFunctions
        //================================================================================================//
        private static int FunctionValue(SignalHead head, SignalScripts.SCRScripts.SCRStatTerm term, int[] localFloats)
        {

            int result = 0;
            int parameter1 = 0;
            int parameter2 = 0;
            SignalFunction function1;
            SignalFunction function2;

            // extract parameters (max. 2)

            if (term.PartParameter != null)
            {
                if (term.PartParameter.Length >= 1)
                {
                    SignalScripts.SCRScripts.SCRParameterType parameter = term.PartParameter[0];
                    parameter1 = TermValue(head, parameter, localFloats);
                    function1 = parameter.SignalFunction;
                }

                if (term.PartParameter.Length >= 2)
                {
                    SignalScripts.SCRScripts.SCRParameterType parameter = term.PartParameter[1];
                    parameter2 = TermValue(head, parameter, localFloats);
                    function2 = parameter.SignalFunction;
                }
            }

            // switch on function
            switch (term.Function)
            {
                // BlockState
                case SignalScripts.SCRExternalFunctions.BLOCK_STATE:
                    result = (int)head.MainSignal.BlockState();
                    break;

                // Route set
                case SignalScripts.SCRExternalFunctions.ROUTE_SET:
                    result = head.VerifyRouteSet();
                    break;

                // next_sig_lr
                case SignalScripts.SCRExternalFunctions.NEXT_SIG_LR:
                    result = (int)head.NextSignalLR(parameter1);
                    break;

                // next_sig_mr
                case SignalScripts.SCRExternalFunctions.NEXT_SIG_MR:
                    result = (int)head.NextSignalMR(parameter1);
                    break;

                // this_sig_lr
                case SignalScripts.SCRExternalFunctions.THIS_SIG_LR:
                    SignalAspectState returnState_lr = head.ThisSignalLR(parameter1);
                    result = returnState_lr != SignalAspectState.Unknown ? (int)returnState_lr : -1;
                    break;

                // this_sig_mr
                case SignalScripts.SCRExternalFunctions.THIS_SIG_MR:
                    SignalAspectState returnState_mr = head.ThisSignalMR(parameter1);
                    result = returnState_mr != SignalAspectState.Unknown ? (int)returnState_mr : -1;
                    break;

                // opp_sig_lr
                case SignalScripts.SCRExternalFunctions.OPP_SIG_LR:
                    result = (int)head.OppositeSignalLR(parameter1);
                    break;

                // opp_sig_mr
                case SignalScripts.SCRExternalFunctions.OPP_SIG_MR:
                    result = (int)head.OppositeSignalMR(parameter1);
                    break;

                // next_nsig_lr
                case SignalScripts.SCRExternalFunctions.NEXT_NSIG_LR:
                    result = (int)head.NextNthSignalLR(parameter1, parameter2);
                    break;

                // dist_multi_sig_mr
                case SignalScripts.SCRExternalFunctions.DIST_MULTI_SIG_MR:
                    result = (int)head.MRSignalMultiOnRoute(parameter1, parameter2);
                    break;

                // dist_multi_sig_mr_of_lr
                case SignalScripts.SCRExternalFunctions.DIST_MULTI_SIG_MR_OF_LR:
                    result = (int)head.LRSignalMultiOnRoute(parameter1, parameter2);
                    break;

                // next_sig_id
                case SignalScripts.SCRExternalFunctions.NEXT_SIG_ID:
                    result = head.NextSignalId(parameter1);
                    break;

                // next_nsig_id
                case SignalScripts.SCRExternalFunctions.NEXT_NSIG_ID:
                    result = head.NextNthSignalId(parameter1, parameter2);
                    break;

                // opp_sig_id
                case SignalScripts.SCRExternalFunctions.OPP_SIG_ID:
                    result = head.OppositeSignalId(parameter1);
                    break;

                // id_sig_enabled
                case SignalScripts.SCRExternalFunctions.ID_SIG_ENABLED:
                    result = head.SignalEnabledById(parameter1);
                    break;

                // id_sig_lr
                case SignalScripts.SCRExternalFunctions.ID_SIG_LR:
                    result = (int)head.SignalLRById(parameter1, parameter2);
                    break;

                // sig_feature
                case SignalScripts.SCRExternalFunctions.SIG_FEATURE:
                    result = Convert.ToInt32(head.VerifySignalFeature(parameter1));
                    break;

                // allow to clear to partial route
                case SignalScripts.SCRExternalFunctions.ALLOW_CLEAR_TO_PARTIAL_ROUTE:
                    head.MainSignal.AllowClearPartialRoute(parameter1);
                    break;

                // approach control position
                case SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_POSITION:
                    result = Convert.ToInt32(head.MainSignal.ApproachControlPosition(parameter1, false));
                    break;

                // approach control position forced
                case SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_POSITION_FORCED:
                    result = Convert.ToInt32(head.MainSignal.ApproachControlPosition(parameter1, true));
                    break;

                // approach control speed
                case (SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_SPEED):
                    result = Convert.ToInt32(head.MainSignal.ApproachControlSpeed(parameter1, parameter2));
                    break;

                // approach control next stop
                case SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_NEXT_STOP:
                    result = Convert.ToInt32(head.MainSignal.ApproachControlNextStop(parameter1, parameter2));
                    break;

                // Lock claim for approach control
                case SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_LOCK_CLAIM:
                    head.MainSignal.LockClaim();
                    break;

                // Activate timing trigger
                case (SignalScripts.SCRExternalFunctions.ACTIVATE_TIMING_TRIGGER):
                    head.MainSignal.ActivateTimingTrigger();
                    break;

                // Check timing trigger
                case SignalScripts.SCRExternalFunctions.CHECK_TIMING_TRIGGER:
                    result = Convert.ToInt32(head.MainSignal.CheckTimingTrigger(parameter1));
                    break;

                // Check for CallOn
                case SignalScripts.SCRExternalFunctions.TRAINHASCALLON:
                    head.MainSignal.CallOnEnabled = true;
                    result = Convert.ToInt32(head.MainSignal.TrainHasCallOn(true, false));
                    break;

                // Check for CallOn Restricted
                case SignalScripts.SCRExternalFunctions.TRAINHASCALLON_RESTRICTED:
                    head.MainSignal.CallOnEnabled = true;
                    result = Convert.ToInt32(head.MainSignal.TrainHasCallOn(false, false));
                    break;

                // Check for CallOn
                case SignalScripts.SCRExternalFunctions.TRAINHASCALLON_ADVANCED:
                    head.MainSignal.CallOnEnabled = true;
                    result = Convert.ToInt32(head.MainSignal.TrainHasCallOn(true, true));
                    break;

                // Check for CallOn Restricted
                case SignalScripts.SCRExternalFunctions.TRAINHASCALLON_RESTRICTED_ADVANCED:
                    head.MainSignal.CallOnEnabled = true;
                    result = Convert.ToInt32(head.MainSignal.TrainHasCallOn(false, true));
                    break;

                // check if train needs next signal
                case SignalScripts.SCRExternalFunctions.TRAIN_REQUIRES_NEXT_SIGNAL:
                    result = Convert.ToInt32(head.MainSignal.RequiresNextSignal(parameter1, parameter2));
                    break;

                case SignalScripts.SCRExternalFunctions.FIND_REQ_NORMAL_SIGNAL:
                    result = head.MainSignal.FindRequiredNormalSignal(parameter1);
                    break;

                // check if route upto required signal is fully cleared
                case SignalScripts.SCRExternalFunctions.ROUTE_CLEARED_TO_SIGNAL:
                    result = (int)head.MainSignal.RouteClearedToSignal(parameter1, false);
                    break;

                // check if route upto required signal is fully cleared, but allow callon
                case SignalScripts.SCRExternalFunctions.ROUTE_CLEARED_TO_SIGNAL_CALLON:
                    result = (int)head.MainSignal.RouteClearedToSignal(parameter1, true);
                    break;

                // check if specified head enabled
                case SignalScripts.SCRExternalFunctions.HASHEAD:
                    result = head.MainSignal.HasHead(parameter1);
                    break;

                // increase active value of SignalNumClearAhead
                case SignalScripts.SCRExternalFunctions.INCREASE_SIGNALNUMCLEARAHEAD:
                    head.MainSignal.IncreaseSignalNumClearAhead(parameter1);
                    break;

                // decrease active value of SignalNumClearAhead
                case SignalScripts.SCRExternalFunctions.DECREASE_SIGNALNUMCLEARAHEAD:
                    head.MainSignal.DecreaseSignalNumClearAhead(parameter1);
                    break;

                // set active value of SignalNumClearAhead
                case SignalScripts.SCRExternalFunctions.SET_SIGNALNUMCLEARAHEAD:
                    head.MainSignal.SetSignalNumClearAhead(parameter1);
                    break;

                // reset active value of SignalNumClearAhead to default
                case SignalScripts.SCRExternalFunctions.RESET_SIGNALNUMCLEARAHEAD:
                    head.MainSignal.ResetSignalNumClearAhead();
                    break;

                // store_lvar
                case SignalScripts.SCRExternalFunctions.STORE_LVAR:
                    head.StoreLocalVariable(parameter1, parameter2);
                    break;

                // this_sig_lvar
                case SignalScripts.SCRExternalFunctions.THIS_SIG_LVAR:
                    result = head.ThisSignalLocalVariable(parameter1);
                    break;

                // next_sig_lvar
                case SignalScripts.SCRExternalFunctions.NEXT_SIG_LVAR:
                    result = head.NextSignalLocalVariable(parameter1, parameter2);
                    break;

                // id_sig_lvar
                case SignalScripts.SCRExternalFunctions.ID_SIG_LVAR:
                    result = head.LocalVariableBySignalId(parameter1, parameter2);
                    break;

                // this_sig_noupdate
                case SignalScripts.SCRExternalFunctions.THIS_SIG_NOUPDATE:
                    head.MainSignal.Static = true;
                    break;

                // this_sig_hasnormalsubtype
                case SignalScripts.SCRExternalFunctions.THIS_SIG_HASNORMALSUBTYPE:
                    result = head.SignalHasNormalSubtype(parameter1);
                    break;

                // next_sig_hasnormalsubtype
                case SignalScripts.SCRExternalFunctions.NEXT_SIG_HASNORMALSUBTYPE:
                    result = head.NextSignalHasNormalSubtype(parameter1);
                    break;

                // next_sig_hasnormalsubtype
                case SignalScripts.SCRExternalFunctions.ID_SIG_HASNORMALSUBTYPE:
                    result = head.SignalHasNormalSubtypeById(parameter1, parameter2);
                    break;

                // switchstand
                case SignalScripts.SCRExternalFunctions.SWITCHSTAND:
                    result = head.Switchstand(parameter1, parameter2);
                    break;

                // def_draw_state
                case SignalScripts.SCRExternalFunctions.DEF_DRAW_STATE:
                    result = head.DefaultDrawState((SignalAspectState)parameter1);
                    break;

                // DEBUG routine : to be implemented later
                default:
                    break;
            }
            // check sign
            if (term.TermOperator == SignalScripts.SCRTermOperator.MINUS)
            {
                result = -result;
            }

            return result;
        }

        //================================================================================================//
        // check IF conditions
        //================================================================================================//
        private static bool ProcessIfCondition(SignalHead head, SignalScripts.SCRScripts.SCRConditionBlock condition, int[] localFloats)
        {
            bool performed = false;

            // check condition

            // if set : execute IF block
            if (ProcessConditionStatement(head, condition.Conditions, localFloats))
            {
                if (!ProcessStatementBlock(head, condition.IfBlock.Statements, localFloats))
                    return false;
                performed = true;
            }

            // not set : check through ELSEIF
            if (!performed)
            {
                int totalElseIf = condition.ElseIfBlock == null ? 0 : condition.ElseIfBlock.Count;

                for (int ielseif = 0; ielseif < totalElseIf && !performed; ielseif++)
                {

                    // first (and only ) entry in ELSEIF block must be IF condition - extract condition
                    object elseifStat = condition.ElseIfBlock[ielseif].Statements[0];
                    if (elseifStat is SignalScripts.SCRScripts.SCRConditionBlock elseifCond)
                    {
                        if (ProcessConditionStatement(head, elseifCond.Conditions, localFloats))
                        {
                            if (!ProcessStatementBlock(head, elseifCond.IfBlock.Statements, localFloats))
                                return false;
                            performed = true;
                        }
                    }
                }
            }

            // ELSE block
            if (!performed && condition.ElseBlock != null)
            {
                if (!ProcessStatementBlock(head, condition.ElseBlock.Statements, localFloats))
                    return false;
            }

            return true;
        }

        //================================================================================================//
        // process condition statement
        //================================================================================================//
        private static bool ProcessConditionStatement(SignalHead head, ArrayList conditionals, int[] localFloats)
        {

            // loop through all conditions
            bool result = true;
            bool negate = false;
            SignalScripts.SCRAndOr condstring = SignalScripts.SCRAndOr.NONE;

            foreach (object condition in conditionals)
            {
                bool newcondition;
                // single condition : process

                if (condition is SignalScripts.SCRNegate)
                {
                    negate = true;
                }

                else if (condition is SignalScripts.SCRScripts.SCRConditions singleCondition)
                {
                    newcondition = ProcessSingleCondition(head, singleCondition, localFloats);

                    if (negate)
                    {
                        negate = false;
                        newcondition = !newcondition;
                    }

                    switch (condstring)
                    {
                        case (SignalScripts.SCRAndOr.AND):
                            result &= newcondition;
                            break;

                        case (SignalScripts.SCRAndOr.OR):
                            result |= newcondition;
                            break;

                        default:
                            result = newcondition;
                            break;
                    }
                }
                // AND or OR indication (to link previous and next part)
                else if (condition is SignalScripts.SCRAndOr or)
                {
                    condstring = or;
                }
                // subcondition
                else
                {
                    ArrayList subCond = (ArrayList)condition;
                    newcondition = ProcessConditionStatement(head, subCond, localFloats);

                    if (negate)
                    {
                        negate = false;
                        newcondition = !newcondition;
                    }

                    switch (condstring)
                    {
                        case (SignalScripts.SCRAndOr.AND):
                            result &= newcondition;
                            break;

                        case (SignalScripts.SCRAndOr.OR):
                            result |= newcondition;
                            break;

                        default:
                            result = newcondition;
                            break;
                    }
                }
            }

            return result;
        }

        //================================================================================================//
        // process single condition
        //================================================================================================//
        private static bool ProcessSingleCondition(SignalHead head, SignalScripts.SCRScripts.SCRConditions condition, int[] localFloats)
        {
            int term1value = 0;
            int term2value = 0;
            bool result = true;

            // get value of first term
            if (condition.Term1.Function != SignalScripts.SCRExternalFunctions.NONE)
            {
                term1value = FunctionValue(head, condition.Term1, localFloats);
            }
            else if (condition.Term1.PartParameter != null)
            {
                SignalScripts.SCRScripts.SCRParameterType parameter = condition.Term1.PartParameter[0];

                term1value = TermValue(head, parameter, localFloats);
                if (condition.Term1.TermOperator == SignalScripts.SCRTermOperator.MINUS)
                {
                    term1value = -term1value;
                }
            }

            // get value of second term
            if (condition.Term2 == null) // if only one value : check for NOT
            {
                if (condition.Term1.Negated)
                {
                    result = !(Convert.ToBoolean(term1value));
                }
                else
                {
                    result = Convert.ToBoolean(term1value);
                }
            }
            // process second term
            else
            {
                if (condition.Term2.Function != SignalScripts.SCRExternalFunctions.NONE)
                {
                    term2value = FunctionValue(head, condition.Term2, localFloats);
                }
                else if (condition.Term2.PartParameter != null)
                {
                    SignalScripts.SCRScripts.SCRParameterType parameter = condition.Term2.PartParameter[0];
                    term2value = TermValue(head, parameter, localFloats);
                    if (condition.Term2.TermOperator == SignalScripts.SCRTermOperator.MINUS)
                    {
                        term2value = -term2value;
                    }
                }

                // check on required condition
                switch (condition.Condition)
                {
                    // GT
                    case SignalScripts.SCRTermCondition.GT:
                        result = term1value > term2value;
                        break;

                    // GE
                    case SignalScripts.SCRTermCondition.GE:
                        result = term1value >= term2value;
                        break;

                    // LT
                    case SignalScripts.SCRTermCondition.LT:
                        result = term1value < term2value;
                        break;

                    // LE
                    case SignalScripts.SCRTermCondition.LE:
                        result = term1value <= term2value;
                        break;

                    // EQ
                    case SignalScripts.SCRTermCondition.EQ:
                        result = term1value == term2value;
                        break;

                    // NE
                    case SignalScripts.SCRTermCondition.NE:
                        result = term1value != term2value;
                        break;
                }
            }
            return result;
        }
    }
}

