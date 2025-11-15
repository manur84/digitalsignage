namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents the result of an operation that may succeed or fail
/// </summary>
/// <typeparam name="T">The type of value returned on success</typeparam>
public class Result<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The value returned on success (null on failure)
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// The error message describing why the operation failed
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// The exception that caused the failure (if any)
    /// </summary>
    public Exception? Exception { get; }

    private Result(bool isSuccess, T? value, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static Result<T> Success(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Success value cannot be null");

        return new Result<T>(true, value, null, null);
    }

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static Result<T> Failure(string errorMessage, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        return new Result<T>(false, default, errorMessage, exception);
    }

    /// <summary>
    /// Maps the value to another type if successful
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(ErrorMessage!, Exception);

        try
        {
            var newValue = mapper(Value!);
            return Result<TNew>.Success(newValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Mapping failed", ex);
        }
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess && Value != null)
        {
            action(Value);
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result failed
    /// </summary>
    public Result<T> OnFailure(Action<string, Exception?> action)
    {
        if (IsFailure)
        {
            action(ErrorMessage!, Exception);
        }
        return this;
    }

    /// <summary>
    /// Gets the value or throws an exception if failed
    /// </summary>
    public T GetValueOrThrow()
    {
        if (IsFailure)
        {
            if (Exception != null)
                throw new InvalidOperationException(ErrorMessage, Exception);
            throw new InvalidOperationException(ErrorMessage);
        }

        return Value!;
    }

    /// <summary>
    /// Gets the value or returns a default value if failed
    /// </summary>
    public T GetValueOrDefault(T defaultValue)
    {
        return IsSuccess && Value != null ? Value : defaultValue;
    }

    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {Value}"
            : $"Failure: {ErrorMessage}";
    }
}

/// <summary>
/// Represents the result of an operation without a return value
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The error message describing why the operation failed
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Convenience alias for error message
    /// </summary>
    public string? Error => ErrorMessage;

    /// <summary>
    /// Optional success message for consumers that expect textual feedback
    /// </summary>
    public string? SuccessMessage { get; }

    /// <summary>
    /// Combined message accessor
    /// </summary>
    public string? Message => IsSuccess ? SuccessMessage : ErrorMessage;

    /// <summary>
    /// The exception that caused the failure (if any)
    /// </summary>
    public Exception? Exception { get; }

    private Result(bool isSuccess, string? errorMessage, Exception? exception, string? successMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
        SuccessMessage = successMessage;
    }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Success(string? message = null)
    {
        return new Result(true, null, null, message);
    }

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static Result Failure(string errorMessage, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        return new Result(false, errorMessage, exception);
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            action();
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result failed
    /// </summary>
    public Result OnFailure(Action<string, Exception?> action)
    {
        if (IsFailure)
        {
            action(ErrorMessage!, Exception);
        }
        return this;
    }

    /// <summary>
    /// Throws an exception if the operation failed
    /// </summary>
    public void ThrowIfFailure()
    {
        if (IsFailure)
        {
            if (Exception != null)
                throw new InvalidOperationException(ErrorMessage, Exception);
            throw new InvalidOperationException(ErrorMessage);
        }
    }

    public override string ToString()
    {
        return IsSuccess
            ? "Success"
            : $"Failure: {ErrorMessage}";
    }
}
