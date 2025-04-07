using Amazon;
using Amazon.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using System.Drawing;

namespace BeautySky.Services
{
    public class S3Service
    {
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;


        public S3Service(IConfiguration configuration)
        {
            _bucketName = configuration["AWS:BucketName"];
            _s3Client = new AmazonS3Client
                (
                    configuration["AWS:AccessKey"],
                    configuration["AWS:SecretKey"],
                    RegionEndpoint.GetBySystemName(configuration["AWS:Region"])
                );
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = fileStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead
            };

            await _s3Client.PutObjectAsync(request);

            return $"https://{_bucketName}.s3.amazonaws.com/{fileName}";
        }
    }
}
