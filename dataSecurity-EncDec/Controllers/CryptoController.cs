using SecurePortal.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace SecurePortal.Controllers
{
    public class CryptoController : Controller
    {
        private readonly AppDbContext _db;

        public CryptoController(AppDbContext db)
        {
            _db = db;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserEmail") != null;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            return View(new CryptoViewModel());
        }

        // -----------------------------------------------
        // تشفير النص بـ TripleDES
        // -----------------------------------------------
        [HttpPost]
        public IActionResult Encrypt(CryptoViewModel model)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.PlainText))
            {
                TempData["Error"] = "Please enter text to encrypt.";
                return RedirectToAction("Index");
            }

            using (TripleDES tripleDES = TripleDES.Create())
            {
                // الخطوة 1: أنشئ مفتاح وIV عشوائيين
                tripleDES.GenerateKey();
                tripleDES.GenerateIV();

                // الخطوة 2: شفّر النص
                ICryptoTransform encryptor = tripleDES.CreateEncryptor();
                byte[] plainBytes = Encoding.UTF8.GetBytes(model.PlainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                // الخطوة 3: حوّل الناتج لـ Base64
                model.EncryptedText = Convert.ToBase64String(encryptedBytes);

                // الخطوة 4: احفظ المفتاح والـ IV مع بعض مفصولين بـ ":"
                string keyBase64 = Convert.ToBase64String(tripleDES.Key);
                string ivBase64 = Convert.ToBase64String(tripleDES.IV);
                model.SecretKey = keyBase64 + ":" + ivBase64;

                // الخطوة 5: احفظ في قاعدة البيانات
                EncryptedRecord record = new EncryptedRecord
                {
                    PlainText = model.PlainText,
                    EncryptedText = model.EncryptedText,
                    SecretKey = model.SecretKey,
                    CreatedAt = DateTime.Now
                };
                _db.EncryptedRecords.Add(record);
                _db.SaveChanges();

                TempData["Success"] = "Text encrypted and saved to database!";
            }

            return View("Index", model);
        }

        // -----------------------------------------------
        // فك تشفير النص
        // -----------------------------------------------
        [HttpPost]
        public IActionResult Decrypt(CryptoViewModel model)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.EncryptedText) || string.IsNullOrWhiteSpace(model.SecretKey))
            {
                TempData["Error"] = "Please provide both the encrypted text and the key.";
                return RedirectToAction("Index");
            }

            try
            {
                // افصل المفتاح والـ IV
                string[] parts = model.SecretKey.Split(':');
                byte[] key = Convert.FromBase64String(parts[0]);
                byte[] iv = Convert.FromBase64String(parts[1]);
                byte[] encryptedBytes = Convert.FromBase64String(model.EncryptedText);

                using (TripleDES tripleDES = TripleDES.Create())
                {
                    tripleDES.Key = key;
                    tripleDES.IV = iv;

                    ICryptoTransform decryptor = tripleDES.CreateDecryptor();
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    model.DecryptedResult = Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch
            {
                TempData["Error"] = "Decryption failed. Wrong key or text.";
            }

            return View("Index", model);
        }
    }
}