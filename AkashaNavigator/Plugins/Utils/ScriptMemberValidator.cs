using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Utils
{
/// <summary>
/// ScriptMember 属性验证器
/// 用于验证所有 Plugin API 类的 ScriptMember 属性是否符合 camelCase 命名规范
/// </summary>
public static class ScriptMemberValidator
{
#region Validation Result Types

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否全部通过验证
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 验证的成员总数
        /// </summary>
        public int TotalMembers { get; set; }

        /// <summary>
        /// 通过验证的成员数
        /// </summary>
        public int ValidMembers { get; set; }

        /// <summary>
        /// 验证失败的成员数
        /// </summary>
        public int InvalidMembers => TotalMembers - ValidMembers;

        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new();

        /// <summary>
        /// 所有验证的成员信息
        /// </summary>
        public List<MemberInfo> Members { get; set; } = new();
    }

    /// <summary>
    /// 验证错误信息
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// 类名
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// 成员名（C# 方法/属性名）
        /// </summary>
        public string MemberName { get; set; } = string.Empty;

        /// <summary>
        /// ScriptMember 属性值
        /// </summary>
        public string ScriptMemberName { get; set; } = string.Empty;

        /// <summary>
        /// 错误类型
        /// </summary>
        public ErrorType Type { get; set; }

        /// <summary>
        /// 错误描述
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 错误类型
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// 缺少 ScriptMember 属性
        /// </summary>
        MissingAttribute,

        /// <summary>
        /// 不符合 camelCase 命名规范
        /// </summary>
        NotCamelCase,

        /// <summary>
        /// ScriptMember 值为空
        /// </summary>
        EmptyValue
    }

    /// <summary>
    /// 成员信息
    /// </summary>
    public class MemberInfo
    {
        /// <summary>
        /// 类名
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// 成员名（C# 方法/属性名）
        /// </summary>
        public string MemberName { get; set; } = string.Empty;

        /// <summary>
        /// 成员类型（Method/Property）
        /// </summary>
        public string MemberType { get; set; } = string.Empty;

        /// <summary>
        /// ScriptMember 属性值
        /// </summary>
        public string ScriptMemberName { get; set; } = string.Empty;

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }
    }

#endregion

#region Public Methods

    /// <summary>
    /// 验证所有 Plugin API 类的 ScriptMember 属性
    /// </summary>
    /// <returns>验证结果</returns>
    public static ValidationResult ValidateAllApiClasses()
    {
        var apiTypes = GetApiTypes();
        return ValidateTypes(apiTypes);
    }

    /// <summary>
    /// 验证指定类型的 ScriptMember 属性
    /// </summary>
    /// <param name="types">要验证的类型列表</param>
    /// <returns>验证结果</returns>
    public static ValidationResult ValidateTypes(IEnumerable<Type> types)
    {
        var result = new ValidationResult();

        foreach (var type in types)
        {
            ValidateType(type, result);
        }

        result.IsValid = result.Errors.Count == 0;
        result.TotalMembers = result.Members.Count;
        result.ValidMembers = result.Members.Count(m => m.IsValid);

        return result;
    }

    /// <summary>
    /// 验证单个类型的 ScriptMember 属性
    /// </summary>
    /// <param name="type">要验证的类型</param>
    /// <returns>验证结果</returns>
    public static ValidationResult ValidateType(Type type)
    {
        var result = new ValidationResult();
        ValidateType(type, result);
        result.IsValid = result.Errors.Count == 0;
        result.TotalMembers = result.Members.Count;
        result.ValidMembers = result.Members.Count(m => m.IsValid);
        return result;
    }

    /// <summary>
    /// 检查字符串是否符合 camelCase 命名规范
    /// </summary>
    /// <param name="name">要检查的名称</param>
    /// <returns>是否符合 camelCase</returns>
    public static bool IsCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // camelCase 规则：
        // 1. 第一个字符必须是小写字母
        // 2. 后续字符可以是字母或数字
        // 3. 不能包含下划线或连字符
        // 4. 不能全是大写字母

        // 第一个字符必须是小写字母
        if (!char.IsLower(name[0]))
            return false;

        // 不能包含下划线或连字符
        if (name.Contains('_') || name.Contains('-'))
            return false;

        // 使用正则表达式验证：以小写字母开头，后跟字母或数字
        var pattern = @"^[a-z][a-zA-Z0-9]*$";
        return Regex.IsMatch(name, pattern);
    }

    /// <summary>
    /// 获取所有 Plugin API 类型
    /// </summary>
    /// <returns>API 类型列表</returns>
    public static IEnumerable<Type> GetApiTypes()
    {
        // 获取所有插件 API 命名空间下的 API 类
        var assembly = typeof(ScriptMemberValidator).Assembly;
        var apiNamespaces = new[]
        {
            "AkashaNavigator.Plugins",
            "AkashaNavigator.Plugins.Apis",
            "AkashaNavigator.Plugins.Apis.Core"
        };

        var apiTypes = assembly.GetTypes()
                           .Where(t => apiNamespaces.Contains(t.Namespace) && t.IsClass && !t.IsAbstract &&
                                       t.Name.EndsWith("Api"))
                           .ToList();

        return apiTypes;
    }

