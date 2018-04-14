using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Threading;

namespace GitCommands.UserRepositoryHistory
{
    /// <summary>
    /// Manages the history of remote git repositories.
    /// </summary>
    public sealed class RemoteRepositoryManager : IRepositoryManager
    {
        private const string KeyRemoteHistory = "history remote";
        private readonly IRepositoryStorage _repositoryStorage;

        public RemoteRepositoryManager(IRepositoryStorage repositoryStorage)
        {
            _repositoryStorage = repositoryStorage;
        }

        /// <summary>
        /// <para>Saves the provided repository URL to history of remote git repositories as the "most recent".</para>
        /// <para>If the history contains an entry for the provided URL, the entry is physically moved
        /// to the top of the history list.</para>
        /// </summary>
        /// <remarks>
        /// The history is loaded from the persistent storage to ensure the most current version of
        /// the history is updated, as it may have been updated by another instance of GE.
        /// </remarks>
        /// <param name="repositoryPathUrl">A repository URL to be save as "most recent".</param>
        /// <returns>The current version of the history of remote git repositories after the update.</returns>
        /// <exception cref="ArgumentException"><paramref name="repositoryPathUrl"/> is <see langword="null"/> or <see cref="string.Empty"/>.</exception>
        /// <exception cref="NotSupportedException"><paramref name="repositoryPathUrl"/> is not a URL.</exception>
        [ContractAnnotation("repositoryPathUrl:null=>halt")]
        public async Task<RepositoryHistory> AddAsMostRecentAsync(string repositoryPathUrl)
        {
            if (string.IsNullOrWhiteSpace(repositoryPathUrl))
            {
                throw new ArgumentException(nameof(repositoryPathUrl));
            }

            if (!PathUtil.IsUrl(repositoryPathUrl))
            {
                // TODO: throw a specific exception
                throw new NotSupportedException();
            }

            return await AddAsMostRecentRepositoryAsync(repositoryPathUrl);

            async Task<RepositoryHistory> AddAsMostRecentRepositoryAsync(string path)
            {
                await TaskScheduler.Default;
                var repositoryHistory = await LoadHistoryAsync();

                var repository = repositoryHistory.Repositories.FirstOrDefault(r => r.Path.Equals(path, StringComparison.CurrentCultureIgnoreCase));
                if (repository != null)
                {
                    if (repositoryHistory.Repositories[0] == repository)
                    {
                        return repositoryHistory;
                    }

                    repositoryHistory.Repositories.Remove(repository);
                }
                else
                {
                    repository = new Repository(path);
                }

                repositoryHistory.Repositories.Insert(0, repository);

                await SaveHistoryAsync(repositoryHistory);

                return repositoryHistory;
            }
        }

        /// <summary>
        /// Loads the history of remote git repositories from a persistent storage.
        /// </summary>
        /// <returns>The history of remote git repositories.</returns>
        public async Task<RepositoryHistory> LoadHistoryAsync()
        {
            await TaskScheduler.Default;

            // BUG: this must be a separate settings
            // TODO: to be addressed separately
            int size = AppSettings.RecentRepositoriesHistorySize;
            var repositoryHistory = new RepositoryHistory(size);

            var history = _repositoryStorage.Load(KeyRemoteHistory);
            if (history == null)
            {
                return repositoryHistory;
            }

            repositoryHistory.Repositories = new BindingList<Repository>(AdjustHistorySize(history, size).ToList());
            return repositoryHistory;
        }

        /// <summary>
        /// Removes <paramref name="repositoryPath"/> from the history of remote git repositories in a persistent storage.
        /// </summary>
        /// <param name="repositoryPath">A repository path to remove.</param>
        /// <returns>The current version of the history of remote git repositories after the update.</returns>
        [ContractAnnotation("repositoryPath:null=>halt")]
        public async Task<RepositoryHistory> RemoveFromHistoryAsync(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                throw new ArgumentException(nameof(repositoryPath));
            }

            await TaskScheduler.Default;
            var repositoryHistory = await LoadHistoryAsync();
            var repository = repositoryHistory.Repositories.FirstOrDefault(r => r.Path.Equals(repositoryPath, StringComparison.CurrentCultureIgnoreCase));
            if (repository == null)
            {
                return repositoryHistory;
            }

            if (!repositoryHistory.Repositories.Remove(repository))
            {
                return repositoryHistory;
            }

            await SaveHistoryAsync(repositoryHistory);
            return repositoryHistory;
        }

        /// <summary>
        /// Loads the history of remote git repositories to a persistent storage.
        /// </summary>
        /// <param name="repositoryHistory">A collection of remote git repositories.</param>
        /// <returns>An awaitable task.</returns>
        /// <remarks>The size of the history will be adjusted as per <see cref="AppSettings.RecentRepositoriesHistorySize"/> setting.</remarks>
        public async Task SaveHistoryAsync(RepositoryHistory repositoryHistory)
        {
            await TaskScheduler.Default;

            // BUG: this must be a separate settings
            // TODO: to be addressed separately
            int size = AppSettings.RecentRepositoriesHistorySize;
            _repositoryStorage.Save(KeyRemoteHistory, AdjustHistorySize(repositoryHistory.Repositories, size));
        }

        private static IEnumerable<Repository> AdjustHistorySize(IEnumerable<Repository> repositories, int recentRepositoriesHistorySize)
        {
            return repositories.Take(recentRepositoriesHistorySize);
        }
    }
}