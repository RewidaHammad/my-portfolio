using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using ScalaVerse.ViewModel.Client_Dashboard_VM;
using ScalaVerse.ViewModel.Profile_VM;


namespace ScalaVerse.Areas.Identity.Controllers
{
    [Area("Identity")]
    [Authorize(Roles = "Client")]
    public class ClientDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClientDashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (client == null)
            {
                TempData["ErrorMessage"] = "Client profile not found.";
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            var allProjects = await _context.Projects
                .Where(p => p.ClientId == client.ClientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var statistics = new StatisticsViewModel
            {
                TotalProjects = allProjects.Count,
                ActiveProjects = allProjects.Count(p => p.Status == "Active" || p.Status == "InProgress"),
                CompletedProjects = allProjects.Count(p => p.Status == "Completed"),
                TotalSpent = allProjects.Sum(p => p.ActualCost ?? 0)
            };

            var recentProjects = allProjects
                .Take(5)
                .Select(p => new ProjectCardViewModel
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    Status = p.Status,
                    ProgressPercentage = p.ProgressPercentage
                })
                .ToList();

            var model = new DashboardViewModel
            {
                ClientName = currentUser.FullName,
                Statistics = statistics,
                RecentProjects = recentProjects
            };

            return View(model);
        }
        // ==================== VIEW PROFILE ====================
        public async Task<IActionResult> ViewProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            var model = new ProfileViewModel
            {
                FullName = currentUser.FullName,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CompanyName = client?.CompanyName,
                Industry = client?.Industry,
                CompanySize = client?.CompanySize,
                Bio = client?.Bio,
                Address = client?.Address,
                City = client?.City,
                Country = client?.Country,
                IsVerified = client?.IsVerified ?? false
            };

            return View(model);
        }

        // ==================== EDIT PROFILE (GET) ====================
        public async Task<IActionResult> EditProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            var model = new EditProfileViewModel
            {
                FullName = currentUser.FullName,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CompanyName = client?.CompanyName,
                Industry = client?.Industry,
                CompanySize = client?.CompanySize,
                Bio = client?.Bio,
                Address = client?.Address,
                City = client?.City,
                Country = client?.Country
            };

            return View(model);
        }

        // ==================== EDIT PROFILE (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Update ApplicationUser
            currentUser.FullName = model.FullName;
            currentUser.Email = model.Email;
            currentUser.PhoneNumber = model.PhoneNumber;
            currentUser.UserName = model.Email;
            await _userManager.UpdateAsync(currentUser);

            // Update Client
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (client != null)
            {
                client.CompanyName = model.CompanyName;
                client.Industry = model.Industry;
                client.CompanySize = model.CompanySize;
                client.Bio = model.Bio;
                client.Address = model.Address;
                client.City = model.City;
                client.Country = model.Country;

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("ViewProfile");
        }
    }
}