using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EventGO.API.Swagger;

/// <summary>
/// Chỉ tác động tới tài liệu Swagger, không đổi response.
/// [ApiController] tự trả 400 application/problem+json (ValidationProblemDetails)
/// khi body không qua DataAnnotations. ASP.NET Core chỉ cho một kiểu body cho mỗi
/// mã trạng thái trong [ProducesResponseType], nên khi action còn trả 400
/// { message, errors? } (application/json) thì kiểu problem+json bị mất khỏi
/// Swagger. Filter này bổ sung lại để Swagger mô tả đúng cả hai.
/// </summary>
public sealed class ValidationProblemResponseOperationFilter : IOperationFilter
{
    private const string ProblemJson = "application/problem+json";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasBody = context.ApiDescription.ParameterDescriptions
            .Any(x => x.Source == BindingSource.Body);
        if (!hasBody)
        {
            return;
        }

        operation.Responses ??= new OpenApiResponses();

        if (!operation.Responses.TryGetValue("400", out var existing)
            || existing is not OpenApiResponse response)
        {
            response = new OpenApiResponse { Description = "Bad Request" };
            operation.Responses["400"] = response;
        }

        response.Content ??= new Dictionary<string, OpenApiMediaType>();
        if (!response.Content.ContainsKey(ProblemJson))
        {
            response.Content[ProblemJson] = new OpenApiMediaType
            {
                Schema = context.SchemaGenerator.GenerateSchema(
                    typeof(ValidationProblemDetails),
                    context.SchemaRepository)
            };
        }
    }
}
