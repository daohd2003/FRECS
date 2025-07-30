using System.Net.Http.Headers;

namespace ShareItFE.Common.Utilities
{
    public class AuthenticatedHttpClientHelper
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticatedHttpClientHelper(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            var accessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];

            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return client;
        }
    }
}
