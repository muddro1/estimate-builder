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
    public int PageIndex { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class AdminCreateEstimateModel
{
    // Toggle: existing customer or new contact
    public bool IsExistingCustomer { get; set; } = false;

    // Existing customer
    public int CustomerId { get; set; }
    public string? CustomerSearch { get; set; }

    // Contact info (for non-customer)
    [StringLength(500)]
    public string? ContactName { get; set; }
    [StringLength(500)]
    public string? ContactEmail { get; set; }
    [StringLength(100)]
    public string? ContactPhone { get; set; }
    [StringLength(500)]
    public string? ContactAddress { get; set; }
    [StringLength(200)]
    public string? ContactCity { get; set; }
    [StringLength(200)]
    public string? ContactState { get; set; }
    [StringLength(50)]
    public string? ContactZip { get; set; }

    // Estimate info
    [Required]
    [StringLength(EstimateBuilderDefaults.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(EstimateBuilderDefaults.DescriptionMaxLength)]
    public string? Description { get; set; }
}

public class AdminEstimateDetailsModel : EstimateItemsModel
{
    // Who this belongs to
    public bool IsCustomerEstimate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerDisplay { get; set; } = string.Empty;

    // Contact info (if no customer)
    [StringLength(500)]
    public string? ContactName { get; set; }
    [StringLength(500)]
    public string? ContactEmail { get; set; }
    [StringLength(100)]
    public string? ContactPhone { get; set; }
    [StringLength(500)]
    public string? ContactAddress { get; set; }
    [StringLength(200)]
    public string? ContactCity { get; set; }
    [StringLength(200)]
    public string? ContactState { get; set; }
    [StringLength(50)]
    public string? ContactZip { get; set; }

    // For assigning a customer to a contact estimate
    public string AssignCustomerSearch { get; set; } = string.Empty;
}
