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
    public class RateLimitedHttpClient : HttpClient
    {
        private readonly SemaphoreSlim RequestLimiter;
        private readonly int _requestLimit;

        private const int RELEASE_INTERVAL = 1000;
        public RateLimitedHttpClient(int requestLimitPerSecond)
        {
            _requestLimit = requestLimitPerSecond;
            RequestLimiter = new SemaphoreSlim(requestLimitPerSecond);
            Console.Write(requestLimitPerSecond);

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
            RequestLimiter.Release();
        }

        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await RequestLimiter.WaitAsync(cancellationToken);
            var result = await base.SendAsync(request, cancellationToken);
            if (result.StatusCode == HttpStatusCode.TooManyRequests)
                return await SendAsync(request, cancellationToken);
            return result;
        }
    }
}
