using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevIO.Api.Extensions
{
    public class JsonWithFilesFormDataModelBinder : IModelBinder
    {
        private readonly IOptions<MvcJsonOptions> _jsonOptions;
        private readonly FormFileModelBinder _formFileModelBinder;

        public JsonWithFilesFormDataModelBinder(IOptions<MvcJsonOptions> jsonOptions, ILoggerFactory loggerFactory)
        {
            _jsonOptions = jsonOptions;
            _formFileModelBinder = new FormFileModelBinder(loggerFactory);
        }

        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
                throw new ArgumentNullException(paramName: nameof(bindingContext));

                //recupere a parte do formulario que contem o JSon
                var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.FieldName);
                if (valueResult == ValueProviderResult.None)
                {
                    // O JSon não foi encontrado
                    var message = bindingContext.ModelMetadata.ModelBindingMessageProvider.MissingBindRequiredValueAccessor(bindingContext.FieldName);
                    bindingContext.ModelState.TryAddModelError(key: bindingContext.ModelName, message);
                    return;
                }

                var rawValue = valueResult.FirstValue;

                // desserializar o modelo JSON
                var model = JsonConvert.DeserializeObject(rawValue, bindingContext.ModelType, _jsonOptions.Value.SerializerSettings);

                //Agora, vincule cada uma das propriedades IFormFile das outras partes do formulário
                foreach (var property in bindingContext.ModelMetadata.Properties)
                {
                    if (property.ModelType != typeof(IFormFile))
                        continue;

                    var fieldName = property.BinderModelName ?? property.PropertyName;
                    var modelName = fieldName;
                    var propertyModel = property.PropertyGetter(bindingContext.Model);
                    ModelBindingResult propertyResult;
                    using(bindingContext.EnterNestedScope(property, fieldName, modelName, propertyModel))
                    {
                        await _formFileModelBinder.BindModelAsync(bindingContext);
                        propertyResult = bindingContext.Result;
                    }

                    if (propertyResult.IsModelSet)
                    {
                        // O IFormFile foi vinculado com sucesso, atribua - o à propriedade correspondente da propriedade do modelo PropertySetter
                        property.PropertySetter(model, propertyResult.Model);

                    }
                    else if (property.IsBindingRequired)
                    {
                        var message = property.ModelBindingMessageProvider.MissingBindRequiredValueAccessor(fieldName);
                        bindingContext.ModelState.TryAddModelError(key: modelName, message);
                    }

                }

                //Defina o modelo construído com sucesso como resultado do modelo binding
                bindingContext.Result = ModelBindingResult.Success(model);
            
        }
    }
}
