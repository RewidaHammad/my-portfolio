using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using Scalaverse.Entitys.Models;
using ScalaVerse.ViewModel.Company_VM;

namespace ScalaVerse.Areas.Company.Controllers
{
	[Area("Company")]
	public class CompaniesController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly ILogger<CompaniesController> _logger;
		private readonly IWebHostEnvironment _environment;

		public CompaniesController(
			ApplicationDbContext context,
			UserManager<ApplicationUser> userManager,
			ILogger<CompaniesController> logger,
			IWebHostEnvironment environment)
		{
			_context = context;
			_userManager = userManager;
			_logger = logger;
			_environment = environment;
		}

		// ==================== INDEX - Browse Companies ====================

		[AllowAnonymous]
		public async Task<IActionResult> Index(string industry, int? minRating, string sortBy = "rating")
		{
			var query = _context.RegisteredCompanies
				.Include(c => c.Services.Where(s => s.IsActive))
				.Include(c => c.Managers)
				.Where(c => c.IsVerified)
				.AsQueryable();

			if (!string.IsNullOrEmpty(industry))
				query = query.Where(c => c.Industry.Contains(industry));

			if (minRating.HasValue)
				query = query.Where(c => c.AverageRating >= minRating.Value);

			query = sortBy switch
			{
				"rating" => query.OrderByDescending(c => c.AverageRating),
				"popular" => query.OrderByDescending(c => c.CompletedProjects),
				"newest" => query.OrderByDescending(c => c.CreatedAt),
				_ => query.OrderByDescending(c => c.AverageRating)
			};

			var companies = await query.ToListAsync();

			ViewBag.Industries = await _context.RegisteredCompanies
				.Where(c => c.IsVerified && !string.IsNullOrEmpty(c.Industry))
				.Select(c => c.Industry)
				.Distinct()
				.OrderBy(i => i)
				.ToListAsync();

			ViewBag.CurrentIndustry = industry;
			ViewBag.CurrentMinRating = minRating;
			ViewBag.CurrentSortBy = sortBy;

			return View(companies);
		}

		// ==================== DETAILS ====================

		[AllowAnonymous]
		public async Task<IActionResult> Details(int id)
		{
			var company = await _context.RegisteredCompanies
				.Include(c => c.Services.Where(s => s.IsActive))
				.Include(c => c.Reviews)
				.Include(c => c.Managers).ThenInclude(m => m.User)
				.FirstOrDefaultAsync(c => c.CompanyId == id && c.IsVerified);

			if (company == null) return NotFound();

			return View(company);
		}
		// 
		// ================================================================

		// ── GET: Create ──
		[Authorize(Roles = "CompanyManager")]
		[HttpGet]
		public async Task<IActionResult> Create()
		{

			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null) return Unauthorized();

			// تحقق لو عنده شركة بالفعل
			var existingManager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			if (existingManager != null)
			{
				TempData["ErrorMessage"] = "You already have a registered company.";
				return RedirectToAction("Dashboard");
			}

