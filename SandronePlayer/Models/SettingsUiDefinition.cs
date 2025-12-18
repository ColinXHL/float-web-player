using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 设置 UI 定义模型
    /// 对应 settings_ui.json 文件，描述插件设置界面
    /// </summary>
    public class SettingsUiDefinition
    {
        /// <summary>
        /// 设置分组列表
        /// </summary>
        [JsonPropertyName("sections")]
        public List<SettingsSection>? Sections { get; set; }

        #region Static Methods

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 从文件加载设置 UI 定义
        /// </summary>
        public static SettingsUiDefinition? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return LoadFromJson(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从 JSON 字符串加载设置 UI 定义
        /// </summary>
        public static SettingsUiDefinition? LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<SettingsUiDefinition>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 设置分组
    /// </summary>
    public class SettingsSection
    {
        /// <summary>
        /// 分组标题
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// 分组内的设置项
        /// </summary>
        [JsonPropertyName("items")]
        public List<SettingsItem>? Items { get; set; }
    }

    /// <summary>
    /// 设置项
    /// </summary>
    public class SettingsItem
    {
        /// <summary>
        /// 控件类型：text, number, checkbox, select, slider, button, group
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 配置键（用于存储值）
        /// </summary>
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        /// <summary>
        /// 显示标签
        /// </summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        [JsonPropertyName("default")]
        public JsonElement? Default { get; set; }

        /// <summary>
        /// 占位符文本（用于 text 类型）
        /// </summary>
        [JsonPropertyName("placeholder")]
        public string? Placeholder { get; set; }

        /// <summary>
        /// 最小值（用于 number, slider 类型）
        /// </summary>
        [JsonPropertyName("min")]
        public double? Min { get; set; }

        /// <summary>
        /// 最大值（用于 number, slider 类型）
        /// </summary>
        [JsonPropertyName("max")]
        public double? Max { get; set; }

        /// <summary>
        /// 步进值（用于 number, slider 类型）
        /// </summary>
        [JsonPropertyName("step")]
        public double? Step { get; set; }

        /// <summary>
        /// 选项列表（用于 select 类型）
        /// </summary>
        [JsonPropertyName("options")]
        public List<SelectOption>? Options { get; set; }

        /// <summary>
        /// 按钮动作（用于 button 类型）
        /// </summary>
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        /// <summary>
        /// 子项（用于 group 类型）
        /// </summary>
        [JsonPropertyName("items")]
        public List<SettingsItem>? Items { get; set; }

        /// <summary>
        /// 获取默认值
        /// </summary>
        public T? GetDefaultValue<T>()
        {
            if (Default == null || Default.Value.ValueKind == JsonValueKind.Undefined)
                return default;

            try
            {
                return Default.Value.Deserialize<T>();
            }
            catch
            {
                return default;
            }
        }
    }

    /// <summary>
    /// 下拉框选项
    /// </summary>
    public class SelectOption
    {
        /// <summary>
        /// 选项值
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 显示标签
        /// </summary>
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// 内置按钮动作
    /// </summary>
    public static class SettingsButtonActions
    {
        /// <summary>
        /// 进入覆盖层编辑模式
        /// </summary>
        public const string EnterEditMode = "enterEditMode";

        /// <summary>
        /// 重置配置为默认值
        /// </summary>
        public const string ResetConfig = "resetConfig";

        /// <summary>
        /// 打开插件目录
        /// </summary>
        public const string OpenPluginFolder = "openPluginFolder";

        /// <summary>
        /// 检查是否为内置动作
        /// </summary>
        public static bool IsBuiltInAction(string? action)
        {
            if (string.IsNullOrEmpty(action))
                return false;

            return action == EnterEditMode ||
                   action == ResetConfig ||
                   action == OpenPluginFolder;
        }
    }
}
