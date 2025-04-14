using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ECommerce.Models
{
    public class DateOnlyModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;

            if (DateOnly.TryParse(value, out var date))
            {
                bindingContext.Result = ModelBindingResult.Success(date);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid date format.");
            }

            return Task.CompletedTask;
        }
    }
}
