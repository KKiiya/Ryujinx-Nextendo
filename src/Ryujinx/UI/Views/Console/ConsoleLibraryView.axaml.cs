using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Console
{
    /// <summary>
    /// Console (HOME-style) library view: Home carousel, All Software grid and
    /// Favorites grid over the existing ordered library. Presentation only — all
    /// ordering/filtering comes from <see cref="MainWindowViewModel"/>, launching
    /// and context actions reuse the existing pipeline.
    /// </summary>
    public partial class ConsoleLibraryView : RyujinxControl<MainWindowViewModel>
    {
        public static readonly RoutedEvent<ApplicationOpenedEventArgs> ApplicationOpenedEvent =
            RoutedEvent.Register<ConsoleLibraryView, ApplicationOpenedEventArgs>(nameof(ApplicationOpened), RoutingStrategies.Bubble);

        public event EventHandler<ApplicationOpenedEventArgs> ApplicationOpened
        {
            add => AddHandler(ApplicationOpenedEvent, value);
            remove => RemoveHandler(ApplicationOpenedEvent, value);
        }

        private const int NavTabs = 0;
        private const int NavContent = 1;
        private const int NavBottom = 2;

        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _gamepadTimer;

        private int _navLevel = NavContent;
        private int _bottomIndex;

        private MainWindow _window;

        private bool _gamepadLeftPressed;
        private bool _gamepadRightPressed;
        private bool _gamepadUpPressed;
        private bool _gamepadDownPressed;
        private bool _gamepadConfirmDown;
        private bool _gamepadMenuDown;
        private bool _gamepadBackDown;
        private bool _gamepadSortDown;
        private bool _gamepadLbDown;
        private bool _gamepadRbDown;

        private IGamepad _menuGamepad;
        private string _menuGamepadId;

        private int _contextMenuIndex;
        private readonly List<Border> _contextMenuItemBorders = [];
        private readonly List<Action> _contextMenuActions = [];
        private Flyout _contextMenuFlyout;

        // Background crossfade state.
        private bool _backgroundFrontVisible;
        private string _backgroundAppId;
        private readonly Dictionary<string, Bitmap> _backgroundCache = [];
        private readonly LinkedList<string> _backgroundCacheOrder = [];

        public ConsoleLibraryView()
        {
            InitializeComponent();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();

            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _gamepadTimer.Tick += (_, _) => PollGamepad();
            _gamepadTimer.Start();

            DataContextChanged += (_, _) => AttachViewModel();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _window = VisualRoot as MainWindow;
            AttachViewModel();
            UpdateClock();
            UpdateEmptyStates();
            _ = UpdateBackgroundAsync(CurrentSelectedApp());
        }

        private MainWindowViewModel _attachedVm;

        private void AttachViewModel()
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
                    UpdateEmptyStates();
                }
            }
        }

        private void OnConsolePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ConsoleLibraryViewModel.CurrentSelectedApp)
                or nameof(ConsoleLibraryViewModel.CurrentApps)
                or nameof(ConsoleLibraryViewModel.SelectedSection)
                or nameof(ConsoleLibraryViewModel.HasGames)
                or nameof(ConsoleLibraryViewModel.HasFavorites))
            {
                UpdateEmptyStates();
                _ = UpdateBackgroundAsync(CurrentSelectedApp());

                if (e.PropertyName == nameof(ConsoleLibraryViewModel.SelectedSection))
                {
                    _navLevel = NavContent;
                    FocusActiveList();
                }
            }
        }

        private void UpdateClock()
        {
            if (ClockTextBlock != null)
            {
                ClockTextBlock.Text = DateTime.Now.ToString("HH:mm");
            }
        }

        private ApplicationData CurrentSelectedApp()
        {
            return (DataContext as MainWindowViewModel)?.ConsoleLibrary?.CurrentSelectedApp;
        }

        private ListBox ActiveList()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return HomeCarousel;
            }

            return vm.ConsoleLibrary.SelectedSection switch
            {
                ConsoleLibrarySection.All => AllGrid,
                ConsoleLibrarySection.Favorites => FavoritesGrid,
                _ => HomeCarousel,
            };
        }

        private static System.Collections.IList ActiveItems(ListBox list) => list?.ItemsSource as System.Collections.IList;

        #region Selection movement

        private void MoveBy(int delta)
        {
            ListBox list = ActiveList();
            System.Collections.IList items = ActiveItems(list);
            if (list == null || items == null || items.Count == 0)
            {
                return;
            }

            int index = list.SelectedItem != null ? items.IndexOf(list.SelectedItem) : -1;
            if (index < 0)
            {
                index = delta > 0 ? -1 : 0;
            }

            int target = Math.Clamp(index + delta, 0, items.Count - 1);
            list.SelectedIndex = target;
            list.ScrollIntoView(items[target]);
            list.Focus();
        }

        private void MoveGridVertical(int direction)
        {
            ListBox list = ActiveList();
            System.Collections.IList items = ActiveItems(list);
            if (list == null || items == null || items.Count == 0 || list == HomeCarousel)
            {
                MoveBy(direction);
                return;
            }

            double cardSize = ViewModel.GridItemSelectorSize + 20;
            int columns = Math.Max(1, (int)(list.Bounds.Width / Math.Max(cardSize, 1)));
            MoveBy(direction * columns);
        }

        private void Confirm()
        {
            if (CurrentSelectedApp() is ApplicationData selected)
            {
                RaiseEvent(new ApplicationOpenedEventArgs(selected, ApplicationOpenedEvent));
            }
        }

        public void ContentList_DoubleTapped(object sender, TappedEventArgs args)
        {
            Confirm();
        }

        private void HomeCarousel_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (_navLevel != NavContent)
            {
                return;
            }

            e.Handled = true;

            double delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            if (delta > 0)
            {
                MoveBy(-1);
            }
            else if (delta < 0)
            {
                MoveBy(1);
            }
        }

        #endregion

        #region Keyboard

        public void ContentList_KeyDown(object sender, KeyEventArgs e)
        {
            SetKeyboardHints();

            switch (e.Key)
            {
                case Avalonia.Input.Key.Left:
                    if (_navLevel == NavTabs)
                    {
                        CycleSection(-1);
                    }
                    else if (_navLevel == NavBottom)
                    {
                        MoveBottom(-1);
                    }
                    else
                    {
                        MoveBy(-1);
                    }
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.Right:
                    if (_navLevel == NavTabs)
                    {
                        CycleSection(1);
                    }
                    else if (_navLevel == NavBottom)
                    {
                        MoveBottom(1);
                    }
                    else
                    {
                        MoveBy(1);
                    }
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.Up:
                    if (_navLevel == NavBottom)
                    {
                        _navLevel = NavContent;
                        FocusActiveList();
                    }
                    else if (_navLevel == NavContent && ActiveList() == HomeCarousel)
                    {
                        _navLevel = NavTabs;
                        TabBar.FocusSelectedTab();
                    }
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.Down:
                    if (_navLevel == NavTabs)
                    {
                        _navLevel = NavContent;
                        FocusActiveList();
                    }
                    else if (_navLevel == NavContent && ActiveList() == HomeCarousel)
                    {
                        _navLevel = NavBottom;
                        _bottomIndex = 0;
                        FocusBottomButton();
                    }
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.Enter:
                case Avalonia.Input.Key.Space:
                    if (_navLevel == NavBottom)
                    {
                        ActivateBottom();
                    }
                    else
                    {
                        Confirm();
                    }
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.Escape:
                    if (IsOptionsOpen())
                    {
                        SetOptionsOpen(false);
                    }
                    else if (_navLevel != NavContent)
                    {
                        _navLevel = NavContent;
                        FocusActiveList();
                    }
                    else
                    {
                        ViewModel.ConsoleLibrary.SelectSection(ConsoleLibrarySection.Home);
                    }
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.S:
                    ToggleOptionsOverlay();
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.F:
                    ToggleFavorite();
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.O:
                    OpenNativeContextMenu();
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Typing in the search box must not trigger library shortcuts.
            if (!e.Handled && e.Source is not TextBox && e.Key is Avalonia.Input.Key.S or Avalonia.Input.Key.F or Avalonia.Input.Key.O or Avalonia.Input.Key.Escape or Avalonia.Input.Key.Enter)
            {
                ContentList_KeyDown(this, e);
            }
        }

        #endregion

        #region Sections, bottom bar, overlay

        private void CycleSection(int delta)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            int next = ((int)vm.ConsoleLibrary.SelectedSection + delta + 3) % 3;
            vm.ConsoleLibrary.SelectSection((ConsoleLibrarySection)next);
            TabBar.FocusSelectedTab();
        }

        private void MoveBottom(int delta)
        {
            _bottomIndex = Math.Clamp(_bottomIndex + delta, 0, 2);
            FocusBottomButton();
        }

        private Control GetBottomButton(int index)
        {
            string name = index switch
            {
                0 => "SettingsCornerButton",
                1 => "OptionsCornerButton",
                _ => "RescanCornerButton",
            };

            return this.FindControl<Control>(name) ?? ActiveList();
        }

        private void FocusBottomButton()
        {
            GetBottomButton(_bottomIndex)?.Focus();
        }

        public void FocusActiveList()
        {
            ListBox list = ActiveList();
            if (list != null && ActiveItems(list)?.Count > 0)
            {
                if (list.SelectedItem == null)
                {
                    list.SelectedIndex = 0;
                }

                list.ScrollIntoView(list.SelectedItem);
                list.Focus();
            }
        }

        private void ActivateBottom()
        {
            switch (_bottomIndex)
            {
                case 0: SettingsButton_OnClick(this, null); break;
                case 1: OptionsButton_OnClick(this, null); break;
                default: RescanButton_OnClick(this, null); break;
            }
        }

        private void ToggleFavorite()
        {
            try
            {
                MainWindowViewModel.ToggleFavorite.Execute(ViewModel);
            }
            catch (Exception)
            {
                // Never let a menu action crash the launcher.
            }
        }

        public void ViewToggle_OnClick(object sender, RoutedEventArgs e) => ViewModel.SetClassicView();

        public void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            _ = (_window?.MenuBarView?.OpenSettings() ?? Task.CompletedTask);
        }

        public void RescanButton_OnClick(object sender, RoutedEventArgs e)
        {
            _window?.LoadApplications();
            FocusActiveList();
        }

        public void AddGameDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            _ = (_window?.MenuBarView?.OpenSettings() ?? Task.CompletedTask);
        }

        public void OptionsButton_OnClick(object sender, RoutedEventArgs e) => ToggleOptionsOverlay();

        private bool IsOptionsOpen() => OptionsOverlay?.IsVisible == true;

        private void ToggleOptionsOverlay()
        {
            SetOptionsOpen(!IsOptionsOpen());
        }

        private void SetOptionsOpen(bool open)
        {
            if (OptionsOverlay == null)
            {
                return;
            }

            if (open)
            {
                InitOptionsOverlay();
            }

            OptionsOverlay.IsVisible = open;

            if (open)
            {
                ConsoleSearchBox?.Focus();
            }
            else
            {
                FocusActiveList();
            }
        }

        private void InitOptionsOverlay()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            foreach (object child in (OptionsOverlay.FindControl<StackPanel>("SortOptionsPanel")?.Children ?? Enumerable.Empty<object>()))
            {
                if (child is RadioButton radio && radio.Tag is string tag &&
                    Enum.TryParse<ApplicationSort>(tag, out ApplicationSort sort))
                {
                    radio.IsChecked = IsSortedBy(vm, sort);
                }
            }

            foreach (object child in (OptionsOverlay.FindControl<StackPanel>("SortOrderPanel")?.Children ?? Enumerable.Empty<object>()))
            {
                if (child is RadioButton radio && radio.Tag is string tag)
                {
                    radio.IsChecked = (tag == "Ascending") == vm.IsAscending;
                }
            }
        }

        private static bool IsSortedBy(MainWindowViewModel vm, ApplicationSort sort) => sort switch
        {
            ApplicationSort.Favorite => vm.IsSortedByFavorite,
            ApplicationSort.Title => vm.IsSortedByTitle,
            ApplicationSort.TitleId => vm.IsSortedByTitleId,
            ApplicationSort.Developer => vm.IsSortedByDeveloper,
            ApplicationSort.TotalTimePlayed => vm.IsSortedByTimePlayed,
            ApplicationSort.LastPlayed => vm.IsSortedByLastPlayed,
            ApplicationSort.FileType => vm.IsSortedByType,
            ApplicationSort.FileSize => vm.IsSortedBySize,
            ApplicationSort.Path => vm.IsSortedByPath,
            _ => false,
        };

        public void SortOption_Checked(object sender, RoutedEventArgs args)
        {
            if (sender is RadioButton { Tag: string sortStrategy } &&
                Enum.TryParse<ApplicationSort>(sortStrategy, out ApplicationSort sort))
            {
                ViewModel.Sort(sort);
            }
        }

        public void SortOrder_Checked(object sender, RoutedEventArgs args)
        {
            if (sender is RadioButton { Tag: string sortOrder })
            {
                ViewModel.Sort(sortOrder is not "Descending");
            }
        }

        private void ConsoleSearchBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            ViewModel.SearchText = ConsoleSearchBox.Text;
        }

        private void UpdateEmptyStates()
        {
            if (DataContext is not MainWindowViewModel vm || EmptyLibraryPanel == null || EmptyFavoritesPanel == null)
            {
                return;
            }

            bool hasGames = vm.ConsoleLibrary.HasGames;
            bool showFavoritesEmpty = hasGames
                && vm.ConsoleLibrary.SelectedSection == ConsoleLibrarySection.Favorites
                && !vm.ConsoleLibrary.HasFavorites;

            // The lists stay visible underneath (their own visibility is section-bound);
            // the panels simply overlay them.
            EmptyLibraryPanel.IsVisible = !hasGames;
            EmptyFavoritesPanel.IsVisible = showFavoritesEmpty;
        }

        #endregion

        #region Blurred background

        private async Task UpdateBackgroundAsync(ApplicationData app)
        {
            if (BackgroundFront == null || BackgroundBack == null)
            {
                return;
            }

            string id = app?.IdString;
            if (id == _backgroundAppId)
            {
                return;
            }

            _backgroundAppId = id;

            if (app?.Icon is not { Length: > 0 })
            {
                FadeBackground(null);
                return;
            }

            if (!_backgroundCache.TryGetValue(id, out Bitmap bitmap))
            {
                byte[] icon = app.Icon;
                try
                {
                    bitmap = await Task.Run(() =>
                    {
                        using var stream = new MemoryStream(icon);
                        return new Bitmap(stream);
                    });
                }
                catch (Exception)
                {
                    return;
                }

                _backgroundCache[id] = bitmap;
                _backgroundCacheOrder.AddLast(id);

                while (_backgroundCacheOrder.Count > 8)
                {
                    string oldest = _backgroundCacheOrder.First.Value;
                    _backgroundCacheOrder.RemoveFirst();
                    if (_backgroundCache.Remove(oldest, out Bitmap old))
                    {
                        old.Dispose();
                    }
                }
            }

            if (id == _backgroundAppId)
            {
                FadeBackground(bitmap);
            }
        }

        private void FadeBackground(Bitmap bitmap)
        {
            bool reduce = (DataContext as MainWindowViewModel)?.ConsoleReduceAnimations == true;

            Image show = _backgroundFrontVisible ? BackgroundBack : BackgroundFront;
            Image hide = _backgroundFrontVisible ? BackgroundFront : BackgroundBack;
            _backgroundFrontVisible = !_backgroundFrontVisible;

            show.Source = bitmap;

            if (reduce)
            {
                show.Transitions = null;
                hide.Transitions = null;
                show.Opacity = bitmap == null ? 0 : 1;
                hide.Opacity = 0;
                return;
            }

            Transitions transitions = new()
            {
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) },
            };

            show.Transitions = transitions;
            hide.Transitions = transitions;
            show.Opacity = bitmap == null ? 0 : 1;
            hide.Opacity = 0;
        }

        #endregion

        #region Context menus

        private void OpenNativeContextMenu()
        {
            ListBox list = ActiveList();
            if (list?.ContextFlyout is FlyoutBase flyout && CurrentSelectedApp() != null)
            {
                ViewModel.SelectedApplication = CurrentSelectedApp();
                FlyoutBase.ShowAttachedFlyout(list);
            }
        }

        // Joystick-navigable per-game options for the gamepad (Y), mirroring the
        // classic carousel: A/B drive it while the content stays put.
        private void OpenGamepadContextMenu()
        {
            ApplicationData selected = CurrentSelectedApp();
            if (selected == null)
            {
                return;
            }

            ViewModel.SelectedApplication = selected;

            _contextMenuActions.Clear();
            _contextMenuItemBorders.Clear();

            _contextMenuActions.Add(() => MainWindowViewModel.RunApplication.Execute(ViewModel));
            _contextMenuActions.Add(() => MainWindowViewModel.ToggleFavorite.Execute(ViewModel));
            _contextMenuActions.Add(() => MainWindowViewModel.OpenTitleUpdateManager.Execute(ViewModel));
            _contextMenuActions.Add(() => MainWindowViewModel.OpenDownloadableContentManager.Execute(ViewModel));
            _contextMenuActions.Add(() => MainWindowViewModel.OpenModManager.Execute(ViewModel));

            string[] labels =
            {
                LocaleManager.Instance[LocaleKeys.GameListContextMenuRunApplication],
                LocaleManager.Instance[LocaleKeys.GameListContextMenuToggleFavorite],
                LocaleManager.Instance[LocaleKeys.GameListContextMenuManageTitleUpdates],
                LocaleManager.Instance[LocaleKeys.GameListContextMenuManageDlc],
                LocaleManager.Instance[LocaleKeys.GameListContextMenuManageMod],
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 6,
            };

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var border = new Border
                {
                    MinWidth = 230,
                    Padding = new Thickness(14, 7),
                    CornerRadius = new CornerRadius(5),
                    Child = new TextBlock
                    {
                        Text = labels[i],
                        FontWeight = FontWeight.SemiBold,
                    },
                };
                border.Tapped += (_, _) => ExecuteContextMenuItem(index);
                panel.Children.Add(border);
                _contextMenuItemBorders.Add(border);
            }

            _contextMenuIndex = 0;
            UpdateContextMenuHighlight();

            _contextMenuFlyout = new Flyout
            {
                Placement = PlacementMode.Bottom,
                Content = panel,
            };

            _contextMenuFlyout.ShowAt(ActiveList(), true);
        }

        private void MoveContextMenuSelection(int delta)
        {
            if (_contextMenuItemBorders.Count == 0)
            {
                return;
            }

            _contextMenuIndex = Math.Clamp(_contextMenuIndex + delta, 0, _contextMenuItemBorders.Count - 1);
            UpdateContextMenuHighlight();
        }

        private void UpdateContextMenuHighlight()
        {
            for (int i = 0; i < _contextMenuItemBorders.Count; i++)
            {
                _contextMenuItemBorders[i].Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(i == _contextMenuIndex ? 255 : 0), 62, 232, 200));
            }
        }

        private void ExecuteContextMenuItem(int index)
        {
            CloseContextMenu();

            if (index < 0 || index >= _contextMenuActions.Count)
            {
                return;
            }

            try
            {
                _contextMenuActions[index]();
            }
            catch (Exception)
            {
                // Never let a menu action crash the launcher.
            }
        }

        private void CloseContextMenu()
        {
            _contextMenuFlyout?.Hide();
            _contextMenuFlyout = null;
            _contextMenuIndex = 0;
        }

        #endregion

        #region Gamepad

        private void SetGamepadHints()
        {
            if (HintBar != null)
            {
                HintBar.GamepadHintsVisible = true;
            }
        }

        private void SetKeyboardHints()
        {
            if (HintBar != null)
            {
                HintBar.GamepadHintsVisible = false;
            }
        }

        private void ResetGamepadInputFlags()
        {
            _gamepadLeftPressed = false;
            _gamepadRightPressed = false;
            _gamepadUpPressed = false;
            _gamepadDownPressed = false;
            _gamepadConfirmDown = false;
            _gamepadMenuDown = false;
            _gamepadBackDown = false;
            _gamepadSortDown = false;
            _gamepadLbDown = false;
            _gamepadRbDown = false;
        }

        private void PollGamepad()
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            // Same gate as the classic carousel: a running game, a hidden view or an
            // open settings window owns the controller instead.
            if (vm.IsGameRunning ||
                !IsEffectivelyVisible ||
                !vm.IsConsoleView ||
                (_window != null && _window.SettingsWindow != null))
            {
                ResetGamepadInputFlags();
                return;
            }

            IGamepad gamepad = GetConfiguredGamepad();
            if (gamepad == null)
            {
                ResetGamepadInputFlags();
                return;
            }

            GamepadStateSnapshot snapshot = gamepad.GetMappedStateSnapshot();
            (float stickX, float stickY) = snapshot.GetStick(StickInputId.Left);

            bool left = snapshot.IsPressed(GamepadButtonInputId.DpadLeft) || stickX < -0.5f;
            bool right = snapshot.IsPressed(GamepadButtonInputId.DpadRight) || stickX > 0.5f;
            bool up = snapshot.IsPressed(GamepadButtonInputId.DpadUp) || stickY > 0.5f;
            bool down = snapshot.IsPressed(GamepadButtonInputId.DpadDown) || stickY < -0.5f;
            bool confirm = snapshot.IsPressed(GamepadButtonInputId.B);
            bool back = snapshot.IsPressed(GamepadButtonInputId.A);
            bool menu = snapshot.IsPressed(GamepadButtonInputId.Y);
            bool sort = snapshot.IsPressed(GamepadButtonInputId.X);
            bool lb = snapshot.IsPressed(GamepadButtonInputId.LeftShoulder);
            bool rb = snapshot.IsPressed(GamepadButtonInputId.RightShoulder);

            if (left || right || up || down || confirm || back || menu || sort || lb || rb)
            {
                SetGamepadHints();
            }

            bool menuOpen = _contextMenuFlyout is { IsOpen: true };
            if (menuOpen)
            {
                if (up && !_gamepadUpPressed)
                {
                    MoveContextMenuSelection(-1);
                }
                if (down && !_gamepadDownPressed)
                {
                    MoveContextMenuSelection(1);
                }
                if (confirm && !_gamepadConfirmDown)
                {
                    ExecuteContextMenuItem(_contextMenuIndex);
                }
                if (back && !_gamepadBackDown)
                {
                    CloseContextMenu();
                }
            }
            else if (IsOptionsOpen())
            {
                if (back && !_gamepadBackDown)
                {
                    SetOptionsOpen(false);
                }
            }
            else
            {
                if (lb && !_gamepadLbDown)
                {
                    CycleSection(-1);
                }
                if (rb && !_gamepadRbDown)
                {
                    CycleSection(1);
                }
                if (down && !_gamepadDownPressed)
                {
                    if (_navLevel == NavTabs)
                    {
                        _navLevel = NavContent;
                        FocusActiveList();
                    }
                    else if (_navLevel == NavContent && ActiveList() == HomeCarousel)
                    {
                        _navLevel = NavBottom;
                        _bottomIndex = 0;
                        FocusBottomButton();
                    }
                    else if (_navLevel == NavContent)
                    {
                        MoveGridVertical(1);
                    }
                }
                if (up && !_gamepadUpPressed)
                {
                    if (_navLevel == NavBottom)
                    {
                        _navLevel = NavContent;
                        FocusActiveList();
                    }
                    else if (_navLevel == NavContent && ActiveList() == HomeCarousel)
                    {
                        _navLevel = NavTabs;
                        TabBar.FocusSelectedTab();
                    }
                    else if (_navLevel == NavContent)
                    {
                        MoveGridVertical(-1);
                    }
                }
                if (left && !_gamepadLeftPressed)
                {
                    if (_navLevel == NavTabs)
                    {
                        CycleSection(-1);
                    }
                    else if (_navLevel == NavBottom)
                    {
                        MoveBottom(-1);
                    }
                    else
                    {
                        MoveBy(-1);
                    }
                }
                if (right && !_gamepadRightPressed)
                {
                    if (_navLevel == NavTabs)
                    {
                        CycleSection(1);
                    }
                    else if (_navLevel == NavBottom)
                    {
                        MoveBottom(1);
                    }
                    else
                    {
                        MoveBy(1);
                    }
                }
                if (confirm && !_gamepadConfirmDown)
                {
                    if (_navLevel == NavBottom)
                    {
                        ActivateBottom();
                    }
                    else
                    {
                        Confirm();
                    }
                }
                if (back && !_gamepadBackDown && _navLevel != NavContent)
                {
                    _navLevel = NavContent;
                    FocusActiveList();
                }
                if (menu && !_gamepadMenuDown)
                {
                    OpenGamepadContextMenu();
                }
                if (sort && !_gamepadSortDown)
                {
                    ToggleOptionsOverlay();
                }
            }

            _gamepadLeftPressed = left;
            _gamepadRightPressed = right;
            _gamepadUpPressed = up;
            _gamepadDownPressed = down;
            _gamepadConfirmDown = confirm;
            _gamepadMenuDown = menu;
            _gamepadBackDown = back;
            _gamepadSortDown = sort;
            _gamepadLbDown = lb;
            _gamepadRbDown = rb;
        }

        private IGamepad GetConfiguredGamepad()
        {
            MainWindowViewModel vm = DataContext as MainWindowViewModel;
            Ryujinx.Input.HLE.InputManager inputManager = vm?.InputManager;
            if (inputManager?.GamepadDriver == null)
            {
                return null;
            }

            Ryujinx.Common.Configuration.Hid.InputConfig config = null;
            if (vm.AppHost?.NpadManager != null)
            {
                config = vm.AppHost.NpadManager.GetPlayerInputConfigByIndex(0);
            }

            string targetId = config is Ryujinx.Common.Configuration.Hid.Controller.StandardControllerInputConfig ? config.Id : null;

            if (string.IsNullOrEmpty(targetId))
            {
                foreach (IGamepad g in inputManager.GamepadDriver.GetGamepads())
                {
                    if (g.IsConnected)
                    {
                        targetId = g.Id;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            if (_menuGamepad != null && _menuGamepad.Id == targetId)
            {
                if (_menuGamepad.IsConnected)
                {
                    return _menuGamepad;
                }

                _menuGamepad?.Dispose();
                _menuGamepad = null;
            }

            if (_menuGamepadId != targetId)
            {
                _menuGamepad?.Dispose();
                _menuGamepad = null;
                _menuGamepadId = targetId;
            }

            if (_menuGamepad == null)
            {
                try
                {
                    _menuGamepad = inputManager.GamepadDriver.GetGamepad(targetId);
                    if (_menuGamepad != null && config != null && !string.IsNullOrEmpty(config.Id))
                    {
                        _menuGamepad.SetConfiguration(config);
                    }
                }
                catch (Exception)
                {
                    _menuGamepad = null;
                }
            }

            return _menuGamepad;
        }

        #endregion
    }
}
