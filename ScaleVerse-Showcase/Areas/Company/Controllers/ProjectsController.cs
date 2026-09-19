using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ScalaVerse.Areas.Company.Controllers
{
    [Area("Company")]
    [Authorize(Roles = "CompanyManager")]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==================== COMPANY PROJECTS ====================
        public async Task<IActionResult> CompanyProjects(string status = "all")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Get company manager
            var manager = await _context.CompanyManagers
                .FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

            if (manager == null)
            {
                TempData["ErrorMessage"] = "Company Manager profile not found.";
                return RedirectToAction("Index", "Home");
            }

            // Query projects for this company
            var query = _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.Milestones)
                .Where(p => p.CompanyId == manager.CompanyId)
                .AsQueryable();

            // Filter by status
            if (status != "all")
            {
                query = query.Where(p => p.Status.ToLower() == status.ToLower());
            }

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.StatusCounts = new
            {
                All = await _context.Projects.Where(p => p.CompanyId == manager.CompanyId).CountAsync(),
                Active = await _context.Projects.Where(p => p.CompanyId == manager.CompanyId && p.Status == "Active").CountAsync(),
                InProgress = await _context.Projects.Where(p => p.CompanyId == manager.CompanyId && p.Status == "InProgress").CountAsync(),
                Completed = await _context.Projects.Where(p => p.CompanyId == manager.CompanyId && p.Status == "Completed").CountAsync()
            };

            return View(projects);
        }
    }
}