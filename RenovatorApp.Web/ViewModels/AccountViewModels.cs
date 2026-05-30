using System.ComponentModel.DataAnnotations;

namespace RenovatorApp.Web.ViewModels;

public sealed class LoginViewModel
{
    [Required]
    public string Login { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class InitialAdminViewModel
{
    [Required]
    public string CompanyName { get; set; } = "RenovatorApp";

    [Required]
    public string Login { get; set; } = "admin";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
