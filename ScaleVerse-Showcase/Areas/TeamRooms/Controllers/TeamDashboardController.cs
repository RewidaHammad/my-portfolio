using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using ScalaVerse.ViewModel.TeamLeader_Dashboard_VM;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScalaVerse.Areas.TeamRooms.Controllers
{
    [Area("TeamRooms")]
    [Authorize(Roles = "TeamLeader")]
    public class TeamDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TeamDashboardController(
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
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var teamLeader = await _context.TeamLeaders
                .FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

            if (teamLeader == null)
            {
                return RedirectToAction("TeamLeaderProfile", "TeamRooms");
            }

            if (teamLeader.VerificationStatus == "Rejected")
            {
                return View("Rejected", teamLeader);
            }


            // Get all team rooms for this leader
            var myTeams = await _context.TeamRooms
                .Where(tr => tr.TeamLeaderId == teamLeader.TeamLeaderId)
                .ToListAsync();

            var teamRoomIds = myTeams.Select(tr => tr.TeamRoomId).ToList();

            // Get all projects for statistics
            var allProjects = await _context.Projects
                .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value))
                .ToListAsync();

            // Get incoming requests (Pending status)
            var incomingRequests = await _context.ProjectRequests
                .Include(pr => pr.Client)
                    .ThenInclude(c => c.User)
                .Where(pr => teamRoomIds.Contains((int)pr.TeamRoomId) && pr.Status == "Pending")
                .OrderByDescending(pr => pr.CreatedAt)
                .Take(5)
                .Select(pr => new ProjectRequestSummary
                {
                    RequestId = pr.RequestId,
                    ProjectName = pr.ProjectName,
                    ClientName = pr.Client.User.FullName,
                    Description = pr.Description,
                    ClientBudget = pr.ClientBudget,
                    Status = pr.Status,
                    CreatedAt = pr.CreatedAt
                })
                .ToListAsync();
            //var incomingRequests = new List<ProjectRequestSummary>();

            // Get recent projects
            var recentProjects = await _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value))
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new RecentProjectSummary
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
            var averageRating = myTeams.Any() ? myTeams.Average(t => t.AverageRating ?? 0) : 0;

            var viewModel = new TeamLeaderDashboardViewModel
            {
                TeamLeaderName = currentUser.FullName,
                MyTeams = myTeams,
                TotalProjects = allProjects.Count,
                ActiveProjects = allProjects.Count(p => p.Status == "Active" || p.Status == "InProgress"),
                CompletedProjects = completedProjects.Count,
                TotalEarnings = completedProjects.Sum(p => p.Budget),
                AverageRating = averageRating,
                //PendingRequestsCount = 0,
                PendingRequestsCount = await _context.ProjectRequests
                    .CountAsync(pr => teamRoomIds.Contains((int)pr.TeamRoomId) && pr.Status == "Pending"),
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

            var teamLeader = await _context.TeamLeaders
                .FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

            var model = new TeamLeaderProfileViewModel
            {
                FullName = currentUser.FullName,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                Bio = teamLeader?.Bio,
                ExpertiseAreas = teamLeader?.ExpertiseAreas,
                YearsOfExperience = teamLeader?.YearsOfExperience ?? 0,
                PortfolioUrl = teamLeader?.PortfolioUrl,
                IsVerified = teamLeader?.IsVerified ?? false
            };

            return View(model);
        }

        // ==================== EDIT PROFILE (GET) ====================
        public async Task<IActionResult> EditProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var teamLeader = await _context.TeamLeaders
                .FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

            var model = new EditTeamLeaderProfileViewModel
            {
                FullName = currentUser.FullName,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                Bio = teamLeader?.Bio,
                ExpertiseAreas = teamLeader?.ExpertiseAreas,
                YearsOfExperience = teamLeader?.YearsOfExperience ?? 0,
                PortfolioUrl = teamLeader?.PortfolioUrl
            };

            return View(model);
        }

        // ==================== EDIT PROFILE (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditTeamLeaderProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            // Update ApplicationUser
            currentUser.FullName = model.FullName;
            currentUser.Email = model.Email;
            currentUser.PhoneNumber = model.PhoneNumber;
            currentUser.UserName = model.Email;
            await _userManager.UpdateAsync(currentUser);

            // Update TeamLeader
            var teamLeader = await _context.TeamLeaders
                .FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

            if (teamLeader != null)
            {
                teamLeader.Bio = model.Bio;
                teamLeader.ExpertiseAreas = model.ExpertiseAreas;
                teamLeader.YearsOfExperience = model.YearsOfExperience;
                teamLeader.PortfolioUrl = model.PortfolioUrl;

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("ViewProfile");
        }
    }
}