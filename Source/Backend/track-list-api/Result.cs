namespace api;

/// <summary>
///     class for returning Result type. Contains IsSuccess, Error message and Data.
///     call Ok() if method ended successfully
///     call Fail() if method ended with an error
///     data field will be default, if operation wasn't successful
/// </summary>
public class Result
{
	protected Result(bool isSuccess, string error)
	{
		if (isSuccess && !string.IsNullOrEmpty(error))
			throw new InvalidOperationException("Success result cannot have error message");
		if (!isSuccess && string.IsNullOrEmpty(error))
			throw new InvalidOperationException("Failure result must have error message");

		IsSuccess = isSuccess;
		Error = error;
	}
	public bool IsSuccess { get; }

	public bool IsFailure => !IsSuccess;

	public string Error { get; }

	public static Result Ok() => new(true, string.Empty);

	public static Result<T> Ok<T>(T value) => new(value, true, string.Empty);
	public static Result Fail(string error) => new(false, error);

	public static Result<T> Fail<T>(string error) => new(default, false, error);
}

public class Result<T> : Result
{
	protected internal Result(T? value, bool isSuccess, string error) : base(isSuccess, error)
	{
		ValueOrDefault = value;
	}

	public T Value => IsSuccess
		? ValueOrDefault!
		: throw new InvalidOperationException($"The result of value is failure: {Error}");

	private T? ValueOrDefault { get; }
}
