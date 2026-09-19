using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using ScalaVerse.ViewModel.Consultant_VM;

namespace ScalaVerse.Areas.Consultant.Controllers
{
	[Area("Consultant")]
	public class ConsultantController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly IWebHostEnvironment _webHostEnvironment;

		public ConsultantController(
			ApplicationDbContext context,
			UserManager<ApplicationUser> userManager,
			IWebHostEnvironment webHostEnvironment)
		{
			_context = context;
			_userManager = userManager;
			_webHostEnvironment = webHostEnvironment;
		}

		// ─────────────────────────────────────────
		// GET: /Consultant/Consultant/Index
		// ─────────────────────────────────────────
		// ─────────────────────────────────────────
// GET: /Consultant/Consultant/Index
// ─────────────────────────────────────────
public async Task<IActionResult> Index(string search, string industry)
{
    var user = await _userManager.GetUserAsync(User);
    
    // ✅ If this is a logged-in consultant
    if (user != null)
    {
        var myConsultant = await _context.Consultants
            .FirstOrDefaultAsync(c => c.UserId == user.Id);
        
        if (myConsultant != null)
        {
            // Redirect to their own profile
            return RedirectToAction("Details", new { id = myConsultant.ConsultantId });
        }
        else
        {
            // If no profile exists, redirect to setup
            return RedirectToAction("SetupProfile");
        }
    }

    // ─────────────────────────────────────────
    // Rest of the code (for public viewing)
    // ─────────────────────────────────────────
    var query = _context.Consultants
        .Include(c => c.User)
        .AsQueryable();

    if (!string.IsNullOrEmpty(search))
        query = query.Where(c =>
            c.Specialty.Contains(search) ||
            c.Bio.Contains(search) ||
            c.ExpertiseAreas.Contains(search));

    if (!string.IsNullOrEmpty(industry))
        query = query.Where(c => c.Industry == industry);

    var consultants = await query.ToListAsync();

    var cards = consultants.Select(c => new ConsultantCardViewModel
    {
        ConsultantId = c.ConsultantId,
        FullName = c.User.FullName,
        ProfileImageUrl = c.User.ProfileImageUrl,
        Specialty = c.Specialty,
        Industry = c.Industry,
        ShortBio = c.Bio?.Length > 120 ? c.Bio[..120] + "…" : c.Bio,
        YearsOfExperience = c.YearsOfExperience,
        AverageRating = c.AverageRating,
        TotalReviews = c.TotalReviews,
        IsVerified = c.IsVerified,
        IsAvailable = c.IsAvailable,
        HourlyRate = c.HourlyRate,
        TopSkills = c.ExpertiseAreas?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Take(3)
            .Select(s => s.Trim())
            .ToList() ?? new()
    }).ToList();

    var industries = await _context.Consultants
        .Select(c => c.Industry)
        .Distinct()
        .Where(i => i != null)
        .ToListAsync();

    ViewBag.Industries = industries;
    ViewBag.Search = search;
    ViewBag.SelectedIndustry = industry;

    return View(cards);
}

		// ─────────────────────────────────────────
		// GET: /Consultant/Consultant/Details/5
		// ─────────────────────────────────────────
		public async Task<IActionResult> Details(int id)
		{
			var consultant = await _context.Consultants
				.Include(c => c.User)
				.Include(c => c.Reviews)
				.FirstOrDefaultAsync(c => c.ConsultantId == id);

			if (consultant == null) return NotFound();

			var vm = new ConsultantProfileViewModel
			{
				ConsultantId = consultant.ConsultantId,
				FullName = consultant.User.FullName,
				Email = consultant.User.Email,
				ProfileImageUrl = consultant.User.ProfileImageUrl ,
				Specialty = consultant.Specialty,
				Industry = consultant.Industry,
				Bio = consultant.Bio,
				YearsOfExperience = consultant.YearsOfExperience,
				ExpertiseAreas = consultant.ExpertiseAreas?
					.Split(',', StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim())
					.ToList() ?? new(),
				HourlyRate = consultant.HourlyRate,
				IsAvailable = consultant.IsAvailable,
				IsVerified = consultant.IsVerified,
				AverageRating = consultant.AverageRating,
				TotalSessions = consultant.TotalSessions,
				TotalReviews = consultant.TotalReviews,
				MemberSince = consultant.CreatedAt,
				LinkedInUrl = consultant.LinkedInUrl,
				GitHubUrl = consultant.GitHubUrl,
				PortfolioUrl = consultant.PortfolioUrl,
				CertificationsUrl = consultant.CertificationsUrl
			};

			return View(vm);
		}

