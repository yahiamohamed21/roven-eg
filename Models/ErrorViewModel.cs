namespace Stockify.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string? OriginalPath { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ExceptionType { get; set; }

    public string? StackTrace { get; set; }
}
