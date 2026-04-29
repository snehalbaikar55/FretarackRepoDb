using RepoDb.Attributes;

namespace GeneratedModels;

[Map("vw_AllCargoList")]
public class AllCargoList
{
    public int CargoID { get; set; }
    public string? JobNo { get; set; }
    public string? ModeOfTransport { get; set; }
    public string? TransportDirection { get; set; }
    public string? Shipper { get; set; }
    public string? Consignee { get; set; }
    public int? ShipperID { get; set; }
    public int? ConsigneeID { get; set; }
    public string? OriginAgent { get; set; }
    public string? DestinationAgent { get; set; }
    public string? ContainerNo { get; set; }
    public string? invoiceno { get; set; }
    public string? OceanLine { get; set; }
    public string? Vessel { get; set; }
    public DateTime? ETD { get; set; }
    public DateTime? ETA { get; set; }
    public string? MasterNo { get; set; }
    public string? HouseNo { get; set; }
    public string? CYCLE { get; set; }
    public string? POL { get; set; }
    public string? POD { get; set; }
    public string? CreatedBys { get; set; }
    public DateTime? DateCreated { get; set; }
    public string? ModifiedBys { get; set; }
    public DateTime? DateModified { get; set; }
    public int? OfficeID { get; set; }
    public string? WebJob { get; set; }
    public string? CustomerName { get; set; }
    [Map("20GP")]
    public int? _20GP { get; set; }
    [Map("20HC")]
    public int? _20HC { get; set; }
    [Map("40GP")]
    public int? _40GP { get; set; }
    [Map("40HC")]
    public int? _40HC { get; set; }
    public string? BookingNumber { get; set; }
    public DateTime? BookingDate { get; set; }
    public string? INVOICE_NUMBER { get; set; }
}