			return View(new CreateCompanyViewModel());
		}

		// ── POST: Create ──
		[Authorize(Roles = "CompanyManager")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreateCompanyViewModel model)
		{
			// Remove file validation from ModelState
			ModelState.Remove(nameof(CreateCompanyViewModel.LogoFile));
			ModelState.Remove(nameof(CreateCompanyViewModel.RegistrationDocumentFile));

			if (!ModelState.IsValid)
				return View(model);

			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null) return Unauthorized();

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				// ── Upload Logo ──
				string logoUrl = null;
				if (model.LogoFile != null && model.LogoFile.Length > 0)
				{
					if (model.LogoFile.Length > 5 * 1024 * 1024)
					{
						ModelState.AddModelError("LogoFile", "Logo must be under 5MB.");
						return View(model);
					}

					var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/svg+xml" };
					if (!allowed.Contains(model.LogoFile.ContentType))
					{
						ModelState.AddModelError("LogoFile", "Only JPG, PNG, WEBP, SVG allowed.");
						return View(model);
					}

					var logoPath = Path.Combine(_environment.WebRootPath, "uploads", "companies", "logos");
					Directory.CreateDirectory(logoPath);
					var logoFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.LogoFile.FileName)}";
					using var stream = new FileStream(Path.Combine(logoPath, logoFileName), FileMode.Create);
					await model.LogoFile.CopyToAsync(stream);
					logoUrl = $"/uploads/companies/logos/{logoFileName}";
				}

				// ── Upload Registration Document ──
				string regDocUrl = null;
				if (model.RegistrationDocumentFile != null && model.RegistrationDocumentFile.Length > 0)
				{
					if (model.RegistrationDocumentFile.Length > 10 * 1024 * 1024)
					{
						ModelState.AddModelError("RegistrationDocumentFile", "Document must be under 10MB.");
						return View(model);
					}

					var docPath = Path.Combine(_environment.WebRootPath, "uploads", "companies", "documents");
					Directory.CreateDirectory(docPath);
					var docFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.RegistrationDocumentFile.FileName)}";
					using var stream = new FileStream(Path.Combine(docPath, docFileName), FileMode.Create);
					await model.RegistrationDocumentFile.CopyToAsync(stream);
					regDocUrl = $"/uploads/companies/documents/{docFileName}";
				}

				// ── Create Company ──
				var company = new RegisteredCompany
				{
					CompanyName = model.CompanyName.Trim(),
					RegistrationNumber = model.RegistrationNumber.Trim(),
					TaxId = model.TaxId?.Trim(),
					Description = model.Description?.Trim(),
					Industry = model.Industry.Trim(),
					CompanySize = model.CompanySize,
					Address = model.Address?.Trim(),
					City = model.City?.Trim(),
					Country = model.Country?.Trim(),
					Website = model.Website?.Trim(),
					LogoUrl = logoUrl,
					RegistrationDocumentUrl = regDocUrl,
					IsVerified = false, // Needs admin approval
					AverageRating = 0,
					TotalReviews = 0,
					CompletedProjects = 0,
					CreatedAt = DateTime.UtcNow
				};

				_context.RegisteredCompanies.Add(company);
				await _context.SaveChangesAsync();

				// ── Link Manager to Company ──
				var manager = new CompanyManager
				{
					UserId = currentUser.Id,
					CompanyId = company.CompanyId,
					Position = "Primary Manager",
					IsPrimaryContact = true,
					IsActive = true,
					JoinedAt = DateTime.UtcNow
				};

				_context.CompanyManagers.Add(manager);
				await _context.SaveChangesAsync();

				// ── Add Services ──
				if (model.Services != null && model.Services.Any())
				{
					foreach (var svc in model.Services.Where(s => !string.IsNullOrWhiteSpace(s.ServiceName)))
					{
						_context.CompanyServices.Add(new CompanyService
						{
							CompanyId = company.CompanyId,
							ServiceName = svc.ServiceName.Trim(),
							Description = svc.Description?.Trim(),
							ServiceTier = svc.ServiceTier,
							StartingPrice = svc.StartingPrice,
							PricingModel = svc.PricingModel,
							DeliveryTime = svc.DeliveryTime?.Trim(),
							IsActive = true
						});
					}
					await _context.SaveChangesAsync();
				}

				CreateAuditLog(
					currentUser.Id,
					"CompanyCreated",
					$"Company '{company.CompanyName}' registered. Awaiting admin verification.",
					"RegisteredCompany",
					company.CompanyId
				);

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				TempData["SuccessMessage"] = "Company registered successfully! It will be visible after admin verification.";
				return RedirectToAction("Index", "CompanyDashboard", new { area = "Company" });
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error creating company");
				ModelState.AddModelError("", "An error occurred. Please try again.");
				return View(model);
			}
		}



		// ==================== BOOK - Client يبعت Request ====================

		[Authorize(Roles = "Client")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Book(int companyId, string projectName,
			string description, decimal? clientBudget,
			int? selectedServiceId, string selectedServiceTier)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null)
				return Json(new { success = false, message = "Unauthorized." });

			var client = await _context.Clients
				.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			if (client == null)
				return Json(new { success = false, message = "Client profile not found." });

			if (string.IsNullOrWhiteSpace(projectName))
				return Json(new { success = false, message = "Project name is required." });

			var company = await _context.RegisteredCompanies
				.Include(c => c.Managers).ThenInclude(m => m.User)
				.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.IsVerified);

			if (company == null)
				return Json(new { success = false, message = "Company not found." });

			// الـ Service اللي اختارها (لو موجودة)
			CompanyService service = null;
			if (selectedServiceId.HasValue)
			{
				service = await _context.CompanyServices
					.FirstOrDefaultAsync(s => s.ServiceId == selectedServiceId && s.CompanyId == companyId);
			}

			try
			{
				var request = new ProjectRequest
				{
					ClientId = client.ClientId,
					CompanyId = companyId,
					TeamRoomId = null,
					ProjectName = projectName.Trim(),
					Description = description?.Trim(),
					Category = company.Industry,
					ClientBudget = clientBudget > 0 ? clientBudget : null,
					SelectedServiceId = selectedServiceId,
					SelectedServiceTier = selectedServiceTier,
					Status = "Pending",
					CreatedAt = DateTime.UtcNow
				};

				_context.ProjectRequests.Add(request);
				await _context.SaveChangesAsync();

				// إشعار للـ Primary Contact Manager
				var primaryManager = company.Managers
					.FirstOrDefault(m => m.IsPrimaryContact && m.IsActive)
					?? company.Managers.FirstOrDefault(m => m.IsActive);

				if (primaryManager != null)
				{
					CreateNotification(
						primaryManager.UserId,
						"NewCompanyRequest",
						"New Project Request 📋",
						$"{currentUser.FullName} sent a request to {company.CompanyName}: {projectName}",
						request.RequestId,
						"ProjectRequest",
						$"/TeamRooms/Projects/CompanyIncomingRequests"
					);
				}

				CreateAuditLog(
					currentUser.Id,
					"CompanyRequested",
					$"Client sent request to company '{company.CompanyName}' for '{projectName}'",
					"ProjectRequest",
					request.RequestId
				);

				await _context.SaveChangesAsync();

				return Json(new
				{
					success = true,
					message = "Request sent successfully!",
					requestId = request.RequestId,
					redirectUrl = Url.Action("MyRequests", "Projects", new { area = "TeamRooms" })
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating company request {CompanyId}", companyId);
				return Json(new { success = false, message = ex.Message + " | " + ex.InnerException?.Message });
			}
		}

		// ==================== HELPERS ====================

		private void CreateNotification(int userId, string type, string title,
			string message, int relatedEntityId, string relatedEntityType, string actionUrl)
		{
			_context.Notifications.Add(new Notification
			{
				UserId = userId,
				Type = type,
				Title = title,
				Message = message,
				RelatedEntityId = relatedEntityId,
				RelatedEntityType = relatedEntityType,
				ActionUrl = actionUrl,
				IsRead = false,
				CreatedAt = DateTime.UtcNow
			});
		}

		private void CreateAuditLog(int userId, string action, string details,
			string entityType = null, int? entityId = null)
		{
			_context.AuditLogs.Add(new AuditLog
			{
				UserId = userId,
				Action = action,
				Details = details,
				EntityType = entityType,
				EntityId = entityId,
				IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
				CreatedAt = DateTime.UtcNow
			});
		}
	}
}