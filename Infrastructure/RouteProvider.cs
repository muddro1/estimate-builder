using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EstimateBuilder.Infrastructure;

public class RouteProvider : IRouteProvider
{
    public int Priority => 0;

    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateList",
            pattern: "estimate",
            defaults: new { controller = "Estimate", action = "Index" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateCreate",
            pattern: "estimate/create",
            defaults: new { controller = "Estimate", action = "Create" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateEdit",
            pattern: "estimate/edit/{id:int}",
            defaults: new { controller = "Estimate", action = "Edit" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateDetails",
            pattern: "estimate/details/{id:int}",
            defaults: new { controller = "Estimate", action = "Details" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateDelete",
            pattern: "estimate/delete/{id:int}",
            defaults: new { controller = "Estimate", action = "Delete" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateCopy",
            pattern: "estimate/copy/{id:int}",
            defaults: new { controller = "Estimate", action = "Copy" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateAddToCart",
            pattern: "estimate/addtocart/{id:int}",
            defaults: new { controller = "Estimate", action = "AddToCart" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateAddItem",
            pattern: "estimate/additem",
            defaults: new { controller = "Estimate", action = "AddToEstimate" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateUpdateItems",
            pattern: "estimate/updateitems",
            defaults: new { controller = "Estimate", action = "UpdateItems" });


        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateExportExcel",
            pattern: "estimate/exportexcel/{id:int}",
            defaults: new { controller = "Estimate", action = "ExportToExcel" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateExportPdf",
            pattern: "estimate/exportpdf/{id:int}",
            defaults: new { controller = "Estimate", action = "ExportToPdf" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateProductsByCategory",
            pattern: "estimate/productsbycategory",
            defaults: new { controller = "Estimate", action = "GetProductsByCategory" });

        endpointRouteBuilder.MapControllerRoute(
            name: "EstimateProductAttributes",
            pattern: "estimate/productattributes",
            defaults: new { controller = "Estimate", action = "GetProductAttributes" });
    }
}
