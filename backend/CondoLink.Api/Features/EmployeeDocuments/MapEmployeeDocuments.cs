namespace CondoLink.Api.Features.EmployeeDocuments;

public static class MapEmployeeDocuments
{
    public static IEndpointRouteBuilder MapEmployeeDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapUploadEmployeeDocumentBatch();
        endpoints.MapListEmployeeDocumentBatches();
        endpoints.MapGetEmployeeDocumentBatch();
        endpoints.MapUpdateEmployeeDocumentAssociation();
        endpoints.MapReplaceEmployeeDocumentFile();
        endpoints.MapConfirmEmployeeDocumentBatch();
        endpoints.MapDeleteEmployeeDocument();
        endpoints.MapDeleteEmployeeDocumentBatch();
        endpoints.MapReopenEmployeeDocumentBatch();
        endpoints.MapPreviewEmployeeDocument();
        endpoints.MapEmployeeDocumentDistributionEndpoints();
        return endpoints;
    }
}
