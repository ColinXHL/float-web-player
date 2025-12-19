using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SandronePlayer.Helpers;

namespace SandronePlayer.Views
{
    /// <summary>
    /// 首次启动欢迎弹窗
    /// </summary>
    public partial class WelcomeDialog : AnimatedWindow
    {
        private const string GitHubUrl = "https://github.com/ColinXHL/sandrone-player";

        public WelcomeDialog()
        {
            InitializeComponent();
        }

        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // 忽略打开链接失败
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