		// ─────────────────────────────────────────
		// GET: /Consultant/Consultant/SetupProfile
		// ─────────────────────────────────────────
		[Authorize(Roles = "Consultant")]
		public async Task<IActionResult> SetupProfile()
		{
			var user = await _userManager.GetUserAsync(User);

			// لو عنده بروفايل بالفعل، روح للـ Details مباشرةً
			var existing = await _context.Consultants
				.FirstOrDefaultAsync(c => c.UserId == user.Id);

			if (existing != null)
				return RedirectToAction("Details", new { id = existing.ConsultantId });

			return View(new ConsultantSetupViewModel());
		}

		// ─────────────────────────────────────────
		// POST: /Consultant/Consultant/SetupProfile
		// ─────────────────────────────────────────
		[HttpPost]
		[Authorize(Roles = "Consultant")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetupProfile(ConsultantSetupViewModel vm)
		{
			if (!ModelState.IsValid) return View(vm);

			var user = await _userManager.GetUserAsync(User);

			// Guard: prevent duplicates
			var alreadyExists = await _context.Consultants.AnyAsync(c => c.UserId == user.Id);
			if (alreadyExists) return RedirectToAction("Index");

			// ── Handle profile image upload ──────────────────
			if (vm.ProfileImage != null && vm.ProfileImage.Length > 0)
			{
				var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
				var ext = Path.GetExtension(vm.ProfileImage.FileName).ToLowerInvariant();

				if (!allowedExtensions.Contains(ext))
				{
					ModelState.AddModelError("ProfileImage", "Only JPG, PNG, or WEBP images are allowed.");
					return View(vm);
				}

				if (vm.ProfileImage.Length > 5 * 1024 * 1024) // 5 MB max
				{
					ModelState.AddModelError("ProfileImage", "Image must be smaller than 5 MB.");
					return View(vm);
				}

				// Save to wwwroot/images/consultants/{userId}_{guid}.ext
				var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "consultants");
				Directory.CreateDirectory(uploadsFolder); // create if not exists

				var fileName = $"{user.Id}_{Guid.NewGuid()}{ext}";
				var filePath = Path.Combine(uploadsFolder, fileName);

				using (var stream = new FileStream(filePath, FileMode.Create))
					await vm.ProfileImage.CopyToAsync(stream);

				// Save relative URL to ApplicationUser
				user.ProfileImageUrl = $"/images/consultants/{fileName}";
				await _userManager.UpdateAsync(user);
			}

			// ── Create consultant record ─────────────────────
			var consultant = new Scalaverse.Entitys.Models.Consultant
			{
				UserId = user.Id,
				Specialty = vm.Specialty,
				Industry = vm.Industry,
				Bio = vm.Bio,
				YearsOfExperience = vm.YearsOfExperience,
				ExpertiseAreas = vm.ExpertiseAreas,
				LinkedInUrl = vm.LinkedInUrl,
				GitHubUrl = vm.GitHubUrl,
				PortfolioUrl = vm.PortfolioUrl,
				CertificationsUrl = vm.CertificationsUrl,
				HourlyRate = vm.HourlyRate,
				IsAvailable = true,
				IsVerified = false,
				CreatedAt = DateTime.UtcNow
			};

			_context.Consultants.Add(consultant);
			await _context.SaveChangesAsync();

			return RedirectToAction("Details", new { id = consultant.ConsultantId });
		}
	}
}
