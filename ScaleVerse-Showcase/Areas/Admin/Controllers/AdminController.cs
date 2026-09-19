using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using Scalaverse.Entitys.Models;
using ScalaVerse.ViewModel.Admin_VM;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScalaVerse.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ==================== DASHBOARD ====================

        public async Task<IActionResult> Index()
        {
            var viewModel = new AdminDashboardViewModel();

            // User Stats
            viewModel.TotalUsers = await _context.Users.CountAsync();
            viewModel.TotalClients = await _context.Clients.CountAsync();
            viewModel.TotalTeamLeaders = await _context.TeamLeaders.CountAsync();
            viewModel.TotalCompanyManagers = await _context.CompanyManagers.CountAsync();
            viewModel.ActiveUsers = await _context.Users.CountAsync(u => u.IsActive && !u.IsSuspended);
            viewModel.SuspendedUsers = await _context.Users.CountAsync(u => u.IsSuspended);

            // Verification Stats
            viewModel.PendingTeams = await _context.TeamLeaders
                .CountAsync(tl => tl.VerificationStatus == "Pending");
            viewModel.PendingCompanies = await _context.RegisteredCompanies
                .CountAsync(c => c.VerificationStatus == "Pending");
            viewModel.TotalVerifications = viewModel.PendingTeams + viewModel.PendingCompanies;

            // Project Stats
            viewModel.TotalProjects = await _context.Projects.CountAsync();
            viewModel.ActiveProjects = await _context.Projects
                .CountAsync(p => p.Status == "Active" || p.Status == "InProgress");
            viewModel.CompletedProjects = await _context.Projects
                .CountAsync(p => p.Status == "Completed");
            viewModel.StuckProjects = await _context.Projects
                .CountAsync(p => p.IsStuck);

            // Financial Stats
            viewModel.TotalRevenue = await _context.Projects
                .Where(p => p.Status == "Completed")
                .SumAsync(p => p.Budget);
            viewModel.PlatformEarnings = await _context.Projects
                .Where(p => p.Status == "Completed")
                .SumAsync(p => p.PlatformEarnings);

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            viewModel.MonthlyRevenue = await _context.Projects
                .Where(p => p.Status == "Completed" &&
                           p.EndDate.HasValue &&
                           p.EndDate.Value.Month == currentMonth &&
                           p.EndDate.Value.Year == currentYear)
                .SumAsync(p => p.Budget);

            // Recent Pending Teams (top 5)
            viewModel.RecentPendingTeams = await _context.TeamLeaders
                .Include(tl => tl.User)
                .Where(tl => tl.VerificationStatus == "Pending")
                .OrderBy(tl => tl.CreatedAt)
                .Take(5)
                .Select(tl => new PendingVerificationItem
                {
                    Id = tl.TeamLeaderId,
                    Name = tl.User.FullName ?? "Unknown",
                    Email = tl.User.Email ?? "N/A",
                    RequestedAt = tl.CreatedAt,
                    DaysWaiting = (int)(DateTime.UtcNow - tl.CreatedAt).TotalDays
                })
                .ToListAsync();

            // Recent Pending Companies (top 5)
            viewModel.RecentPendingCompanies = await _context.RegisteredCompanies
                .Include(c => c.Managers)
                    .ThenInclude(m => m.User)
                .Where(c => c.VerificationStatus == "Pending")
                .OrderBy(c => c.CreatedAt)
                .Take(5)
                .Select(c => new PendingVerificationItem
                {
                    Id = c.CompanyId,
                    Name = c.CompanyName,
                    Email = c.Managers.FirstOrDefault(m => m.IsPrimaryContact).User.Email ?? "N/A",
                    RequestedAt = c.CreatedAt,
                    DaysWaiting = (int)(DateTime.UtcNow - c.CreatedAt).TotalDays
                })
                .ToListAsync();

            // Recent Stuck Projects
            viewModel.RecentStuckProjects = await _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Where(p => p.IsStuck)
                .OrderByDescending(p => p.LastActivityAt)
                .Take(5)
                .Select(p => new StuckProjectItem
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    ClientName = p.Client.User.FullName ?? "Unknown",
                    DaysStuck = p.LastActivityAt.HasValue
                        ? (int)(DateTime.UtcNow - p.LastActivityAt.Value).TotalDays
                        : 0,
                    Status = p.Status
                })
                .ToListAsync();

            return View(viewModel);
        }

        // ==================== USER MANAGEMENT ====================

        public async Task<IActionResult> Users(string search, string role, string status, int page = 1, int pageSize = 5)
        {
            var query = _context.Users
                .Where(u => u.FullName != null && u.Email != null)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search));
            }

            // Status filter
            if (status == "Active")
                query = query.Where(u => u.IsActive && !u.IsSuspended);
            else if (status == "Suspended")
                query = query.Where(u => u.IsSuspended);

            var totalUsers = await query.CountAsync();
            var hasMore = totalUsers > (page * pageSize);

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    FullName = u.FullName ?? "Unknown",
                    Email = u.Email ?? "N/A",
                    PhoneNumber = u.PhoneNumber ?? "N/A",
                    u.IsActive,
                    u.IsSuspended,
                    SuspensionReason = u.SuspensionReason ?? string.Empty,
                    u.LastLoginAt,
                    u.CreatedAt
                })
                .ToListAsync();

            var userItems = new List<UserListItem>();

            foreach (var user in users)
            {
                var appUser = await _context.Users.FindAsync(user.Id);
                if (appUser == null) continue;

                var roles = await _userManager.GetRolesAsync(appUser);
                var userRole = roles.FirstOrDefault() ?? "Unknown";

                // Apply role filter AFTER getting the role
                if (!string.IsNullOrEmpty(role) && role != "All" && userRole != role)
                    continue;

                userItems.Add(new UserListItem
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = userRole,
                    IsActive = user.IsActive,
                    IsSuspended = user.IsSuspended,
                    SuspensionReason = user.SuspensionReason,
                    LastLoginAt = user.LastLoginAt,
                    CreatedAt = user.CreatedAt,
                    ProjectsCount = 0
                });
            }

            var viewModel = new UserManagementViewModel
            {
                Users = userItems,
                SearchTerm = search,
                RoleFilter = role ?? "All",
                StatusFilter = status ?? "All",
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize),
                TotalUsers = totalUsers
            };

            ViewBag.HasMore = hasMore;
            ViewBag.NextPage = page + 1;

            return View(viewModel);
        }

        // ==================== SUSPEND USER ====================

        [HttpGet]
        public async Task<IActionResult> SuspendUser(int id)
        {
            System.Diagnostics.Debug.WriteLine($"GET SuspendUser called with id: {id}");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                System.Diagnostics.Debug.WriteLine($"User not found with id: {id}");
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Users");
            }

            System.Diagnostics.Debug.WriteLine($"User found: {user.FullName}");

            var viewModel = new SuspendUserViewModel
            {
                UserId = user.Id,
                UserName = user.FullName ?? "Unknown",
                Email = user.Email ?? "N/A"
            };

            System.Diagnostics.Debug.WriteLine($"Returning view with UserId: {viewModel.UserId}");

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendUser(int userId, string suspensionReason)
        {
            System.Diagnostics.Debug.WriteLine($"POST: UserId={userId}, Reason={suspensionReason}");

            if (string.IsNullOrWhiteSpace(suspensionReason))
            {
                TempData["ErrorMessage"] = "Suspension reason is required.";
                return RedirectToAction("SuspendUser", new { id = userId });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Users");
            }

            try
            {
                var currentAdmin = await _userManager.GetUserAsync(User);
                if (currentAdmin == null)
                {
                    TempData["ErrorMessage"] = "Admin not found.";
                    return RedirectToAction("Index");
                }

                user.IsSuspended = true;
                user.SuspensionReason = suspensionReason.Trim();
                user.SuspendedAt = DateTime.UtcNow;
                user.SuspendedByAdminId = currentAdmin.Id;

                _context.Update(user);

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentAdmin.Id,
                    Action = "SuspendedUser",
                    EntityType = "User",
                    EntityId = user.Id,
                    Details = $"Suspended user '{user.FullName}' ({user.Email}). Reason: {suspensionReason}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    CreatedAt = DateTime.UtcNow
                });

                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Type = "AccountSuspended",
                    Title = "Account Suspended",
                    Message = $"Your account has been suspended. Reason: {suspensionReason}",
                    IsRead = false,
                    RelatedEntityType = "User",
                    RelatedEntityId = user.Id,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                _logger.LogWarning("Admin {AdminId} suspended user {UserId}", currentAdmin.Id, userId);

                TempData["SuccessMessage"] = $"User '{user.FullName}' has been suspended successfully.";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suspending user {UserId}", userId);
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("SuspendUser", new { id = userId });
            }
        }
        
        // ==================== UNSUSPEND USER ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnsuspendUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Users");
            }

            if (!user.IsSuspended)
            {
                TempData["ErrorMessage"] = "User is not suspended.";
                return RedirectToAction("Users");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);

            // Unsuspend user
            user.IsSuspended = false;
            user.SuspensionReason = string.Empty;
            user.SuspendedAt = null;
            user.SuspendedByAdminId = null;

            // Create audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentAdmin.Id,
                Action = "UnsuspendedUser",
                EntityType = "User",
                EntityId = user.Id,
                Details = $"Unsuspended user '{user.FullName}' ({user.Email})",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            });

            // Create notification
            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = "AccountUnsuspended",
                Title = "Account Restored",
                Message = "Good news! Your account suspension has been lifted. You can now use all platform features normally.",
                ActionUrl = null,
                IsRead = false,
                RelatedEntityType = "User",
                RelatedEntityId = user.Id,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} unsuspended user {UserId}", currentAdmin.Id, user.Id);

            TempData["SuccessMessage"] = $"User {user.FullName} has been unsuspended.";
            return RedirectToAction("Users");
        }

        
        
        // ==================== TEAM VERIFICATION ====================

        public async Task<IActionResult> PendingTeams()
        {
            var pendingTeams = await _context.TeamLeaders
                .Include(tl => tl.User)
                .Where(tl => tl.VerificationStatus == "Pending")
                .OrderBy(tl => tl.CreatedAt)
                .Select(tl => new PendingTeamItem
                {
                    TeamLeaderId = tl.TeamLeaderId,
                    FullName = tl.User.FullName ?? "Unknown",
                    Email = tl.User.Email ?? "N/A",
                    PhoneNumber = tl.User.PhoneNumber ?? "N/A",
                    Bio = tl.Bio,
                    ExpertiseAreas = tl.ExpertiseAreas,
                    YearsOfExperience = tl.YearsOfExperience,
                    PortfolioUrl = tl.PortfolioUrl,
                    CreatedAt = tl.CreatedAt,
                    DaysWaiting = (int)(DateTime.UtcNow - tl.CreatedAt).TotalDays
                })
                .ToListAsync();

            var viewModel = new PendingTeamsViewModel
            {
                PendingTeams = pendingTeams,
                TotalPending = pendingTeams.Count
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTeam(int id)
        {
            var teamLeader = await _context.TeamLeaders
                .Include(tl => tl.User)
                .FirstOrDefaultAsync(tl => tl.TeamLeaderId == id);

            if (teamLeader == null)
            {
                TempData["ErrorMessage"] = "Team Leader not found.";
                return RedirectToAction("PendingTeams");
            }

            if (teamLeader.VerificationStatus != "Pending")
            {
                TempData["ErrorMessage"] = "This team has already been processed.";
                return RedirectToAction("PendingTeams");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);

            // Approve team
            teamLeader.VerificationStatus = "Approved";
            teamLeader.IsVerified = true;
            teamLeader.ApprovedAt = DateTime.UtcNow;
            teamLeader.ApprovedByAdminId = currentAdmin.Id;
            teamLeader.RejectionReason = string.Empty;
            teamLeader.RejectedAt = null;
            teamLeader.ReapplicationAllowedDate = null;

            // Create audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentAdmin.Id,
                Action = "ApprovedTeam",
                EntityType = "TeamLeader",
                EntityId = teamLeader.TeamLeaderId,
                Details = $"Approved team leader '{teamLeader.User.FullName}' ({teamLeader.User.Email})",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            });

            // Create notification for team leader
            _context.Notifications.Add(new Notification
            {
                UserId = teamLeader.UserId,
                Type = "TeamApproved",
                Title = "Team Registration Approved! 🎉",
                Message = "Congratulations! Your team leader registration has been approved by our admin team. You can now start accepting projects!",
                ActionUrl = "/TeamRooms/TeamDashboard/Index",
                IsRead = false,
                RelatedEntityType = "TeamLeader",
                RelatedEntityId = teamLeader.TeamLeaderId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} approved team {TeamId}", currentAdmin.Id, teamLeader.TeamLeaderId);

            TempData["SuccessMessage"] = $"Team leader {teamLeader.User.FullName} has been approved.";
            return RedirectToAction("PendingTeams");
        }

        [HttpGet]
        public async Task<IActionResult> RejectTeam(int id)
        {
            var teamLeader = await _context.TeamLeaders
                .Include(tl => tl.User)
                .FirstOrDefaultAsync(tl => tl.TeamLeaderId == id);

            if (teamLeader == null)
            {
                TempData["ErrorMessage"] = "Team Leader not found.";
                return RedirectToAction("PendingTeams");
            }

            if (teamLeader.VerificationStatus != "Pending")
            {
                TempData["ErrorMessage"] = "This team has already been processed.";
                return RedirectToAction("PendingTeams");
            }

            var viewModel = new RejectTeamViewModel
            {
                TeamLeaderId = teamLeader.TeamLeaderId,
                TeamLeaderName = teamLeader.User.FullName ?? "Unknown"
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTeam(RejectTeamViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var teamLeader = await _context.TeamLeaders
                .Include(tl => tl.User)
                .FirstOrDefaultAsync(tl => tl.TeamLeaderId == model.TeamLeaderId);

            if (teamLeader == null)
            {
                TempData["ErrorMessage"] = "Team Leader not found.";
                return RedirectToAction("PendingTeams");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            var reapplicationDays = settings?.ReapplicationWaitDays ?? 30;

            // Reject team
            teamLeader.VerificationStatus = "Rejected";
            teamLeader.IsVerified = false;
            teamLeader.RejectionReason = model.RejectionReason ?? "No reason provided";
            teamLeader.RejectedAt = DateTime.UtcNow;
            teamLeader.RejectedByAdminId = currentAdmin.Id;
            teamLeader.ReapplicationAllowedDate = DateTime.UtcNow.AddDays(reapplicationDays);

            // Create audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentAdmin.Id,
                Action = "RejectedTeam",
                EntityType = "TeamLeader",
                EntityId = teamLeader.TeamLeaderId,
                Details = $"Rejected team leader '{teamLeader.User.FullName}'. Reason: {model.RejectionReason}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            });

            // Create notification
            _context.Notifications.Add(new Notification
            {
                UserId = teamLeader.UserId,
                Type = "TeamRejected",
                Title = "Team Registration Reviewed",
                Message = $"Unfortunately, your team leader registration was not approved. Reason: {model.RejectionReason}. You may reapply after {teamLeader.ReapplicationAllowedDate:MMM dd, yyyy}.",
                ActionUrl = "/TeamRooms/TeamDashboard/Index",
                IsRead = false,
                RelatedEntityType = "TeamLeader",
                RelatedEntityId = teamLeader.TeamLeaderId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} rejected team {TeamId}", currentAdmin.Id, teamLeader.TeamLeaderId);

            TempData["SuccessMessage"] = $"Team leader {teamLeader.User.FullName} has been rejected.";
            return RedirectToAction("PendingTeams");
        }

        // ==================== COMPANY VERIFICATION ====================

        public async Task<IActionResult> PendingCompanies()
        {
            var pendingCompanies = await _context.RegisteredCompanies
                .Include(c => c.Managers)
                    .ThenInclude(m => m.User)
                .Include(c => c.Services)
                .Where(c => c.VerificationStatus == "Pending")
                .OrderBy(c => c.CreatedAt)
                .Select(c => new PendingCompanyItem
                {
                    CompanyId = c.CompanyId,
                    CompanyName = c.CompanyName,
                    ManagerName = c.Managers.FirstOrDefault(m => m.IsPrimaryContact).User.FullName ?? "Unknown",
                    Email = c.Managers.FirstOrDefault(m => m.IsPrimaryContact).User.Email ?? "N/A",
                    RegistrationNumber = c.RegistrationNumber,
                    TaxId = c.TaxId,
                    Industry = c.Industry,
                    CompanySize = c.CompanySize,
                    Description = c.Description,
                    Website = c.Website,
                    LogoUrl = c.LogoUrl,
                    RegistrationDocumentUrl = c.RegistrationDocumentUrl,
                    ServicesCount = c.Services.Count,
                    CreatedAt = c.CreatedAt,
                    DaysWaiting = (int)(DateTime.UtcNow - c.CreatedAt).TotalDays
                })
                .ToListAsync();

            var viewModel = new PendingCompaniesViewModel
            {
                PendingCompanies = pendingCompanies,
                TotalPending = pendingCompanies.Count
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCompany(int id)
        {
            var company = await _context.RegisteredCompanies
                .Include(c => c.Managers)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.CompanyId == id);

            if (company == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction("PendingCompanies");
            }

            if (company.VerificationStatus != "Pending")
            {
                TempData["ErrorMessage"] = "This company has already been processed.";
                return RedirectToAction("PendingCompanies");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            var primaryManager = company.Managers.FirstOrDefault(m => m.IsPrimaryContact);

            // Approve company
            company.VerificationStatus = "Approved";
            company.IsVerified = true;
            company.ApprovedAt = DateTime.UtcNow;
            company.ApprovedByAdminId = currentAdmin.Id;
            company.RejectionReason = string.Empty;
            company.RejectedAt = null;
            company.ReapplicationAllowedDate = null;

            // Create audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentAdmin.Id,
                Action = "ApprovedCompany",
                EntityType = "RegisteredCompany",
                EntityId = company.CompanyId,
                Details = $"Approved company '{company.CompanyName}'",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            });

            // Create notification
            if (primaryManager != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = primaryManager.UserId,
                    Type = "CompanyApproved",
                    Title = "Company Registration Approved! 🎉",
                    Message = $"Congratulations! Your company '{company.CompanyName}' has been approved. You can now start accepting project requests!",
                    ActionUrl = "/Company/CompanyDashboard/Index",
                    IsRead = false,
                    RelatedEntityType = "RegisteredCompany",
                    RelatedEntityId = company.CompanyId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} approved company {CompanyId}", currentAdmin.Id, company.CompanyId);

            TempData["SuccessMessage"] = $"Company {company.CompanyName} has been approved.";
            return RedirectToAction("PendingCompanies");
        }

        [HttpGet]
        public async Task<IActionResult> RejectCompany(int id)
        {
            var company = await _context.RegisteredCompanies
                .FirstOrDefaultAsync(c => c.CompanyId == id);

            if (company == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction("PendingCompanies");
            }

            if (company.VerificationStatus != "Pending")
            {
                TempData["ErrorMessage"] = "This company has already been processed.";
                return RedirectToAction("PendingCompanies");
            }

            var viewModel = new RejectCompanyViewModel
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCompany(RejectCompanyViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var company = await _context.RegisteredCompanies
                .Include(c => c.Managers)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.CompanyId == model.CompanyId);

            if (company == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction("PendingCompanies");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            var reapplicationDays = settings?.ReapplicationWaitDays ?? 30;
            var primaryManager = company.Managers.FirstOrDefault(m => m.IsPrimaryContact);

            // Reject company
            company.VerificationStatus = "Rejected";
            company.IsVerified = false;
            company.RejectionReason = model.RejectionReason ?? "No reason provided";
            company.RejectedAt = DateTime.UtcNow;
            company.RejectedByAdminId = currentAdmin.Id;
            company.ReapplicationAllowedDate = DateTime.UtcNow.AddDays(reapplicationDays);

            // Create audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentAdmin.Id,
                Action = "RejectedCompany",
                EntityType = "RegisteredCompany",
                EntityId = company.CompanyId,
                Details = $"Rejected company '{company.CompanyName}'. Reason: {model.RejectionReason}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            });

            // Create notification
            if (primaryManager != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = primaryManager.UserId,
                    Type = "CompanyRejected",
                    Title = "Company Registration Reviewed",
                    Message = $"Unfortunately, your company '{company.CompanyName}' registration was not approved. Reason: {model.RejectionReason}. You may reapply after {company.ReapplicationAllowedDate:MMM dd, yyyy}.",
                    ActionUrl = "/Company/CompanyDashboard/Index",
                    IsRead = false,
                    RelatedEntityType = "RegisteredCompany",
                    RelatedEntityId = company.CompanyId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} rejected company {CompanyId}", currentAdmin.Id, company.CompanyId);

            TempData["SuccessMessage"] = $"Company {company.CompanyName} has been rejected.";
            return RedirectToAction("PendingCompanies");
        }

        // ==================== PROJECT MONITORING ====================

        public async Task<IActionResult> Projects(string status, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.TeamRoom)
                .Include(p => p.Company)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                if (status == "Stuck")
                    query = query.Where(p => p.IsStuck);
                else
                    query = query.Where(p => p.Status == status);
            }

            var totalProjects = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalProjects / (double)pageSize);

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var projectItems = new List<ProjectMonitorItem>();

            foreach (var p in projects)
            {
                // Count milestones
                int totalMilestones = await _context.Milestones
                    .CountAsync(m => m.ProjectId == p.ProjectId);

                int completedMilestones = await _context.Milestones
                    .CountAsync(m => m.ProjectId == p.ProjectId && m.Status == "Completed");

                projectItems.Add(new ProjectMonitorItem
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    ClientName = p.Client?.User?.FullName ?? "Unknown",
                    ProviderName = p.TeamRoom != null ? p.TeamRoom.TeamName :
                                  p.Company != null ? p.Company.CompanyName : "N/A",
                    ProviderType = p.TeamRoom != null ? "Team" : p.Company != null ? "Company" : "N/A",
                    Status = p.Status,
                    ProgressPercentage = p.ProgressPercentage,
                    Budget = p.Budget,
                    PlatformEarnings = p.PlatformEarnings,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    LastActivityAt = p.LastActivityAt,
                    IsStuck = p.IsStuck,
                    StuckReason = p.StuckReason ?? string.Empty,
                    DaysSinceActivity = p.LastActivityAt.HasValue
                        ? (int)(DateTime.UtcNow - p.LastActivityAt.Value).TotalDays
                        : (int)(DateTime.UtcNow - p.CreatedAt).TotalDays,
                    TotalMilestones = totalMilestones,
                    CompletedMilestones = completedMilestones
                });
            }

            var viewModel = new ProjectMonitoringViewModel
            {
                Projects = projectItems,
                StatusFilter = status ?? "All",
                TotalProjects = await _context.Projects.CountAsync(),
                ActiveProjects = await _context.Projects.CountAsync(p => p.Status == "Active" || p.Status == "InProgress"),
                CompletedProjects = await _context.Projects.CountAsync(p => p.Status == "Completed"),
                StuckProjects = await _context.Projects.CountAsync(p => p.IsStuck),
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(viewModel);
        }

        public async Task<IActionResult> StuckProjects()
        {
            var stuckProjects = await _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.TeamRoom)
                .Include(p => p.Company)
                .Where(p => p.IsStuck)
                .OrderByDescending(p => p.LastActivityAt)
                .ToListAsync();

            var projectItems = stuckProjects.Select(p => new ProjectMonitorItem
            {
                ProjectId = p.ProjectId,
                ProjectName = p.ProjectName,
                ClientName = p.Client?.User?.FullName ?? "Unknown",
                ProviderName = p.TeamRoom != null ? p.TeamRoom.TeamName :
                              p.Company != null ? p.Company.CompanyName : "N/A",
                ProviderType = p.TeamRoom != null ? "Team" : p.Company != null ? "Company" : "N/A",
                Status = p.Status,
                ProgressPercentage = p.ProgressPercentage,
                Budget = p.Budget,
                PlatformEarnings = p.PlatformEarnings,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                LastActivityAt = p.LastActivityAt,
                IsStuck = p.IsStuck,
                StuckReason = p.StuckReason ?? string.Empty,
                DaysSinceActivity = p.LastActivityAt.HasValue
                    ? (int)(DateTime.UtcNow - p.LastActivityAt.Value).TotalDays
                    : (int)(DateTime.UtcNow - p.CreatedAt).TotalDays
            }).ToList();

            var viewModel = new StuckProjectsViewModel
            {
                StuckProjects = projectItems,
                TotalStuck = projectItems.Count
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStuckStatus(int id, bool isStuck, string reason = null)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                TempData["ErrorMessage"] = "Project not found.";
                return RedirectToAction("Projects");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);

            project.IsStuck = isStuck;
            project.StuckReason = isStuck ? (reason ?? "Marked as stuck by admin") : string.Empty;

            // Create audit log
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentAdmin.Id,
                Action = isStuck ? "MarkedProjectStuck" : "UnmarkedProjectStuck",
                EntityType = "Project",
                EntityId = project.ProjectId,
                Details = isStuck
                    ? $"Marked project '{project.ProjectName}' as stuck. Reason: {reason ?? "No specific reason"}"
                    : $"Unmarked project '{project.ProjectName}' as stuck",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} {Action} project {ProjectId}",
                currentAdmin.Id, isStuck ? "marked as stuck" : "unmarked as stuck", project.ProjectId);

            TempData["SuccessMessage"] = isStuck
                ? "Project marked as stuck."
                : "Project unmarked as stuck.";

            return RedirectToAction("Projects");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DetectStuckProjects()
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            var stuckDays = settings?.ProjectStuckDays ?? 30;
            var currentAdmin = await _userManager.GetUserAsync(User);

            var cutoffDate = DateTime.UtcNow.AddDays(-stuckDays);

            var stuckProjects = await _context.Projects
                .Where(p => (p.Status == "Active" || p.Status == "InProgress") &&
                           !p.IsStuck &&
                           (p.LastActivityAt == null || p.LastActivityAt < cutoffDate))
                .ToListAsync();

            foreach (var project in stuckProjects)
            {
                project.IsStuck = true;
                project.StuckReason = $"No activity for {stuckDays}+ days (auto-detected)";

                // Create audit log
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentAdmin.Id,
                    Action = "AutoDetectedStuckProject",
                    EntityType = "Project",
                    EntityId = project.ProjectId,
                    Details = $"Auto-flagged project '{project.ProjectName}' as stuck (no activity for {stuckDays}+ days)",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} ran stuck detection: {Count} projects flagged",
                currentAdmin.Id, stuckProjects.Count);

            TempData["SuccessMessage"] = $"Detected and flagged {stuckProjects.Count} stuck project(s).";
            return RedirectToAction("Projects");
        }

        // ==================== FINANCIAL OVERVIEW ====================

        public async Task<IActionResult> FinancialOverview()
        {
            var completedProjects = await _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.TeamRoom)
                .Include(p => p.Company)
                .Where(p => p.Status == "Completed")
                .ToListAsync();

            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();

            var viewModel = new FinancialOverviewViewModel
            {
                TotalRevenue = completedProjects.Sum(p => p.Budget),
                TotalPlatformEarnings = completedProjects.Sum(p => p.PlatformEarnings),
                AverageProjectValue = completedProjects.Any()
                    ? completedProjects.Average(p => p.Budget)
                    : 0,
                TotalTransactions = completedProjects.Count,
                CommissionPercentage = settings?.CommissionPercentage ?? 10.00M
            };

            // Monthly revenue for current year
            var currentYear = DateTime.UtcNow.Year;
            var monthlyData = completedProjects
                .Where(p => p.EndDate.HasValue && p.EndDate.Value.Year == currentYear)
                .GroupBy(p => p.EndDate.Value.Month)
                .Select(g => new MonthlyRevenueItem
                {
                    Month = new DateTime(currentYear, g.Key, 1).ToString("MMM yyyy"),
                    Revenue = g.Sum(p => p.Budget),
                    PlatformEarnings = g.Sum(p => p.PlatformEarnings),
                    ProjectsCount = g.Count()
                })
                .OrderBy(m => m.Month)
                .ToList();

            viewModel.MonthlyBreakdown = monthlyData;

            // Current month revenue
            var currentMonth = DateTime.UtcNow.Month;
            var monthlyRevenue = completedProjects
                .Where(p => p.EndDate.HasValue &&
                           p.EndDate.Value.Month == currentMonth &&
                           p.EndDate.Value.Year == currentYear)
                .Sum(p => p.Budget);
            var monthlyEarnings = completedProjects
                .Where(p => p.EndDate.HasValue &&
                           p.EndDate.Value.Month == currentMonth &&
                           p.EndDate.Value.Year == currentYear)
                .Sum(p => p.PlatformEarnings);

            viewModel.MonthlyRevenue = monthlyRevenue;
            viewModel.MonthlyEarnings = monthlyEarnings;

            // Top earning teams
            var teamEarnings = await _context.Projects
                .Where(p => p.Status == "Completed" && p.TeamRoomId.HasValue)
                .GroupBy(p => p.TeamRoomId)
                .Select(g => new
                {
                    TeamRoomId = g.Key.Value,
                    TotalEarned = g.Sum(p => p.Budget - p.PlatformEarnings),
                    ProjectsCompleted = g.Count()
                })
                .OrderByDescending(t => t.TotalEarned)
                .Take(10)
                .ToListAsync();

            var topTeams = new List<TopEarnerItem>();
            foreach (var te in teamEarnings)
            {
                var team = await _context.TeamRooms.FindAsync(te.TeamRoomId);
                if (team != null)
                {
                    topTeams.Add(new TopEarnerItem
                    {
                        Id = team.TeamRoomId,
                        Name = team.TeamName,
                        TotalEarned = te.TotalEarned,
                        ProjectsCompleted = te.ProjectsCompleted,
                        AverageRating = team.AverageRating ?? 0
                    });
                }
            }
            viewModel.TopTeams = topTeams;

            // Top earning companies
            var companyEarnings = await _context.Projects
                .Where(p => p.Status == "Completed" && p.CompanyId.HasValue)
                .GroupBy(p => p.CompanyId)
                .Select(g => new
                {
                    CompanyId = g.Key.Value,
                    TotalEarned = g.Sum(p => p.Budget - p.PlatformEarnings),
                    ProjectsCompleted = g.Count()
                })
                .OrderByDescending(c => c.TotalEarned)
                .Take(10)
                .ToListAsync();

            var topCompanies = new List<TopEarnerItem>();
            foreach (var ce in companyEarnings)
            {
                var company = await _context.RegisteredCompanies.FindAsync(ce.CompanyId);
                if (company != null)
                {
                    topCompanies.Add(new TopEarnerItem
                    {
                        Id = company.CompanyId,
                        Name = company.CompanyName,
                        TotalEarned = ce.TotalEarned,
                        ProjectsCompleted = ce.ProjectsCompleted,
                        AverageRating = company.AverageRating ?? 0
                    });
                }
            }
            viewModel.TopCompanies = topCompanies;

            // Recent transactions
            var recentTransactions = completedProjects
                .OrderByDescending(p => p.EndDate)
                .Take(10)
                .Select(p => new TransactionItem
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    ClientName = p.Client?.User?.FullName ?? "Unknown",
                    ProviderName = p.TeamRoom != null ? p.TeamRoom.TeamName :
                                  p.Company != null ? p.Company.CompanyName : "N/A",
                    Amount = p.Budget,
                    PlatformEarnings = p.PlatformEarnings,
                    CompletedAt = p.EndDate ?? DateTime.UtcNow
                })
                .ToList();

            viewModel.RecentTransactions = recentTransactions;

            return View(viewModel);
        }

        // ==================== PLATFORM SETTINGS ====================

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                // Create default settings if none exist
                settings = new PlatformSettings
                {
                    CommissionPercentage = 10.00M,
                    MinProjectBudget = 100.00M,
                    MaxProjectBudget = 1000000.00M,
                    ProjectStuckDays = 30,
                    ReapplicationWaitDays = 30,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.PlatformSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            var updatedByAdmin = settings.UpdatedByAdminId.HasValue
                ? await _context.Users.FindAsync(settings.UpdatedByAdminId.Value)
                : null;

            var viewModel = new PlatformSettingsViewModel
            {
                SettingsId = settings.SettingsId,
                CommissionPercentage = settings.CommissionPercentage,
                MinProjectBudget = settings.MinProjectBudget,
                MaxProjectBudget = settings.MaxProjectBudget,
                ProjectStuckDays = settings.ProjectStuckDays,
                ReapplicationWaitDays = settings.ReapplicationWaitDays,
                UpdatedAt = settings.UpdatedAt,
                UpdatedByAdmin = updatedByAdmin?.FullName ?? "System"
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(PlatformSettingsViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                TempData["ErrorMessage"] = "Settings not found.";
                return RedirectToAction("Settings");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);

            // Track old values for audit
            var oldCommission = settings.CommissionPercentage;
            var oldMinBudget = settings.MinProjectBudget;
            var oldMaxBudget = settings.MaxProjectBudget;
            var oldStuckDays = settings.ProjectStuckDays;
            var oldReappDays = settings.ReapplicationWaitDays;

            // Update settings
            settings.CommissionPercentage = model.CommissionPercentage;
            settings.MinProjectBudget = model.MinProjectBudget;
            settings.MaxProjectBudget = model.MaxProjectBudget;
            settings.ProjectStuckDays = model.ProjectStuckDays;
            settings.ReapplicationWaitDays = model.ReapplicationWaitDays;
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedByAdminId = currentAdmin.Id;

            // Create audit log
            var changes = new List<string>();
            if (oldCommission != model.CommissionPercentage)
                changes.Add($"Commission: {oldCommission}% → {model.CommissionPercentage}%");
            if (oldMinBudget != model.MinProjectBudget)
                changes.Add($"Min Budget: ${oldMinBudget} → ${model.MinProjectBudget}");
            if (oldMaxBudget != model.MaxProjectBudget)
                changes.Add($"Max Budget: ${oldMaxBudget} → ${model.MaxProjectBudget}");
            if (oldStuckDays != model.ProjectStuckDays)
                changes.Add($"Stuck Days: {oldStuckDays} → {model.ProjectStuckDays}");
            if (oldReappDays != model.ReapplicationWaitDays)
                changes.Add($"Reapp Days: {oldReappDays} → {model.ReapplicationWaitDays}");

            if (changes.Any())
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentAdmin.Id,
                    Action = "UpdatedPlatformSettings",
                    EntityType = "PlatformSettings",
                    EntityId = settings.SettingsId,
                    Details = $"Updated settings: {string.Join(", ", changes)}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} updated platform settings", currentAdmin.Id);

            TempData["SuccessMessage"] = "Platform settings updated successfully!";
            return RedirectToAction("Settings");
        }

        // ==================== AUDIT LOGS ====================

        public async Task<IActionResult> AuditLogs(string action = "All", int page = 1)
        {
            const int pageSize = 50;

            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            // Filter by action if provided
            if (!string.IsNullOrEmpty(action) && action != "All")
            {
                query = query.Where(a => a.Action == action);
            }

            var totalLogs = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new
            {
                Logs = logs,
                ActionFilter = action ?? "All",
                CurrentPage = page,
                TotalPages = totalPages,
                TotalLogs = totalLogs
            };

            return View(viewModel);
        }
    }
}