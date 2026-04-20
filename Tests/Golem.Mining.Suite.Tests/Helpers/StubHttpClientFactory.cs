using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Golem.Mining.Suite.Tests.Helpers
{
    /// <summary>
    /// Minimal in-memory IHttpClientFactory stand-in so tests don't require Moq or real network
    /// calls. Hand it a HttpMessageHandler (or lambda) and every CreateClient returns an HttpClient
    /// wrapping that handler.
    /// </summary>
    internal sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            // disposeHandler: false — handler is reusable across clients in a test.
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.uexcorp.uk/")
            };
        }

        /// <summary>Factory that always throws — used to force RefineryService into its fallback path.</summary>
        public static StubHttpClientFactory AlwaysThrow()
            => new StubHttpClientFactory(new ThrowingHandler());

        /// <summary>Factory that returns the given body as 200 OK for every request.</summary>
        public static StubHttpClientFactory FromResponse(string body)
            => new StubHttpClientFactory(new StaticResponseHandler(body));

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new HttpRequestException("simulated network failure");
        }

        private sealed class StaticResponseHandler : HttpMessageHandler
        {
            private readonly string _body;
            public StaticResponseHandler(string body) { _body = body; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body)
                };
                return Task.FromResult(response);
            }
        }
    }
}
