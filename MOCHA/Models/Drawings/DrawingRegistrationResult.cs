namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面登録・更新の結果
/// </summary>
public sealed class DrawingRegistrationResult
{
    private DrawingRegistrationResult(bool succeeded, string? error, DrawingDocument? document)
    {
        Succeeded = succeeded;
        Error = error;
        Document = document;
    }

    public bool Succeeded { get; }
    public string? Error { get; }
    public DrawingDocument? Document { get; }

    public static DrawingRegistrationResult Success(DrawingDocument document)
    {
        return new DrawingRegistrationResult(true, null, document);
    }

    public static DrawingRegistrationResult Fail(string error)
    {
        return new DrawingRegistrationResult(false, error, null);
    }
}
