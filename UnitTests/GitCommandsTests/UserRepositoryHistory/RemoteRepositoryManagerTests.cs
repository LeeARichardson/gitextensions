using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.UserRepositoryHistory
{
    [TestFixture]
    public class RemoteRepositoryManagerTests
    {
        private const string Key = "history remote";
        private IRepositoryStorage _repositoryStorage;
        private RemoteRepositoryManager _manager;

        [SetUp]
        public void Setup()
        {
            _repositoryStorage = Substitute.For<IRepositoryStorage>();
            _manager = new RemoteRepositoryManager(_repositoryStorage);
        }

        [Test]
        public async Task AddAsMostRecentAsync_should_add_new_path_as_top_entry()
        {
            const string repoToAdd = "https://path.to/add";
            var history = new List<Repository>
            {
                new Repository("http://path1/"),
                new Repository("http://path3/"),
                new Repository("http://path4/"),
                new Repository("http://path5/"),
            };
            _repositoryStorage.Load(Key).Returns(x => history);

            var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

            newHistory.Repositories.Count.Should().Be(5);
            newHistory.Repositories[0].Path.Should().Be(repoToAdd);
        }

        [Test]
        public async Task AddAsMostRecentAsync_should_move_existing_path_as_top_entry()
        {
            const string repoToAdd = "https://path.to/add";
            var history = new List<Repository>
            {
                new Repository("git://path1/"),
                new Repository("git://path3/"),
                new Repository("git://path4/"),
                new Repository(repoToAdd),
                new Repository("git://path5/"),
            };
            _repositoryStorage.Load(Key).Returns(x => history);

            var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

            newHistory.Repositories.Count.Should().Be(5);
            newHistory.Repositories[0].Path.Should().Be(repoToAdd);
        }

        [Test]
        public async Task AddAsMostRecentAsync_should_move_only_first_existing_path_as_top_entry()
        {
            const string repoToAdd = "https://path.to/add";
            var history = new List<Repository>
            {
                new Repository("ssh://path1/"),
                new Repository("ssh://path3/"),
                new Repository(repoToAdd),
                new Repository("ssh://path4/"),
                new Repository(repoToAdd),
                new Repository("http://path5/"),
            };
            _repositoryStorage.Load(Key).Returns(x => history);

            var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

            newHistory.Repositories.Count.Should().Be(6);
            newHistory.Repositories[0].Path.Should().Be(repoToAdd);
            newHistory.Repositories[4].Path.Should().Be(repoToAdd);
        }

        [Test]
        public async Task AddAsMostRecentAsync_should_not_move_if_path_already_as_top_entry()
        {
            const string repoToAdd = "https://path.to/add";
            var history = new List<Repository>
            {
                new Repository(repoToAdd),
                new Repository("http://path1/"),
                new Repository("http://path3/"),
                new Repository("http://path4/"),
                new Repository("http://path5/"),
            };
            _repositoryStorage.Load(Key).Returns(x => history);

            var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

            newHistory.Repositories.Count.Should().Be(5);
            newHistory.Repositories[0].Path.Should().Be(repoToAdd);
            _repositoryStorage.DidNotReceive().Save(Key, Arg.Any<IList<Repository>>());
        }

        [Test]
        public async Task RemoveFromHistoryAsync_should_remove_if_exists()
        {
            const string repoToDelete = "path to delete";
            var history = new RepositoryHistory
            {
                Repositories = new BindingList<Repository>
                {
                    new Repository("path1"),
                    new Repository(repoToDelete),
                    new Repository("path3"),
                    new Repository("path4"),
                    new Repository("path5"),
                }
            };
            _repositoryStorage.Load(Key).Returns(x => history.Repositories);

            var newHistory = await _manager.RemoveFromHistoryAsync(repoToDelete);

            newHistory.Repositories.Count.Should().Be(4);
            newHistory.Repositories.Should().NotContain(repoToDelete);

            _repositoryStorage.Received(1).Load(Key);
            _repositoryStorage.Received(1).Save(Key, Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path != repoToDelete)));
        }

        [Test]
        public async Task RemoveFromHistoryAsync_should_not_crash_if_not_exists()
        {
            const string repoToDelete = "path to delete";
            var history = new RepositoryHistory
            {
                Repositories = new BindingList<Repository>
                {
                    new Repository("path1"),
                    new Repository("path2"),
                    new Repository("path3"),
                    new Repository("path4"),
                    new Repository("path5"),
                }
            };
            _repositoryStorage.Load(Key).Returns(x => history.Repositories);

            var newHistory = await _manager.RemoveFromHistoryAsync(repoToDelete);

            newHistory.Repositories.Count.Should().Be(5);
            newHistory.Repositories.Should().NotContain(repoToDelete);

            _repositoryStorage.Received(1).Load(Key);
            _repositoryStorage.DidNotReceive().Save(Key, Arg.Any<IEnumerable<Repository>>());
        }

        [Test]
        public async Task SaveHistoryAsync_should_trim_history_size()
        {
            const int size = 3;
            AppSettings.RecentRepositoriesHistorySize = size;

            var history = new RepositoryHistory
            {
                Repositories = new BindingList<Repository>
                {
                    new Repository("path1"),
                    new Repository("path2"),
                    new Repository("path3"),
                    new Repository("path4"),
                    new Repository("path5"),
                }
            };

            await _manager.SaveHistoryAsync(history);

            _repositoryStorage.Received(1).Save(Key, Arg.Is<IEnumerable<Repository>>(h => h.Count() == size));
        }
    }
}