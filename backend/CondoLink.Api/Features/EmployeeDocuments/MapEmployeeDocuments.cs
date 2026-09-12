namespace CondoLink.Api.Features.EmployeeDocuments;

public static class MapEmployeeDocuments
{
    public static IEndpointRouteBuilder MapEmployeeDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapUploadEmployeeDocumentBatch();
        endpoints.MapListEmployeeDocumentBatches();
        endpoints.MapGetEmployeeDocumentBatch();
        endpoints.MapUpdateEmployeeDocumentAssociation();
        endpoints.MapConfirmEmployeeDocumentBatch();
        endpoints.MapPreviewEmployeeDocument();
        endpoints.MapEmployeeDocumentDistributionEndpoints();
        return endpoints;
    }
}
