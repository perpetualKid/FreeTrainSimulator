using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>
    /// Identifies one save attempt and carries its immutable source snapshot and persistence completion task.
    /// </summary>
    internal sealed class PathSaveOperation
    {
        /// <summary>
        /// Creates a save operation.
        /// </summary>
        public PathSaveOperation(bool acquired, PathModel sourceModel, string sourcePathId, Task<PathPersistenceValidationResult> persistenceTask)
        {
            Acquired = acquired;
            SourceModel = sourceModel;
            SourcePathId = sourcePathId;
            PersistenceTask = persistenceTask ?? throw new ArgumentNullException(nameof(persistenceTask));
        }

        /// <summary>Whether this operation acquired the editor's pending-save token.</summary>
        public bool Acquired { get; }

        /// <summary>The editor model captured when this operation acquired the token.</summary>
        public PathModel SourceModel { get; }

        /// <summary>The path identity captured when this save operation began.</summary>
        public string SourcePathId { get; }

        /// <summary>The asynchronous persistence result for this operation.</summary>
        public Task<PathPersistenceValidationResult> PersistenceTask { get; }
    }

    /// <summary>
    /// Consumes save completion results and guarantees that completion or fault cleanup is dispatched through the
    /// supplied game-thread bridge.
    /// </summary>
    internal static class PathSaveOperationConsumer
    {
        /// <summary>
        /// Awaits one save operation and completes or cancels only its owned editor token on the game thread.
        /// </summary>
        public static async Task<PathPersistenceValidationResult> ConsumeAsync(PathEditor editor, PathSaveOperation operation,
            Func<Func<Task>, Task> gameThreadInvoker)
        {
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(gameThreadInvoker);

            try
            {
                PathPersistenceValidationResult validation = await operation.PersistenceTask.ConfigureAwait(false);
                await gameThreadInvoker(() =>
                {
                    editor.CompleteSave(operation, validation);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                return validation;
            }
            catch (Exception)
            {
                await gameThreadInvoker(() =>
                {
                    editor.CancelSave(operation);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                throw;
            }
        }
    }
}
