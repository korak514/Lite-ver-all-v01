// Views/DailyTimelineWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class DailyTimelineWindow : Window
    {
        private DailyTimelineViewModel _viewModel;

        private Point? _scrollDragStartPoint = null;
        private double _hOffset = 0;
        private double _vOffset = 0;
        private Point? _favoriteDragStartPoint = null;
        private bool _isPanning = false;

        public DailyTimelineWindow(IDataRepository repository, string tableName, DateTime targetDate)
        {
            InitializeComponent();
            _viewModel = new DailyTimelineViewModel(repository, tableName, targetDate);
            this.DataContext = _viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenDatePicker_Click(object sender, RoutedEventArgs e)
        {
            HiddenDatePicker.IsDropDownOpen = true;
        }

        private void TimelineViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && _viewModel != null)
            {
                _viewModel.UpdateViewportWidth(e.NewSize.Width);
            }
        }

        // ==========================================
        // PANNING (SWIPE) LOGIC
        // ==========================================
        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is TimelineBlockModel)
                return;

            _scrollDragStartPoint = e.GetPosition(this);
            _hOffset = TimelineScrollViewer.HorizontalOffset;
            _vOffset = TimelineScrollViewer.VerticalOffset;
            _isPanning = false;
            TimelineScrollViewer.CaptureMouse();
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_scrollDragStartPoint.HasValue)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _scrollDragStartPoint.Value.X;
                double deltaY = currentPoint.Y - _scrollDragStartPoint.Value.Y;

                if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
                {
                    _isPanning = true;
                    TimelineScrollViewer.ScrollToHorizontalOffset(_hOffset - deltaX);
                    TimelineScrollViewer.ScrollToVerticalOffset(_vOffset - deltaY);
                }
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_scrollDragStartPoint.HasValue)
            {
                _scrollDragStartPoint = null;
                TimelineScrollViewer.ReleaseMouseCapture();
            }
        }

        // ==========================================
        // CLICK ON EMPTY CANVAS -> ADD NEW EVENT
        // ==========================================
        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning) return; // Prevent triggering if they were just swiping

            if (e.OriginalSource == MainCanvas) // Ensure they didn't click a block
            {
                Point clickPoint = e.GetPosition(MainCanvas);
                _viewModel.HandleDropOrClickAdd(null, clickPoint.X, clickPoint.Y);
            }
        }

        // ==========================================
        // DRAG AND DROP LOGIC (Favorites)
        // ==========================================
        private void FavoriteBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe is Button) return;
            _favoriteDragStartPoint = e.GetPosition(null);
        }

        private void FavoriteBorder_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _favoriteDragStartPoint.HasValue)
            {
                Point currentPos = e.GetPosition(null);
                Vector diff = _favoriteDragStartPoint.Value - currentPos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement element && element.DataContext is FavoriteEvent fav)
                    {
                        _favoriteDragStartPoint = null;
                        DragDrop.DoDragDrop(element, fav, DragDropEffects.Copy);
                    }
                }
            }
        }

        private void FavoriteBorder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_favoriteDragStartPoint.HasValue)
            {
                _favoriteDragStartPoint = null;
                if (e.OriginalSource is FrameworkElement fe && fe is Button) return;
                if (sender is FrameworkElement element && element.DataContext is FavoriteEvent fav)
                {
                    _viewModel.ExecuteQuickAddFavorite(fav);
                }
            }
        }

        private void TimelineCanvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FavoriteEvent))) e.Effects = DragDropEffects.Copy;
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void TimelineCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FavoriteEvent)))
            {
                var fav = (FavoriteEvent)e.Data.GetData(typeof(FavoriteEvent));
                var canvas = sender as Canvas;

                if (canvas != null && fav != null)
                {
                    Point dropPoint = e.GetPosition(canvas);
                    _viewModel.HandleDropOrClickAdd(fav, dropPoint.X, dropPoint.Y);
                }
            }
            e.Handled = true;
        }
    }
}