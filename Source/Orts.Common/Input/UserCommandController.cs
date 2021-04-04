using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class UserCommandController<T> where T : Enum
    {
        /// <summary>
        /// https://stackoverflow.com/questions/16968546/assigning-a-method-to-a-delegate-where-the-delegate-has-more-parameters-than-the?noredirect=1&lq=1
        /// </summary>
        private static class DelegateConverter
        {
            public static TTarget ConvertDelegate<TSource, TTarget>(TSource sourceDelegate, int[] indexes = null)
            {
                if (!typeof(Delegate).IsAssignableFrom(typeof(TSource)))
                    throw new InvalidOperationException("TSource must be a delegate.");
                if (!typeof(Delegate).IsAssignableFrom(typeof(TTarget)))
                    throw new InvalidOperationException("TTarget must be a delegate.");
                if (sourceDelegate == null)
                    throw new ArgumentNullException(nameof(sourceDelegate));

                ParameterExpression[] parameterExpressions = typeof(TTarget)
                    .GetMethod("Invoke")
                    .GetParameters()
                    .Select(p => Expression.Parameter(p.ParameterType))
                    .ToArray();

                Expression<TTarget> expression;
                if (indexes != null)
                {
                    expression = Expression.Lambda<TTarget>(
                        Expression.Invoke(
                            Expression.Constant(sourceDelegate),
                            parameterExpressions.Where((p, i) => indexes.Contains(i))),
                        parameterExpressions);
                }
                else
                {
                    int sourceParametersCount = typeof(TSource)
                        .GetMethod("Invoke")
                        .GetParameters()
                        .Length;

                    expression = Expression.Lambda<TTarget>(
                       Expression.Invoke(
                           Expression.Constant(sourceDelegate),
                           parameterExpressions.Take(sourceParametersCount)),
                       parameterExpressions);
                }
                return expression.Compile();
            }
        }

        private readonly Dictionary<Delegate, List<Action<UserCommandArgs, GameTime>>> sourceActions = new Dictionary<Delegate, List<Action<UserCommandArgs, GameTime>>>();
        private readonly EnumArray<Action<UserCommandArgs, GameTime>, T> configurableUserCommands = new EnumArray<Action<UserCommandArgs, GameTime>, T>();

        private readonly EnumArray<Action<UserCommandArgs, GameTime, KeyModifiers>, CommonUserCommand> commonUserCommandsArgs = new EnumArray<Action<UserCommandArgs, GameTime, KeyModifiers>, CommonUserCommand>();

        internal void Trigger(T command, UserCommandArgs commandArgs, GameTime gameTime)
        {
            configurableUserCommands[command]?.Invoke(commandArgs, gameTime);
        }

        internal void Trigger(CommonUserCommand command, UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifier = KeyModifiers.None)
        {
            commonUserCommandsArgs[command]?.Invoke(commandArgs, gameTime, modifier);
        }

        #region user-defined (key) events
        public void AddEvent(T userCommand, Action<UserCommandArgs, GameTime> action)
        {
            configurableUserCommands[userCommand] += action;
        }

        public void AddEvent(T userCommand, Action action, bool enableUnsubscribe = false)
        {
            Action<UserCommandArgs, GameTime> command = DelegateConverter.ConvertDelegate<Action, Action<UserCommandArgs, GameTime>>(action);
            configurableUserCommands[userCommand] += command;
            if (enableUnsubscribe)
            {
                if (!sourceActions.ContainsKey(action))
                    sourceActions.Add(action, new List<Action<UserCommandArgs, GameTime>>() { command });
                else
                    sourceActions[action].Add(command);
            }
        }

        public void AddEvent(T userCommand, Action<GameTime> action, bool enableUnsubscribe = false)
        {
            Action<UserCommandArgs, GameTime> command = DelegateConverter.ConvertDelegate<Action<GameTime>, Action<UserCommandArgs, GameTime>>(action, new int[] { 1 });
            configurableUserCommands[userCommand] += command;
            if (enableUnsubscribe)
            {
                if (!sourceActions.ContainsKey(action))
                    sourceActions.Add(action, new List<Action<UserCommandArgs, GameTime>>() { command });
                else
                    sourceActions[action].Add(command);
            }
        }

        public void AddEvent(T userCommand, Action<UserCommandArgs> action, bool enableUnsubscribe = false)
        {
            Action<UserCommandArgs, GameTime> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs>, Action<UserCommandArgs, GameTime>>(action);
            configurableUserCommands[userCommand] += command;
            if (enableUnsubscribe)
            {
                if (!sourceActions.ContainsKey(action))
                    sourceActions.Add(action, new List<Action<UserCommandArgs, GameTime>>() { command });
                else
                    sourceActions[action].Add(command);
            }
        }

        public void RemoveEvent(T userCommand, Action<UserCommandArgs, GameTime> action)
        {
            configurableUserCommands[userCommand] -= action;
        }

        public void RemoveEvent(T userCommand, Action action)
        {
            if (sourceActions.TryGetValue(action, out List<Action<UserCommandArgs, GameTime>> commandList) && commandList.Count > 0)
            {
                configurableUserCommands[userCommand] -= commandList[0];
                commandList.RemoveAt(0);
            }
        }

        public void RemoveEvent(T userCommand, Action<GameTime> action)
        {
            if (sourceActions.TryGetValue(action, out List<Action<UserCommandArgs, GameTime>> commandList) && commandList.Count > 0)
            {
                configurableUserCommands[userCommand] -= commandList[0];
                commandList.RemoveAt(0);
            }
        }

        public void RemoveEvent(T userCommand, Action<UserCommandArgs> action)
        {
            if (sourceActions.TryGetValue(action, out List<Action<UserCommandArgs, GameTime>> commandList) && commandList.Count > 0)
            {
                configurableUserCommands[userCommand] -= commandList[0];
                commandList.RemoveAt(0);
            }
        }
        #endregion

        #region Generic events
        public void AddEvent(CommonUserCommand userCommand, Action<UserCommandArgs, GameTime, KeyModifiers> action)
        {
            commonUserCommandsArgs[userCommand] += action;
        }

        public void AddEvent(CommonUserCommand userCommand, Action<UserCommandArgs> action)
        {
            Action<UserCommandArgs, GameTime, KeyModifiers> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs>, Action<UserCommandArgs, GameTime, KeyModifiers>>(action);
            commonUserCommandsArgs[userCommand] += command;
        }

        public void AddEvent(CommonUserCommand userCommand, Action<UserCommandArgs, KeyModifiers> action)
        {
            Action<UserCommandArgs, GameTime, KeyModifiers> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs, KeyModifiers>, Action<UserCommandArgs, GameTime, KeyModifiers>>(action, new int[] { 0, 2 });
            commonUserCommandsArgs[userCommand] += command;
        }

        public void RemoveEvent(CommonUserCommand userCommand, Action<UserCommandArgs, GameTime, KeyModifiers> action)
        {
            commonUserCommandsArgs[userCommand] -= action;
        }

        public void RemoveEvent(CommonUserCommand userCommand, Action<UserCommandArgs> action)
        {
            Action<UserCommandArgs, GameTime, KeyModifiers> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs>, Action<UserCommandArgs, GameTime, KeyModifiers>>(action);
            commonUserCommandsArgs[userCommand] -= command;
        }

        public void RemoveEvent(CommonUserCommand userCommand, Action<UserCommandArgs, KeyModifiers> action)
        {
            Action<UserCommandArgs, GameTime, KeyModifiers> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs, KeyModifiers>, Action<UserCommandArgs, GameTime, KeyModifiers>>(action, new int[] { 0, 2 });
            commonUserCommandsArgs[userCommand] -= command;
        }
        #endregion
    }
}
