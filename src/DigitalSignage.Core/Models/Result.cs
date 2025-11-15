namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents the outcome of an operation with success flag and message.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string Message { get; }
    public string? Error => IsSuccess ? null : Message;

    private Result(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static Result Success(string message = "Operation completed successfully")
        => new Result(true, message);

    public static Result Failure(string error)
        => new Result(false, error);
}
