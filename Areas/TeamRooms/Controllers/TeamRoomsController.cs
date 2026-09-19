using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using Scalaverse.Entitys.Models;
using ScalaVerse.ViewModel;
using ScalaVerse.ViewModel.TeamRoom_VM;

namespace ScalaVerse.Areas.TeamRooms.Controllers
{
	[Area("TeamRooms")]
	public class TeamRoomsController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly ILogger<TeamRoomsController> _logger;

		public TeamRoomsController(
			ApplicationDbContext context,
			UserManager<ApplicationUser> userManager,
			ILogger<TeamRoomsController> logger)
		{
			_context = context;
			_userManager = userManager;
			_logger = logger;
		}

		// ==================== PUBLIC BROWSE (للجميع - Clients & Guests) ====================

		/// <summary>
		/// Browse all available team rooms (Public & Clients)
		/// </summary>
		[AllowAnonymous]
		public async Task<IActionResult> Index(string specialty, decimal? maxRate, int? minRating, string sortBy = "rating")
		{
			// إذا كان المستخدم TeamLeader أو Consultant، وجهه لصفحته
			if (User.Identity.IsAuthenticated)
			{
				if (User.IsInRole("TeamLeader"))
				{
					return RedirectToAction("MyTeams");
				}
				else if (User.IsInRole("Consultant"))
				{
					return RedirectToAction("Index", "Consultant");
				}
			}

			var query = _context.TeamRooms
				.Include(t => t.TeamLeader)
					.ThenInclude(tl => tl.User)
				.Include(t => t.TeamMembers)
				.Where(t => t.IsAvailable )
				.AsQueryable();

			// Apply Filters
			if (!string.IsNullOrEmpty(specialty))
			{
				query = query.Where(t => t.Specialty.Contains(specialty));
			}

			if (maxRate.HasValue)
			{
				query = query.Where(t => t.HourlyRate <= maxRate.Value);
			}

			if (minRating.HasValue)
			{
				query = query.Where(t => t.AverageRating >= minRating.Value);
			}

			// Apply Sorting
			query = sortBy.ToLower() switch
			{
				"rating" => query.OrderByDescending(t => t.AverageRating),
				"price-low" => query.OrderBy(t => t.HourlyRate),
				"price-high" => query.OrderByDescending(t => t.HourlyRate),
				"popular" => query.OrderByDescending(t => t.CompletedProjects),
				"newest" => query.OrderByDescending(t => t.CreatedAt),
				_ => query.OrderByDescending(t => t.AverageRating)
			};

			var teamRooms = await query.ToListAsync();

			// Get distinct specialties for filter dropdown
			ViewBag.Specialties = await _context.TeamRooms
				.Where(t => t.IsAvailable && t.IsVerified)
				.Select(t => t.Specialty)
				.Distinct()
				.OrderBy(s => s)
				.ToListAsync();

			ViewBag.CurrentSpecialty = specialty;
			ViewBag.CurrentMaxRate = maxRate;
			ViewBag.CurrentMinRating = minRating;
			ViewBag.CurrentSortBy = sortBy;

			return View(teamRooms);
		}

		// ==================== DETAILS (للجميع) ====================

		/// <summary>
		/// View team room details
		/// </summary>
		[AllowAnonymous]
		public async Task<IActionResult> Details(int id)
		{
			var teamRoom = await _context.TeamRooms
				.Include(t => t.TeamLeader)
					.ThenInclude(tl => tl.User)
				.Include(t => t.TeamMembers)
				.Include(t => t.Reviews)
				.Include(t => t.Availability.Where(a => a.StartDate >= DateTime.UtcNow))
				.Include(t => t.Projects.Where(p => p.Status == "Completed"))
				.FirstOrDefaultAsync(t => t.TeamRoomId == id);

			if (teamRoom == null)
			{
				return NotFound();
			}

			var viewModel = new TeamRoomDetailsViewModel
			{
				TeamRoom = teamRoom,
				AvailableSlots = teamRoom.Availability
					.Where(a => !a.IsBooked && a.StartDate >= DateTime.UtcNow)
					.OrderBy(a => a.StartDate)
					.ToList(),
				RecentReviews = teamRoom.Reviews
					.OrderByDescending(r => r.CreatedAt)
					.Take(5)
					.ToList(),
				CompletedProjectsCount = teamRoom.Projects.Count(),
				TeamMembers = teamRoom.TeamMembers
					.OrderBy(m => m.DisplayOrder)
					.ToList()
			};

			return View(viewModel);
		}

		// ==================== BOOK (للـ Clients فقط) ====================
		// استبدل الـ Book GET و POST الموجودين بالكود ده كامل