#endregion

#region Private Methods

    /// <summary>
    /// 验证单个类型并将结果添加到 ValidationResult
    /// </summary>
    private static void ValidateType(Type type, ValidationResult result)
    {
        var className = type.Name;

        // 获取所有公共方法（不包括继承的 Object 方法）
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                          .Where(m => !m.IsSpecialName); // 排除属性的 get_/set_ 方法

        foreach (var method in methods)
        {
            ValidateMember(className, method.Name, "Method", method, result);
        }

        // 获取所有公共属性
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var property in properties)
        {
            ValidateMember(className, property.Name, "Property", property, result);
        }
    }

    /// <summary>
    /// 验证单个成员
    /// </summary>
    private static void ValidateMember(string className, string memberName, string memberType,
                                       System.Reflection.MemberInfo member, ValidationResult result)
    {
        // 获取 ScriptMember 属性
        var scriptMemberAttr = member.GetCustomAttribute<ScriptMemberAttribute>();

        var memberInfo = new MemberInfo { ClassName = className, MemberName = memberName, MemberType = memberType,
                                          ScriptMemberName = scriptMemberAttr?.Name ?? string.Empty, IsValid = true };

        // 检查是否有 ScriptMember 属性
        if (scriptMemberAttr == null)
        {
            memberInfo.IsValid = false;
            result.Errors.Add(new ValidationError {
                ClassName = className, MemberName = memberName, ScriptMemberName = string.Empty,
                Type = ErrorType.MissingAttribute,
                Message =
                    $"Public {memberType.ToLower()} '{memberName}' in '{className}' is missing [ScriptMember] attribute"
            });
        }
        else
        {
            var scriptName = scriptMemberAttr.Name;

            // 检查 ScriptMember 值是否为空
            if (string.IsNullOrEmpty(scriptName))
            {
                memberInfo.IsValid = false;
                result.Errors.Add(new ValidationError {
                    ClassName = className, MemberName = memberName, ScriptMemberName = scriptName ?? string.Empty,
                    Type = ErrorType.EmptyValue,
                    Message = $"[ScriptMember] attribute on '{memberName}' in '{className}' has empty value"
                });
            }
            // 检查是否符合 camelCase
            else if (!IsCamelCase(scriptName))
            {
                memberInfo.IsValid = false;
                result.Errors.Add(new ValidationError {
                    ClassName = className, MemberName = memberName, ScriptMemberName = scriptName,
                    Type = ErrorType.NotCamelCase,
                    Message = $"[ScriptMember(\"{scriptName}\")] on '{memberName}' in '{className}' is not camelCase"
                });
            }
        }

        result.Members.Add(memberInfo);
    }

#endregion
}
}
