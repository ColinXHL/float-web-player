using System;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 配置 API
    /// 提供插件配置的读写功能
    /// 支持点号分隔的路径（如 "overlay.x"）
    /// 无需权限
    /// </summary>
    public class ConfigApi
    {
        #region Fields

        private readonly PluginConfig _config;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建配置 API 实例
        /// </summary>
        /// <param name="config">插件配置</param>
        public ConfigApi(PluginConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion

        #region Methods

        /// <summary>
        /// 获取配置值
        /// 支持点号分隔的路径，如 "overlay.x"
        /// </summary>
        /// <param name="key">配置键（支持点号路径）</param>
        /// <param name="defaultValue">默认值（可选）</param>
        /// <returns>配置值或默认值</returns>
        public object? Get(string key, object? defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            try
            {
                // 使用 PluginConfig 的 Get 方法
                var value = _config.Get<object?>(key);
                Services.LogService.Instance.Debug("ConfigApi", $"Get({key}) = {value ?? "null"}, defaultValue = {defaultValue ?? "null"}");
                return value ?? defaultValue;
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error("ConfigApi", $"Get({key}) failed: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 获取配置值（泛型版本）
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值或默认值</returns>
        public T Get<T>(string key, T defaultValue = default!)
        {
            return _config.Get(key, defaultValue);
        }

        /// <summary>
        /// 设置配置值
        /// 支持点号分隔的路径，如 "overlay.x"
        /// 设置后自动保存到文件
        /// </summary>
        /// <param name="key">配置键（支持点号路径）</param>
        /// <param name="value">配置值</param>
        public void Set(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _config.Set(key, value);

            // 自动保存
            try
            {
                _config.SaveToFile();
            }
            catch
            {
                // 忽略保存错误
            }
        }

        /// <summary>
        /// 检查配置键是否存在
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否存在</returns>
        public bool Has(string key)
        {
            return _config.ContainsKey(key);
        }

        /// <summary>
        /// 移除配置键
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(string key)
        {
            var result = _config.Remove(key);
            if (result)
            {
                try
                {
                    _config.SaveToFile();
                }
                catch
                {
                    // 忽略保存错误
                }
            }
            return result;
        }

        #endregion
    }
}
