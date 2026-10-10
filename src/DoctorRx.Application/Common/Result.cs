using System;

namespace DoctorRx.Application.Common;

public enum ResultErrorCode
{
    None = 0,
    General = 1,
    DuplicateWarning = 2,
    Validation = 3,
    NotFound = 4,
    Conflict = 5,
    ReferenceRestriction = 6
}

/// <summary>
/// Encapsulates the result of an operation with success flag, optional error message, and typed error code.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public ResultErrorCode ErrorCode { get; }

    protected Result(bool isSuccess, string? errorMessage, ResultErrorCode errorCode = ResultErrorCode.None)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, null, ResultErrorCode.None);
    public static Result Failure(string errorMessage, ResultErrorCode errorCode = ResultErrorCode.General) => new(false, errorMessage, errorCode);
}

/// <summary>
/// Encapsulates the typed result of an operation.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    protected Result(bool isSuccess, T? value, string? errorMessage, ResultErrorCode errorCode = ResultErrorCode.None)
        : base(isSuccess, errorMessage, errorCode)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, ResultErrorCode.None);
    public static new Result<T> Failure(string errorMessage, ResultErrorCode errorCode = ResultErrorCode.General) => new(false, default, errorMessage, errorCode);
}
