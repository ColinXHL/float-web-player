using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Helpers
{
    /// <summary>
    /// 设置 UI 渲染器
    /// 根据 SettingsUiDefinition 动态生成 WPF 控件
    /// </summary>
    public class SettingsUiRenderer
    {
        #region Events

        /// <summary>
        /// 配置值变更事件
        /// </summary>
        public event EventHandler<SettingsValueChangedEventArgs>? ValueChanged;

        /// <summary>
        /// 按钮动作事件
        /// </summary>
        public event EventHandler<SettingsButtonActionEventArgs>? ButtonAction;

        #endregion

        #region Fields

        private readonly PluginConfig _config;
        private readonly SettingsUiDefinition _definition;
        private readonly Dictionary<string, FrameworkElement> _controlMap = new();

        #endregion

        #region Constructor

        /// <summary>
        /// 创建设置 UI 渲染器
        /// </summary>
        /// <param name="definition">设置 UI 定义</param>
        /// <param name="config">插件配置</param>
        public SettingsUiRenderer(SettingsUiDefinition definition, PluginConfig config)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 渲染设置 UI
        /// </summary>
        /// <returns>包含所有设置控件的面板</returns>
        public FrameworkElement Render()
        {
            var panel = new StackPanel();

            if (_definition.Sections == null || _definition.Sections.Count == 0)
                return panel;

            foreach (var section in _definition.Sections)
            {
                RenderSection(panel, section);
            }

            return panel;
        }

        /// <summary>
        /// 获取指定键的控件
        /// </summary>
        public FrameworkElement? GetControl(string key)
        {
            return _controlMap.TryGetValue(key, out var control) ? control : null;
        }

        /// <summary>
        /// 刷新所有控件的值
        /// </summary>
        public void RefreshValues()
        {
            foreach (var kvp in _controlMap)
            {
                RefreshControlValue(kvp.Key, kvp.Value);
            }
        }

        #endregion

        #region Section Rendering

        private void RenderSection(StackPanel parent, SettingsSection section)
        {
            // 分组标题
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                var header = new TextBlock
                {
                    Text = section.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                    Margin = new Thickness(0, 16, 0, 8)
                };
                parent.Children.Add(header);
            }

            // 渲染分组内的设置项
            if (section.Items != null)
            {
                foreach (var item in section.Items)
                {
                    var control = RenderItem(item);
                    if (control != null)
                    {
                        parent.Children.Add(control);
                    }
                }
            }
        }

        #endregion

        #region Item Rendering

        private FrameworkElement? RenderItem(SettingsItem item)
        {
            return item.Type.ToLowerInvariant() switch
            {
                "text" => RenderTextBox(item),
                "number" => RenderNumberBox(item),
                "checkbox" => RenderCheckBox(item),
                "select" => RenderComboBox(item),
                "slider" => RenderSlider(item),
                "button" => RenderButton(item),
                "group" => RenderGroupBox(item),
                _ => null
            };
        }

        #endregion

        #region TextBox Rendering

        private FrameworkElement RenderTextBox(SettingsItem item)
        {
            var container = CreateItemContainer(item.Label);

            var textBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12,
                MinWidth = 200
            };

            // 设置占位符
            if (!string.IsNullOrEmpty(item.Placeholder))
            {
                // WPF 没有原生占位符，使用 Tag 存储
                textBox.Tag = item.Placeholder;
            }

            // 加载当前值或默认值
            var currentValue = GetConfigValue<string>(item.Key, item.GetDefaultValue<string>() ?? string.Empty);
            textBox.Text = currentValue;

            // 值变更事件
            textBox.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, textBox.Text);
                }
            };

            if (!string.IsNullOrEmpty(item.Key))
            {
                _controlMap[item.Key] = textBox;
            }

            container.Children.Add(textBox);
            return container;
        }

        #endregion

        #region NumberBox Rendering

        private FrameworkElement RenderNumberBox(SettingsItem item)
        {
            var container = CreateItemContainer(item.Label);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12,
                MinWidth = 100,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // 加载当前值或默认值
            var defaultValue = item.GetDefaultValue<double?>() ?? 0;
            var currentValue = GetConfigValue(item.Key, defaultValue);
            textBox.Text = currentValue.ToString();

            // 增减按钮
            var btnDecrease = CreateSpinButton("-");
            var btnIncrease = CreateSpinButton("+");
            Grid.SetColumn(btnDecrease, 1);
            Grid.SetColumn(btnIncrease, 2);

            var step = item.Step ?? 1;
            var min = item.Min ?? double.MinValue;
            var max = item.Max ?? double.MaxValue;

            // 值变更处理
            Action updateValue = () =>
            {
                if (double.TryParse(textBox.Text, out var value))
                {
                    value = Math.Max(min, Math.Min(max, value));
                    textBox.Text = value.ToString();
                    if (!string.IsNullOrEmpty(item.Key))
                    {
                        OnValueChanged(item.Key, value);
                    }
                }
            };

            textBox.LostFocus += (s, e) => updateValue();
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    updateValue();
            };

            btnDecrease.Click += (s, e) =>
            {
                if (double.TryParse(textBox.Text, out var value))
                {
                    value = Math.Max(min, value - step);
                    textBox.Text = value.ToString();
                    if (!string.IsNullOrEmpty(item.Key))
                    {
                        OnValueChanged(item.Key, value);
                    }
                }
            };

            btnIncrease.Click += (s, e) =>
            {
                if (double.TryParse(textBox.Text, out var value))
                {
                    value = Math.Min(max, value + step);
                    textBox.Text = value.ToString();
                    if (!string.IsNullOrEmpty(item.Key))
                    {
                        OnValueChanged(item.Key, value);
                    }
                }
            };

            if (!string.IsNullOrEmpty(item.Key))
            {
                _controlMap[item.Key] = textBox;
            }

            grid.Children.Add(textBox);
            grid.Children.Add(btnDecrease);
            grid.Children.Add(btnIncrease);
            container.Children.Add(grid);
            return container;
        }

        private Button CreateSpinButton(string content)
        {
            return new Button
            {
                Content = content,
                Width = 28,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        #endregion

        #region CheckBox Rendering

        private FrameworkElement RenderCheckBox(SettingsItem item)
        {
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // 使用 ToggleButton 样式的开关
            var toggle = new ToggleButton
            {
                Width = 44,
                Height = 22,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // 应用开关样式
            ApplyToggleSwitchStyle(toggle);

            // 加载当前值或默认值
            var defaultValue = item.GetDefaultValue<bool?>() ?? false;
            var currentValue = GetConfigValue(item.Key, defaultValue);
            toggle.IsChecked = currentValue;

            // 值变更事件
            toggle.Checked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, true);
                }
            };
            toggle.Unchecked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, false);
                }
            };

            if (!string.IsNullOrEmpty(item.Key))
            {
                _controlMap[item.Key] = toggle;
            }

            container.Children.Add(toggle);

            if (!string.IsNullOrEmpty(item.Label))
            {
                container.Children.Add(new TextBlock
                {
                    Text = item.Label,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            return container;
        }

        private void ApplyToggleSwitchStyle(ToggleButton toggle)
        {
            // 简化的开关样式
            toggle.Template = CreateToggleSwitchTemplate();
        }

        private ControlTemplate CreateToggleSwitchTemplate()
        {
            var template = new ControlTemplate(typeof(ToggleButton));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            var trackFactory = new FrameworkElementFactory(typeof(Border));
            trackFactory.Name = "track";
            trackFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
            trackFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));

            var thumbFactory = new FrameworkElementFactory(typeof(Border));
            thumbFactory.Name = "thumb";
            thumbFactory.SetValue(Border.WidthProperty, 18.0);
            thumbFactory.SetValue(Border.HeightProperty, 18.0);
            thumbFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            thumbFactory.SetValue(Border.BackgroundProperty, Brushes.White);
            thumbFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            thumbFactory.SetValue(Border.MarginProperty, new Thickness(2, 0, 0, 0));

            gridFactory.AppendChild(trackFactory);
            gridFactory.AppendChild(thumbFactory);

            template.VisualTree = gridFactory;

            // 触发器
            var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "track"));
            checkedTrigger.Setters.Add(new Setter(Border.MarginProperty, new Thickness(24, 0, 0, 0), "thumb"));
            template.Triggers.Add(checkedTrigger);

            return template;
        }

        #endregion

        #region ComboBox Rendering

        private FrameworkElement RenderComboBox(SettingsItem item)
        {
            var container = CreateItemContainer(item.Label);

            var comboBox = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12,
                MinWidth = 150
            };

            // 添加选项
            if (item.Options != null)
            {
                foreach (var option in item.Options)
                {
                    comboBox.Items.Add(new ComboBoxItem
                    {
                        Content = option.Label,
                        Tag = option.Value
                    });
                }
            }

            // 加载当前值或默认值
            var defaultValue = item.GetDefaultValue<string>() ?? string.Empty;
            var currentValue = GetConfigValue(item.Key, defaultValue);

            // 选中匹配的项
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem cbi && cbi.Tag?.ToString() == currentValue)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // 值变更事件
            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem && !string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, selectedItem.Tag?.ToString() ?? string.Empty);
                }
            };

            if (!string.IsNullOrEmpty(item.Key))
            {
                _controlMap[item.Key] = comboBox;
            }

            container.Children.Add(comboBox);
            return container;
        }

        #endregion

        #region Slider Rendering

        private FrameworkElement RenderSlider(SettingsItem item)
        {
            var container = CreateItemContainer(item.Label);

            var sliderContainer = new Grid();
            sliderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var slider = new Slider
            {
                Minimum = item.Min ?? 0,
                Maximum = item.Max ?? 100,
                TickFrequency = item.Step ?? 1,
                IsSnapToTickEnabled = item.Step.HasValue,
                MinWidth = 150,
                VerticalAlignment = VerticalAlignment.Center
            };

            var valueLabel = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                FontSize = 12,
                MinWidth = 40,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(valueLabel, 1);

            // 加载当前值或默认值
            var defaultValue = item.GetDefaultValue<double?>() ?? item.Min ?? 0;
            var currentValue = GetConfigValue(item.Key, defaultValue);
            slider.Value = currentValue;
            valueLabel.Text = currentValue.ToString("F0");

            // 值变更事件
            slider.ValueChanged += (s, e) =>
            {
                valueLabel.Text = slider.Value.ToString("F0");
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, slider.Value);
                }
            };

            if (!string.IsNullOrEmpty(item.Key))
            {
                _controlMap[item.Key] = slider;
            }

            sliderContainer.Children.Add(slider);
            sliderContainer.Children.Add(valueLabel);
            container.Children.Add(sliderContainer);
            return container;
        }

        #endregion

        #region Button Rendering

        private FrameworkElement RenderButton(SettingsItem item)
        {
            var button = new Button
            {
                Content = item.Label ?? "按钮",
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            button.Click += (s, e) =>
            {
                OnButtonAction(item.Action ?? string.Empty);
            };

            return button;
        }

        #endregion

        #region GroupBox Rendering

        private FrameworkElement RenderGroupBox(SettingsItem item)
        {
            var groupBox = new GroupBox
            {
                Header = item.Label,
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(8)
            };

            var content = new StackPanel();

            if (item.Items != null)
            {
                foreach (var subItem in item.Items)
                {
                    var control = RenderItem(subItem);
                    if (control != null)
                    {
                        content.Children.Add(control);
                    }
                }
            }

            groupBox.Content = content;
            return groupBox;
        }

        #endregion

        #region Helper Methods

        private StackPanel CreateItemContainer(string? label)
        {
            var container = new StackPanel
            {
                Margin = new Thickness(0, 8, 0, 0)
            };

            if (!string.IsNullOrEmpty(label))
            {
                container.Children.Add(new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            return container;
        }

        private T GetConfigValue<T>(string? key, T defaultValue)
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;

            return _config.Get(key, defaultValue);
        }

        private void RefreshControlValue(string key, FrameworkElement control)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.Text = _config.Get(key, string.Empty);
                    break;
                case ToggleButton toggle:
                    toggle.IsChecked = _config.Get(key, false);
                    break;
                case ComboBox comboBox:
                    var value = _config.Get(key, string.Empty);
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        if (comboBox.Items[i] is ComboBoxItem cbi && cbi.Tag?.ToString() == value)
                        {
                            comboBox.SelectedIndex = i;
                            break;
                        }
                    }
                    break;
                case Slider slider:
                    slider.Value = _config.Get(key, 0.0);
                    break;
            }
        }

        private void OnValueChanged(string key, object value)
        {
            _config.Set(key, value);
            ValueChanged?.Invoke(this, new SettingsValueChangedEventArgs(key, value));
        }

        private void OnButtonAction(string action)
        {
            ButtonAction?.Invoke(this, new SettingsButtonActionEventArgs(action));
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// 设置值变更事件参数
    /// </summary>
    public class SettingsValueChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object Value { get; }

        public SettingsValueChangedEventArgs(string key, object value)
        {
            Key = key;
            Value = value;
        }
    }

    /// <summary>
    /// 按钮动作事件参数
    /// </summary>
    public class SettingsButtonActionEventArgs : EventArgs
    {
        public string Action { get; }

        public SettingsButtonActionEventArgs(string action)
        {
            Action = action;
        }
    }

    #endregion
}
