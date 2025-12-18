using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SandronePlayer.Helpers
{
    /// <summary>
    /// 带动画效果的窗口基类
    /// 提供打开/关闭的淡入淡出+缩放动画
    /// </summary>
    public class AnimatedWindow : Window
    {
        #region Constants

        private const double AnimationDuration = 0.2;
        private const double CloseAnimationDuration = 0.15;
        private const double ScaleFrom = 0.96;

        #endregion

        #region Fields

        private bool _isClosing = false;

        #endregion

        #region Constructor

        public AnimatedWindow()
        {
            // 设置无边框透明窗口
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            
            // 设置 Topmost=true，确保管理窗口显示在其他应用程序之上
            // 通过 Owner 关系确保显示在 PlayerWindow 之上
            Topmost = true;

            Loaded += OnWindowLoaded;
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// 获取主容器元素（子类需重写以返回带动画的容器）
        /// 容器必须命名为 MainContainer 且包含 ContainerScale 的 ScaleTransform
        /// </summary>
        protected virtual FrameworkElement? GetMainContainer()
        {
            return FindName("MainContainer") as FrameworkElement;
        }

        /// <summary>
        /// 带动画关闭窗口
        /// </summary>
        protected void CloseWithAnimation(Action? onComplete = null)
        {
            if (_isClosing) return;
            _isClosing = true;

            var container = GetMainContainer();
            if (container == null)
            {
                ActivateOwnerAndClose(onComplete);
                return;
            }

            var scaleTransform = container.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                ActivateOwnerAndClose(onComplete);
                return;
            }

            var storyboard = new Storyboard();

            // Opacity 动画
            var opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(CloseAnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnim, container);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnim);

            // ScaleX 动画
            var scaleXAnim = new DoubleAnimation
            {
                From = 1,
                To = ScaleFrom,
                Duration = TimeSpan.FromSeconds(CloseAnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleXAnim, container);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleXAnim);

            // ScaleY 动画
            var scaleYAnim = new DoubleAnimation
            {
                From = 1,
                To = ScaleFrom,
                Duration = TimeSpan.FromSeconds(CloseAnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleYAnim, container);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleYAnim);

            storyboard.Completed += (s, e) =>
            {
                ActivateOwnerAndClose(onComplete);
            };

            storyboard.Begin();
        }

        /// <summary>
        /// 激活 Owner 窗口并关闭当前窗口
        /// 这确保焦点正确返回到父窗口，而不是被其他窗口（如 Topmost 的 PlayerWindow）抢走
        /// </summary>
        private void ActivateOwnerAndClose(Action? onComplete)
        {
            onComplete?.Invoke();
            
            // 在关闭前激活 Owner 窗口，确保焦点正确返回
            // 这是处理 Topmost 窗口干扰焦点的标准做法
            if (Owner != null && Owner.IsVisible)
            {
                Owner.Activate();
            }
            
            Close();
        }

        /// <summary>
        /// 标题栏拖动支持
        /// </summary>
        protected void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        #endregion

        #region Private Methods

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            PlayOpenAnimation();
        }

        private void PlayOpenAnimation()
        {
            var container = GetMainContainer();
            if (container == null) return;

            // 初始状态
            container.Opacity = 0;
            var scaleTransform = container.RenderTransform as ScaleTransform;
            if (scaleTransform != null)
            {
                scaleTransform.ScaleX = ScaleFrom;
                scaleTransform.ScaleY = ScaleFrom;
            }

            var storyboard = new Storyboard();

            // Opacity 动画
            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(AnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnim, container);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnim);

            // ScaleX 动画
            var scaleXAnim = new DoubleAnimation
            {
                From = ScaleFrom,
                To = 1,
                Duration = TimeSpan.FromSeconds(AnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleXAnim, container);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleXAnim);

            // ScaleY 动画
            var scaleYAnim = new DoubleAnimation
            {
                From = ScaleFrom,
                To = 1,
                Duration = TimeSpan.FromSeconds(AnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleYAnim, container);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleYAnim);

            storyboard.Begin();
        }

        #endregion
    }
}
