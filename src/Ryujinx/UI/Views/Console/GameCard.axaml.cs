using Avalonia;
using Avalonia.Controls;
using Ryujinx.Ava.Systems.AppLibrary;

namespace Ryujinx.Ava.UI.Views.Console
{
    /// <summary>
    /// Rounded game tile used by the Console library view. Presentation only:
    /// no loading or sorting logic lives here.
    /// </summary>
    public partial class GameCard : UserControl
    {
        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<GameCard, bool>(nameof(ShowTitle), true);

        public bool ShowTitle
        {
            get => GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        public GameCard()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => UpdatePlaceholder();
        }

        private void UpdatePlaceholder()
        {
            bool hasArtwork = DataContext is ApplicationData app && app.Icon is { Length: > 0 };
            if (CoverImage != null)
            {
                CoverImage.IsVisible = hasArtwork;
            }

            if (PlaceholderGrid != null)
            {
                PlaceholderGrid.IsVisible = !hasArtwork;
            }

            if (PlaceholderText != null)
            {
                string name = (DataContext as ApplicationData)?.Name;
                PlaceholderText.Text = string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
            }
        }
    }
}