		/// <summary>
		/// GET: Book — مش محتاجه تاني لأن المودال في الـ Index
		/// بس خليناه عشان لو حد دخل على الـ URL مباشرة
		/// </summary>
		[Authorize(Roles = "Client")]
		[HttpGet]
		public async Task<IActionResult> Book(int id)
		{
			// redirect للـ Index وافتح المودال من هناك
			return RedirectToAction(nameof(Index));
		}

		/// <summary>
		/// POST: Book — بيعمل ProjectRequest بدل Project مباشرة
		/// </summary>
		[Authorize(Roles = "Client")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Book(BookTeamRoomViewModel model)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null)
				return Json(new { success = false, message = "Unauthorized. Please log in." });

			var client = await _context.Clients
				.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

			if (client == null)
				return Json(new { success = false, message = "Client profile not found." });

			if (string.IsNullOrWhiteSpace(model.ProjectName))
				return Json(new { success = false, message = "Project name is required." });

			var team = await _context.TeamRooms
				.Include(t => t.TeamLeader)
					.ThenInclude(tl => tl.User)
				.FirstOrDefaultAsync(t => t.TeamRoomId == model.TeamRoomId);

			if (team == null)
				return Json(new { success = false, message = "Team not found." });

			if (!team.IsAvailable)
				return Json(new { success = false, message = "This team is no longer available." });

			try
			{
				// ✅ عمل ProjectRequest بدل Project مباشرة
				var request = new ProjectRequest
				{
					ClientId = client.ClientId,
					TeamRoomId = team.TeamRoomId,
					ProjectName = model.ProjectName.Trim(),
					Description = model.ProjectDescription?.Trim(),
					Category = team.Specialty,
					ClientBudget = model.Budget > 0 ? model.Budget : (decimal?)null,
					Status = "Pending",
					CreatedAt = DateTime.UtcNow
				};

				_context.ProjectRequests.Add(request);
				await _context.SaveChangesAsync();

				// إشعار للـ TeamLeader
				CreateNotification(
					team.TeamLeader.UserId,
					"NewProjectRequest",
					"New Project Request 📋",
					$"{currentUser.FullName} sent a request for: {model.ProjectName}",
					request.RequestId,
					"ProjectRequest",
					$"/TeamRooms/Projects/IncomingRequests"
				);

				await CreateAuditLog(
					currentUser.Id,
					"ProjectRequested",
					$"Client sent request to team '{team.TeamName}' for '{model.ProjectName}'",
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
				_logger.LogError(ex, "Error creating project request for team {TeamRoomId}", model.TeamRoomId);
				return Json(new { success = false, message = ex.Message + " | " + ex.InnerException?.Message });
			}
		}


