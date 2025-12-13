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
        public AboutWindow()
        {
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
