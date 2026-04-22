using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrvixFlow.Infrastructure.Services;

namespace OrvixFlow.Tests;

public class ResendEmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_WhenResendAcceptsRequest_PostsExpectedPayload()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient("resend-email")).Returns(httpClient);

        var service = new ResendEmailService(
            httpClientFactory.Object,
            Options.Create(new EmailOptions
            {
                ResendApiKey = "re_test_key",
                FromEmail = "noreply@orvixflow.local",
                FromName = "OrvixFlow Identity"
            }),
            NullLogger<ResendEmailService>.Instance);

        await service.SendEmailAsync("user@example.com", "Verify", "<p>Hello</p>");

        handler.Request.Should().NotBeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("https://api.resend.com/emails");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("re_test_key");

        var body = await handler.Request.Content!.ReadAsStringAsync();
        body.Should().Contain("OrvixFlow Identity \\u003Cnoreply@orvixflow.local\\u003E");
        body.Should().Contain("\"subject\":\"Verify\"");
        body.Should().Contain("\\u003Cp\\u003EHello\\u003C/p\\u003E");
        body.Should().Contain("user@example.com");
    }

    [Fact]
    public async Task SendEmailAsync_WhenResendReturnsFailure_ThrowsHttpRequestException()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient("resend-email")).Returns(httpClient);

        var service = new ResendEmailService(
            httpClientFactory.Object,
            Options.Create(new EmailOptions
            {
                ResendApiKey = "re_test_key",
                FromEmail = "noreply@orvixflow.local"
            }),
            NullLogger<ResendEmailService>.Instance);

        var act = () => service.SendEmailAsync("user@example.com", "Verify", "<p>Hello</p>");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
