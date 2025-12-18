using System.Reflection;
using System.Windows;
using System.Windows.Input;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// AboutWindow - 关于窗口
    /// </summary>
    public partial class AboutWindow : AnimatedWindow
    {
        /// <summary>
        /// 版本号（从程序集读取）
        /// </summary>
        public string VersionText { get; }

        public AboutWindow()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v0.0.0";
            DataContext = this;
            InitializeComponent();
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
    }
}
