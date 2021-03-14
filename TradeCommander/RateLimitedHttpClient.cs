using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace TradeCommander
{
    public class RateLimitedHandler : HttpClientHandler
    {
        private readonly SemaphoreSlim _requestLimiter;
        private readonly int _requestLimit;

        private const int RELEASE_INTERVAL = 1000;

        public RateLimitedHandler(int requestLimitPerSecond) : base()
        {
            _requestLimit = requestLimitPerSecond;
            _requestLimiter = new SemaphoreSlim(requestLimitPerSecond);

            StartLimitReleaser();
        }

        private void StartLimitReleaser()
        {
            var timer = new System.Timers.Timer(RELEASE_INTERVAL / _requestLimit);
            timer.Elapsed += ReleaseRequest;
            timer.Enabled = true;
        }

        private void ReleaseRequest(object sender, ElapsedEventArgs args)
        {
            _requestLimiter.Release();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _requestLimiter.WaitAsync(cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return await SendAsync(request, cancellationToken);

            return response;
        }
    }
}
