using System.Windows;
using System.Windows.Controls;
using Img2Pdf.App.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Img2Pdf.App;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedPaths && droppedPaths.Length > 0)
            {
                await ViewModel.AddPathsAsync(droppedPaths);
            }
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void OnAddFilesClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = LocalizationService.GetString("Dialog_SelectImagesTitle"),
            Filter = LocalizationService.GetString("Dialog_ImageFilter")
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.AddPathsAsync(dialog.FileNames);
        }
    }

    private async void OnAddFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.GetString("Dialog_SelectFolderTitle")
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.AddPathsAsync([dialog.FolderName]);
        }
    }

    private void OnBrowseFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.GetString("Dialog_SelectOutputTitle")
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.CustomOutputFolder = dialog.FolderName;
        }
    }

    private void OnToggleThemeClicked(object sender, RoutedEventArgs e)
    {
        var current = ApplicationThemeManager.GetAppTheme();
        var target = current == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(target);
    }

    private void OnLanguageMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            var menu = new ContextMenu();
            foreach (var lang in LocalizationService.Instance.SupportedLanguages)
            {
                var item = new MenuItem
                {
                    Header = $"{lang.Flag} {lang.NativeName}",
                    IsChecked = LocalizationService.Instance.CurrentLanguage.Code == lang.Code,
                    IsCheckable = true
                };
                item.Click += (_, _) =>
                {
                    LocalizationService.Instance.SetLanguage(lang.Code);
                };
                menu.Items.Add(item);
            }
            menu.PlacementTarget = element;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
