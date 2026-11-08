using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Stockify.Models.Entities;
using Stockify.ViewModels.Account;

namespace Stockify.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailInput = model.Email?.Trim() ?? string.Empty;
        var user = await _userManager.FindByEmailAsync(emailInput)
                   ?? await _userManager.FindByNameAsync(emailInput);

        // Fallback alias support: allows user to sign in with either @roven.local or @stockify.local
        if (user == null && emailInput.EndsWith("@roven.local", StringComparison.OrdinalIgnoreCase))
        {
            var altEmail = emailInput.Replace("@roven.local", "@stockify.local", StringComparison.OrdinalIgnoreCase);
            user = await _userManager.FindByEmailAsync(altEmail) ?? await _userManager.FindByNameAsync(altEmail);
        }
        else if (user == null && emailInput.EndsWith("@stockify.local", StringComparison.OrdinalIgnoreCase))
        {
            var altEmail = emailInput.Replace("@stockify.local", "@roven.local", StringComparison.OrdinalIgnoreCase);
            user = await _userManager.FindByEmailAsync(altEmail) ?? await _userManager.FindByNameAsync(altEmail);
        }
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid login credentials or account is disabled.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Invalid login credentials.");
        return View(model);
    }

    // Accepts both POST and GET to ensure seamless logout from buttons or links
    [HttpPost, HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        var model = new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Role = roles.FirstOrDefault() ?? "Employee"
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Clear password fields from validation since this form only updates profile details
        ModelState.Remove(nameof(model.CurrentPassword));
        ModelState.Remove(nameof(model.NewPassword));
        ModelState.Remove(nameof(model.ConfirmPassword));

        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "الاسم الكامل مطلوب (Full Name is required).");
            var roles = await _userManager.GetRolesAsync(user);
            model.Email = user.Email ?? string.Empty;
            model.Role = roles.FirstOrDefault() ?? "Employee";
            ViewBag.ActiveTab = "overview";
            return View("Profile", model);
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Refresh authentication cookie so FullName claim updates immediately across layout & session
            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "تم حفظ التعديلات وتحديث بيانات الحساب بنجاح في قاعدة البيانات.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        model.Email = user.Email ?? string.Empty;
        model.Role = currentRoles.FirstOrDefault() ?? "Employee";
        ViewBag.ActiveTab = "overview";
        return View("Profile", model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Clear profile fields from validation since this form only changes password
        ModelState.Remove(nameof(model.FullName));
        ModelState.Remove(nameof(model.Email));
        ModelState.Remove(nameof(model.PhoneNumber));
        ModelState.Remove(nameof(model.Role));

        var roles = await _userManager.GetRolesAsync(user);
        model.FullName = user.FullName;
        model.Email = user.Email ?? string.Empty;
        model.PhoneNumber = user.PhoneNumber;
        model.Role = roles.FirstOrDefault() ?? "Employee";
        ViewBag.ActiveTab = "security";

        if (string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "يرجى إدخال كلمة المرور الحالية.");
        }

        if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "يجب ألا تقل كلمة المرور الجديدة عن 6 أحرف.");
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "كلمة المرور الجديدة وتأكيد كلمة المرور غير متطابقين.");
        }

        if (!ModelState.IsValid)
        {
            return View("Profile", model);
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword!, model.NewPassword!);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "تم تحديث كلمة المرور وتأمين الحساب بنجاح في قاعدة البيانات.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            var msg = error.Code switch
            {
                "PasswordMismatch" => "كلمة المرور الحالية غير صحيحة.",
                "PasswordRequiresDigit" => "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل.",
                "PasswordRequiresUpper" => "يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل.",
                "PasswordRequiresLower" => "يجب أن تحتوي كلمة المرور على حرف صغير واحد على الأقل.",
                "PasswordRequiresNonAlphanumeric" => "يجب أن تحتوي كلمة المرور على رمز خاص واحد على الأقل.",
                _ => error.Description
            };
            ModelState.AddModelError(nameof(model.CurrentPassword), msg);
            ModelState.AddModelError(string.Empty, msg);
        }

        return View("Profile", model);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
