﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CommandManagerExtensions.cs" company="Wild Gums">
//   Copyright (c) 2008 - 2015 Wild Gums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orchestra
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Input;
    using Catel;
    using Catel.IoC;
    using Catel.Logging;
    using Catel.MVVM;
    using Catel.Reflection;
    using InputGesture = Catel.Windows.Input.InputGesture;

    public static class CommandManagerExtensions
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        public static Dictionary<string, ICommand> FindCommandsByGesture(this ICommandManager commandManager, InputGesture inputGesture)
        {
            Argument.IsNotNull(() => commandManager);
            Argument.IsNotNull(() => inputGesture);

            var commands = new Dictionary<string, ICommand>();

            foreach (var commandName in commandManager.GetCommands())
            {
                var commandInputGesture = commandManager.GetInputGesture(commandName);
                if (inputGesture.Equals(commandInputGesture))
                {
                    commands[commandName] = commandManager.GetCommand(commandName);
                }
            }

            return commands;
        }

        public static void CreateCommandWithGesture(this ICommandManager commandManager, Type containerType, string commandNameFieldName)
        {
            Argument.IsNotNull(() => commandManager);
            Argument.IsNotNull(() => containerType);
            Argument.IsNotNullOrWhitespace(() => commandNameFieldName);

            Log.Debug("Creating command '{0}'", commandNameFieldName);

            var commandNameField = containerType.GetFieldEx(commandNameFieldName, BindingFlags.Public | BindingFlags.Static);
            if (commandNameField == null)
            {
                Log.ErrorAndThrowException<InvalidOperationException>("Command '{0}' is not available on container type '{1}'",
                    commandNameFieldName, containerType.GetSafeFullName());
            }

            var commandName = (string)commandNameField.GetValue(null);
            if (commandManager.IsCommandCreated(commandName))
            {
                Log.Debug("Command '{0}' is already created, skipping...", commandName);
                return;
            }

            InputGesture commandInputGesture = null;
            var inputGestureField = containerType.GetFieldEx(string.Format("{0}InputGesture", commandNameFieldName),
                BindingFlags.Public | BindingFlags.Static);
            if (inputGestureField != null)
            {
                commandInputGesture = inputGestureField.GetValue(null) as InputGesture;
            }

            commandManager.CreateCommand(commandName, commandInputGesture);

            var commandContainerName = string.Format("{0}CommandContainer", commandName.Replace(".", string.Empty));

            var commandContainerType = (from type in TypeCache.GetTypes()
                                        where type.Name.EqualsIgnoreCase(commandContainerName)
                                        select type).FirstOrDefault();
            if (commandContainerType != null)
            {
                Log.Debug("Found command container '{0}', registering it in the ServiceLocator now", commandContainerType.GetSafeFullName());

                var serviceLocator = commandManager.GetServiceLocator();
                if (!serviceLocator.IsTypeRegistered(commandContainerType))
                {
                    var typeFactory = serviceLocator.ResolveType<ITypeFactory>();
                    var commandContainer = typeFactory.CreateInstance(commandContainerType);
                    if (commandContainer != null)
                    {
                        serviceLocator.RegisterInstance(commandContainer);
                    }
                    else
                    {
                        Log.Warning("Cannot create command container '{0}', skipping registration", commandContainerType.GetSafeFullName());
                    }
                }
            }
        }
    }
}