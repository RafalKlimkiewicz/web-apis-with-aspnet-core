using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace MyBGList.Attributes;

public class ManualValidationFilterAttribute : Attribute, IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        for (var i = 0; i < action.Filters.Count; i++)
        {
            //Sadly, the ModelStateInvalidFilterFactory type is marked as internal, which prevents us from checking for the filter presence by using a strongly typed approach
            if (action.Filters[i] is ModelStateInvalidFilter || action.Filters[i].GetType().Name == "ModelStateInvalidFilterFactory")
            {
                action.Filters.RemoveAt(i);
                break;
            }
        }
    }
}
