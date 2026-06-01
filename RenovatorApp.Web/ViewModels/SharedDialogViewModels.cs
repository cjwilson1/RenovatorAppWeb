namespace RenovatorApp.Web.ViewModels;

public sealed class ConfirmationDialogViewModel
{
    public string ModalId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Controller { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string RouteId { get; init; } = string.Empty;
    public string ConfirmText { get; init; } = "Yes";
    public string CancelText { get; init; } = "No";
    public string ConfirmButtonClass { get; init; } = "btn-danger";
}
