using System.Net;
using Ipfs.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace StreamBox.Server.Controllers
{
    [Route("ipfs")]
    public class FileController : ControllerBase
    {
        private const string IpfsApiUri = "http://localhost:5001";
        private const string IpfsGatewayUri = "http://localhost:8080/ipfs/";

        private readonly IpfsClient _core;
        private readonly HttpClient _gateway;

        public FileController()
        {
            _core = new IpfsClient(IpfsApiUri);
            _gateway = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            _gateway.BaseAddress = new Uri(IpfsGatewayUri, UriKind.Absolute);
        }
        
        [HttpGet("{path}")]
        public async Task<IActionResult> GetAsync(string path, CancellationToken cancellationToken)
        {
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var node = await _core.FileSystem.ListFileAsync(path, cancellationToken);

            if (node.IsDirectory)
                return StatusCode((int)HttpStatusCode.NotImplemented, new { error = "CID is a directory." });

            var request = new HttpRequestMessage(HttpMethod.Head, path);
            var response = await _gateway.SendAsync(request, cancellationToken);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var lastModified = response.Content.Headers.LastModified;
            var etag = new EntityTagHeaderValue($"\"{node.Id}\"", isWeak: false);

            var stream = await AcquireFileStream(path, cancellationToken);

            return File(stream, contentType, lastModified, etag, true);
        }

        private async Task<Stream> AcquireFileStream(string path, CancellationToken cancellationToken)
        {
            return await _core.PostDownloadAsync("cat", cancellationToken, path);
        }
    }
}