using System.ComponentModel.DataAnnotations;
using Stockify.Common.Constants;

namespace Stockify.ViewModels.Users;

public class UserItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Initials { get; set; } = "US";
    public List<string> GrantedPermissions { get; set; } = new();
    public bool IsCurrentUser { get; set; }
}

public class UserIndexViewModel
{
    public List<UserItemViewModel> Users { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int TotalUsers => Users.Count;
    public int ActiveUsers => Users.Count(u => u.IsActive);
    public int AdminCount => Users.Count(u => u.Role == Roles.Admin);
    public int EmployeeCount => Users.Count(u => u.Role == Roles.Employee);
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [Display(Name = "الاسم الكامل")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
    [Display(Name = "رقم الهاتف")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "يرجى تحديد الدور الوظيفي")]
    [Display(Name = "الدور الوظيفي")]
    public string Role { get; set; } = Roles.Employee;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(100, ErrorMessage = "يجب أن تكون كلمة المرور 6 خانات على الأقل وبها حرف كبير ورقم", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور")]
    [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "حالة الحساب")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "الصلاحيات والصفحات المسموحة")]
    public List<string> SelectedPermissions { get; set; } = new();
}

public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [Display(Name = "الاسم الكامل")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
    [Display(Name = "رقم الهاتف")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "يرجى تحديد الدور الوظيفي")]
    [Display(Name = "الدور الوظيفي")]
    public string Role { get; set; } = Roles.Employee;

    [Display(Name = "حالة الحساب")]
    public bool IsActive { get; set; }

    [StringLength(100, ErrorMessage = "يجب أن تكون كلمة المرور 6 خانات على الأقل", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "تعيين كلمة مرور جديدة (اختياري)")]
    public string? NewPassword { get; set; }

    [Display(Name = "الصلاحيات والصفحات المسموحة")]
    public List<string> SelectedPermissions { get; set; } = new();

    public bool IsCurrentUser { get; set; }
}

public class UserResetPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [StringLength(100, ErrorMessage = "يجب ألا تقل عن 6 أحرف وتحتوي على رقم وحرف كبير", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}
