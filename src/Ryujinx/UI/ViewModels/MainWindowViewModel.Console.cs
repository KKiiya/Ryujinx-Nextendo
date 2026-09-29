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
            OnPropertyChanged(nameof(IsConsoleView));
            OnPropertyChanged(nameof(IsClassicView));
            ConsoleLibrary?.Refresh();
        }

        public void SetClassicView()
        {
            ConfigurationState.Instance.UI.LibraryPresentation.Value = (int)LibraryPresentation.Classic;
            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            OnPropertyChanged(nameof(IsConsoleView));
            OnPropertyChanged(nameof(IsClassicView));
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
