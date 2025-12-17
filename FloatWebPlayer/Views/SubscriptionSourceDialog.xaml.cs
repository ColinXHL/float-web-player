using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 订阅源管理对话框
    /// </summary>
    public partial class SubscriptionSourceDialog : Window
    {
        private bool _hasChanges = false;

        public SubscriptionSourceDialog()
        {
            InitializeComponent();
            Resources.Add("BoolToVisibilityConverter", new BoolToVisibilityConverter());
            Loaded += SubscriptionSourceDialog_Loaded;
            UrlInput.TextChanged += UrlInput_TextChanged;
        }

        private void SubscriptionSourceDialog_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSourceList();
            UrlInput.Focus();
        }

        private void UrlInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UrlPlaceholder.Visibility = string.IsNullOrEmpty(UrlInput.Text) 
                ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 刷新订阅源列表
        /// </summary>
        private void RefreshSourceList()
        {
            var sources = ProfileMarketplaceService.Instance.GetSubscriptionSources();
            var viewModels = sources.Select(s => new SubscriptionSourceViewModel(s)).ToList();
            
            SourceList.ItemsSource = viewModels;
            NoSourcesText.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// URL 输入框按键事件
        /// </summary>
        private void UrlInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddSource();
            }
        }

        /// <summary>
        /// 添加按钮点击
        /// </summary>
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            AddSource();
        }

        /// <summary>
        /// 添加订阅源
        /// </summary>
        private async void AddSource()
        {
            var url = UrlInput.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                NotificationService.Instance.Info("请输入订阅源 URL", "提示");
                return;
            }

            // 禁用输入
            UrlInput.IsEnabled = false;
            BtnAdd.IsEnabled = false;
            BtnAdd.Content = "添加中...";

            try
            {
                var result = await ProfileMarketplaceService.Instance.AddSubscriptionSourceAsync(url);
                
                if (result.IsSuccess)
                {
                    UrlInput.Text = string.Empty;
                    RefreshSourceList();
                    _hasChanges = true;

                    var message = $"订阅源添加成功！";
                    if (!string.IsNullOrEmpty(result.SourceName))
                    {
                        message += $"\n\n名称: {result.SourceName}";
                    }
                    if (result.ProfileCount > 0)
                    {
                        message += $"\n包含 {result.ProfileCount} 个 Profile";
                    }
                    
                    NotificationService.Instance.Success(message, "添加成功");
                }
                else
                {
                    NotificationService.Instance.Error($"添加失败: {result.ErrorMessage}", "添加失败");
                }
            }
            finally
            {
                UrlInput.IsEnabled = true;
                BtnAdd.IsEnabled = true;
                BtnAdd.Content = "添加";
                UrlInput.Focus();
            }
        }

        /// <summary>
        /// 删除订阅源按钮点击
        /// </summary>
        private async void BtnRemoveSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url)
            {
                var confirmed = await NotificationService.Instance.ConfirmAsync(
                    $"确定要删除此订阅源吗？\n\n{url}",
                    "确认删除");

                if (confirmed)
                {
                    ProfileMarketplaceService.Instance.RemoveSubscriptionSource(url);
                    RefreshSourceList();
                    _hasChanges = true;
                }
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _hasChanges;
            Close();
        }
    }

    /// <summary>
    /// 订阅源视图模型
    /// </summary>
    public class SubscriptionSourceViewModel
    {
        private readonly MarketplaceSource _source;

        public SubscriptionSourceViewModel(MarketplaceSource source)
        {
            _source = source;
        }

        public string Url => _source.Url;
        public string Name => _source.Name;
        public bool Enabled => _source.Enabled;
        public DateTime? LastFetched => _source.LastFetched;

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "未命名订阅源" : Name;
        public bool HasLastFetched => LastFetched.HasValue;
        public string LastFetchedText => LastFetched.HasValue 
            ? $"上次更新: {LastFetched.Value:yyyy-MM-dd HH:mm}" 
            : string.Empty;
    }
}
