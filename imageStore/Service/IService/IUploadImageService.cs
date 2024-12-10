using Azure.Core;

namespace imageStore.Service.IService
{
    public interface IUploadImageService
    {
        Task<string> UploadToDropbox(IFormFile imageUpload, string accessToken);
        Task<string> UploadToGoogleDrive(IFormFile imageUpload);
        Task<string> UploadToOneDrive(IFormFile imageUpload);
        Task<string> UploadToSharePoint(IFormFile imageUpload);
    }
}
