using BDAS2_Flowers.Models.ViewModels.AdminModels;
using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.OrderModels
{
    public class IdNameVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OrderItemVm
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class OrderCreateVm : IValidatableObject
    {
        public int UserId { get; set; }
        public int DeliveryMethodId { get; set; }
        public int ShopId { get; set; }
        public bool UseNewAddress { get; set; } = false;
        public int? AddressId { get; set; }
        public int PostalCode { get; set; }
        public string Street { get; set; } = "";
        public int HouseNumber { get; set; }
        public string PaymentType { get; set; } = "cash";
        public string? CardNumber { get; set; }
        public decimal? CashAccepted { get; set; }
        public string? CuponCode { get; set; }
        public string? CuponFallbackType { get; set; } = "cash";
        public List<OrderItemVm> Items { get; set; } = new();
        public List<IdNameVm> DeliveryMethods { get; set; } = new();
        public List<IdNameVm> Shops { get; set; } = new();
        public List<IdNameVm> Addresses { get; set; } = new();
        public List<IdNameVm> Products { get; set; } = new();
        public decimal EstimatedTotal { get; set; }
        public decimal CartTotal { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (UseNewAddress)
            {
                if (string.IsNullOrWhiteSpace(Street) || HouseNumber <= 0 || PostalCode <= 0)
                    yield return new ValidationResult(
                        "Vyplňte prosím ulici, číslo domu a PSČ.",
                        new[] { nameof(Street), nameof(HouseNumber), nameof(PostalCode) });
            }
            else
            {
                if (AddressId is null || AddressId <= 0)
                    yield return new ValidationResult(
                        "Vyberte prosím existující adresu.",
                        new[] { nameof(AddressId) });
            }

            var pt = PaymentType?.ToLowerInvariant();

            if (pt == "card")
            {
                if (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Count(char.IsDigit) < 12)
                    yield return new ValidationResult(
                        "Zadejte platné číslo karty (min. 12 číslic).",
                        new[] { nameof(CardNumber) });
            }
            else if (pt == "cash")
            {
                if (CashAccepted is null || CashAccepted <= 0)
                    yield return new ValidationResult(
                        "Zadejte přijatou hotovost.",
                        new[] { nameof(CashAccepted) });
            }
            else if (pt == "cupon")
            {
                if (string.IsNullOrWhiteSpace(CuponCode))
                    yield return new ValidationResult(
                        "Zadejte kód kupónu.",
                        new[] { nameof(CuponCode) });

                var fb = CuponFallbackType?.ToLowerInvariant();
                if (fb != "cash" && fb != "card")
                    yield return new ValidationResult(
                        "Vyberte způsob doplatku.",
                        new[] { nameof(CuponFallbackType) });

                if (fb == "cash")
                {
                    if (CashAccepted is null || CashAccepted <= 0)
                        yield return new ValidationResult(
                            "Zadejte přijatou hotovost.",
                            new[] { nameof(CashAccepted) });
                }
                else if (fb == "card")
                {
                    if (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Count(char.IsDigit) < 12)
                        yield return new ValidationResult(
                            "Zadejte platné číslo karty (min. 12 číslic).",
                            new[] { nameof(CardNumber) });
                }
            }

            if (Items == null || Items.Count == 0 || Items.Any(i => i.ProductId <= 0 || i.Quantity <= 0))
                yield return new ValidationResult(
                    "Přidejte alespoň jednu platnou položku.",
                    new[] { nameof(Items) });
        }
    }

    public class OrderItemDetailsVm
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class AdminOrdersListVm
    {
        public string? Status { get; set; }
        public string? Query { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public int TotalPages => (int)Math.Max(1, (Total + PageSize - 1) / PageSize);
        public List<string> StatusNames { get; set; } = new();
        public List<AdminOrderRowVm> Rows { get; set; } = new();
    }

    public class OrderDetailsVm
    {
        public int OrderId { get; set; }
        public string PublicNo { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string Customer { get; set; } = "";
        public string Status { get; set; } = "";
        public string Delivery { get; set; } = "";
        public string Shop { get; set; } = "";
        public decimal Total { get; set; }
        public string PaymentType { get; set; } = "";
        public string? PaymentMasked { get; set; }      
        public decimal Amount { get; set; }                
        public int? CardLast4 { get; set; }              
        public decimal? CashAccepted { get; set; }
        public decimal? CashReturned { get; set; }
        public decimal? CuponBonus { get; set; }
        public DateTime? CuponExpiry { get; set; }
        public List<OrderItemDetailsVm> Items { get; set; } = new();
        public List<string> StatusNames { get; set; } = new();
    }
}