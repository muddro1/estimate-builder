# Estimate Builder (nopCommerce 4.90)

Lets registered customers save products, including their selected attributes, into named estimate lists. They can export a list to Excel or PDF, or move it into the cart. Admins can manage every estimate and create estimates for walk-in contacts who have no account.

## Building

The project references the nopCommerce assemblies directly, not through project references. Point `NopRefs` at a folder containing `Nop.Core.dll`, `Nop.Data.dll`, `Nop.Services.dll`, `Nop.Web.Framework.dll`, `Nop.Web.dll`, `FluentMigrator*.dll`, `linq2db.dll`, `ClosedXML.dll` and `iTextSharp.LGPLv2.Core.dll`. A built `Nop.Web/bin/Release/net9.0` folder works:

```sh
dotnet build -c Release -p:NopRefs=/path/to/nopCommerce/src/Presentation/Nop.Web/bin/Release/net9.0
```

If you leave out `NopRefs`, it defaults to `../refs`. The output goes to `../output` and contains the DLL, `plugin.json`, `Views/` and `Content/`. Copy that folder to `Plugins/Misc.EstimateBuilder` in the store.

## Permissions

The admin pages require **Admin area. Orders. View** to look at estimates and **Admin area. Orders. Create, edit, delete** to change them. Customer search also requires **Customers. View**. Vendor accounts can't open the admin pages, even though vendors have *Orders. View* by default.

## Pricing

Prices go through nopCommerce's own pricing, so they include tier, role and special prices, discounts, attribute adjustments, tax display and the working currency. Customer estimates are priced for their owner. Contact-only estimates get guest prices. Storefront users without the *Display prices* permission don't see prices, and *Call for price* products are left out of the total.
