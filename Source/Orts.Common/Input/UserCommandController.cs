using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    /// <summary>
    /// Non-Generic base class just to enable holder-properties in non-generic types
    /// </summary>
    public class UserCommandController
    {
        public bool SuppressDownLevelEventHandling { get; set; }
    }

    public class UserCommandController<T>: UserCommandController where T : Enum
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
        private List<UserCommandController<T>> topLayerControllers;

        private readonly EnumArray2D<Action<UserCommandArgs, GameTime>, T, KeyEventType> configurableUserCommands = new EnumArray2D<Action<UserCommandArgs, GameTime>, T, KeyEventType>();
        private readonly EnumArray<Action<UserCommandArgs, GameTime, KeyModifiers>, CommonUserCommand> commonUserCommandsArgs = new EnumArray<Action<UserCommandArgs, GameTime, KeyModifiers>, CommonUserCommand>();
        private readonly EnumArray<Action<UserCommandArgs, GameTime>, AnalogUserCommand> analogUserCommandsArgs = new EnumArray<Action<UserCommandArgs, GameTime>, AnalogUserCommand>();
        private readonly EnumArray<Action<ControllerCommandArgs>, CommandControllerInput> controllerInputCommand = new EnumArray<Action<ControllerCommandArgs>, CommandControllerInput>();
        private static readonly int[] parameterIndexes0 = [1];
        private static readonly int[] parameterIndexes1 = [0, 2];

        internal void Trigger(T command, KeyEventType keyEventType, UserCommandArgs commandArgs, GameTime gameTime)
        {
            for (int i = 0; i < (topLayerControllers?.Count ?? 0); i++)
            {
                topLayerControllers[i].Trigger(command, keyEventType, commandArgs, gameTime);
                if (topLayerControllers[i].SuppressDownLevelEventHandling || commandArgs.Handled)
                    return;
            }
            configurableUserCommands[command, keyEventType]?.Invoke(commandArgs, gameTime);
        }

        internal void Trigger(CommonUserCommand command, UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifier = KeyModifiers.None)
        {
            for (int i = 0; i < (topLayerControllers?.Count ?? 0); i++)
            {
                topLayerControllers[i].Trigger(command, commandArgs, gameTime, modifier);
                if (topLayerControllers[i].SuppressDownLevelEventHandling || commandArgs.Handled)
                    return;
            }
            commonUserCommandsArgs[command]?.Invoke(commandArgs, gameTime, modifier);
        }

        internal void Trigger(AnalogUserCommand command, UserCommandArgs commandArgs, GameTime gameTime)
        {
            for (int i = 0; i < (topLayerControllers?.Count ?? 0); i++)
            {
                topLayerControllers[i].Trigger(command, commandArgs, gameTime);
                if (topLayerControllers[i].SuppressDownLevelEventHandling || commandArgs.Handled)
                    return;
            }
            analogUserCommandsArgs[command]?.Invoke(commandArgs, gameTime);
        }

        #region Layering
        public UserCommandController<T> AddTopLayerController()
        {
            if (null == topLayerControllers)
                topLayerControllers = new List<UserCommandController<T>>();
            UserCommandController<T> layeredController = new UserCommandController<T>();
            topLayerControllers.Add(layeredController);
            return layeredController;
        }

        public bool RemoveTopLayerController(UserCommandController<T> commandController)
        {
            return topLayerControllers?.Remove(commandController) ?? false;
        }

        public bool TopLayerControllerBringOnTop(UserCommandController<T> commandController)
        {
            int index;
            if (topLayerControllers?.Count > 1 && (index = topLayerControllers.IndexOf(commandController)) > -1)
            {
                topLayerControllers.RemoveAt(index);
                topLayerControllers.Add(commandController);
                return true;
            }
            return false;
        }
        #endregion

        #region user-defined (key) events
        public void AddEvent(T userCommand, KeyEventType keyEventType, Action<UserCommandArgs, GameTime> action)
        {
            configurableUserCommands[userCommand, keyEventType] += action;
        }

        public void AddEvent(T userCommand, KeyEventType keyEventType, Action action, bool enableUnsubscribe = false)
        {
            Action<UserCommandArgs, GameTime> command = DelegateConverter.ConvertDelegate<Action, Action<UserCommandArgs, GameTime>>(action);
            configurableUserCommands[userCommand, keyEventType] += command;
            if (enableUnsubscribe)
            {
                if (!sourceActions.ContainsKey(action))
                    sourceActions.Add(action, new List<Action<UserCommandArgs, GameTime>>() { command });
                else
                    sourceActions[action].Add(command);
            }
        }

        public void AddEvent(T userCommand, KeyEventType keyEventType, Action<GameTime> action, bool enableUnsubscribe = false)
        {
            Action<UserCommandArgs, GameTime> command = DelegateConverter.ConvertDelegate<Action<GameTime>, Action<UserCommandArgs, GameTime>>(action, parameterIndexes0);
            configurableUserCommands[userCommand, keyEventType] += command;
            if (enableUnsubscribe)
            {
                if (!sourceActions.ContainsKey(action))
                    sourceActions.Add(action, new List<Action<UserCommandArgs, GameTime>>() { command });
                else
                    sourceActions[action].Add(command);
            }
        }

        public void AddEvent(T userCommand, KeyEventType keyEventType, Action<UserCommandArgs> action, bool enableUnsubscribe = false)
        {
            Action<UserCommandArgs, GameTime> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs>, Action<UserCommandArgs, GameTime>>(action);
            configurableUserCommands[userCommand, keyEventType] += command;
            if (enableUnsubscribe)
            {
                if (!sourceActions.ContainsKey(action))
                    sourceActions.Add(action, new List<Action<UserCommandArgs, GameTime>>() { command });
                else
                    sourceActions[action].Add(command);
            }
        }

        public void RemoveEvent(T userCommand, KeyEventType keyEventType, Action<UserCommandArgs, GameTime> action)
        {
            configurableUserCommands[userCommand, keyEventType] -= action;
        }

        public void RemoveEvent(T userCommand, KeyEventType keyEventType, Action action)
        {
            if (sourceActions.TryGetValue(action, out List<Action<UserCommandArgs, GameTime>> commandList) && commandList.Count > 0)
            {
                configurableUserCommands[userCommand, keyEventType] -= commandList[0];
                commandList.RemoveAt(0);
            }
        }

        public void RemoveEvent(T userCommand, KeyEventType keyEventType, Action<GameTime> action)
        {
            if (sourceActions.TryGetValue(action, out List<Action<UserCommandArgs, GameTime>> commandList) && commandList.Count > 0)
            {
                configurableUserCommands[userCommand, keyEventType] -= commandList[0];
                commandList.RemoveAt(0);
            }
        }

        public void RemoveEvent(T userCommand, KeyEventType keyEventType, Action<UserCommandArgs> action)
        {
            if (sourceActions.TryGetValue(action, out List<Action<UserCommandArgs, GameTime>> commandList) && commandList.Count > 0)
            {
                configurableUserCommands[userCommand, keyEventType] -= commandList[0];
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
            Action<UserCommandArgs, GameTime, KeyModifiers> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs, KeyModifiers>, Action<UserCommandArgs, GameTime, KeyModifiers>>(action, parameterIndexes1);
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
            Action<UserCommandArgs, GameTime, KeyModifiers> command = DelegateConverter.ConvertDelegate<Action<UserCommandArgs, KeyModifiers>, Action<UserCommandArgs, GameTime, KeyModifiers>>(action, parameterIndexes1);
            commonUserCommandsArgs[userCommand] -= command;
        }
        #endregion

        #region analog command events (levers, switches or user handles such as RailDriver input)
        public void AddEvent(AnalogUserCommand command, Action<UserCommandArgs, GameTime> action)
        {
            analogUserCommandsArgs[command] += action;
        }

        public void RemoveEvent(AnalogUserCommand command, Action<UserCommandArgs, GameTime> action)
        {
            analogUserCommandsArgs[command] -= action;
        }

        #endregion

        #region Controller command input
        public void Send<TCommandType>(CommandControllerInput command, TCommandType value)
        {
            controllerInputCommand[command]?.Invoke(new ControllerCommandArgs<TCommandType>() { Value = value });
        }

        public void Send(CommandControllerInput command)
        {
            controllerInputCommand[command]?.Invoke(ControllerCommandArgs.Empty);
        }

        public void AddControllerInputEvent(CommandControllerInput command, Action<ControllerCommandArgs> action)
        {
            controllerInputCommand[command] += action;
        }

        public void RemoveControllerInputEvent(CommandControllerInput command, Action<ControllerCommandArgs> action)
        {
            controllerInputCommand[command] -= action;
        }
        #endregion
    }
}
