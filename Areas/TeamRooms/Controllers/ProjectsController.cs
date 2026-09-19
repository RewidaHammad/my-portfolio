using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalaverse.DataAccess.Data;
using Scalaverse.Entities.Models;
using Scalaverse.Entitys.Models;
using ScalaVerse.ViewModel.Project_VM;

namespace ScalaVerse.Controllers
{
	[Area("TeamRooms")]
	[Authorize]
	public class ProjectsController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly ILogger<ProjectsController> _logger;
		private readonly IWebHostEnvironment _environment;

		public ProjectsController(
			ApplicationDbContext context,
			UserManager<ApplicationUser> userManager,
			ILogger<ProjectsController> logger,
			IWebHostEnvironment environment)
		{
			_context = context;
			_userManager = userManager;
			_logger = logger;
			_environment = environment;
		}

		// ==================== INDEX ====================

		[Authorize(Roles = "Client")]
		public async Task<IActionResult> Index(string status = "all")
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null) return Unauthorized();

			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			if (client == null)
			{
				TempData["ErrorMessage"] = "Client profile not found.";
				return RedirectToAction("Index", "Home");
			}

			var baseQuery = _context.Projects.Where(p => p.ClientId == client.ClientId);

			var projects = await baseQuery
				.Include(p => p.TeamRoom).ThenInclude(t => t.TeamLeader).ThenInclude(tl => tl.User)
				.Include(p => p.Company)
				.Include(p => p.Milestones)
				.Where(p => status == "all" || p.Status.ToLower() == status.ToLower())
				.OrderByDescending(p => p.CreatedAt)
				.ToListAsync();

			ViewBag.CurrentStatus = status;
			ViewBag.StatusCounts = new
			{
				All = await baseQuery.CountAsync(),
				Pending = await baseQuery.CountAsync(p => p.Status == "Pending"),
				Active = await baseQuery.CountAsync(p => p.Status == "Active"),
				Completed = await baseQuery.CountAsync(p => p.Status == "Completed")
			};

			return View(projects);
		}

		// ==================== DETAILS ====================

		[Authorize(Roles = "Client,TeamLeader,CompanyManager")]
		public async Task<IActionResult> Details(int id)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null) return Unauthorized();

			var project = await _context.Projects
				.Include(p => p.Client).ThenInclude(c => c.User)
				.Include(p => p.TeamRoom).ThenInclude(t => t.TeamLeader).ThenInclude(tl => tl.User)
				.Include(p => p.TeamRoom.TeamMembers)
				.Include(p => p.Company).ThenInclude(c => c.Managers).ThenInclude(m => m.User)
				.Include(p => p.Milestones).ThenInclude(m => m.Payment)
				.Include(p => p.Documents)
				.Include(p => p.Comments).ThenInclude(c => c.User)
				.Include(p => p.Review)
				.Include(p => p.Conversation).ThenInclude(c => c.Messages).ThenInclude(m => m.Sender)
				.FirstOrDefaultAsync(p => p.ProjectId == id);

			if (project == null) return NotFound();

			project.Milestones = project.Milestones.OrderBy(m => m.DisplayOrder).ToList();
			project.Comments = project.Comments.OrderByDescending(c => c.CreatedAt).ToList();

			if (project.Conversation != null)
				project.Conversation.Messages = project.Conversation.Messages
					.OrderBy(m => m.SentAt).Take(50).ToList();

			// Check access
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			var teamLeader = await _context.TeamLeaders.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);
			var companyManager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			bool isClient = client != null && project.ClientId == client.ClientId;
			bool isTeamLeader = teamLeader != null && project.TeamRoom?.TeamLeaderId == teamLeader.TeamLeaderId;
			bool isCompanyManager = companyManager != null && project.CompanyId == companyManager.CompanyId;

			if (!isClient && !isTeamLeader && !isCompanyManager)
			{
				TempData["ErrorMessage"] = "You don't have access to this project.";
				return RedirectToAction("Index");
			}

			var totalPaid = project.Milestones
				.Where(m => m.Status == "Approved")
				.Sum(m => m.Amount);

			var viewModel = new ProjectDetailsViewModel
			{
				Project = project,
				Milestones = project.Milestones.ToList(),
				Documents = project.Documents.OrderByDescending(d => d.UploadedAt).ToList(),
				Comments = project.Comments.ToList(),
				RecentMessages = project.Conversation?.Messages?.ToList() ?? new List<Message>(),
				IsClient = isClient,
				IsTeamLeader = isTeamLeader,
				IsCompanyManager = isCompanyManager,
				TotalPaid = totalPaid,
				CompletedMilestones = project.Milestones.Count(m => m.Status == "Approved"),
				TotalMilestones = project.Milestones.Count,
				DaysRemaining = project.EstimatedEndDate.HasValue
					? Math.Max(0, (project.EstimatedEndDate.Value - DateTime.UtcNow).Days) : 0,
				CanSubmitReview = isClient
					&& project.Status == "Completed"
					&& project.Review == null
			};

			return View(viewModel);
		}

		// ==================== UPDATE STATUS (Client) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> UpdateStatus(int projectId, string newStatus)
		{
			var validStatuses = new[] { "Active", "OnHold", "Completed", "Cancelled" };
			if (!validStatuses.Contains(newStatus))
				return Json(new { success = false, message = "Invalid status" });

			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

			var project = await _context.Projects
				.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.ClientId == client.ClientId);

			if (project == null)
				return Json(new { success = false, message = "Project not found or unauthorized" });

			project.Status = newStatus;
			if (newStatus == "Completed")
			{
				project.CompletedAt = DateTime.UtcNow;
				project.ProgressPercentage = 100;
			}

			await _context.SaveChangesAsync();
			await LogAudit(currentUser.Id, "ProjectStatusUpdated",
				$"Status changed to {newStatus}", "Project", projectId);

			return Json(new { success = true, message = $"Project status updated to {newStatus}" });
		}

		// ==================== COMPLETE MILESTONE (TeamLeader / CompanyManager) ====================

		[HttpPost]
		[Authorize(Roles = "TeamLeader,CompanyManager")]
		public async Task<IActionResult> CompleteMilestone(int milestoneId)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);
			var companyManager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			if (teamLeader == null && companyManager == null)
				return Json(new { success = false, message = "Unauthorized" });

			// جلب الـ milestone مع التحقق من الصلاحية
			var milestone = teamLeader != null
				? await _context.Milestones
					.Include(m => m.Project).ThenInclude(p => p.Client)
					.Include(m => m.Project).ThenInclude(p => p.TeamRoom)
					.FirstOrDefaultAsync(m => m.MilestoneId == milestoneId
						&& m.Project.TeamRoom.TeamLeaderId == teamLeader.TeamLeaderId)
				: await _context.Milestones
					.Include(m => m.Project).ThenInclude(p => p.Client)
					.Include(m => m.Project).ThenInclude(p => p.Company)
					.FirstOrDefaultAsync(m => m.MilestoneId == milestoneId
						&& m.Project.CompanyId == companyManager.CompanyId);

			if (milestone == null)
				return Json(new { success = false, message = "Milestone not found or unauthorized" });

			if (milestone.Status != "Pending" && milestone.Status != "Rejected" && milestone.Status != "InProgress")
				return Json(new { success = false, message = "Milestone cannot be submitted in its current state" });

			milestone.Status = "UnderReview";
			milestone.CompletedAt = DateTime.UtcNow;
			milestone.RejectionReason = null;

			await _context.SaveChangesAsync();

			await CreateNotification(
				milestone.Project.Client.UserId,
				"MilestoneCompleted",
				"Milestone Ready for Review",
				$"Milestone '{milestone.Title}' is ready for your review",
				milestone.MilestoneId, "Milestone",
				$"/TeamRooms/Projects/Details/{milestone.ProjectId}");

			await LogAudit(currentUser.Id, "MilestoneSubmitted",
				$"Submitted milestone: {milestone.Title}", "Milestone", milestoneId);

			return Json(new { success = true, message = "Milestone submitted for client review!" });
		}

		// ==================== UPDATE MILESTONE STATUS (TeamLeader / CompanyManager) ====================

		[HttpPost]
		[Authorize(Roles = "TeamLeader,CompanyManager")]
		public async Task<IActionResult> UpdateMilestoneStatus(int milestoneId, string newStatus)
		{
			var allowedStatuses = new[] { "Pending", "InProgress" };
			if (!allowedStatuses.Contains(newStatus))
				return Json(new { success = false, message = "Invalid status transition" });

			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);
			var companyManager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			if (teamLeader == null && companyManager == null)
				return Json(new { success = false, message = "Unauthorized" });

			var milestone = teamLeader != null
				? await _context.Milestones
					.Include(m => m.Project).ThenInclude(p => p.TeamRoom)
					.FirstOrDefaultAsync(m => m.MilestoneId == milestoneId
						&& m.Project.TeamRoom.TeamLeaderId == teamLeader.TeamLeaderId)
				: await _context.Milestones
					.Include(m => m.Project)
					.FirstOrDefaultAsync(m => m.MilestoneId == milestoneId
						&& m.Project.CompanyId == companyManager.CompanyId);

			if (milestone == null)
				return Json(new { success = false, message = "Milestone not found" });

			if (milestone.Status == "Approved")
				return Json(new { success = false, message = "Approved milestones cannot be changed" });

			if (milestone.Status == "UnderReview")
				return Json(new { success = false, message = "Milestone is under client review. Wait for response." });

			var oldStatus = milestone.Status;
			milestone.Status = newStatus;

			await _context.SaveChangesAsync();
			await LogAudit(currentUser.Id, "MilestoneStatusUpdated",
				$"Milestone '{milestone.Title}' changed from {oldStatus} to {newStatus}",
				"Milestone", milestoneId);

			return Json(new { success = true, message = $"Milestone moved to {newStatus}", newStatus });
		}

		// ==================== SIMULATE PAYMENT / APPROVE MILESTONE (Client) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> SimulatePayment(int milestoneId)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			if (client == null)
				return Json(new { success = false, message = "Unauthorized" });

			var milestone = await _context.Milestones
				.Include(m => m.Project).ThenInclude(p => p.TeamRoom).ThenInclude(tr => tr.TeamLeader)
				.Include(m => m.Project).ThenInclude(p => p.Company).ThenInclude(c => c.Managers)
				.Include(m => m.Payment)
				.FirstOrDefaultAsync(m => m.MilestoneId == milestoneId
					&& m.Project.ClientId == client.ClientId);

			if (milestone == null)
				return Json(new { success = false, message = "Milestone not found" });

			if (milestone.Status != "UnderReview")
				return Json(new { success = false, message = "Milestone must be under review to approve payment" });

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				milestone.Status = "Approved";
				milestone.ApprovedAt = DateTime.UtcNow;
				milestone.ApprovedByUserId = currentUser.Id;

				var platformFee = milestone.Amount * 0.10m;
				var transactionId = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{milestoneId}";

				// تحديد الـ receiver — TeamLeader أو Primary Manager
				int receiverUserId;
				if (milestone.Project.TeamRoom?.TeamLeader != null)
				{
					receiverUserId = milestone.Project.TeamRoom.TeamLeader.UserId;
				}
				else
				{
					var primaryManager = milestone.Project.Company?.Managers
						.FirstOrDefault(m => m.IsPrimaryContact && m.IsActive)
						?? milestone.Project.Company?.Managers.FirstOrDefault(m => m.IsActive);
					receiverUserId = primaryManager?.UserId ?? currentUser.Id;
				}

				if (milestone.Payment != null)
				{
					milestone.Payment.Status = "Completed";
					milestone.Payment.ReleasedAt = DateTime.UtcNow;
					milestone.Payment.TransactionId = transactionId;
					milestone.Payment.ReceiverUserId = receiverUserId;
				}
				else
				{
					_context.Payments.Add(new Payment
					{
						MilestoneId = milestoneId,
						PayerUserId = currentUser.Id,
						ReceiverUserId = receiverUserId,
						Amount = milestone.Amount,
						PlatformFee = platformFee,
						NetAmount = milestone.Amount - platformFee,
						PaymentMethod = "Simulated",
						Status = "Completed",
						TransactionId = transactionId,
						CreatedAt = DateTime.UtcNow,
						ReleasedAt = DateTime.UtcNow
					});
				}

				// تحديث progress
				var totalMilestones = await _context.Milestones
					.CountAsync(m => m.ProjectId == milestone.ProjectId);
				var approvedCount = await _context.Milestones
					.CountAsync(m => m.ProjectId == milestone.ProjectId && m.Status == "Approved") + 1;

				milestone.Project.ProgressPercentage = totalMilestones > 0
					? (int)(approvedCount / (decimal)totalMilestones * 100) : 0;

				if (approvedCount >= totalMilestones)
				{
					milestone.Project.Status = "Completed";
					milestone.Project.CompletedAt = DateTime.UtcNow;
				}

				await _context.SaveChangesAsync();

				await CreateNotification(
					receiverUserId,
					"PaymentReleased",
					"Payment Released 💰",
					$"${milestone.Amount:N0} released for milestone '{milestone.Title}'",
					milestone.MilestoneId, "Milestone",
					$"/TeamRooms/Projects/Details/{milestone.ProjectId}");

				await LogAudit(currentUser.Id, "PaymentSimulated",
					$"Simulated payment approved for: {milestone.Title}", "Milestone", milestoneId);

				await transaction.CommitAsync();

				return Json(new
				{
					success = true,
					message = $"Payment of ${milestone.Amount:N0} released successfully!",
					progress = milestone.Project.ProgressPercentage,
					transactionId
				});
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error simulating payment for milestone {MilestoneId}", milestoneId);
				return Json(new { success = false, message = "An error occurred. Please try again." });
			}
		}

		// ==================== REJECT MILESTONE (Client) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> RejectMilestone(int milestoneId, string reason)
		{
			if (string.IsNullOrWhiteSpace(reason))
				return Json(new { success = false, message = "Rejection reason is required" });

			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			if (client == null) return Json(new { success = false, message = "Unauthorized" });

			var milestone = await _context.Milestones
				.Include(m => m.Project).ThenInclude(p => p.TeamRoom).ThenInclude(tr => tr.TeamLeader)
				.Include(m => m.Project).ThenInclude(p => p.Company).ThenInclude(c => c.Managers)
				.FirstOrDefaultAsync(m => m.MilestoneId == milestoneId
					&& m.Project.ClientId == client.ClientId);

			if (milestone == null)
				return Json(new { success = false, message = "Milestone not found or unauthorized" });

			if (milestone.Status != "UnderReview")
				return Json(new { success = false, message = "Only milestones under review can be rejected" });

			milestone.Status = "Rejected";
			milestone.RejectionReason = reason;
			await _context.SaveChangesAsync();

			// إشعار للـ TeamLeader أو Manager
			if (milestone.Project.TeamRoom?.TeamLeader?.UserId != null)
			{
				await CreateNotification(
					milestone.Project.TeamRoom.TeamLeader.UserId,
					"MilestoneRejected", "Milestone Needs Revision",
					$"Milestone '{milestone.Title}' requires changes: {reason}",
					milestone.MilestoneId, "Milestone",
					$"/TeamRooms/Projects/Details/{milestone.ProjectId}");
			}
			else if (milestone.Project.Company?.Managers != null)
			{
				var primaryManager = milestone.Project.Company.Managers
					.FirstOrDefault(m => m.IsPrimaryContact && m.IsActive)
					?? milestone.Project.Company.Managers.FirstOrDefault(m => m.IsActive);

				if (primaryManager != null)
					await CreateNotification(
						primaryManager.UserId,
						"MilestoneRejected", "Milestone Needs Revision",
						$"Milestone '{milestone.Title}' requires changes: {reason}",
						milestone.MilestoneId, "Milestone",
						$"/TeamRooms/Projects/Details/{milestone.ProjectId}");
			}

			await LogAudit(currentUser.Id, "MilestoneRejected",
				$"Rejected: {milestone.Title}. Reason: {reason}", "Milestone", milestoneId);

			return Json(new { success = true, message = "Milestone rejected successfully" });
		}

		// ==================== UPLOAD DOCUMENT ====================

		[HttpPost]
		[Authorize(Roles = "Client,TeamLeader,CompanyManager")]
		public async Task<IActionResult> UploadDocument(int projectId, IFormFile file, string category = "Other")
		{
			if (file == null || file.Length == 0)
				return Json(new { success = false, message = "No file selected" });

			if (file.Length > 10 * 1024 * 1024)
				return Json(new { success = false, message = "File size exceeds 10MB limit" });

			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			var teamLeader = await _context.TeamLeaders.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);
			var companyManager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			var project = await _context.Projects
				.Include(p => p.TeamRoom)
				.FirstOrDefaultAsync(p => p.ProjectId == projectId);

			if (project == null) return Json(new { success = false, message = "Project not found" });

			bool hasAccess =
				(client != null && project.ClientId == client.ClientId) ||
				(teamLeader != null && project.TeamRoom?.TeamLeaderId == teamLeader.TeamLeaderId) ||
				(companyManager != null && project.CompanyId == companyManager.CompanyId);

			if (!hasAccess) return Json(new { success = false, message = "Unauthorized" });

			try
			{
				var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "projects", projectId.ToString());
				Directory.CreateDirectory(uploadsPath);

				var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
				var filePath = Path.Combine(uploadsPath, fileName);

				using (var stream = new FileStream(filePath, FileMode.Create))
					await file.CopyToAsync(stream);

				var document = new ProjectDocument
				{
					ProjectId = projectId,
					FileName = Path.GetFileName(file.FileName),
					FileUrl = $"/uploads/projects/{projectId}/{fileName}",
					FileType = file.ContentType,
					FileSize = file.Length,
					Category = category,
					UploadedByUserId = currentUser.Id,
					UploadedAt = DateTime.UtcNow
				};

				_context.ProjectDocuments.Add(document);
				await _context.SaveChangesAsync();
				await LogAudit(currentUser.Id, "DocumentUploaded",
					$"Uploaded: {document.FileName}", "ProjectDocument", document.DocumentId);

				return Json(new
				{
					success = true,
					message = "File uploaded successfully",
					documentId = document.DocumentId,
					fileName = document.FileName
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error uploading document for project {ProjectId}", projectId);
				return Json(new { success = false, message = "Upload failed. Please try again." });
			}
		}

		// ==================== ADD COMMENT ====================

		[HttpPost]
		[Authorize(Roles = "Client,TeamLeader,CompanyManager")]
		public async Task<IActionResult> AddComment(int projectId, string commentText)
		{
			if (string.IsNullOrWhiteSpace(commentText))
				return Json(new { success = false, message = "Comment cannot be empty" });

			var currentUser = await _userManager.GetUserAsync(User);

			var comment = new ProjectComment
			{
				ProjectId = projectId,
				UserId = currentUser.Id,
				CommentText = commentText.Trim(),
				IsInternal = false,
				CreatedAt = DateTime.UtcNow
			};

			_context.ProjectComments.Add(comment);
			await _context.SaveChangesAsync();

			return Json(new
			{
				success = true,
				comment = new
				{
					comment.CommentId,
					userName = currentUser.FullName,
					comment.CommentText,
					createdAt = comment.CreatedAt.ToString("MMM dd, HH:mm")
				}
			});
		}

		// ==================== SEND MESSAGE ====================

		[HttpPost]
		[Authorize(Roles = "Client,TeamLeader,CompanyManager")]
		public async Task<IActionResult> SendMessage(int projectId, string messageText)
		{
			if (string.IsNullOrWhiteSpace(messageText))
				return Json(new { success = false, message = "Message cannot be empty" });

			var currentUser = await _userManager.GetUserAsync(User);
			var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.ProjectId == projectId);
			if (conversation == null)
				return Json(new { success = false, message = "Conversation not found" });

			var message = new Message
			{
				ConversationId = conversation.ConversationId,
				SenderUserId = currentUser.Id,
				MessageText = messageText.Trim(),
				MessageType = "Text",
				SentAt = DateTime.UtcNow
			};

			_context.Messages.Add(message);
			conversation.LastMessageAt = DateTime.UtcNow;
			await _context.SaveChangesAsync();

			return Json(new
			{
				success = true,
				message = new
				{
					message.MessageId,
					senderName = currentUser.FullName,
					message.MessageText,
					sentAt = message.SentAt.ToString("HH:mm")
				}
			});
		}

		// ==================== SUBMIT REVIEW ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> SubmitReview(int projectId, int rating, string comment,
			int? communicationRating, int? qualityRating, int? timelinessRating, int? professionalismRating)
		{
			if (rating < 1 || rating > 5)
				return Json(new { success = false, message = "Rating must be between 1 and 5" });

			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			if (client == null) return Json(new { success = false, message = "Unauthorized" });

			var project = await _context.Projects
				.Include(p => p.TeamRoom)
				.Include(p => p.Review)
				.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.ClientId == client.ClientId);

			if (project == null) return Json(new { success = false, message = "Project not found" });
			if (project.Status != "Completed") return Json(new { success = false, message = "Can only review completed projects" });
			if (project.Review != null) return Json(new { success = false, message = "Review already submitted" });

			var review = new Review
			{
				ProjectId = projectId,
				TeamRoomId = project.TeamRoomId,
				CompanyId = project.CompanyId,
				ReviewerUserId = currentUser.Id,
				Rating = rating,
				Comment = comment?.Trim(),
				CommunicationRating = communicationRating,
				QualityRating = qualityRating,
				TimelinessRating = timelinessRating,
				ProfessionalismRating = professionalismRating,
				IsPublic = true,
				CreatedAt = DateTime.UtcNow
			};

			_context.Reviews.Add(review);

			// تحديث rating الـ TeamRoom
			if (project.TeamRoomId.HasValue)
			{
				var teamRoom = await _context.TeamRooms.FindAsync(project.TeamRoomId.Value);
				if (teamRoom != null)
				{
					var existingRatings = await _context.Reviews
						.Where(r => r.TeamRoomId == teamRoom.TeamRoomId)
						.Select(r => r.Rating).ToListAsync();
					teamRoom.TotalReviews = existingRatings.Count + 1;
					teamRoom.AverageRating = (existingRatings.Sum() + rating) / (decimal)teamRoom.TotalReviews;
				}
			}

			// تحديث rating الـ Company
			if (project.CompanyId.HasValue)
			{
				var company = await _context.RegisteredCompanies.FindAsync(project.CompanyId.Value);
				if (company != null)
				{
					var existingRatings = await _context.Reviews
						.Where(r => r.CompanyId == company.CompanyId)
						.Select(r => r.Rating).ToListAsync();
					company.TotalReviews = existingRatings.Count + 1;
					company.AverageRating = (existingRatings.Sum() + rating) / (decimal)company.TotalReviews;
				}
			}

			await _context.SaveChangesAsync();
			await LogAudit(currentUser.Id, "ReviewSubmitted",
				$"Review submitted with rating: {rating}", "Review", review.ReviewId);

			return Json(new { success = true, message = "Review submitted successfully. Thank you!" });
		}

		// ==================== MY REQUESTS (Client) ====================

		[Authorize(Roles = "Client")]
		public async Task<IActionResult> MyRequests(string status = "all")
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

			var query = _context.ProjectRequests
				.Include(r => r.TeamRoom).ThenInclude(t => t.TeamLeader).ThenInclude(tl => tl.User)
				.Include(r => r.Company)
				.Include(r => r.SelectedService)
				.Include(r => r.ProposedMilestones)
				.Where(r => r.ClientId == client.ClientId);

			if (status != "all")
				query = query.Where(r => r.Status.ToLower() == status.ToLower());

			var requests = await query
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();

			ViewBag.CurrentStatus = status;
			return View(requests);
		}

		// ==================== INCOMING REQUESTS (TeamLeader) ====================

		[Authorize(Roles = "TeamLeader")]
		public async Task<IActionResult> IncomingRequests()
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			var requests = await _context.ProjectRequests
				.Include(r => r.Client).ThenInclude(c => c.User)
				.Include(r => r.ProposedMilestones)
				.Where(r => r.TeamRoom.TeamLeaderId == teamLeader.TeamLeaderId
						 && r.Status == "Pending")
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();

			return View(requests);
		}

		// ==================== SEND PROPOSAL (TeamLeader) ====================

		[HttpPost]
		[Authorize(Roles = "TeamLeader")]
		public async Task<IActionResult> SendProposal(int requestId, decimal proposedBudget,
			string proposalNote, DateTime estimatedEndDate,
			List<ProposedMilestoneInput> milestones)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var teamLeader = await _context.TeamLeaders
				.FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

			var request = await _context.ProjectRequests
				.Include(r => r.Client).ThenInclude(c => c.User)
				.Include(r => r.TeamRoom)
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.TeamRoom.TeamLeaderId == teamLeader.TeamLeaderId
					&& r.Status == "Pending");

			if (request == null)
				return Json(new { success = false, message = "Request not found" });

			if (milestones == null || !milestones.Any())
				return Json(new { success = false, message = "At least one milestone is required" });

			var milestonesTotal = milestones.Sum(m => m.Amount);
			if (Math.Abs(milestonesTotal - proposedBudget) > 0.01m)
				return Json(new
				{
					success = false,
					message = $"Milestones total (${milestonesTotal:N0}) must equal proposed budget (${proposedBudget:N0})"
				});

			var oldMilestones = await _context.ProposedMilestones
				.Where(m => m.RequestId == requestId).ToListAsync();
			_context.ProposedMilestones.RemoveRange(oldMilestones);

			int order = 1;
			foreach (var ms in milestones)
			{
				_context.ProposedMilestones.Add(new ProposedMilestone
				{
					RequestId = requestId,
					Title = ms.Title,
					Description = ms.Description,
					Amount = ms.Amount,
					DaysFromStart = ms.DaysFromStart,
					DisplayOrder = order++
				});
			}

			request.ProposedBudget = proposedBudget;
			request.ProposalNote = proposalNote;
			request.EstimatedEndDate = estimatedEndDate;
			request.Status = "ProposalSent";
			request.RespondedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();

			await CreateNotification(
				request.Client.UserId,
				"ProposalReceived",
				"Proposal Received! 🎉",
				$"Team '{request.TeamRoom.TeamName}' sent a proposal for '{request.ProjectName}'",
				request.RequestId, "ProjectRequest",
				$"/TeamRooms/Projects/RequestDetails/{request.RequestId}");

			await LogAudit(currentUser.Id, "ProposalSent",
				$"Proposal sent for request: {request.ProjectName}", "ProjectRequest", requestId);

			return Json(new { success = true, message = "Proposal sent successfully!" });
		}

		// ==================== ACCEPT PROPOSAL (Client - TeamRoom) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> AcceptProposal(int requestId)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

			var request = await _context.ProjectRequests
				.Include(r => r.TeamRoom).ThenInclude(t => t.TeamLeader).ThenInclude(tl => tl.User)
				.Include(r => r.ProposedMilestones)
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.ClientId == client.ClientId
					&& r.TeamRoomId != null
					&& r.Status == "ProposalSent");

			if (request == null)
				return Json(new { success = false, message = "Proposal not found" });

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				var project = new Project
				{
					ClientId = client.ClientId,
					TeamRoomId = request.TeamRoomId,  // int? → int? ✅
					CompanyId = null,
					ProjectName = request.ProjectName,
					Description = request.Description,
					Category = request.Category,
					StartDate = DateTime.UtcNow,
					EstimatedEndDate = request.EstimatedEndDate,
					Budget = request.ProposedBudget ?? 0,
					Status = "Active",
					Priority = "Medium",
					ProgressPercentage = 0,
					CreatedAt = DateTime.UtcNow
				};

				_context.Projects.Add(project);
				await _context.SaveChangesAsync();

				foreach (var pm in request.ProposedMilestones.OrderBy(m => m.DisplayOrder))
				{
					_context.Milestones.Add(new Milestone
					{
						ProjectId = project.ProjectId,
						Title = pm.Title,
						Description = pm.Description,
						DisplayOrder = pm.DisplayOrder,
						Amount = pm.Amount,
						DueDate = DateTime.UtcNow.AddDays(pm.DaysFromStart),
						Status = "Pending",
						RejectionReason = string.Empty
					});
				}

				var conversation = new Conversation
				{
					ProjectId = project.ProjectId,
					ConversationType = "Project",
					Title = $"{project.ProjectName} - Workspace",
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				_context.Conversations.Add(conversation);
				await _context.SaveChangesAsync();

				_context.ConversationParticipants.AddRange(
					new ConversationParticipant
					{
						ConversationId = conversation.ConversationId,
						UserId = currentUser.Id,
						Role = "Admin",
						JoinedAt = DateTime.UtcNow
					},
					new ConversationParticipant
					{
						ConversationId = conversation.ConversationId,
						UserId = request.TeamRoom.TeamLeader.UserId,
						Role = "Member",
						JoinedAt = DateTime.UtcNow
					}
				);

				request.Status = "Accepted";
				request.ProjectId = project.ProjectId;
				request.AcceptedAt = DateTime.UtcNow;

				await _context.SaveChangesAsync();

				await CreateNotification(
					request.TeamRoom.TeamLeader.UserId,
					"ProposalAccepted", "Proposal Accepted! 🚀",
					$"Client accepted your proposal for '{project.ProjectName}'. Project is now active!",
					project.ProjectId, "Project",
					$"/TeamRooms/Projects/Details/{project.ProjectId}");

				await LogAudit(currentUser.Id, "ProposalAccepted",
					$"Client accepted proposal, project created: {project.ProjectName}",
					"Project", project.ProjectId);

				await transaction.CommitAsync();

				return Json(new
				{
					success = true,
					message = "Proposal accepted! Project is now active.",
					projectId = project.ProjectId
				});
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error accepting proposal {RequestId}", requestId);
				return Json(new { success = false, message = "An error occurred. Please try again." });
			}
		}

		// ==================== REJECT PROPOSAL (Client) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> RejectProposal(int requestId, string reason)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

			var request = await _context.ProjectRequests
				.Include(r => r.TeamRoom).ThenInclude(t => t.TeamLeader)
				.Include(r => r.Company).ThenInclude(c => c.Managers)
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.ClientId == client.ClientId
					&& r.Status == "ProposalSent");

			if (request == null)
				return Json(new { success = false, message = "Proposal not found" });

			request.Status = "Rejected";
			request.RejectionReason = reason;
			await _context.SaveChangesAsync();

			// إشعار للـ TeamLeader أو Manager
			if (request.TeamRoom?.TeamLeader != null)
			{
				await CreateNotification(
					request.TeamRoom.TeamLeader.UserId,
					"ProposalRejected", "Proposal Rejected",
					$"Client rejected your proposal for '{request.ProjectName}'" +
					(string.IsNullOrEmpty(reason) ? "." : $": {reason}"),
					request.RequestId, "ProjectRequest",
					$"/TeamRooms/Projects/IncomingRequests");
			}
			else if (request.Company?.Managers != null)
			{
				var primaryManager = request.Company.Managers
					.FirstOrDefault(m => m.IsPrimaryContact && m.IsActive)
					?? request.Company.Managers.FirstOrDefault(m => m.IsActive);

				if (primaryManager != null)
					await CreateNotification(
						primaryManager.UserId,
						"ProposalRejected", "Proposal Rejected",
						$"Client rejected your proposal for '{request.ProjectName}'" +
						(string.IsNullOrEmpty(reason) ? "." : $": {reason}"),
						request.RequestId, "ProjectRequest",
						$"/TeamRooms/Projects/CompanyIncomingRequests");
			}

			return Json(new { success = true, message = "Proposal rejected." });
		}

		// ==================== COMPANY INCOMING REQUESTS (CompanyManager) ====================

		[Authorize(Roles = "CompanyManager")]
		public async Task<IActionResult> CompanyIncomingRequests()
		{
			var currentUser = await _userManager.GetUserAsync(User);
			if (currentUser == null) return Unauthorized();

			var manager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);
			if (manager == null)
			{
				TempData["ErrorMessage"] = "Company Manager profile not found.";
				return RedirectToAction("Index", "Home");
			}

			var requests = await _context.ProjectRequests
				.Include(r => r.Client).ThenInclude(c => c.User)
				.Include(r => r.Company)
				.Include(r => r.SelectedService)
				.Include(r => r.ProposedMilestones)
				.Where(r => r.CompanyId == manager.CompanyId && r.Status == "Pending")
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();

			ViewBag.CompanyName = (await _context.RegisteredCompanies
				.FindAsync(manager.CompanyId))?.CompanyName;

			return View(requests);
		}

		// ==================== COMPANY SEND PROPOSAL (CompanyManager) ====================

		[HttpPost]
		[Authorize(Roles = "CompanyManager")]
		public async Task<IActionResult> CompanySendProposal(int requestId, decimal proposedBudget,
			string proposalNote, DateTime estimatedEndDate,
			List<ProposedMilestoneInput> milestones)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var manager = await _context.CompanyManagers
				.Include(m => m.Company)
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			if (manager == null)
				return Json(new { success = false, message = "Unauthorized" });

			var request = await _context.ProjectRequests
				.Include(r => r.Client).ThenInclude(c => c.User)
				.Include(r => r.Company)
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.CompanyId == manager.CompanyId
					&& r.Status == "Pending");

			if (request == null)
				return Json(new { success = false, message = "Request not found" });

			if (milestones == null || !milestones.Any())
				return Json(new { success = false, message = "At least one milestone is required" });

			var milestonesTotal = milestones.Sum(m => m.Amount);
			if (Math.Abs(milestonesTotal - proposedBudget) > 0.01m)
				return Json(new
				{
					success = false,
					message = $"Milestones total (${milestonesTotal:N0}) must equal proposed budget (${proposedBudget:N0})"
				});

			var old = await _context.ProposedMilestones
				.Where(m => m.RequestId == requestId).ToListAsync();
			_context.ProposedMilestones.RemoveRange(old);

			int order = 1;
			foreach (var ms in milestones)
			{
				_context.ProposedMilestones.Add(new ProposedMilestone
				{
					RequestId = requestId,
					Title = ms.Title,
					Description = ms.Description,
					Amount = ms.Amount,
					DaysFromStart = ms.DaysFromStart,
					DisplayOrder = order++
				});
			}

			request.ProposedBudget = proposedBudget;
			request.ProposalNote = proposalNote;
			request.EstimatedEndDate = estimatedEndDate;
			request.Status = "ProposalSent";
			request.RespondedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();

			await CreateNotification(
				request.Client.UserId,
				"CompanyProposalReceived",
				$"Proposal from {manager.Company.CompanyName} 🏢",
				$"{manager.Company.CompanyName} sent a proposal for '{request.ProjectName}'",
				request.RequestId, "ProjectRequest",
				$"/TeamRooms/Projects/RequestDetails/{request.RequestId}");

			await LogAudit(currentUser.Id, "CompanyProposalSent",
				$"Company proposal sent for: {request.ProjectName}", "ProjectRequest", requestId);

			return Json(new { success = true, message = "Proposal sent successfully!" });
		}

		// ==================== ACCEPT COMPANY PROPOSAL (Client) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> AcceptCompanyProposal(int requestId)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
			if (client == null)
				return Json(new { success = false, message = "Unauthorized" });

			var request = await _context.ProjectRequests
				.Include(r => r.Company).ThenInclude(c => c.Managers).ThenInclude(m => m.User)
				.Include(r => r.ProposedMilestones)
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.ClientId == client.ClientId
					&& r.CompanyId != null
					&& r.Status == "ProposalSent");

			if (request == null)
				return Json(new { success = false, message = "Proposal not found" });

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				var project = new Project
				{
					ClientId = client.ClientId,
					CompanyId = request.CompanyId,
					TeamRoomId = null,
					ProjectName = request.ProjectName,
					Description = request.Description,
					Category = request.Category,
					StartDate = DateTime.UtcNow,
					EstimatedEndDate = request.EstimatedEndDate,
					Budget = request.ProposedBudget ?? 0,
					Status = "Active",
					Priority = "Medium",
					ProgressPercentage = 0,
					CreatedAt = DateTime.UtcNow
				};

				_context.Projects.Add(project);
				await _context.SaveChangesAsync();

				foreach (var pm in request.ProposedMilestones.OrderBy(m => m.DisplayOrder))
				{
					_context.Milestones.Add(new Milestone
					{
						ProjectId = project.ProjectId,
						Title = pm.Title,
						Description = pm.Description,
						DisplayOrder = pm.DisplayOrder,
						Amount = pm.Amount,
						DueDate = DateTime.UtcNow.AddDays(pm.DaysFromStart),
						Status = "Pending",
						RejectionReason = string.Empty
					});
				}

				var primaryManager = request.Company.Managers
					.FirstOrDefault(m => m.IsPrimaryContact && m.IsActive)
					?? request.Company.Managers.FirstOrDefault(m => m.IsActive);

				var conversation = new Conversation
				{
					ProjectId = project.ProjectId,
					ConversationType = "Project",
					Title = $"{project.ProjectName} - Workspace",
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				_context.Conversations.Add(conversation);
				await _context.SaveChangesAsync();

				_context.ConversationParticipants.Add(new ConversationParticipant
				{
					ConversationId = conversation.ConversationId,
					UserId = currentUser.Id,
					Role = "Admin",
					JoinedAt = DateTime.UtcNow
				});

				if (primaryManager != null)
					_context.ConversationParticipants.Add(new ConversationParticipant
					{
						ConversationId = conversation.ConversationId,
						UserId = primaryManager.UserId,
						Role = "Member",
						JoinedAt = DateTime.UtcNow
					});

				request.Status = "Accepted";
				request.ProjectId = project.ProjectId;
				request.AcceptedAt = DateTime.UtcNow;

				await _context.SaveChangesAsync();

				if (primaryManager != null)
					await CreateNotification(
						primaryManager.UserId,
						"CompanyProposalAccepted", "Proposal Accepted! 🚀",
						$"Client accepted your proposal for '{project.ProjectName}'. Project is now active!",
						project.ProjectId, "Project",
						$"/TeamRooms/Projects/Details/{project.ProjectId}");

				await LogAudit(currentUser.Id, "CompanyProposalAccepted",
					$"Client accepted company proposal, project created: {project.ProjectName}",
					"Project", project.ProjectId);

				await transaction.CommitAsync();

				return Json(new
				{
					success = true,
					message = "Proposal accepted! Project is now active.",
					projectId = project.ProjectId
				});
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error accepting company proposal {RequestId}", requestId);
				return Json(new { success = false, message = "An error occurred. Please try again." });
			}
		}

		// ==================== DECLINE COMPANY REQUEST (CompanyManager) ====================

		[HttpPost]
		[Authorize(Roles = "CompanyManager")]
		public async Task<IActionResult> DeclineCompanyRequest(int requestId, string reason = null)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var manager = await _context.CompanyManagers
				.FirstOrDefaultAsync(m => m.UserId == currentUser.Id && m.IsActive);

			if (manager == null)
				return Json(new { success = false, message = "Unauthorized" });

			var request = await _context.ProjectRequests
				.Include(r => r.Client).ThenInclude(c => c.User)
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.CompanyId == manager.CompanyId
					&& r.Status == "Pending");

			if (request == null)
				return Json(new { success = false, message = "Request not found" });

			request.Status = "Rejected";
			request.RejectionReason = reason;
			await _context.SaveChangesAsync();

			await CreateNotification(
				request.Client.UserId,
				"CompanyRequestDeclined", "Request Declined",
				$"Your request to the company was declined" +
				(string.IsNullOrEmpty(reason) ? "." : $": {reason}"),
				request.RequestId, "ProjectRequest",
				$"/TeamRooms/Projects/MyRequests");

			return Json(new { success = true, message = "Request declined." });
		}

		// ==================== CANCEL REQUEST (Client) ====================

		[HttpPost]
		[Authorize(Roles = "Client")]
		public async Task<IActionResult> CancelRequest(int requestId)
		{
			var currentUser = await _userManager.GetUserAsync(User);
			var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

			var request = await _context.ProjectRequests
				.FirstOrDefaultAsync(r => r.RequestId == requestId
					&& r.ClientId == client.ClientId
					&& r.Status == "Pending");

			if (request == null)
				return Json(new { success = false, message = "Request not found or cannot be cancelled" });

			request.Status = "Cancelled";
			await _context.SaveChangesAsync();

			return Json(new { success = true, message = "Request cancelled." });
		}

		// ==================== HELPERS ====================

		private async Task LogAudit(int userId, string action, string details,
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
			await _context.SaveChangesAsync();
		}

		private async Task CreateNotification(int userId, string type, string title, string message,
			int? relatedEntityId = null, string relatedEntityType = null, string actionUrl = null)
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
			await _context.SaveChangesAsync();
		}
        // ==================== TEAM LEADER - VIEW ALL PROJECTS ====================

        /// <summary>
        /// View all projects for team leader's teams (with status filter)
        /// </summary>
        [Authorize(Roles = "TeamLeader")]
        public async Task<IActionResult> TeamProjects(string status = "all")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Get team leader profile
            var teamLeader = await _context.TeamLeaders
                .FirstOrDefaultAsync(tl => tl.UserId == currentUser.Id);

            if (teamLeader == null)
            {
                TempData["ErrorMessage"] = "Team Leader profile not found.";
                return RedirectToAction("Index", "Home");
            }

            // Get all team rooms for this leader
            var teamRoomIds = await _context.TeamRooms
                .Where(tr => tr.TeamLeaderId == teamLeader.TeamLeaderId)
                .Select(tr => tr.TeamRoomId)
                .ToListAsync();

            // Query projects for these team rooms
            var query = _context.Projects
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.TeamRoom)
                .Include(p => p.Milestones)
                .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value))
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
                All = await _context.Projects
                    .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value))
                    .CountAsync(),
                Active = await _context.Projects
                    .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value) && p.Status == "Active")
                    .CountAsync(),
                InProgress = await _context.Projects
                    .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value) && p.Status == "InProgress")
                    .CountAsync(),
                Completed = await _context.Projects
                    .Where(p => p.TeamRoomId.HasValue && teamRoomIds.Contains(p.TeamRoomId.Value) && p.Status == "Completed")
                    .CountAsync()
            };

            return View(projects);
        }
    }
}