using Dropbox.Api.Files;
using Dropbox.Api;
using imageStore.Service.IService;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace imageStore.Service
{
    public class UploadImageService : IUploadImageService
    {
        private readonly string _accessToken;
        private readonly string _clientIdOneDrive;
        private readonly string _clientSecretOneDrive;
        private readonly string _tenantIdOneDrive;
        private readonly string _userIdOneDrive;
        private readonly string _clientIdSharePoint;
        private readonly string _clientSecretSharePoint;
        private readonly string _tenantIdSharePoint;
        private readonly string _siteIdSharePoint;
        private readonly string _folderId;
        private static readonly string ApplicationName = "Image Uploader";

        public UploadImageService(IConfiguration configuration)
        {
            _accessToken = configuration["Dropbox:AccessToken"];
            _clientIdOneDrive = configuration["MicrosoftAzureOneDrive:ClientId"];
            _clientSecretOneDrive = configuration["MicrosoftAzureOneDrive:ClientSecret"];
            _tenantIdOneDrive = configuration["MicrosoftAzureOneDrive:TenantId"];
            _userIdOneDrive = configuration["MicrosoftAzureOneDrive:UserId"];
            _folderId = configuration["FolderId"];
            _clientIdSharePoint = configuration["MicrosoftAzureSharePoint:ClientId"];
            _clientSecretSharePoint = configuration["MicrosoftAzureSharePoint:ClientSecret"];
            _tenantIdSharePoint = configuration["MicrosoftAzureSharePoint:TenantId"];
            _siteIdSharePoint = configuration["MicrosoftAzureSharePoint:UserId"];
        }

        public async Task<string> UploadToDropbox(IFormFile imageUpload, string accessToken)
        {
            if (imageUpload == null || imageUpload.Length == 0)
            {
                return null;
            }

            try
            {
                if (string.IsNullOrEmpty(_accessToken))
                {
                    throw new InvalidOperationException("Failed to get access token.");
                }

                // Initialize DropboxClient with the access token
                using (var dbx = new DropboxClient(accessToken))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await imageUpload.CopyToAsync(memoryStream);

                        memoryStream.Position = 0;

                        var uploadPath = $"/{imageUpload.FileName}";

                        // Upload the file to Dropbox
                        var fileMetadata = await dbx.Files.UploadAsync(
                            uploadPath, WriteMode.Overwrite.Instance, body: memoryStream);

                        if (fileMetadata != null)
                        {
                            // Create a shared link for the uploaded file
                            var sharedLinkMetadata = await dbx.Sharing.CreateSharedLinkWithSettingsAsync(uploadPath);
                            return sharedLinkMetadata.Url;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file to Dropbox: {ex.Message}");
                return null;
            }
        }


        public async Task<string> UploadToGoogleDrive(IFormFile imageUpload)
        {
            if (imageUpload == null || imageUpload.Length == 0)
            {
                throw new Exception("No image selected.");
            }

            string serviceAccountKeyFilePath = "D:\\imageStore\\imageStore\\image-uploader-443816-eb748645b94d.json";
            string[] Scopes = { DriveService.Scope.Drive };

            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountKeyFilePath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(Scopes);
            }

            var _driveService = new DriveService(new DriveService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            using (var memoryStream = new MemoryStream())
            {
                await imageUpload.CopyToAsync(memoryStream);
                string folderId = _folderId;
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = imageUpload.FileName,
                    Parents = new List<string> { folderId }
                };

                var request = _driveService.Files.Create(fileMetadata, memoryStream, imageUpload.ContentType);
                request.Fields = "id"; 

                var uploadResult = await request.UploadAsync();

                if (uploadResult.Status == Google.Apis.Upload.UploadStatus.Failed)
                {
                    throw new Exception($"Failed to upload file: {uploadResult.Exception.Message}");
                }

                var uploadedFile = request.ResponseBody; 

                return $"https://drive.google.com/file/d/{uploadedFile.Id}/view?usp=sharing";
            }
        }



        public async Task<string> UploadToOneDrive(IFormFile imageUpload)
        {
            if (imageUpload == null || imageUpload.Length == 0)
            {
                return null;
            }

            try
            {
                var cca = ConfidentialClientApplicationBuilder.Create(_clientIdOneDrive)
                    .WithClientSecret(_clientSecretOneDrive)
                    .WithAuthority(new Uri($"https://login.microsoftonline.com/{_tenantIdOneDrive}"))
                    .Build();

                var result = await cca.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                    .ExecuteAsync();

                string accessToken = result.AccessToken;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    using (var memoryStream = new MemoryStream())
                    {
                        await imageUpload.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        var uploadUrl = "https://graph.microsoft.com/v1.0/users/"+_userIdOneDrive+"/drive/root:/images/" + imageUpload.FileName + ":/content";

                        var content = new StreamContent(memoryStream);
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                        var response = await client.PutAsync(uploadUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync();
                            return $"File uploaded successfully: {responseBody}";
                        }
                        else
                        {
                            Console.WriteLine($"Error uploading file: {response.StatusCode}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file to OneDrive: {ex.Message}");
                return null;
            }
        }

        public async Task<string> UploadToSharePoint(IFormFile imageUpload)
        {
            if (imageUpload == null || imageUpload.Length == 0)
            {
                return null;
            }

            try
            {
                var cca = ConfidentialClientApplicationBuilder.Create(_clientIdSharePoint)
                    .WithClientSecret(_clientSecretSharePoint)
                    .WithAuthority(new Uri($"https://login.microsoftonline.com/{_tenantIdSharePoint}"))
                    .Build();

                var result = await cca.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                    .ExecuteAsync();

                string accessToken = result.AccessToken;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    using (var memoryStream = new MemoryStream())
                    {
                        await imageUpload.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        var siteId = _siteIdSharePoint; 
                        var libraryName = "Documents";

                        var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{libraryName}/root:/{imageUpload.FileName}:/content";

                        var content = new StreamContent(memoryStream);
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                        var response = await client.PutAsync(uploadUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync();
                            return $"File uploaded successfully: {responseBody}";
                        }
                        else
                        {
                            Console.WriteLine($"Error uploading file: {response.StatusCode}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file to SharePoint: {ex.Message}");
                return null;
            }
        }

    }
}
