using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Img2Pdf.App.Models;
using Img2Pdf.App.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Img2Pdf.App;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private int _lastSortColumn = -1;
    private bool _sortAscending = true;
    private Point _dragStartPoint;
    private bool _isDragging;

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

    private void OnListViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnListViewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var item = GetItemAtPoint(e.GetPosition(FilesListView)) ?? ViewModel.SelectedItem;
                if (item != null)
                {
                    _isDragging = true;
                    try
                    {
                        var data = new DataObject("Img2PdfItemReorder", item);
                        DragDrop.DoDragDrop(FilesListView, data, DragDropEffects.Move);
                    }
                    finally
                    {
                        _isDragging = false;
                    }
                }
            }
        }
    }

    private void OnListViewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("Img2PdfItemReorder"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnListViewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped && dropped.Length > 0)
            {
                await ViewModel.AddPathsAsync(dropped);
                e.Handled = true;
            }
        }
        else if (e.Data.GetDataPresent("Img2PdfItemReorder"))
        {
            if (e.Data.GetData("Img2PdfItemReorder") is ImageFileItem sourceItem)
            {
                var targetItem = GetItemAtPoint(e.GetPosition(FilesListView));
                if (targetItem != null && targetItem != sourceItem)
                {
                    int oldIndex = ViewModel.Files.IndexOf(sourceItem);
                    int newIndex = ViewModel.Files.IndexOf(targetItem);
                    ViewModel.MoveItem(oldIndex, newIndex);
                    ViewModel.SelectedItem = sourceItem;
                }
            }
            e.Handled = true;
        }
    }

    private ImageFileItem? GetItemAtPoint(Point point)
    {
        var hitResult = VisualTreeHelper.HitTest(FilesListView, point);
        if (hitResult?.VisualHit == null) return null;

        DependencyObject? current = hitResult.VisualHit;
        while (current != null && current != FilesListView)
        {
            if (current is ListViewItem item && item.DataContext is ImageFileItem fileItem)
            {
                return fileItem;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OnListViewItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.DataContext is ImageFileItem fileItem)
        {
            ViewModel.OpenPreview(fileItem);
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (FocusManager.GetFocusedElement(this) is TextBox)
        {
            return;
        }

        if (ViewModel.IsPreviewOpen)
        {
            if (e.Key == Key.Escape || e.Key == Key.Space)
            {
                ViewModel.ClosePreview();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                ViewModel.PreviousPreview();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                ViewModel.NextPreview();
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                ViewModel.RotateSelectedCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.C)
            {
                ViewModel.ToggleCropModeCommand.Execute(null);
                e.Handled = true;
            }
        }
        else
        {
            if (e.Key == Key.Space && ViewModel.SelectedItem != null)
            {
                ViewModel.OpenPreview();
                e.Handled = true;
            }
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

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
        {
            if (sender is ListView listView && listView.View is GridView gridView)
            {
                int colIndex = gridView.Columns.IndexOf(header.Column);
                if (colIndex >= 0)
                {
                    if (_lastSortColumn == colIndex)
                    {
                        _sortAscending = !_sortAscending;
                    }
                    else
                    {
                        _lastSortColumn = colIndex;
                        _sortAscending = true;
                    }

                    ViewModel.SortFiles(colIndex, _sortAscending);
                }
            }
        }
    }
}
