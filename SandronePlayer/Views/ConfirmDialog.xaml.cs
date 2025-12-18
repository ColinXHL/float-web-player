using System;
using System.Windows;
using SandronePlayer.Helpers;

namespace SandronePlayer.Views
{
    /// <summary>
    /// ConfirmDialog - 确认对话框
    /// 继承 AnimatedWindow，提供淡入淡出动画效果
    /// 用于替代系统 MessageBox 的确认对话框
    /// </summary>
    public partial class ConfirmDialog : AnimatedWindow
    {
        #region Properties

        /// <summary>
        /// 对话框结果：true=确定，false=取消，null=关闭按钮
        /// </summary>
        public bool? Result { get; private set; } = null;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建确认对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="confirmText">确定按钮文本</param>
        /// <param name="cancelText">取消按钮文本</param>
        public ConfirmDialog(string message, string? title = null, 
                            string confirmText = "确定", string cancelText = "取消")
        {
            InitializeComponent();

            // 设置消息
            MessageText.Text = message;

            // 设置标题
            if (!string.IsNullOrWhiteSpace(title))
            {
                TitleText.Text = title;
            }

            // 设置按钮文本
            BtnConfirm.Content = confirmText;
            BtnCancel.Content = cancelText;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 确定按钮点击
        /// </summary>
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            CloseWithAnimation();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseWithAnimation();
        }

        /// <summary>
        /// 关闭按钮点击（返回 false）
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseWithAnimation();
        }

        #endregion
    }
}
