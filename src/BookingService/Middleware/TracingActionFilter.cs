using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BookingService.Middleware;

public class TracingActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var activity = Activity.Current;

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("mvc.controller", context.RouteData.Values["controller"]);
            activity.SetTag("mvc.action", context.RouteData.Values["action"]);

            var routeTags = context.RouteData.Values
                .Where(kv => kv.Value is not null)
                .Select(kv => new KeyValuePair<string, object?>($"route.{kv.Key}", kv.Value))
                .ToList();

            activity.AddEvent(new ActivityEvent("action.executing",
                tags: new ActivityTagsCollection(routeTags)));

            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        e => e.Key,
                        e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

                activity.AddEvent(new ActivityEvent(
                    "model_validation.failed",
                    tags: new ActivityTagsCollection
                    {
                        ["validation.error_count"] = context.ModelState.ErrorCount,
                        ["validation.fields"] = JsonSerializer.Serialize(errors),
                    }));
            }
        }

        var executed = await next();

        if (activity is { IsAllDataRequested: true })
        {
            if (executed.Exception is { } ex && !executed.ExceptionHandled)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
                activity.SetTag("error.type", ex.GetType().FullName);
            }
            else if (executed.Result is ObjectResult { StatusCode: >= 500 } errorResult)
            {
                activity.SetStatus(ActivityStatusCode.Error, "Server error result from action");
                activity.AddEvent(new ActivityEvent(
                    "action.error_result",
                    tags: new ActivityTagsCollection
                    {
                        ["result.status_code"] = errorResult.StatusCode,
                        ["result.type"] = errorResult.GetType().Name,
                    }));
            }
        }
    }
}