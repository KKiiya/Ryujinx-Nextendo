using Avalonia.Threading;
using Ryujinx.Ava.Systems.Configuration;

namespace Ryujinx.Ava.UI.ViewModels
{
    /// <summary>
    /// Console vs Classic library presentation. Console is the new HOME-style
    /// launcher; Classic keeps the pre-existing List / Grid / Carousel views.
    /// Persisted in UI.LibraryPresentation (0 = Console, 1 = Classic).
    /// </summary>
    public enum LibraryPresentation
    {
        Console = 0,
        Classic = 1,
    }

    public partial class MainWindowViewModel
    {
        public ConsoleLibraryViewModel ConsoleLibrary { get; private set; }

        public bool IsConsoleView => (LibraryPresentation)ConfigurationState.Instance.UI.LibraryPresentation.Value == LibraryPresentation.Console;
        public bool IsClassicView => !IsConsoleView;

        /// <summary>
        /// Classic sub-views, gated on the Classic presentation so they never
        /// render underneath the Console view.
        /// </summary>
        public bool IsListActive => IsClassicView && IsList;
        public bool IsGridActive => IsClassicView && IsGrid;
        public bool IsCarouselActive => IsClassicView && IsCarousel;

        public bool ConsoleReduceAnimations
        {
            get => ConfigurationState.Instance.UI.ConsoleReduceAnimations.Value;
            set
            {
                ConfigurationState.Instance.UI.ConsoleReduceAnimations.Value = value;
                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
                OnPropertyChanged();
            }
        }

        public void SetConsoleView()
        {
            ConfigurationState.Instance.UI.LibraryPresentation.Value = (int)LibraryPresentation.Console;
            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            NotifyLibraryPresentation();
            ConsoleLibrary?.Refresh();
        }

        public void SetClassicView()
        {
            ConfigurationState.Instance.UI.LibraryPresentation.Value = (int)LibraryPresentation.Classic;
            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            NotifyLibraryPresentation();
        }

        internal void NotifyLibraryPresentation()
        {
            OnPropertyChanged(nameof(IsConsoleView));
            OnPropertyChanged(nameof(IsClassicView));
            OnPropertyChanged(nameof(IsListActive));
            OnPropertyChanged(nameof(IsGridActive));
            OnPropertyChanged(nameof(IsCarouselActive));
        }

        public void ToggleLibraryPresentation()
        {
            if (IsConsoleView)
            {
                SetClassicView();
            }
            else
            {
                SetConsoleView();
            }
        }

        private void EnsureConsoleLibrary()
        {
            ConsoleLibrary ??= new ConsoleLibraryViewModel(this);
        }

        private void RefreshConsoleLibrary()
        {
            EnsureConsoleLibrary();
            if (Dispatcher.UIThread.CheckAccess())
            {
                ConsoleLibrary.Refresh();
            }
            else
            {
                Dispatcher.UIThread.Post(() => ConsoleLibrary.Refresh());
            }
        }
    }
}
