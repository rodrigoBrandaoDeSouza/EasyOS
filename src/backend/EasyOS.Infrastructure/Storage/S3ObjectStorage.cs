using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace EasyOS.Infrastructure.Storage;

/// <summary>Storage S3-compatível (Cloudflare R2, Backblaze B2, MinIO...).</summary>
public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3ObjectStorage(StorageOptions options)
    {
        _bucket = options.Bucket ?? throw new InvalidOperationException("Storage:Bucket não configurado.");
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = options.Region,
            // R2/B2 não suportam os checksums que o SDK v4 envia por padrão.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = StorageKeys.SanitizeRelativePath(key),
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true,
        }, ct);
    }

    public async Task<ObjectContent?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, StorageKeys.SanitizeRelativePath(key), ct);
            return new ObjectContent(response.ResponseStream, response.Headers.ContentType ?? StorageKeys.ContentTypeFor(key), response.ContentLength);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await _client.DeleteObjectAsync(_bucket, StorageKeys.SanitizeRelativePath(key), ct);

    public async Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var result = new List<StoredObject>();
        var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = StorageKeys.SanitizeRelativePath(prefix) + "/" };
        ListObjectsV2Response response;
        do
        {
            response = await _client.ListObjectsV2Async(request, ct);
            result.AddRange((response.S3Objects ?? []).Select(o =>
                new StoredObject(o.Key, o.Size ?? 0, o.LastModified?.ToUniversalTime() ?? DateTime.MinValue)));
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);
        return result;
    }

    public void Dispose() => _client.Dispose();
}
