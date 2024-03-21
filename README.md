# Kentico Xperience 13 Extensions, Helpers and Tasks

This package contains a collection of helpers and scheduled tasks for use with Kentico Xperience 13.0.

## Dependencies

This packaged is built using .NET Standard 2.0 and is compatible with the CMSApp.

## How to Use?

This package is not currently available on nuget.

## Included in the package

### Scheduled Tasks

#### ClearOldDataFromRecycleBinTask

This task can be configured to automatically removed objects and pages from the recycling bin older than a specified number of days.

Can be scheduled as necessary or run on demand.

#### TrimObjectAndPageVersionHistoryTask

This task will automatically remove older versions of objects and pages. As per the version retention settings configured in the Kentico settings module.

This task is expected to be run on demand in instances where the number of retention versions is reduced in the Kentico settings.

### Helpers

#### CustomCrawlerChecker

This is a custom implementation of the ICrawlerChecker from Kentico. This instance can be used as an example of how to include additional keywords in the crawler user agent check in order to prevent features such as contact creation and online marketing activities from initialising.