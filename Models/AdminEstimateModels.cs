using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Misc.EstimateBuilder.Models;

public class AdminEstimateRowModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OwnerDisplay { get; set; } = string.Empty; // customer name/email or contact name
    public string OwnerType { get; set; } = string.Empty;   // "Customer" or "Contact"
    public int ItemCount { get; set; }
    public string CreatedOn { get; set; } = string.Empty;
}

public class AdminEstimateListsModel
{
    public IList<AdminEstimateRowModel> Estimates { get; set; } = new List<AdminEstimateRowModel>();
    public string? SearchTerm { get; set; }
}

public class AdminCreateEstimateModel
{
    // Toggle: existing customer or new contact
    public bool IsExistingCustomer { get; set; } = false;

    // Existing customer
    public int CustomerId { get; set; }
    public string CustomerSearch { get; set; } = string.Empty;

    // Contact info (for non-customer)
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactAddress { get; set; }
    public string? ContactCity { get; set; }
    public string? ContactState { get; set; }
    public string? ContactZip { get; set; }

    // Estimate info
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AdminEstimateDetailsModel : EstimateItemsModel
{
    // Who this belongs to
    public bool IsCustomerEstimate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerDisplay { get; set; } = string.Empty;

    // Contact info (if no customer)
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactAddress { get; set; }
    public string? ContactCity { get; set; }
    public string? ContactState { get; set; }
    public string? ContactZip { get; set; }

    // For assigning a customer to a contact estimate
    public string AssignCustomerSearch { get; set; } = string.Empty;
}
