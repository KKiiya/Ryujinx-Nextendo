using Avalonia;
using Avalonia.Controls;
using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;

namespace Ryujinx.Ava.UI.Views.Console
{
    /// <summary>
    /// Title + real-metadata line for the currently selected game.
    /// Only data the project reliably provides is shown (developer, version,
    /// play time, last played); missing pieces are omitted, never invented.
    /// </summary>
    public partial class GameDetailsPanel : RyujinxControl<MainWindowViewModel>
    {
        public GameDetailsPanel()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => Attach();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Attach();
        }

        private ConsoleLibraryViewModel _attached;

        private void Attach()
        {
            if (DataContext is MainWindowViewModel vm && vm.ConsoleLibrary != _attached)
            {
                if (_attached != null)
                {
                    _attached.PropertyChanged -= OnConsolePropertyChanged;
                }

                _attached = vm.ConsoleLibrary;
                _attached.PropertyChanged += OnConsolePropertyChanged;
                UpdateDetails();
            }
        }

        private void OnConsolePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ConsoleLibraryViewModel.CurrentSelectedApp)
                or nameof(ConsoleLibraryViewModel.CurrentApps)
                or nameof(ConsoleLibraryViewModel.SelectedSection))
            {
                UpdateDetails();
            }
        }

        private void UpdateDetails()
        {
            ApplicationData app = (DataContext as MainWindowViewModel)?.ConsoleLibrary?.CurrentSelectedApp;

            if (TitleText != null)
            {
                TitleText.Text = app?.Name ?? string.Empty;
            }

            if (MetaText != null)
            {
                MetaText.Text = app == null ? string.Empty : BuildMetaLine(app);
                MetaText.IsVisible = !string.IsNullOrEmpty(MetaText.Text);
            }
        }

        private static string BuildMetaLine(ApplicationData app)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(app.Developer) && app.Developer != "Unknown")
            {
                parts.Add(app.Developer);
            }

            if (!string.IsNullOrWhiteSpace(app.Version) && app.Version != "0")
            {
                parts.Add("v" + app.Version);
            }

            if (!string.IsNullOrWhiteSpace(app.TimePlayedString))
            {
                parts.Add(app.TimePlayedString);
            }

            // LastPlayedString contains a newline (date + time lines); flatten it.
            string lastPlayed = app.LastPlayedString?.Replace("\n", " ");
            if (!string.IsNullOrWhiteSpace(lastPlayed))
            {
                parts.Add(lastPlayed);
            }

            return string.Join("  •  ", parts);
        }
    }
}
