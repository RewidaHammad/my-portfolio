using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using ScalaVerse.ViewModel.Company_Dashboard_VM;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScalaVerse.Areas.Company.Controllers
{
    [Area("Company")]
    [Authorize(Roles = "CompanyManager")]
    public class CompanyDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CompanyDashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==================== DASHBOARD INDEX ====================
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var manager = await _context.CompanyManagers
                .Include(m => m.Company)
                .FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

            if (manager == null)
            {
                TempData["InfoMessage"] = "Register your company to get started!";
                return RedirectToAction("Create", "Companies");
            }

            if (manager.Company.VerificationStatus == "Rejected")
            {
                return View("Rejected", manager.Company);
            }

            var companyId = manager.CompanyId;

            // Get all projects for statistics
            var allProjects = await _context.Projects
                .Where(p => p.CompanyId == companyId)
                .ToListAsync();

            // Get incoming requests (Pending status)
            var incomingRequests = await _context.ProjectRequests
                .Include(pr => pr.Client)
                    .ThenInclude(c => c.User)
                .Include(pr => pr.SelectedService)
                .Where(pr => pr.CompanyId == companyId && pr.Status == "Pending")
                .OrderByDescending(pr => pr.CreatedAt)
                .Take(5)
                .Select(pr => new CompanyRequestSummary
                {
                    RequestId = pr.RequestId,
                    ProjectName = pr.ProjectName,
                    ClientName = pr.Client.User.FullName,
                    Description = pr.Description,
                    ClientBudget = pr.ClientBudget,
                    Status = pr.Status,
                    CreatedAt = pr.CreatedAt,
                    SelectedServiceName = pr.SelectedService != null ? pr.SelectedService.ServiceName : null,
                    SelectedServiceTier = pr.SelectedServiceTier
                })
                .ToListAsync();

            // Get recent projects
            var recentProjects = await _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Where(p => p.CompanyId == companyId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new CompanyProjectSummary
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    ClientName = p.Client.User.FullName,
                    Status = p.Status,
                    ProgressPercentage = p.ProgressPercentage,
                    Budget = p.Budget
                })
                .ToListAsync();

            // Calculate statistics
            var completedProjects = allProjects.Where(p => p.Status == "Completed").ToList();

            var viewModel = new CompanyDashboardViewModel
            {
                ManagerName = currentUser.FullName,
                CompanyName = manager.Company.CompanyName,
                Company = manager.Company,
                TotalProjects = allProjects.Count,
                ActiveProjects = allProjects.Count(p => p.Status == "Active" || p.Status == "InProgress"),
                CompletedProjects = completedProjects.Count,
                TotalRevenue = completedProjects.Sum(p => p.Budget),
                AverageRating = manager.Company.AverageRating ?? 0,
                PendingRequestsCount = await _context.ProjectRequests
                    .CountAsync(pr => pr.CompanyId == companyId && pr.Status == "Pending"),
                IncomingRequests = incomingRequests,
                RecentProjects = recentProjects
            };

            return View(viewModel);
        }

        // ==================== VIEW PROFILE ====================
        public async Task<IActionResult> ViewProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var manager = await _context.CompanyManagers
                .Include(m => m.Company)
                .FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

            if (manager == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction("Index");
            }

            var model = new CompanyProfileViewModel
            {
                ManagerName = currentUser.FullName,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CompanyName = manager.Company.CompanyName,
                RegistrationNumber = manager.Company.RegistrationNumber,
                TaxId = manager.Company.TaxId,
                Industry = manager.Company.Industry,
                CompanySize = manager.Company.CompanySize,
                Description = manager.Company.Description,
                Address = manager.Company.Address,
                City = manager.Company.City,
                Country = manager.Company.Country,
                Website = manager.Company.Website,
                LogoUrl = manager.Company.LogoUrl,
                IsVerified = manager.Company.IsVerified,
                AverageRating = manager.Company.AverageRating ?? 0,
                TotalReviews = manager.Company.TotalReviews,
                CompletedProjects = manager.Company.CompletedProjects
            };

            return View(model);
        }

        // ==================== EDIT PROFILE (GET) ====================
        public async Task<IActionResult> EditProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var manager = await _context.CompanyManagers
                .Include(m => m.Company)
                .FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

            if (manager == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction("Index");
            }

            var model = new EditCompanyProfileViewModel
            {
                ManagerName = currentUser.FullName,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CompanyName = manager.Company.CompanyName,
                Industry = manager.Company.Industry,
                CompanySize = manager.Company.CompanySize,
                Description = manager.Company.Description,
                Address = manager.Company.Address,
                City = manager.Company.City,
                Country = manager.Company.Country,
                Website = manager.Company.Website
            };

            return View(model);
        }

        // ==================== EDIT PROFILE (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditCompanyProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            // Update ApplicationUser
            currentUser.FullName = model.ManagerName;
            currentUser.Email = model.Email;
            currentUser.PhoneNumber = model.PhoneNumber;
            currentUser.UserName = model.Email;
            await _userManager.UpdateAsync(currentUser);

            // Update Company
            var manager = await _context.CompanyManagers
                .Include(m => m.Company)
                .FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

            if (manager != null && manager.Company != null)
            {
                manager.Company.CompanyName = model.CompanyName;
                manager.Company.Industry = model.Industry;
                manager.Company.CompanySize = model.CompanySize;
                manager.Company.Description = model.Description;
                manager.Company.Address = model.Address;
                manager.Company.City = model.City;
                manager.Company.Country = model.Country;
                manager.Company.Website = model.Website;

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("ViewProfile");
        }
    }
}