using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ECommerce.Models
{
    public class ViewRenderService : IViewRenderService
    {
        private readonly ITempDataProvider _tempDataProvider;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;

        public ViewRenderService(
            ITempDataProvider tempDataProvider,
            ICompositeViewEngine viewEngine,
            IServiceProvider serviceProvider)
        {
            _tempDataProvider = tempDataProvider;
            _viewEngine = viewEngine;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> RenderToStringAsync(string viewName, object model)
        {
            var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };

            var routeData = new RouteData();
            routeData.Values["controller"] = "Home"; //  specify the controller folder

            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: true);

            if (!viewResult.Success)
            {
                throw new InvalidOperationException($"View '{viewName}' not found. Searched locations: {string.Join(", ", viewResult.SearchedLocations)}");
            }

            var view = viewResult.View;
            var sb = new StringBuilder();

            using (var writer = new StringWriter(sb))
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
                    new TempDataDictionary(actionContext.HttpContext, _serviceProvider.GetService<ITempDataProvider>()),
                    writer,
                    new HtmlHelperOptions()
                );

                await view.RenderAsync(viewContext);
            }

            return sb.ToString();
        }

    }
}