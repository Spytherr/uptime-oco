namespace uptime_oco;

public enum ServiceErrorCategory
{
    NotFound,
    ValidationError,
    Conflict,
    Forbidden
}

public class ServiceResult
{
    public bool IsSuccess { get; }
    public ServiceErrorCategory? Error { get; }
    public string? ErrorMessage { get; }

    protected ServiceResult(bool isSuccess, ServiceErrorCategory? error, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorMessage = errorMessage;
    }

    public static ServiceResult Success() => new(true, null, null);
    public static ServiceResult Failure(ServiceErrorCategory error, string message) =>
        new(false, error, message);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; }

    private ServiceResult(bool isSuccess, T? value, ServiceErrorCategory? error, string? errorMessage)
        : base(isSuccess, error, errorMessage)
    {
        Value = value;
    }

    public static ServiceResult<T> Success(T value) => new(true, value, null, null);
    public new static ServiceResult<T> Failure(ServiceErrorCategory error, string message) =>
        new(false, default, error, message);
}
