namespace ECommerce.Models
{
    public class LowercaseUrlMiddleware
    {
        private readonly RequestDelegate _next;

        public LowercaseUrlMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var request = context.Request;

            if (request.Method == "GET" && request.Path.HasValue)
            {
                string lowercaseUrl = request.Path.Value.ToLowerInvariant();

                if (request.Path.Value != lowercaseUrl)
                {
                    context.Response.Redirect(lowercaseUrl + request.QueryString, true);
                    return;
                }
            }

            await _next(context);
        }
    }

    // Extension method to use middleware easily
    public static class LowercaseUrlMiddlewareExtensions
    {
        public static IApplicationBuilder UseLowercaseUrls(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LowercaseUrlMiddleware>();
        }
    }

}
