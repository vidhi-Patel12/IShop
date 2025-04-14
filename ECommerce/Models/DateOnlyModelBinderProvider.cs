using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ECommerce.Models
{
    public class DateOnlyModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(DateOnly))
            {
                return new DateOnlyModelBinder();
            }

            return null!;
        }
    }
}
