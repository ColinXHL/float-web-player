using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// BookmarkPopup - 收藏夹弹出窗口
    /// </summary>
    public partial class BookmarkPopup : AnimatedWindow
    {
        #region Events

        /// <summary>
        /// 选择收藏项事件
        /// </summary>000
        public event EventHandler<string>? BookmarkItemSelected;

        #endregion

        #region Constructor

        public BookmarkPopup()
        {
            InitializeComponent();
            LoadBookmarks();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载收藏夹
        /// </summary>
        private void LoadBookmarks()
        {
            var searchText = SearchBox.Text.Trim();
            var bookmarks = string.IsNullOrEmpty(searchText)
                ? DataService.Instance.GetBookmarks()
                : DataService.Instance.SearchBookmarks(searchText);
            
            BookmarkList.ItemsSource = bookmarks;
            
            // 更新空状态提示
            EmptyHint.Visibility = bookmarks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 搜索框文本变化
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadBookmarks();
        }

        /// <summary>
        /// 清空全部
        /// </summary>
        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清空所有收藏吗？此操作不可撤销。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DataService.Instance.ClearBookmarks();
                LoadBookmarks();
            }
        }

        /// <summary>
        /// 删除单项
        /// </summary>
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                DataService.Instance.DeleteBookmark(id);
                LoadBookmarks();
            }
        }

        /// <summary>
        /// 双击打开链接
        /// </summary>
        private void BookmarkList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarkList.SelectedItem is BookmarkItem item)
            {
                CloseWithAnimation(() => BookmarkItemSelected?.Invoke(this, item.Url));
            }
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        #endregion
    }
}
