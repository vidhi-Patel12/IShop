using System.Text.RegularExpressions;

namespace ECommerce.Models
{
    public class SlugConstraint : IParameterPolicy
    {
        private static readonly Regex SlugRegex = new Regex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        public bool Match(HttpContext httpContext, IRouter route, string routeKey,
                      RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (values.ContainsKey(routeKey))
            {
                var slug = values[routeKey]?.ToString();
                return !string.IsNullOrEmpty(slug) && slug.All(c => char.IsLetterOrDigit(c) || c == '-');
            }
            return false;
        }
    }
}
