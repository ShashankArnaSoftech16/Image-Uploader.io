using System.Diagnostics;
using Dropbox.Api;
using imageStore.Models;
using imageStore.Service.IService;
using Microsoft.AspNetCore.Mvc;

namespace imageStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUploadImageService _uploadImageService;
        private readonly IConfiguration _config;

        public HomeController(ILogger<HomeController> logger, IUploadImageService uploadImageService, IConfiguration config)
        {
            _logger = logger;
            _uploadImageService = uploadImageService;
            _config = config;
        }

        public IActionResult Authenticate()
        {
            var appKey = _config["Dropbox:AppKey"];
            var redirectUri = _config["Dropbox:RedirectUri"];

            if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(redirectUri))
            {
                throw new InvalidOperationException("Dropbox AppKey or RedirectUri is not configured.");
            }

            var authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri(
                OAuthResponseType.Code,
                appKey,
                redirectUri);

            return Redirect(authorizeUri.ToString());
        }

        public async Task<IActionResult> Callback([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return BadRequest("Authorization code is missing.");
            }

            try
            {
                var appKey = _config["Dropbox:AppKey"];
                var appSecret = _config["Dropbox:AppSecret"];
                var redirectUri = _config["Dropbox:RedirectUri"];

                var response = await DropboxOAuth2Helper.ProcessCodeFlowAsync(
                    code, appKey, appSecret, redirectUri);

                // Save the access token in the session
                HttpContext.Session.SetString("DropboxAccessToken", response.AccessToken);

                return RedirectToAction("Upload");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing authorization code");
                return StatusCode(500, "Error processing authorization code.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile imageUpload, string cloudStorage)
        {
            if (imageUpload == null || imageUpload.Length == 0)
            {
                return Json(new { success = false, message = "No file selected." });
            }

            string imageUrl = null;

            // Process image upload based on the selected cloud storage option
            switch (cloudStorage)
            {
                case "Dropbox":
                    // Handle Dropbox upload
                    var accessToken = HttpContext.Session.GetString("DropboxAccessToken");
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        return Json(new { success = false, message = "User not authenticated with Dropbox." });
                    }
                    imageUrl = await _uploadImageService.UploadToDropbox(imageUpload, accessToken);
                    break;

                case "GoogleDrive":
                    // Handle Google Drive upload
                    imageUrl = await _uploadImageService.UploadToGoogleDrive(imageUpload);
                    break;

                case "OneDrive":
                    // Handle OneDrive upload
                    imageUrl = await _uploadImageService.UploadToOneDrive(imageUpload);
                    break;

                case "SharePoint":
                    // Handle SharePoint upload
                    imageUrl = await _uploadImageService.UploadToSharePoint(imageUpload);
                    var sharePointResult = await _uploadImageService.UploadToSharePoint(imageUpload);
                    break;

                default:
                    return Json(new { success = false, message = "Invalid cloud storage option." });
            }

            if (string.IsNullOrEmpty(imageUrl))

            {
                return Json(new { success = false, message = "image uploading failed." });
            }


            // If the image was successfully uploaded to the chosen service
            return Json(new { success = true, imageUrl });
        }
        public IActionResult Upload()
        {
            return View("Index");
        }

        public IActionResult Index()
        {
            var appKey = _config["Dropbox:AppKey"];
            var redirectUri = _config["Dropbox:RedirectUri"];

            if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(redirectUri))
            {
                throw new InvalidOperationException("Dropbox AppKey or RedirectUri is not configured.");
            }

            var authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri(
                OAuthResponseType.Code,
                appKey,
                redirectUri);

            return Redirect(authorizeUri.ToString());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}


