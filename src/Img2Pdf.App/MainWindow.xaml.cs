using System.Windows;
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
            Title = "Velg bilder som skal konverteres",
            Filter = "Bildefiler (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Alle filer (*.*)|*.*"
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
            Title = "Velg mappe med bilder"
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
            Title = "Velg mappe for lagring av PDF-filer"
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
}
