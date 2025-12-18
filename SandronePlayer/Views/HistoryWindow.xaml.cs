using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SandronePlayer.Helpers;
using SandronePlayer.Models;
using SandronePlayer.Services;

namespace SandronePlayer.Views
{
    /// <summary>
    /// HistoryWindow - 历史记录窗口
    /// </summary>
    public partial class HistoryWindow : AnimatedWindow
    {
        #region Events

        /// <summary>
        /// 选择历史记录项事件
        /// </summary>
        public event EventHandler<string>? HistoryItemSelected;

        #endregion

        #region Constructor

        public HistoryWindow()
        {
            InitializeComponent();
            LoadHistory();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载历史记录
        /// </summary>
        private void LoadHistory()
        {
            var searchText = SearchBox.Text.Trim();
            var history = string.IsNullOrEmpty(searchText)
                ? DataService.Instance.GetHistory()
                : DataService.Instance.SearchHistory(searchText);
            
            HistoryList.ItemsSource = history;
            
            // 更新空状态提示
            EmptyHint.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 搜索框文本变化
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadHistory();
        }

        /// <summary>
        /// 删除单项
        /// </summary>
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                DataService.Instance.DeleteHistory(id);
                LoadHistory();
            }
        }

        /// <summary>
        /// 清空全部
        /// </summary>
        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清空所有历史记录吗？此操作不可撤销。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DataService.Instance.ClearHistory();
                LoadHistory();
            }
        }

        /// <summary>
        /// 双击打开链接
        /// </summary>
        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryList.SelectedItem is HistoryItem item)
            {
                CloseWithAnimation(() => HistoryItemSelected?.Invoke(this, item.Url));
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
