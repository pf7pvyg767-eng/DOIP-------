namespace DoipSimulator.Protocols.Doip;

public sealed class DecodeResult<T>
{
    private DecodeResult(T? value, DoipProtocolError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public DoipProtocolError? Error { get; }

    public static DecodeResult<T> Success(T value) => new(value, null);

    public static DecodeResult<T> Failure(DoipProtocolError error) => new(default, error);
}
