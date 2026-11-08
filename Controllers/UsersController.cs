using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Models.Entities;
using Stockify.ViewModels.Users;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
    }

    // GET: /Users
    public async Task<IActionResult> Index(string? search)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s) || (u.Email != null && u.Email.ToLower().Contains(s)));
        }

        var usersList = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var viewModels = new List<UserItemViewModel>();

        foreach (var u in usersList)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var roleName = roles.FirstOrDefault() ?? Roles.Employee;

            List<string> perms;
            if (!string.IsNullOrWhiteSpace(u.Permissions))
            {
                perms = u.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else
            {
                perms = (roleName == Roles.Admin ? Permissions.DefaultAdmin : Permissions.DefaultSales).ToList();
            }

            viewModels.Add(new UserItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                PhoneNumber = u.PhoneNumber,
                Role = roleName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                Initials = GetInitials(u.FullName),
                GrantedPermissions = perms,
                IsCurrentUser = (u.Id == currentUserId)
            });
        }

        var model = new UserIndexViewModel
        {
            Users = viewModels,
            SearchTerm = search
        };

        return View(model);
    }

    // GET: /Users/Create
    public IActionResult Create()
    {
        ViewBag.AllPermissions = Permissions.All;
        var model = new UserCreateViewModel
        {
            Role = Roles.Employee,
            IsActive = true,
            SelectedPermissions = Permissions.DefaultSales.ToList()
        };
        return View(model);
    }

    // POST: /Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        ViewBag.AllPermissions = Permissions.All;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "البريد الإلكتروني مسجل بالفعل لمستخدم آخر.");
            return View(model);
        }

        // Determine permissions string
        var permissionsList = model.SelectedPermissions != null && model.SelectedPermissions.Any()
            ? model.SelectedPermissions.Distinct().ToList()
            : (model.Role == Roles.Admin ? Permissions.DefaultAdmin.ToList() : Permissions.DefaultSales.ToList());

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            PhoneNumber = model.PhoneNumber?.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            Permissions = string.Join(",", permissionsList)
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // Assign Role
        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            await _roleManager.CreateAsync(new IdentityRole(model.Role));
        }
        await _userManager.AddToRoleAsync(user, model.Role);

        TempData["Success"] = $"تم إنشاء حساب المستخدم {user.FullName} بنجاح وتعيين الصلاحيات المحددة.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Users/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "المستخدم غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roles = await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault() ?? Roles.Employee;

        List<string> selectedPerms;
        if (!string.IsNullOrWhiteSpace(user.Permissions))
        {
            selectedPerms = user.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        else
        {
            selectedPerms = (roleName == Roles.Admin ? Permissions.DefaultAdmin : Permissions.DefaultSales).ToList();
        }

        ViewBag.AllPermissions = Permissions.All;

        var model = new UserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            Role = roleName,
            IsActive = user.IsActive,
            SelectedPermissions = selectedPerms,
            IsCurrentUser = (user.Id == currentUserId)
        };

        return View(model);
    }

    // POST: /Users/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        ViewBag.AllPermissions = Permissions.All;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            TempData["Error"] = "المستخدم غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isCurrent = (user.Id == currentUserId);

        // Prevent self-deactivation
        if (isCurrent && !model.IsActive)
        {
            ModelState.AddModelError("IsActive", "لا يمكنك تعطيل حسابك الشخصي الذي تستخدمه لتسجيل الدخول حالياً.");
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        user.IsActive = model.IsActive;

        // Update permissions string
        var permissionsList = model.SelectedPermissions != null && model.SelectedPermissions.Any()
            ? model.SelectedPermissions.Distinct().ToList()
            : (model.Role == Roles.Admin ? Permissions.DefaultAdmin.ToList() : Permissions.DefaultSales.ToList());

        user.Permissions = string.Join(",", permissionsList);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // Update Role if changed
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role))
        {
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }
            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        // Update Password if provided
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var removePass = await _userManager.RemovePasswordAsync(user);
            if (removePass.Succeeded)
            {
                var addPass = await _userManager.AddPasswordAsync(user, model.NewPassword);
                if (!addPass.Succeeded)
                {
                    foreach (var err in addPass.Errors)
                    {
                        ModelState.AddModelError("NewPassword", err.Description);
                    }
                    return View(model);
                }
            }
        }

        // Refresh sign-in cookie if current user
        if (isCurrent)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        TempData["Success"] = $"تم تحديث بيانات وصلاحيات المستخدم {user.FullName} بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/ToggleStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Json(new { success = false, message = "المستخدم غير موجود." });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentUserId)
        {
            return Json(new { success = false, message = "لا يمكنك تغيير حالة الحساب الشخصي النشط حالياً." });
        }

        if (string.Equals(user.Email, "admin@roven.local", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, "admin@stockify.local", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = false, message = "لا يمكن تعطيل الحساب الرئيسي للنظام (Super Admin)." });
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        var statusText = user.IsActive ? "تفعيل" : "تعطيل";
        return Json(new
        {
            success = true,
            isActive = user.IsActive,
            message = $"تم {statusText} حساب المستخدم {user.FullName} بنجاح."
        });
    }

    // POST: /Users/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword([FromBody] UserResetPasswordViewModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.UserId) || string.IsNullOrWhiteSpace(model.NewPassword))
        {
            return Json(new { success = false, message = "بيانات كلمة المرور غير مكتملة." });
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return Json(new { success = false, message = "المستخدم غير موجود." });
        }

        var removeRes = await _userManager.RemovePasswordAsync(user);
        if (!removeRes.Succeeded)
        {
            return Json(new { success = false, message = "تعذر إلغاء كلمة المرور السابقة." });
        }

        var addRes = await _userManager.AddPasswordAsync(user, model.NewPassword);
        if (!addRes.Succeeded)
        {
            var firstErr = addRes.Errors.FirstOrDefault()?.Description ?? "كلمة المرور لا تطابق معايير الأمان (6 أحرف ورقم وحرف كبير).";
            return Json(new { success = false, message = firstErr });
        }

        return Json(new { success = true, message = $"تم تعيين كلمة المرور الجديدة للمستخدم {user.FullName} بنجاح." });
    }

    // POST: /Users/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "المستخدم غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentUserId)
        {
            TempData["Error"] = "لا يمكنك حذف الحساب الخاص بك أثناء تسجيل الدخول.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(user.Email, "admin@roven.local", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, "admin@stockify.local", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "لا يمكن حذف الحساب الجذري للنظام (Super Admin).";
            return RedirectToAction(nameof(Index));
        }

        var res = await _userManager.DeleteAsync(user);
        if (res.Succeeded)
        {
            TempData["Success"] = $"تم حذف حساب المستخدم {user.FullName} نهائياً من قاعدة البيانات.";
        }
        else
        {
            TempData["Error"] = "حدث خطأ أثناء محاولة حذف المستخدم.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "US";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
    }
}
