using System;

namespace AkashaNavigator.Models.Common
{
    /// <summary>
    /// 通用的操作结果类型
    /// </summary>
    /// <typeparam name="T">成功时的返回值类型</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 成功时的值
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// 失败时的错误信息
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// 失败时的异常（可选）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static Result<T> Success(T value) =>
            new Result<T>(true, value, null, null);

        /// <summary>
        /// 创建失败结果（错误信息）
        /// </summary>
        public static Result<T> Failure(string error) =>
            new Result<T>(false, default, error, null);

        /// <summary>
        /// 创建失败结果（异常）
        /// </summary>
        public static Result<T> Failure(Exception ex) =>
            new Result<T>(false, default, ex.Message, ex);

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private Result(bool isSuccess, T? value, string? error, Exception? exception)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            Exception = exception;
        }

        /// <summary>
        /// 隐式转换：从 T 转换为 Result<T>
        /// </summary>
        public static implicit operator Result<T>(T value) => Success(value);
    }

    /// <summary>
    /// 无返回值的操作结果
    /// </summary>
    public class Result
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 失败时的错误信息
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// 失败时的异常（可选）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static Result Success() =>
            new Result(true, null, null);

        /// <summary>
        /// 创建失败结果（错误信息）
        /// </summary>
        public static Result Failure(string error) =>
            new Result(false, error, null);

        /// <summary>
        /// 创建失败结果（异常）
        /// </summary>
        public static Result Failure(Exception ex) =>
            new Result(false, ex.Message, ex);

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private Result(bool isSuccess, string? error, Exception? exception)
        {
            IsSuccess = isSuccess;
            Error = error;
            Exception = exception;
        }
    }
}
