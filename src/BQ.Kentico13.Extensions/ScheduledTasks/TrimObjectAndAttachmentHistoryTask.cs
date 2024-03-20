using CMS.Core;
using CMS.DocumentEngine;
using CMS.Scheduler;
using CMS.Synchronization;
using System;
using System.Linq;
using CMS.SiteProvider;

namespace BQ.Kentico13.Extensions.ScheduledTasks
{
    public class TrimObjectAndPageVersionHistoryTask : ITask
    {
        public string Execute(TaskInfo task)
        {
            // Execute task
            try
            {
                return ExecuteInternal();
            }
            catch (Exception ex)
            {
                return Error("EXECUTE", "Error while executing task. Please review event log.", ex);
            }
        }

        private string Error(string source, string message, Exception ex = null)
        {
            var eventLogService = Service.Resolve<IEventLogService>();
            if (ex != null)
                eventLogService.LogException(nameof(TrimObjectAndPageVersionHistoryTask), source, ex, additionalMessage: message);
            else
                eventLogService.LogError(nameof(TrimObjectAndPageVersionHistoryTask), source, message);

            return message;
        }

        private void LogStatus(string message)
        {
            var eventLogService = Service.Resolve<IEventLogService>();
            eventLogService.LogInformation(nameof(TrimObjectAndPageVersionHistoryTask), "EXECUTE", message);
        }

        private string ExecuteInternal()
        {
            var pagesProcessed = TrimPageVersions();
            var objectsProcessed = TrimObjectVersions();

            return $"Processed {pagesProcessed} pages and {objectsProcessed} objects";
        }

        private int TrimPageVersions()
        {
            var siteInfoProvider = Service.Resolve<ISiteInfoProvider>();
            var sites = siteInfoProvider.Get().ToArray();

            var pagesProcessed = 0;

            foreach (var site in sites)
            {
                var treeProvider = new TreeProvider();
                var versionManager = VersionManager.GetInstance(treeProvider);

                var documents = treeProvider.SelectNodes()
                    .WhereEquals(nameof(TreeNode.NodeSiteID), site.SiteID)
                    .Published(false)
                    .ToArray();

                for (var i = 0; i < documents.Length; i++)
                {
                    var document = documents[i];

                    // Trims page versions
                    versionManager.DeleteOlderVersions(document.DocumentID, site.SiteName);
                    pagesProcessed++;
                }
            }

            return pagesProcessed;
        }

        private int TrimObjectVersions()
        {
            var objectVersionHistoryInfoProvider = Service.Resolve<IObjectVersionHistoryInfoProvider>();
            var siteInfoProvider = Service.Resolve<ISiteInfoProvider>();

            // Get a distinct list of object types and their IDs
            var distinctObjects = objectVersionHistoryInfoProvider.Get()
                .Columns(nameof(ObjectVersionHistoryInfo.VersionObjectType), nameof(ObjectVersionHistoryInfo.VersionObjectID), nameof(ObjectVersionHistoryInfo.VersionObjectSiteID))
                .WhereNull(nameof(ObjectVersionHistoryInfo.VersionDeletedWhen))
                .Distinct()
                .ToArray()
                .Select(x => new { x.VersionObjectType, x.VersionObjectID, x.VersionObjectSiteID })
                .Distinct()
                .ToArray();

            var sites = siteInfoProvider.Get().ToArray();

            var objectsProcessed = 0;

            for (var i = 0; i < distinctObjects.Length; i++)
            {
                var distinctObject = distinctObjects[i];

                // Trims object versions
                ObjectVersionManager.DeleteOlderVersions(distinctObject.VersionObjectType,
                    distinctObject.VersionObjectID,
                    sites.FirstOrDefault(x => x.SiteID == distinctObject.VersionObjectSiteID)?.SiteName);

                objectsProcessed++;
            }

            return objectsProcessed;
        }
    }
}
