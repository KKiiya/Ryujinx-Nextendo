using Avalonia;
using Avalonia.Controls;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.UI.Views.Console
{
    /// <summary>
    /// Contextual button hints. The parent view flips <see cref="GamepadHintsVisible"/>
    /// based on the last input used (keyboard vs gamepad).
    /// </summary>
    public partial class ButtonHintBar : RyujinxControl<MainWindowViewModel>
    {
        public static readonly StyledProperty<bool> GamepadHintsVisibleProperty =
            AvaloniaProperty.Register<ButtonHintBar, bool>(nameof(GamepadHintsVisible));

        public bool GamepadHintsVisible
        {
            get => GetValue(GamepadHintsVisibleProperty);
            set => SetValue(GamepadHintsVisibleProperty, value);
        }

        public ButtonHintBar()
        {
            InitializeComponent();
        }
    }
}
