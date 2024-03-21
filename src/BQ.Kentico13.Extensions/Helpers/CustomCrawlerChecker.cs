using System.Linq;
using CMS.Core;
using CMS;
using CMS.Helpers.Internal;
using CMS.Base.Internal;
using BQ.Kentico13.Extensions.Helpers;

[assembly: RegisterImplementation(typeof(ICrawlerChecker), typeof(CustomCrawlerChecker), Priority = RegistrationPriority.Default)]

namespace BQ.Kentico13.Extensions.Helpers
{
    internal class CustomCrawlerChecker : ICrawlerChecker
    {
        private readonly ICrawlerChecker _crawlerChecker;
        private readonly IHttpContextRetriever _httpContextRetriever;

        private static readonly string[] CrawlerKeywords = {
            "bot",
            "slurp",
            "spider",
            "google-structured-data-testing-tool",
            "facebookexternalhit",
            "skypeuripreview",
            "postman",
            "probe",
            // Add any more crawler or probe keywords here
        };

        public CustomCrawlerChecker(ICrawlerChecker checker, IHttpContextRetriever httpContextRetriever)
        {
            _crawlerChecker = checker;
            _httpContextRetriever = httpContextRetriever;
        }

        /// <summary>
        /// Checks whether the request matches a Kentico predefined crawler pattern, or matches one of the user agents keywords specified here.
        /// </summary>
        /// <returns><c>true</c>, if current request comes from the crawler; otherwise, <c>false</c>.</returns>
        public bool IsCrawler() => _crawlerChecker.IsCrawler() || IsUserAgentCrawler(_httpContextRetriever.GetContext()?.Request?.UserAgent);

        /// <summary>
        /// Parses UserAgent for known list of crawlers.
        /// </summary>
        private static bool IsUserAgentCrawler(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return false;

            var agent = userAgent.ToLowerInvariant();
            return CrawlerKeywords.Any(keyword => agent.Contains(keyword));
        }
    }
}
