using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using ScalaVerse.ViewModel.Identity_VM;

namespace ScalaVerse.Areas.Identity.Controllers
{
	[Area("Identity")]
	public class AccountController : Controller
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly SignInManager<ApplicationUser> _signInManager;
		private readonly ApplicationDbContext _context; // DbContext للتأكد من وجود TeamLeader profile

		public AccountController(
			UserManager<ApplicationUser> userManager,
			SignInManager<ApplicationUser> signInManager,
			ApplicationDbContext context)
		{
			_userManager = userManager;
			_signInManager = signInManager;
			_context = context;
		}

		// ==================== LOGIN ====================
		[HttpGet]
		[AllowAnonymous]
		public IActionResult Login(string returnUrl = null)
		{
			ViewData["ReturnUrl"] = returnUrl;
			return View();
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
		{
			ViewData["ReturnUrl"] = returnUrl;
			if (!ModelState.IsValid)
				return View(model);

			var result = await _signInManager.PasswordSignInAsync(
				model.Email,
				model.Password,
				model.RememberMe,
				lockoutOnFailure: true);

			if (result.Succeeded)
			{
				var user = await _userManager.FindByEmailAsync(model.Email);
				if (!user.IsActive)
				{
					await _signInManager.SignOutAsync();
					ModelState.AddModelError("", "Your account has been deactivated.");
					return View(model);
				}

				var roles = await _userManager.GetRolesAsync(user);

				if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
					return Redirect(returnUrl);

				if (roles.Contains("Admin"))
					return RedirectToAction("Index", "Admin", new { area = "Admin" });

				else if (roles.Contains("Client"))
					return RedirectToAction("Index", "ClientDashboard", new { area = "Identity" });

				else if (roles.Contains("TeamLeader"))
				{
					var profileExists = await _context.TeamLeaders
						.AnyAsync(tl => tl.UserId == user.Id);

					if (!profileExists)
						return RedirectToAction("TeamLeaderProfile", "TeamRooms", new { area = "TeamRooms" });
					else
						return RedirectToAction("Index", "TeamDashboard", new { area = "TeamRooms" });
				}

                else if (roles.Contains("CompanyManager"))
                {
                    // Check if they have a company registered
                    var hasCompany = await _context.CompanyManagers
                        .AnyAsync(cm => cm.UserId == user.Id && cm.IsActive);

                    if (!hasCompany)
                    {
                        // No company yet → send to registration
                        return RedirectToAction("Create", "Companies", new { area = "Company" });
                    }
                    else
                    {
                        // Has company → send to dashboard
                        return RedirectToAction("Index", "CompanyDashboard", new { area = "Company" });
                    }
                }

                else if (roles.Contains("Consultant"))
					return RedirectToAction("Index", "Consultant", new { area = "Consultant" });

				return RedirectToAction("Index", "Home", new { area = "" });
			}

			if (result.IsLockedOut)
			{
				ModelState.AddModelError("", "Account locked due to multiple failed login attempts.");
				return View(model);
			}

			ModelState.AddModelError("", "Invalid login attempt.");
			return View(model);
		}

		// ==================== REGISTER ====================
		[HttpGet]
		[AllowAnonymous]
		public IActionResult Register()
		{
			return View();
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Register(RegisterViewModel model)
		{
			if (!ModelState.IsValid)
				return View(model);

			var existingUser = await _userManager.FindByEmailAsync(model.Email);
			if (existingUser != null)
			{
				ModelState.AddModelError("Email", "This email is already registered.");
				return View(model);
			}

			var user = new ApplicationUser
			{
				UserName = model.Email,
				Email = model.Email,
				FullName = model.FullName,
				PhoneNumber = model.PhoneNumber,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};

			var result = await _userManager.CreateAsync(user, model.Password);
			if (!result.Succeeded)
			{
				foreach (var error in result.Errors)
					ModelState.AddModelError("", error.Description);
				return View(model);
			}

			await _userManager.AddToRoleAsync(user, model.UserType);
			await _signInManager.SignInAsync(user, isPersistent: false);

			if (model.UserType == "Admin")
				return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });

			else if (model.UserType == "Client")
				return RedirectToAction("Index", "ClientDashboard", new { area = "Identity" });

			else if (model.UserType == "TeamLeader")
				return RedirectToAction("TeamLeaderProfile", "TeamRooms", new { area = "TeamRooms" });

			else if (model.UserType == "CompanyManager")
				return RedirectToAction("Create", "Companies", new { area = "Company" });

			else if (model.UserType == "Consultant")
				return RedirectToAction("Dashboard", "Consultant", new { area = "Consultant" });

			return RedirectToAction("Index", "Home", new { area = "" });
		}

		// ==================== LOGOUT ====================
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Logout()
		{
			await _signInManager.SignOutAsync();
			return RedirectToAction("Index", "Home", new { area = "" });
		}

		// ==================== ACCESS DENIED ====================
		[HttpGet]
		public IActionResult AccessDenied()
		{
			return View();
		}

        // ==================== CHANGE PASSWORD ====================
        [HttpGet]
        [Authorize] // Must be logged in
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            // 1. Check if form is valid
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 2. Get current logged-in user
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            // 3. Try to change password
            var result = await _userManager.ChangePasswordAsync(
                user,
                model.OldPassword,
                model.NewPassword
            );

            // 4. Check if password change succeeded
            if (result.Succeeded)
            {
                // Success! Re-sign in the user
                await _signInManager.RefreshSignInAsync(user);

                TempData["SuccessMessage"] = "Password changed successfully!";
                return RedirectToAction("Index", "ClientDashboard");
            }

            // 5. If failed, show errors
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
