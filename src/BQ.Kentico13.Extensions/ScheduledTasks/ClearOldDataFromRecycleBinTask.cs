using CMS.Base;
using CMS.Core;
using CMS.DataEngine;
using CMS.DocumentEngine;
using CMS.EventLog;
using CMS.Helpers;
using CMS.Scheduler;
using CMS.SiteProvider;
using CMS.Synchronization;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace BQ.Kentico13.Extensions.ScheduledTasks
{
    public class ClearOldDataFromRecycleBinTask : ITask
    {
        private class TaskOptions
        {
            public bool ClearObjects { get; set; }
            public int ClearObjectsOlderThanDays { get; set; }
            public bool ClearPages { get; set; }
            public int ClearPagesOlderThanDays { get; set; }
        }

        public string Execute(TaskInfo task)
        {
            // Parse the task data options
            TaskOptions options;
            try
            {
                if (string.IsNullOrWhiteSpace(task.TaskData))
                    throw new ArgumentNullException(nameof(task.TaskData));
                
                options = JsonConvert.DeserializeObject<TaskOptions>(task.TaskData);

                if (options == null)
                    throw new ArgumentNullException(nameof(task.TaskData));
            }
            catch (Exception ex)
            {
                return Error("CONFIG", $"Failed to parse task data. Task data expected in format:{Environment.NewLine}{JsonConvert.SerializeObject(new TaskOptions(), Formatting.Indented)}", ex);
            }

            // Validate options
            if (options.ClearObjects && options.ClearObjectsOlderThanDays < 0)
                return Error("CONFIG", $"{nameof(TaskOptions.ClearObjectsOlderThanDays)} must be 0 or greater.");

            // Validate options
            if (options.ClearPages && options.ClearPagesOlderThanDays < 0)
                return Error("CONFIG", $"{nameof(TaskOptions.ClearPagesOlderThanDays)} must be 0 or greater.");

            // Execute task
            try
            {
                return ExecuteInternal(options);
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
                eventLogService.LogException(nameof(ClearOldDataFromRecycleBinTask), source, ex, additionalMessage: message);
            else
                eventLogService.LogError(nameof(ClearOldDataFromRecycleBinTask), source, message);

            return message;
        }

        private void LogStatus(string message)
        {
            var eventLogService = Service.Resolve<IEventLogService>();
            eventLogService.LogInformation(nameof(ClearOldDataFromRecycleBinTask), "EXECUTE", message);
        }

        private string ExecuteInternal(TaskOptions taskOptions)
        {
            var removedPages = 0;
            if (taskOptions.ClearPages)
                removedPages = TrimPageHistory(taskOptions);

            var removedObjects = 0;
            if (taskOptions.ClearObjects)
                removedObjects = TrimObjectHistory(taskOptions);

            var result = $"Cleared {removedObjects} objects and {removedPages} pages from the recycle bin.";
            LogStatus(result);

            return result;
        }

        private int TrimObjectHistory(TaskOptions taskOptions)
        {
            var deleteBeforeDate = DateTime.Today.AddDays(-taskOptions.ClearObjectsOlderThanDays);

            var whereCondition = new WhereCondition()
                .WhereNotNull(nameof(ObjectVersionHistoryInfo.VersionDeletedWhen))
                .WhereLessThan(nameof(ObjectVersionHistoryInfo.VersionDeletedWhen), deleteBeforeDate);

            var objectsToDelete = ObjectVersionHistoryInfoProvider.GetRecycleBin(0, whereCondition.ToString(true),
                    nameof(ObjectVersionHistoryInfo.VersionDeletedWhen), -1, string.Join(", ", new[]
                    {
                        nameof(ObjectVersionHistoryInfo.VersionID),
                        nameof(ObjectVersionHistoryInfo.VersionObjectType),
                        nameof(ObjectVersionHistoryInfo.VersionObjectID),
                        nameof(ObjectVersionHistoryInfo.VersionObjectDisplayName),
                        nameof(ObjectVersionHistoryInfo.VersionObjectSiteID)
                    }))
                .ToArray()
                .Select(x => new
                {
                    x.VersionID,
                    x.VersionObjectType,
                    x.VersionObjectID,
                    x.VersionObjectDisplayName,
                    x.VersionObjectSiteID
                })
                .Distinct()
                .ToArray();

            LogStatus($"Identified {objectsToDelete.Length} objects to delete");

            if (objectsToDelete.Any())
            {
                for (var i = 0; i < objectsToDelete.Length; i++)
                {
                    var objectToDelete = objectsToDelete[i];

                    var objName = HTMLHelper.HTMLEncode(objectToDelete.VersionObjectDisplayName);
                    ObjectVersionManager.DestroyObjectHistory(objectToDelete.VersionObjectType, objectToDelete.VersionObjectID);
                    LogContext.LogEventToCurrent(EventType.INFORMATION, "Objects", "DESTROYOBJECT",
                        string.Format(ResHelper.GetString("objectversioning.Recyclebin.objectdestroyed"), objName),
                        RequestContext.RawURL, 0, null, 0, null, RequestContext.UserHostAddress, 0,
                        SystemContext.MachineName, RequestContext.URLReferrer, RequestContext.UserAgent, DateTime.Now);
                }
            }

            return objectsToDelete.Length;
        }

        private int TrimPageHistory(TaskOptions taskOptions)
        {
            var deleteBeforeDate = DateTime.Today.AddDays(-taskOptions.ClearPagesOlderThanDays);

            var pagesToDelete = VersionHistoryInfoProvider.GetRecycleBin(0, orderBy: $"{nameof(VersionHistoryInfo.VersionNodeAliasPath)} ASC", modifiedTo: deleteBeforeDate)
                .ToArray();

            LogStatus($"Identified {pagesToDelete.Length} pages to delete");

            if (pagesToDelete.Any())
            {
                var tree = new TreeProvider { AllowAsyncActions = false };
                var versionManager = VersionManager.GetInstance(tree);

                for (var i = 0; i < pagesToDelete.Length; i++)
                {
                    var pageToDelete = pagesToDelete[i];

                    var name = $"{pageToDelete.VersionDocumentName} ({pageToDelete.VersionNodeAliasPath})";

                    // Destroy the version
                    versionManager.DestroyDocumentHistory(pageToDelete.DocumentID);

                    LogContext.LogEventToCurrent(EventType.INFORMATION, "Content", "DESTROYDOC",
                        string.Format(ResHelper.GetString("Recyclebin.documentdestroyed"), name), RequestContext.RawURL,
                        0, null, 0, null, RequestContext.UserHostAddress,
                        SiteContext.CurrentSiteID, SystemContext.MachineName, RequestContext.URLReferrer,
                        RequestContext.UserAgent, DateTime.Now);
                }
            }

            return pagesToDelete.Length;
        }
    }
}
