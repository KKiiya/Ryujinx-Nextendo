using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.UI.Views.Console
{
    /// <summary>
    /// Pill tab bar for the Console library sections (Home / All Software / Favorites).
    /// </summary>
    public partial class SectionTabBar : RyujinxControl<MainWindowViewModel>
    {
        public SectionTabBar()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => Attach();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Attach();
        }

        private MainWindowViewModel _attachedVm;

        private void Attach()
        {
            if (DataContext is MainWindowViewModel vm && vm != _attachedVm)
            {
                if (_attachedVm?.ConsoleLibrary != null)
                {
                    _attachedVm.ConsoleLibrary.PropertyChanged -= OnConsolePropertyChanged;
                }

                _attachedVm = vm;

                if (vm.ConsoleLibrary != null)
                {
                    vm.ConsoleLibrary.PropertyChanged += OnConsolePropertyChanged;
                }
            }

            RefreshTabHighlights();
        }

        private void OnConsolePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConsoleLibraryViewModel.SelectedSection))
            {
                RefreshTabHighlights();
            }
        }

        public void SectionTab_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag } && int.TryParse(tag, out int section))
            {
                ViewModel.ConsoleLibrary.SelectSection((ConsoleLibrarySection)section);
                RefreshTabHighlights();
            }
        }

        public void RefreshTabHighlights()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            StyleTab(HomeTab, vm.ConsoleLibrary.IsHomeSelected);
            StyleTab(AllTab, vm.ConsoleLibrary.IsAllSelected);
            StyleTab(FavoritesTab, vm.ConsoleLibrary.IsFavoritesSelected);
        }

        public void FocusSelectedTab()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            Button tab = vm.ConsoleLibrary.SelectedSection switch
            {
                ConsoleLibrarySection.All => AllTab,
                ConsoleLibrarySection.Favorites => FavoritesTab,
                _ => HomeTab,
            };

            tab?.Focus();
        }

        private static void StyleTab(Button tab, bool selected)
        {
            if (tab == null)
            {
                return;
            }

            if (Application.Current?.TryGetResource("ConsolePillSelectedBackground", null, out object accent) == true &&
                Application.Current?.TryGetResource("ConsoleTextOnAccent", null, out object onAccent) == true &&
                Application.Current?.TryGetResource("ConsoleTextPrimary", null, out object primary) == true)
            {
                tab.Background = (IBrush)(selected ? accent : Brushes.Transparent);
                tab.Foreground = (IBrush)(selected ? onAccent : primary);
            }
        }
    }
}