		// ==================== CREATE TEAM(للـ TeamLeaders فقط) ====================
		/// <summary>
		/// Create team leader profile (TeamLeaders only)
		/// </summary>
		/// <summary>
		[Authorize(Roles = "TeamLeader")]
		[HttpGet]
		public async Task<IActionResult> TeamLeaderProfile()
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null)
				return Unauthorized();

			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			if (teamLeader != null)
			{
				var vm = new TeamLeaderProfileViewModel
				{
					Bio = teamLeader.Bio,
					ExpertiseAreas = teamLeader.ExpertiseAreas,
					YearsOfExperience = teamLeader.YearsOfExperience,
					PortfolioUrl = teamLeader.PortfolioUrl
				};

				return View(vm);
			}

			return View(new TeamLeaderProfileViewModel());
		}
		[Authorize(Roles = "TeamLeader")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> TeamLeaderProfile(TeamLeaderProfileViewModel model)
		{
			if (!ModelState.IsValid)
				return View(model);

			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null)
				return Unauthorized();

			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			if (teamLeader == null)
			{
				teamLeader = new TeamLeader
				{
					UserId = currentUser.Id,
					Bio = model.Bio,
					ExpertiseAreas = model.ExpertiseAreas,
					YearsOfExperience = model.YearsOfExperience,
					PortfolioUrl = model.PortfolioUrl,
					IsVerified = false,
					CreatedAt = DateTime.UtcNow
				};

				_context.TeamLeaders.Add(teamLeader);
			}
			else
			{
				teamLeader.Bio = model.Bio;
				teamLeader.ExpertiseAreas = model.ExpertiseAreas;
				teamLeader.YearsOfExperience = model.YearsOfExperience;
				teamLeader.PortfolioUrl = model.PortfolioUrl;
			}

			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Profile saved successfully.";
			return RedirectToAction("Create", "TeamRooms");
		}





		/// Create new team room (TeamLeaders only)
		/// </summary>
		[Authorize(Roles = "TeamLeader")]
		[HttpGet]
		public IActionResult Create()
		{
			return View();
		}

		/// <summary>
		/// Process team room creation
		/// </summary>
		[Authorize(Roles = "TeamLeader")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreateTeamRoomViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null)
			{
				return Unauthorized();
			}

			// Get TeamLeader profile
			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			if (teamLeader == null)
			{
				TempData["ErrorMessage"] = "TeamLeader profile not found. Please contact support.";
				return RedirectToAction("Profile", "Account");
			}

			try
			{
				// Create TeamRoom
				var teamRoom = new TeamRoom
				{
					TeamLeaderId = teamLeader.TeamLeaderId,
					TeamName = model.TeamName,
					Description = model.Description,
					Specialty = model.Specialty,
					Skills = System.Text.Json.JsonSerializer.Serialize(model.Skills ?? new List<string>()),
					TeamSize = model.TeamMembers?.Count ?? 0,
					HourlyRate = model.HourlyRate,
					ProjectRate = model.ProjectRate,
					IsAvailable = true,
					IsVerified = false, // Needs admin approval
					AverageRating = 0,
					TotalReviews = 0,
					CompletedProjects = 0,
					CreatedAt = DateTime.UtcNow
				};

				_context.TeamRooms.Add(teamRoom);
				await _context.SaveChangesAsync();

				// Add team members
				if (model.TeamMembers != null && model.TeamMembers.Any())
				{
					int displayOrder = 1;
					foreach (var memberModel in model.TeamMembers)
					{
						var member = new TeamMember
						{
							TeamRoomId = teamRoom.TeamRoomId,
							MemberName = memberModel.MemberName,
							Role = memberModel.Role,
							Skills = System.Text.Json.JsonSerializer.Serialize(memberModel.Skills ?? new List<string>()),
							DisplayOrder = displayOrder++,
							ProfileImageUrl = memberModel.ProfileImageUrl
						};
						_context.TeamMembers.Add(member);
					}
					await _context.SaveChangesAsync();
				}

				// Create audit log
				await CreateAuditLog(
					currentUser.Id,
					"TeamRoomCreated",
					$"Created new team room: {model.TeamName}"
				);

				TempData["SuccessMessage"] = "Team Room created successfully! It will be available after admin approval.";
				return RedirectToAction(nameof(MyTeams));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating team room");
				TempData["ErrorMessage"] = "An error occurred while creating the team. Please try again.";
				return View(model);
			}
		}


		// ==================== MY TEAMS (للـ TeamLeaders فقط) ====================

		/// <summary>
		/// View my team rooms (TeamLeaders only)
		/// </summary>
		[Authorize(Roles = "TeamLeader")]
		public async Task<IActionResult> MyTeams()
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null)
			{
				return Unauthorized();
			}

			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			if (teamLeader == null)
			{
				TempData["InfoMessage"] = "Create your first team room to get started!";
				return RedirectToAction(nameof(Create));
			}

			var teamRooms = await _context.TeamRooms
				.Include(t => t.TeamMembers)
				.Include(t => t.Projects)
				.Where(t => t.TeamLeaderId == teamLeader.TeamLeaderId)
				.OrderByDescending(t => t.CreatedAt)
				.ToListAsync();

			return View(teamRooms);
		}

		// ==================== EDIT TEAM (للـ TeamLeaders فقط) ====================

		/// <summary>
		/// Edit team room
		/// </summary>
		[Authorize(Roles = "TeamLeader")]
		[HttpGet]
		public async Task<IActionResult> Edit(int id)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			var teamRoom = await _context.TeamRooms
				.Include(t => t.TeamMembers)
				.FirstOrDefaultAsync(t => t.TeamRoomId == id && t.TeamLeaderId == teamLeader.TeamLeaderId);

			if (teamRoom == null)
			{
				return NotFound();
			}

			var viewModel = new EditTeamRoomViewModel
			{
				TeamRoomId = teamRoom.TeamRoomId,
				TeamName = teamRoom.TeamName,
				Description = teamRoom.Description,
				Specialty = teamRoom.Specialty,
				Skills = System.Text.Json.JsonSerializer.Deserialize<List<string>>(teamRoom.Skills),
				HourlyRate = teamRoom.HourlyRate,
				ProjectRate = teamRoom.ProjectRate,
				IsAvailable = teamRoom.IsAvailable
			};

			return View(viewModel);
		}

		[Authorize(Roles = "TeamLeader")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(EditTeamRoomViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			var teamRoom = await _context.TeamRooms
				.FirstOrDefaultAsync(t => t.TeamRoomId == model.TeamRoomId && t.TeamLeaderId == teamLeader.TeamLeaderId);

			if (teamRoom == null)
			{
				return NotFound();
			}

			teamRoom.TeamName = model.TeamName;
			teamRoom.Description = model.Description;
			teamRoom.Specialty = model.Specialty;
			teamRoom.Skills = System.Text.Json.JsonSerializer.Serialize(model.Skills);
			teamRoom.HourlyRate = model.HourlyRate;
			teamRoom.ProjectRate = model.ProjectRate;
			teamRoom.IsAvailable = model.IsAvailable;

			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Team room updated successfully!";
			return RedirectToAction(nameof(MyTeams));
		}

		// ==================== DELETE TEAM (للـ TeamLeaders فقط) ====================

		[Authorize(Roles = "TeamLeader")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			var teamRoom = await _context.TeamRooms
				.Include(t => t.Projects)
				.FirstOrDefaultAsync(t => t.TeamRoomId == id && t.TeamLeaderId == teamLeader.TeamLeaderId);

			if (teamRoom == null)
			{
				return NotFound();
			}

			// Check if team has active projects
			if (teamRoom.Projects.Any(p => p.Status == "InProgress" || p.Status == "Pending"))
			{
				TempData["ErrorMessage"] = "Cannot delete team room with active projects.";
				return RedirectToAction(nameof(MyTeams));
			}

			_context.TeamRooms.Remove(teamRoom);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Team room deleted successfully.";
			return RedirectToAction(nameof(MyTeams));
		}

		// ==================== SEARCH (AJAX) ====================

		[HttpGet]
		public async Task<IActionResult> Search(string term)
		{
			if (string.IsNullOrWhiteSpace(term))
			{
				return Json(new List<object>());
			}

			var results = await _context.TeamRooms
				.Where(t => t.IsAvailable && t.IsVerified &&
						   (t.TeamName.Contains(term) ||
							t.Specialty.Contains(term) ||
							t.Description.Contains(term)))
				.Select(t => new
				{
					id = t.TeamRoomId,
					name = t.TeamName,
					specialty = t.Specialty,
					rating = t.AverageRating,
					rate = t.HourlyRate
				})
				.Take(10)
				.ToListAsync();

			return Json(results);
		}

		// ==================== HELPER METHODS ====================

		private async Task ReloadTeamRoom(BookTeamRoomViewModel model)
		{
			var teamRoom = await _context.TeamRooms
				.Include(t => t.TeamLeader)
					.ThenInclude(tl => tl.User)
				.FirstOrDefaultAsync(t => t.TeamRoomId == model.TeamRoomId);

			if (teamRoom != null)
			{
				model.TeamName = teamRoom.TeamName;
				model.TeamLeaderName = teamRoom.TeamLeader.User.FullName;
				model.HourlyRate = teamRoom.HourlyRate;
				model.ProjectRate = teamRoom.ProjectRate;
			}
		}

		private void CreateDefaultMilestones(int projectId, decimal budget, string projectName)
		{
			int count = 3;
			decimal amount = budget / count;
			var start = DateTime.UtcNow;

			for (int i = 1; i <= count; i++)
			{
				_context.Milestones.Add(new Milestone
				{
					ProjectId = projectId,
					Title = $"Milestone {i}",
					Description = $"Deliverable {i} for {projectName}",
					DisplayOrder = i,
					DueDate = start.AddDays(10 * i),
					Amount = amount,
					Status = "Pending",
					RejectionReason = string.Empty
				});
			}
		}

		private async Task CreateProjectConversation(
			int projectId,
			string projectName,
			int clientUserId,
			int teamLeaderUserId)
		{
			var conversation = new Conversation
			{
				ProjectId = projectId,
				ConversationType = "Project",
				Title = $"{projectName} - Workspace",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};

			_context.Conversations.Add(conversation);
			await _context.SaveChangesAsync(); // 👈 مطلوب فقط هنا

			_context.ConversationParticipants.AddRange(
				new ConversationParticipant
				{
					ConversationId = conversation.ConversationId,
					UserId = clientUserId,
					Role = "Admin",
					JoinedAt = DateTime.UtcNow
				},
				new ConversationParticipant
				{
					ConversationId = conversation.ConversationId,
					UserId = teamLeaderUserId,
					Role = "Member",
					JoinedAt = DateTime.UtcNow
				}
			);
		}

		private void CreateNotification(
			int userId,
			string type,
			string title,
			string message,
			int relatedEntityId,
			string relatedEntityType,
			string actionUrl)
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

		private Task CreateAuditLog(
		int userId,
		string action,
		string details,
		string entityType = null,
		int? entityId = null)
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

			return Task.CompletedTask;
		}

	}
}
