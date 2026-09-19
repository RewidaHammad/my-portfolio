using SecurePortal.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace SecurePortal.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db)
        {
            _db = db;
        }

        private string HashPassword(string pwd)   //iam working on this method to hash the password using MD5 
        {
            byte[] codedPwd = new MD5CryptoServiceProvider()
                                  .ComputeHash(Encoding.UTF8.GetBytes(pwd));
            StringBuilder str = new StringBuilder();
            foreach (byte b in codedPwd)
            {
                str.Append(b.ToString("x2"));
            }
            return str.ToString();
        }

        // -----------------------------------------------
        // REGISTER
        // -----------------------------------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            bool emailExists = _db.Users.Any(u => u.Email == model.Email);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(model);
            }

            string hashedPassword = HashPassword(model.Password);

            User newUser = new User
            {
                Name = model.Name,
                Email = model.Email,
                PasswordHash = hashedPassword,
                FailedAttempts = 0,
                LockoutEnd = null
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();

            TempData["Success"] = "Account created! Please login.";
            return RedirectToAction("Login");
        }

        // -----------------------------------------------
        // LOGIN
        // -----------------------------------------------
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            User? user = _db.Users.FirstOrDefault(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            // see law hwa el account locked aw la2
            if (user.LockoutEnd != null && user.LockoutEnd > DateTime.Now)
            {
                double secondsLeft = (user.LockoutEnd.Value - DateTime.Now).TotalSeconds;
                ModelState.AddModelError("", $"Account locked. Try again in {(int)secondsLeft} seconds.");
                return View(model);
            }

            string enteredHash = HashPassword(model.Password);

            if (enteredHash == user.PasswordHash)
            {
                
                user.FailedAttempts = 0;
                user.LockoutEnd = null;
                _db.SaveChanges();

                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetString("UserName", user.Name);

                return RedirectToAction("Index", "Home");
            }
            else
            {
                
                user.FailedAttempts += 1;

                if (user.FailedAttempts >= 3)
                {
                    user.LockoutEnd = DateTime.Now.AddMinutes(1);
                    ModelState.AddModelError("", "Too many failed attempts. Account locked for 1 minute.");
                }
                else
                {
                    int attemptsLeft = 3 - user.FailedAttempts;
                    ModelState.AddModelError("", $"Wrong password. {attemptsLeft} attempt(s) left before lockout.");
                }

                _db.SaveChanges();
                return View(model);
            }
        }

        // -----------------------------------------------
        // LOGOUT
        // -----------------------------------------------
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // -----------------------------------------------
        // CHANGE PASSWORD
        // -----------------------------------------------
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (HttpContext.Session.GetString("UserEmail") == null)
                return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            string? email = HttpContext.Session.GetString("UserEmail");
            if (email == null)
                return RedirectToAction("Login");

            if (!ModelState.IsValid)
                return View(model);

            User? user = _db.Users.FirstOrDefault(u => u.Email == email);

            string currentHash = HashPassword(model.CurrentPassword);
            if (currentHash != user.PasswordHash)
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            _db.SaveChanges();

            TempData["Success"] = "Password changed successfully!";
            return RedirectToAction("Index", "Home");
        }

        // -----------------------------------------------
        // RESET PASSWORD + CAPTCHA (saves to DB)
        // -----------------------------------------------
        // -----------------------------------------------
        // RESET PASSWORD + CAPTCHA
        [HttpGet]
        public IActionResult ResetPassword()
        {
            string captchaCode = GenerateCaptcha();
            HttpContext.Session.SetString("CaptchaCode", captchaCode);
            ViewBag.CaptchaCode = captchaCode;
            return View(new ResetPasswordViewModel());
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
           
            string? correctCode = HttpContext.Session.GetString("CaptchaCode");

           
            if (!string.IsNullOrEmpty(model.Email))
            {
                var captchaLog = new CaptchaToken
                {
                    Email = model.Email,
                    Code = correctCode ?? "",
                    ExpiresAt = DateTime.Now.AddMinutes(5)
                };
                _db.CaptchaTokens.Add(captchaLog);
                _db.SaveChanges();
            }

         
            if (model.CaptchaInput?.Trim().ToUpper() != correctCode?.Trim().ToUpper())
            {
                ModelState.AddModelError("CaptchaInput", "Wrong CAPTCHA. Try again.");

                string newCode = GenerateCaptcha();
                HttpContext.Session.SetString("CaptchaCode", newCode);
                ViewBag.CaptchaCode = newCode;
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CaptchaCode = correctCode;
                return View(model);
            }

            User? user = _db.Users.FirstOrDefault(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError("Email", "No account found with this email.");
                ViewBag.CaptchaCode = correctCode;
                return View(model);
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            user.FailedAttempts = 0;
            user.LockoutEnd = null;
            _db.SaveChanges();

          
            HttpContext.Session.Remove("CaptchaCode");

            TempData["Success"] = "Password reset successfully! Please login.";
            return RedirectToAction("Login");
        }
        private string GenerateCaptcha()
        {
            string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Random random = new Random();
            string captcha = "";
            for (int i = 0; i < 6; i++)
            {
                captcha += chars[random.Next(chars.Length)];
            }
            return captcha;
        }

        //public IActionResult TestCaptcha()
        //{
        //    CaptchaToken testToken = new CaptchaToken
        //    {
        //        Email = "test@test.com",
        //        Code = "TEST123",
        //        ExpiresAt = DateTime.Now.AddMinutes(5)
        //    };

        //    _db.CaptchaTokens.Add(testToken);
        //    int rowsSaved = _db.SaveChanges();

        //    return Content($"Rows saved: {rowsSaved} | Token ID: {testToken.Id}");
        //}
    }
}