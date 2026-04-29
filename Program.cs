using RepoDb;
using RepoDbApi.Repositories;
using RepoDbApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Initialize RepoDB for SQL Server
SqlServerBootstrap.Initialize();

const string LocalAngularCorsPolicy = "LocalAngularCorsPolicy";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalAngularCorsPolicy, policy =>
    {
        policy
            .WithOrigins("https://localhost:4200", "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
            
    });
});
builder.Services.AddScoped<ICargoRepository, CargoRepository>();
builder.Services.AddScoped<ICargoService, CargoService>();
builder.Services.AddScoped<IAllCargoListRepository, AllCargoListRepository>();
builder.Services.AddScoped<IAllCargoListService, AllCargoListService>();
builder.Services.AddScoped<ICargoRelatedDataRepository, CargoRelatedDataRepository>();
builder.Services.AddScoped<ICargoRelatedDataService, CargoRelatedDataService>();
builder.Services.AddScoped<IInvoicesRepository, InvoicesRepository>();
builder.Services.AddScoped<IInvoicesService, InvoicesService>();
builder.Services.AddScoped<IVendorBillsRepository, VendorBillsRepository>();
builder.Services.AddScoped<IVendorBillsService, VendorBillsService>();
builder.Services.AddScoped<ICostSheetsRepository, CostSheetsRepository>();
builder.Services.AddScoped<ICostSheetsService, CostSheetsService>();
builder.Services.AddScoped<ICargoChargesRepository, CargoChargesRepository>();
builder.Services.AddScoped<ICargoChargesService, CargoChargesService>();
builder.Services.AddScoped<ICargoContainersRepository, CargoContainersRepository>();
builder.Services.AddScoped<ICargoContainersService, CargoContainersService>();
builder.Services.AddScoped<ICargoPackagesRepository, CargoPackagesRepository>();
builder.Services.AddScoped<ICargoPackagesService, CargoPackagesService>();
builder.Services.AddScoped<ICompanyAddressesRepository, CompanyAddressesRepository>();
builder.Services.AddScoped<ICompanyAddressesService, CompanyAddressesService>();
builder.Services.AddScoped<IAirShipmentRoutingRepository, AirShipmentRoutingRepository>();
builder.Services.AddScoped<IAirShipmentRoutingService, AirShipmentRoutingService>();
builder.Services.AddScoped<IOceanShipmentRoutingRepository, OceanShipmentRoutingRepository>();
builder.Services.AddScoped<IOceanShipmentRoutingService, OceanShipmentRoutingService>();
builder.Services.AddScoped<ICargoHBLRepository, CargoHBLRepository>();
builder.Services.AddScoped<ICargoHBLService, CargoHBLService>();
builder.Services.AddScoped<ICargoAWBRepository, CargoAWBRepository>();
builder.Services.AddScoped<ICargoAWBService, CargoAWBService>();
builder.Services.AddScoped<IFundRequiremntRepository, FundRequiremntRepository>();
builder.Services.AddScoped<IFundRequiremntService, FundRequiremntService>();
builder.Services.AddScoped<IVwInvoiceListRepository, VwInvoiceListRepository>();
builder.Services.AddScoped<IVwInvoiceListService, VwInvoiceListService>();
builder.Services.AddScoped<IVwBillListRepository, VwBillListRepository>();
builder.Services.AddScoped<IVwBillListService, VwBillListService>();
builder.Services.AddScoped<IVwCargoMintCargoDetailsRepository, VwCargoMintCargoDetailsRepository>();
builder.Services.AddScoped<IVwCargoMintCargoDetailsService, VwCargoMintCargoDetailsService>();
builder.Services.AddScoped<ICostSheetChargesRepository, CostSheetChargesRepository>();
builder.Services.AddScoped<ICostSheetChargesService, CostSheetChargesService>();
builder.Services.AddScoped<ICargoStatusesRepository, CargoStatusesRepository>();
builder.Services.AddScoped<ICargoStatusesService, CargoStatusesService>();
builder.Services.AddScoped<ICargoDocumentsRepository, CargoDocumentsRepository>();
builder.Services.AddScoped<ICargoDocumentsService, CargoDocumentsService>();
builder.Services.AddScoped<IJobVendorDetailsRepository, JobVendorDetailsRepository>();
builder.Services.AddScoped<IJobVendorDetailsService, JobVendorDetailsService>();
builder.Services.AddScoped<ICargoDetailsRepository, CargoDetailsRepository>();
builder.Services.AddScoped<ICargoDetailsService, CargoDetailsService>();
builder.Services.AddScoped<ICargoCANRepository, CargoCANRepository>();
builder.Services.AddScoped<ICargoCANService, CargoCANService>();
builder.Services.AddScoped<ICargoEntitiesRepository, CargoEntitiesRepository>();
builder.Services.AddScoped<ICargoEntitiesService, CargoEntitiesService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseCors(LocalAngularCorsPolicy);

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
