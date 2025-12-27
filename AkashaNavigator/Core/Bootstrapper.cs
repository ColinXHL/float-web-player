using Microsoft.Extensions.DependencyInjection;
using System;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// 应用程序启动引导器
    /// 负责初始化 DI 容器和应用程序核心组件
    /// </summary>
    public class Bootstrapper
    {
        private readonly IServiceProvider _serviceProvider;

        public Bootstrapper()
        {
            // 配置服务并构建 DI 容器
            var services = new ServiceCollection();
            services.ConfigureAppServices();
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// 启动应用程序
        /// </summary>
        public void Run()
        {
            // 从 DI 容器获取主窗口
            var mainWindow = _serviceProvider.GetRequiredService<PlayerWindow>();
            mainWindow.Show();
        }

        /// <summary>
        /// 获取服务提供者（用于需要手动解析服务的场景）
        /// </summary>
        public IServiceProvider GetServiceProvider()
        {
            return _serviceProvider;
        }
    }
}
