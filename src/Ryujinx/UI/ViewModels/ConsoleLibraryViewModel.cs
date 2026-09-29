using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Ava.Systems.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ryujinx.Ava.UI.ViewModels
{
    /// <summary>
    /// Selected section inside the Console (HOME-style) library view.
    /// Only sections backed by real data are exposed: Home (recent),
    /// All Software (full library in existing Ryujinx order) and Favorites.
    /// </summary>
    public enum ConsoleLibrarySection
    {
        Home = 0,
        All = 1,
        Favorites = 2,
    }

    /// <summary>
    /// Presentation-only view model for the Console library view.
    /// Data flow: existing discovery/scan -> existing Ryujinx ordering/filtering
    /// (MainWindowViewModel.AppsObservableList) -> this class (derived Home/Favorites
    /// projections) -> Carousel / Grid views -> existing launch pipeline.
    /// This class never re-implements sorting; "Ryujinx order" is AllApps itself.
    /// </summary>
    public class ConsoleLibraryViewModel : BaseModel
    {
        private readonly MainWindowViewModel _owner;
        private readonly Dictionary<ConsoleLibrarySection, string> _lastSelectedId = new();

        private ConsoleLibrarySection _selectedSection;
        private ApplicationData _selectedHomeApp;
        private ApplicationData _selectedAllApp;
        private ApplicationData _selectedFavoritesApp;

        public ObservableCollection<ApplicationData> HomeApps { get; } = [];
        public ObservableCollection<ApplicationData> FavoriteApps { get; } = [];

        public ConsoleLibraryViewModel(MainWindowViewModel owner)
        {
            _owner = owner;
            _selectedSection = (ConsoleLibrarySection)Math.Clamp(ConfigurationState.Instance.UI.ConsoleSection.Value, 0, 2);
        }

        public ConsoleLibrarySection SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (_selectedSection == value)
                {
                    return;
                }

                RememberSelection(_selectedSection, CurrentSelectedApp);
                _selectedSection = value;
                ConfigurationState.Instance.UI.ConsoleSection.Value = (int)value;
                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHomeSelected));
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(IsFavoritesSelected));
                OnPropertyChanged(nameof(CurrentApps));
                OnPropertyChanged(nameof(CurrentSelectedApp));
                OnPropertyChanged(nameof(SectionTitle));

                RestoreSelection(value);
            }
        }

        public bool IsHomeSelected => SelectedSection == ConsoleLibrarySection.Home;
        public bool IsAllSelected => SelectedSection == ConsoleLibrarySection.All;
        public bool IsFavoritesSelected => SelectedSection == ConsoleLibrarySection.Favorites;

        public string SectionTitle => SelectedSection switch
        {
            ConsoleLibrarySection.Home => "Home",
            ConsoleLibrarySection.All => "All Software",
            ConsoleLibrarySection.Favorites => "Favorites",
            _ => "Home",
        };

        /// <summary>
        /// Full library in the existing Ryujinx order (source of truth, not a copy).
        /// May be null before the first scan completes.
        /// </summary>
        public IReadOnlyList<ApplicationData> AllApps =>
            (IReadOnlyList<ApplicationData>)_owner.AppsObservableList ?? Array.Empty<ApplicationData>();

        public System.Collections.IEnumerable CurrentApps => SelectedSection switch
        {
            ConsoleLibrarySection.Home => HomeApps,
            ConsoleLibrarySection.All => (System.Collections.IEnumerable)AllApps,
            ConsoleLibrarySection.Favorites => FavoriteApps,
            _ => HomeApps,
        };

        public int AllCount => AllApps.Count;
        public int FavoriteCount => FavoriteApps.Count;
        public bool HasGames => AllCount > 0;
        public bool HasFavorites => FavoriteCount > 0;

        public ApplicationData SelectedHomeApp
        {
            get => _selectedHomeApp;
            set
            {
                _selectedHomeApp = value;
                OnPropertyChanged();
                if (SelectedSection == ConsoleLibrarySection.Home)
                {
                    OnPropertyChanged(nameof(CurrentSelectedApp));
                    SyncOwnerSelection(value);
                }
            }
        }

        public ApplicationData SelectedAllApp
        {
            get => _selectedAllApp;
            set
            {
                _selectedAllApp = value;
                OnPropertyChanged();
                if (SelectedSection == ConsoleLibrarySection.All)
                {
                    OnPropertyChanged(nameof(CurrentSelectedApp));
                    SyncOwnerSelection(value);
                }
            }
        }

        public ApplicationData SelectedFavoritesApp
        {
            get => _selectedFavoritesApp;
            set
            {
                _selectedFavoritesApp = value;
                OnPropertyChanged();
                if (SelectedSection == ConsoleLibrarySection.Favorites)
                {
                    OnPropertyChanged(nameof(CurrentSelectedApp));
                    SyncOwnerSelection(value);
                }
            }
        }

        public ApplicationData CurrentSelectedApp
        {
            get => SelectedSection switch
            {
                ConsoleLibrarySection.Home => SelectedHomeApp,
                ConsoleLibrarySection.All => SelectedAllApp,
                ConsoleLibrarySection.Favorites => SelectedFavoritesApp,
                _ => SelectedHomeApp,
            };
            set
            {
                switch (SelectedSection)
                {
                    case ConsoleLibrarySection.Home:
                        SelectedHomeApp = value;
                        break;
                    case ConsoleLibrarySection.All:
                        SelectedAllApp = value;
                        break;
                    case ConsoleLibrarySection.Favorites:
                        SelectedFavoritesApp = value;
                        break;
                }
            }
        }

        /// <summary>
        /// Rebuilds the derived Home/Favorites projections from the existing ordered
        /// list and restores the per-section selection (focus restore after rescan,
        /// sort/filter change, or returning from a game).
        /// </summary>
        public void Refresh()
        {
            RememberSelection(ConsoleLibrarySection.Home, SelectedHomeApp);
            RememberSelection(ConsoleLibrarySection.All, SelectedAllApp);
            RememberSelection(ConsoleLibrarySection.Favorites, SelectedFavoritesApp);

            IReadOnlyList<ApplicationData> ordered =
                (IReadOnlyList<ApplicationData>)_owner.AppsObservableList ?? Array.Empty<ApplicationData>();

            List<ApplicationData> played = ordered
                .Where(a => a.LastPlayed.HasValue)
                .OrderByDescending(a => a.LastPlayed!.Value)
                .ToList();

            IEnumerable<ApplicationData> home;
            if (played.Count == 0)
            {
                // Graceful degradation: no last-played data -> fall back to existing order.
                home = ordered;
            }
            else
            {
                var playedSet = new HashSet<ApplicationData>(played);
                home = played.Concat(ordered.Where(a => !playedSet.Contains(a)));
            }

            ReplaceContents(HomeApps, home);
            ReplaceContents(FavoriteApps, ordered.Where(a => a.Favorite));

            RestoreSelection(ConsoleLibrarySection.Home);
            RestoreSelection(ConsoleLibrarySection.All);
            RestoreSelection(ConsoleLibrarySection.Favorites);

            OnPropertyChanged(nameof(AllApps));
            OnPropertyChanged(nameof(CurrentApps));
            OnPropertyChanged(nameof(CurrentSelectedApp));
            OnPropertyChanged(nameof(AllCount));
            OnPropertyChanged(nameof(FavoriteCount));
            OnPropertyChanged(nameof(HasGames));
            OnPropertyChanged(nameof(HasFavorites));
        }

        public void SelectSection(ConsoleLibrarySection section) => SelectedSection = section;

        private void SyncOwnerSelection(ApplicationData app)
        {
            if (app != null)
            {
                _owner.SelectedApplication = app;
            }
        }

        private void RememberSelection(ConsoleLibrarySection section, ApplicationData app)
        {
            if (app != null)
            {
                _lastSelectedId[section] = app.IdString;
            }
        }

        private void RestoreSelection(ConsoleLibrarySection section)
        {
            System.Collections.IList list = section switch
            {
                ConsoleLibrarySection.Home => HomeApps,
                ConsoleLibrarySection.All => (System.Collections.IList)AllApps,
                ConsoleLibrarySection.Favorites => FavoriteApps,
                _ => HomeApps,
            };

            ApplicationData current = section switch
            {
                ConsoleLibrarySection.Home => _selectedHomeApp,
                ConsoleLibrarySection.All => _selectedAllApp,
                ConsoleLibrarySection.Favorites => _selectedFavoritesApp,
                _ => null,
            };

            if (list.Count == 0)
            {
                SetSectionSelection(section, null);
                return;
            }

            if (current != null && list.Contains(current))
            {
                return;
            }

            if (_lastSelectedId.TryGetValue(section, out string id))
            {
                foreach (ApplicationData app in list)
                {
                    if (app.IdString == id)
                    {
                        SetSectionSelection(section, app);
                        return;
                    }
                }
            }

            // Fall back to the first item so keyboard/gamepad always have a focus target.
            if (current == null || !list.Contains(current))
            {
                SetSectionSelection(section, (ApplicationData)list[0]);
            }
        }

        private void SetSectionSelection(ConsoleLibrarySection section, ApplicationData app)
        {
            switch (section)
            {
                case ConsoleLibrarySection.Home:
                    _selectedHomeApp = app;
                    OnPropertyChanged(nameof(SelectedHomeApp));
                    break;
                case ConsoleLibrarySection.All:
                    _selectedAllApp = app;
                    OnPropertyChanged(nameof(SelectedAllApp));
                    break;
                case ConsoleLibrarySection.Favorites:
                    _selectedFavoritesApp = app;
                    OnPropertyChanged(nameof(SelectedFavoritesApp));
                    break;
            }

            if (section == _selectedSection)
            {
                OnPropertyChanged(nameof(CurrentSelectedApp));
                SyncOwnerSelection(app);
            }
        }

        private static void ReplaceContents(ObservableCollection<ApplicationData> target, IEnumerable<ApplicationData> source)
        {
            target.Clear();
            foreach (ApplicationData app in source)
            {
                target.Add(app);
            }
        }
    }
}
