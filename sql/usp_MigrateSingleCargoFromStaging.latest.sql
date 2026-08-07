SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER   PROCEDURE [dbo].[usp_MigrateSingleCargoFromStaging]
    @FretrackCargoId int,
    @OrgId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.Organizations
            WHERE OrgId = @OrgId
        )
        BEGIN
            THROW 50002, 'Invalid OrgId passed to usp_MigrateSingleCargoFromStaging.', 1;
        END;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.Fretrack_Cargo_Staging s
            WHERE s.CargoId = @FretrackCargoId
        )
        BEGIN
            THROW 50001, 'Fretrack cargo not found in dbo.Fretrack_Cargo_Staging.', 1;
        END;

        /* =========================================================
           1. Cargo staging updates
           ========================================================= */
        UPDATE s
        SET s.CargoMintCompanyId = c.CompanyId
        FROM dbo.Fretrack_Cargo_Staging s
        JOIN dbo.Companies c
            ON ',' + c.FretrackCompanyIDs + ',' LIKE '%,' + CAST(s.CustomerID AS varchar(20)) + ',%'
        WHERE s.CargoId = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintOverseasAgentId = c.CompanyId
        FROM dbo.Fretrack_Cargo_Staging s
        JOIN dbo.Companies c
            ON ',' + c.FretrackCompanyIDs + ',' LIKE '%,' + CAST(s.OverseasAgentId AS varchar(20)) + ',%'
        WHERE s.CargoId = @FretrackCargoId;

        UPDATE c
        SET c.CargoMintSalesPersonId = u.CargoMintUserID
        FROM dbo.Fretrack_Cargo_Staging c
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON c.SalesPersonId = u.FretrackUserId
        WHERE c.CargoId = @FretrackCargoId;

        UPDATE c
        SET c.CargoMintHandledById = u.CargoMintUserID
        FROM dbo.Fretrack_Cargo_Staging c
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON c.HandledById = u.FretrackUserId
        WHERE c.CargoId = @FretrackCargoId;

        UPDATE c
        SET c.CargoMintCreatedById = u.CargoMintUserID
        FROM dbo.Fretrack_Cargo_Staging c
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON c.CreatedBy = u.FretrackUserId
        WHERE c.CargoId = @FretrackCargoId;

        UPDATE c
        SET c.CargoMintDeletedById = u.CargoMintUserID
        FROM dbo.Fretrack_Cargo_Staging c
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON c.DeletedBy = u.FretrackUserId
        WHERE c.CargoId = @FretrackCargoId;

        UPDATE c
        SET c.CargoMintModifiedById = u.CargoMintUserID
        FROM dbo.Fretrack_Cargo_Staging c
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON c.ModifiedBy = u.FretrackUserId
        WHERE c.CargoId = @FretrackCargoId;

          UPDATE c
        SET c.ShipmentType=1
        FROM dbo.Fretrack_Cargo_Staging c
        WHERE c.CargoId = @FretrackCargoId;

        UPDATE dbo.Fretrack_Cargo_Staging
        SET
            IsSelectedForMigration = 1,
            MigrationRemarks = 'Shipment',
            ImportedOn = GETDATE()
        WHERE CargoId = @FretrackCargoId
          AND ISNULL(isDeleted, 0) = 0;

        /* =========================================================
           2. Shipment insert/update
           ========================================================= */
        INSERT INTO dbo.Shipments
        (
            OrgId,
            ShipmentNo,
            JobNo,
            MasterNo,
            HouseNo,
            CustomerId,
            BranchId,
            SalesPersonId,
            ShipmentStatus,
            NominationType,
            OverseasAgentId,
            HandledById,
            ShipmentType,
            IsConsolidated,
            ShipmentDate,
            QuotationId,
            CustomerRefNo,
            ExternalRemarks,
            InternalRemarks,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate,
            DeletedBy,
            DeletedDate,
            IsDeleted,
            FretrackCargoId
        )
        SELECT
            s.OrgId,
            s.CargoNumber,
            s.JobNo,
            s.MasterNo,
            s.HouseNo,
            s.CargoMintCompanyId,
            CASE
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'M' THEN 22
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'C' THEN 23
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'P' THEN 24
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'B' THEN 4
                ELSE s.BranchId
            END,
            s.CargoMintSalesPersonId,
            ISNULL(s.ShipmentStatus, 'Planned'),
            s.NominationType,
            s.CargoMintOverseasAgentId,
            s.CargoMintHandledById,
            1 as ShipmentType,
            ISNULL(s.isConsolidation, 0),
            ISNULL(s.ShipmentDate, s.DateCreated),
            s.CargoMintQuotationId,
            s.CustomerReference,
            s.Notes,
            s.Notes,
            s.CargoMintCreatedById,
            s.DateCreated,
            s.CargoMintModifiedById,
            s.DateModified,
            s.CargoMintDeletedById,
            s.DateDeleted,
            ISNULL(s.isDeleted, 0),
            s.CargoId
        FROM dbo.Fretrack_Cargo_Staging s
        WHERE s.CargoId = @FretrackCargoId
          AND ISNULL(s.isDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.Shipments sh
              WHERE sh.FretrackCargoId = s.CargoId
          );

        UPDATE s
        SET s.CargomintShipmentId = sh.ShipmentId
        FROM dbo.Fretrack_Cargo_Staging s
        JOIN dbo.Shipments sh
            ON s.CargoId = sh.FretrackCargoId
        WHERE s.CargoId = @FretrackCargoId;

        UPDATE sh
        SET
            sh.ShipmentNo = s.CargoNumber,
            sh.JobNo = s.JobNo,
            sh.MasterNo = s.MasterNo,
            sh.HouseNo = s.HouseNo,
            sh.CustomerId = s.CargoMintCompanyId,
            sh.BranchId = CASE
                              WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'M' THEN 22
                              WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'C' THEN 23
                              WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'P' THEN 24
                              WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'B' THEN 4
                              ELSE sh.BranchId
                          END,
            sh.SalesPersonId = s.CargoMintSalesPersonId,
            sh.ShipmentStatus = ISNULL(s.ShipmentStatus, 'Planned'),
            sh.NominationType = s.NominationType,
            sh.OverseasAgentId = s.CargoMintOverseasAgentId,
            sh.HandledById = s.CargoMintHandledById,
            sh.ShipmentType = s.ShipmentType,
            sh.IsConsolidated = ISNULL(s.isConsolidation, 0),
            sh.ShipmentDate = ISNULL(s.ShipmentDate, s.DateCreated),
            sh.QuotationId = s.CargoMintQuotationId,
            sh.CustomerRefNo = s.CustomerReference,
            sh.ExternalRemarks = s.Notes,
            sh.InternalRemarks = s.Notes,
            sh.CreatedBy = s.CargoMintCreatedById,
            sh.CreatedDate = s.DateCreated,
            sh.UpdatedBy = s.CargoMintModifiedById,
            sh.UpdatedDate = s.DateModified,
            sh.DeletedBy = s.CargoMintDeletedById,
            sh.DeletedDate = s.DateDeleted,
            sh.IsDeleted = ISNULL(s.isDeleted, 0)
        FROM dbo.Shipments sh
        JOIN dbo.Fretrack_Cargo_Staging s
            ON sh.FretrackCargoId = s.CargoId
        WHERE s.CargoId = @FretrackCargoId;

        UPDATE s
        SET s.BranchId =
            CASE
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'M' THEN 22
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'C' THEN 23
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'P' THEN 24
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNo)), 1)) = 'B' THEN 4
                ELSE s.BranchId
            END
        FROM dbo.Shipments s
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND (s.BranchId IS NULL OR s.BranchId = 0)
          AND s.JobNo IS NOT NULL
          AND LTRIM(RTRIM(s.JobNo)) <> '';

        /* =========================================================
           3. Shipment service staging updates
           ========================================================= */
        UPDATE s
        SET s.ServiceTypeId = m.CargoMintServiceTypeId
        FROM dbo.Fretrack_ShipmentService_Staging s
        JOIN dbo.Fretrack_Service_Mode_Mapping m
            ON UPPER(LTRIM(RTRIM(s.JobTypeName))) = UPPER(LTRIM(RTRIM(m.JobTypeName)))
           AND UPPER(LTRIM(RTRIM(s.TransportMode))) = UPPER(LTRIM(RTRIM(m.ModeOfTransport)))
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND m.CargoMintServiceTypeId IS NOT NULL;

        UPDATE s
        SET s.TransportModeId = m.CargoMintTransportModeId
        FROM dbo.Fretrack_ShipmentService_Staging s
        JOIN dbo.Fretrack_Service_Mode_Mapping m
            ON UPPER(LTRIM(RTRIM(s.JobTypeName))) = UPPER(LTRIM(RTRIM(m.JobTypeName)))
           AND UPPER(LTRIM(RTRIM(s.TransportMode))) = UPPER(LTRIM(RTRIM(m.ModeOfTransport)))
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND m.CargoMintTransportModeId IS NOT NULL;

        UPDATE dbo.Fretrack_ShipmentService_Staging
        SET TransportDirectionId =
            CASE
                WHEN UPPER(LTRIM(RTRIM(TransportDirection))) = 'EXPORT' THEN 1
                WHEN UPPER(LTRIM(RTRIM(TransportDirection))) = 'IMPORT' THEN 2
                WHEN UPPER(LTRIM(RTRIM(TransportDirection))) = 'DOMESTIC' THEN 3
                WHEN UPPER(LTRIM(RTRIM(TransportDirection))) = 'THIRDCOUNTRY' THEN 4
                ELSE NULL
            END
        WHERE FretrackCargoId = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintCarrierId =
            CASE
                WHEN UPPER(LTRIM(RTRIM(s.TransportMode))) = 'AIR' THEN a.CargoMintCarrierId
                WHEN UPPER(LTRIM(RTRIM(s.TransportMode))) IN ('OCEAN', 'SURFACE') THEN o.CargoMintCarrierId
                ELSE s.CargoMintCarrierId
            END
        FROM dbo.Fretrack_ShipmentService_Staging s
        LEFT JOIN dbo.Fretrack_AirlineMaster_Staging a
            ON s.CarrierId = a.AirlineID
        LEFT JOIN dbo.Fretrack_OceanLineMaster_Staging o
            ON s.CarrierId = o.OceanLineID
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND (
                a.CargoMintCarrierId IS NOT NULL
             OR o.CargoMintCarrierId IS NOT NULL
          );

        UPDATE s
        SET s.CargomintShipmentId = sh.ShipmentId
        FROM dbo.Fretrack_ShipmentService_Staging s
        JOIN dbo.Shipments sh
            ON s.FretrackCargoId = sh.FretrackCargoId
        WHERE s.FretrackCargoId = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintPolId = l.CargoMintLocationId
        FROM dbo.Fretrack_ShipmentService_Staging s
        JOIN dbo.Fretrack_Location_Master l
            ON s.PolId = l.LocationID
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.PolId IS NOT NULL;

        UPDATE s
        SET s.CargoMintPodId = l.CargoMintLocationId
        FROM dbo.Fretrack_ShipmentService_Staging s
        JOIN dbo.Fretrack_Location_Master l
            ON s.PodId = l.LocationID
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.PodId IS NOT NULL;

        UPDATE dbo.Fretrack_ShipmentService_Staging
        SET
            IsSelectedForMigration = 1,
            MigrationRemarks = 'Service',
            ImportedOn = GETDATE()
        WHERE FretrackCargoId = @FretrackCargoId
          AND ISNULL(IsDeleted, 0) = 0;

        /* =========================================================
           4. Shipment service insert/update
           ========================================================= */
        INSERT INTO dbo.ShipmentServiceDetails
        (
            OrgId,
            ShipmentId,
            IsPrimary,
            LegTypeId,
            ServiceSequence,
            ServiceTypeId,
            TransportMode,
            TransportDirection,
            ConsolidationType,
            PickupLocId,
            PolId,
            PodId,
            DeliveryLocId,
            ServiceLocationId,
            VendorId,
            CarrierId,
            ETD,
            ETA,
            ServiceLegStatus,
            IsDeleted,
            TransportModeId,
            TransportDirectionId,
            ConsolidationTypeId,
            FretrackCargoID
        )
        SELECT
            s.OrgId,
            s.CargomintShipmentId,
            ISNULL(s.IsPrimary, 1),
            s.LegTypeId,
            ISNULL(s.ServiceSequence, 1),
            s.ServiceTypeId,
            s.TransportMode,
            s.TransportDirection,
            null ConsolidationType,
            s.CargoMintPickupLocId,
            s.CargoMintPolId,
            s.CargoMintPodId,
            s.CargoMintDeliveryLocId,
            null ServiceLocationId,
            NULL,
            s.CargoMintCarrierId,
            s.ETD,
            s.ETA,
            s.ServiceLegStatus,
            ISNULL(s.IsDeleted, 0),
            s.TransportModeId,
            s.TransportDirectionId,
            s.ConsolidationTypeId,
            s.FretrackCargoId
        FROM dbo.Fretrack_ShipmentService_Staging s
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.CargomintShipmentId IS NOT NULL
          AND s.ServiceTypeId IS NOT NULL
          AND s.TransportModeId IS NOT NULL
          AND ISNULL(s.IsDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentServiceDetails d
              WHERE d.FretrackCargoID = s.FretrackCargoId
                AND d.ServiceTypeId = s.ServiceTypeId
                AND ISNULL(d.ServiceSequence, 1) = ISNULL(s.ServiceSequence, 1)
          );

        UPDATE s
        SET s.CargoMintShipmentServiceId = d.ShipmentServiceId
        FROM dbo.Fretrack_ShipmentService_Staging s
        JOIN dbo.ShipmentServiceDetails d
            ON d.FretrackCargoID = s.FretrackCargoId
           AND d.ServiceTypeId = s.ServiceTypeId
           AND ISNULL(d.ServiceSequence, 1) = ISNULL(s.ServiceSequence, 1)
        WHERE s.FretrackCargoId = @FretrackCargoId;

        UPDATE d
        SET
            d.OrgId = s.OrgId,
            d.ShipmentId = s.CargomintShipmentId,
            d.IsPrimary = ISNULL(s.IsPrimary, 1),
            d.LegTypeId = s.LegTypeId,
            d.ServiceSequence = ISNULL(s.ServiceSequence, 1),
            d.ServiceTypeId = s.ServiceTypeId,
            d.TransportMode = s.TransportMode,
            d.TransportDirection = s.TransportDirection,
            d.ConsolidationType = NULL,
            d.PickupLocId = s.CargoMintPickupLocId,
            d.PolId = s.CargoMintPolId,
            d.PodId = s.CargoMintPodId,
            d.DeliveryLocId = s.CargoMintDeliveryLocId,
            d.ServiceLocationId = NULL,
            d.VendorId = NULL,
            d.CarrierId = s.CargoMintCarrierId,
            d.ETD = s.ETD,
            d.ETA = s.ETA,
            d.ServiceNotes = NULL,
            d.ServiceLegStatus = s.ServiceLegStatus,
            d.IsHouseRequired = NULL,
            d.IsContainerRequired = NULL,
            d.IsCustomsRequired = NULL,
            d.IsBookingRequired = NULL,
            d.IsConsolidation = s.IsConsolidation,
            d.QuotationId = NULL,
            d.QuotationServiceId = NULL,
            d.QuotationOptionId = NULL,
            d.CreatedBy = s.CargoMinCreatedById,
            d.CreatedDate = NULL,
            d.UpdatedBy = NULL,
            d.UpdatedDate = NULL,
            d.DeletedBy = NULL,
            d.DeletedDate = NULL,
            d.IsDeleted = ISNULL(s.IsDeleted, 0),
            d.TransportModeId = s.TransportModeId,
            d.TransportDirectionId = s.TransportDirectionId,
            d.ConsolidationTypeId = s.ConsolidationTypeId
        FROM dbo.ShipmentServiceDetails d
        JOIN dbo.Fretrack_ShipmentService_Staging s
            ON d.FretrackCargoID = s.FretrackCargoId
           AND d.ServiceTypeId = s.ServiceTypeId
           AND ISNULL(d.ServiceSequence, 1) = ISNULL(s.ServiceSequence, 1)
        WHERE s.FretrackCargoId = @FretrackCargoId;

        UPDATE sh
        SET sh.PrimaryServiceId = sd.ShipmentServiceId
        FROM dbo.Shipments sh
        JOIN dbo.ShipmentServiceDetails sd
            ON sh.ShipmentId = sd.ShipmentId
        WHERE sh.FretrackCargoId = @FretrackCargoId
          AND ISNULL(sd.IsPrimary, 0) = 1;

        UPDATE c
        SET c.PrimaryServiceId = sh.PrimaryServiceId
        FROM dbo.Fretrack_Cargo_Staging c
        JOIN dbo.Shipments sh
            ON c.CargoId = sh.FretrackCargoId
        WHERE c.CargoId = @FretrackCargoId;


        /* =========================================================
           6. Container staging + insert
           ========================================================= */
        UPDATE sc
        SET sc.CargoMintContainerTypeId = cp.CargoMintContainerTypeId
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Fretrack_Container_Package_Types_Master_Staging cp
            ON sc.ContainerTypeId = cp.PackageTypeID
        WHERE sc.FretrackCargoId = @FretrackCargoId
          AND cp.CargoMintContainerTypeId IS NOT NULL;

        UPDATE sc
        SET sc.CargomintShipmentId = sh.ShipmentId
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Shipments sh
            ON sc.FretrackCargoId = sh.FretrackCargoId
        WHERE sc.FretrackCargoId = @FretrackCargoId;

        UPDATE sc
        SET sc.CargoMintShipmentServiceId = ss.CargoMintShipmentServiceId
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Fretrack_ShipmentService_Staging ss
            ON sc.FretrackCargoId = ss.FretrackCargoId
           AND ss.IsPrimary = 1
        WHERE sc.FretrackCargoId = @FretrackCargoId;

        UPDATE sc
        SET sc.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sc.CreatedBy = u.FretrackUserId
        WHERE sc.FretrackCargoId = @FretrackCargoId;

        UPDATE sc
        SET sc.CargoMintUpdatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sc.ModifiedBy = u.FretrackUserId
        WHERE sc.FretrackCargoId = @FretrackCargoId;

        UPDATE sc
        SET sc.CargoMintDeletedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sc.DeletedBy = u.FretrackUserId
        WHERE sc.FretrackCargoId = @FretrackCargoId;

        UPDATE sc
        SET sc.CargoMintModifiedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentContainers_Staging sc
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sc.ModifiedBy = u.FretrackUserId
        WHERE sc.FretrackCargoId = @FretrackCargoId;

        UPDATE s
        SET
            s.IsSelectedForMigration = 1,
            s.MigrationRemarks = 'ShipmentContainers',
            s.ImportedOn = GETDATE()
        FROM dbo.Fretrack_ShipmentContainers_Staging s
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND ISNULL(s.IsDeleted, 0) = 0
          AND s.CargomintShipmentId IS NOT NULL
          AND s.CargoMintShipmentServiceId IS NOT NULL;

        INSERT INTO dbo.ShipmentContainers
        (
            ShipmentServiceId,
            ContainerTypeId,
            ContainerNo,
            Seal1,
            Seal2,
            OrgId,
            VgmWeight,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate,
            DeletedBy,
            DeletedDate,
            IsDeleted,
            FretrackCargoID,
            FretrackContainerID
        )
        SELECT
            s.CargoMintShipmentServiceId,
            s.CargoMintContainerTypeId,
            s.ContainerNo,
            s.Seal1,
            s.Seal2,
            s.OrgId,
            s.VgmWeight,
            s.CargoMintCreatedBy,
            s.CreatedDate,
            s.CargoMintModifiedBy,
            s.DateModified,
            s.CargoMintDeletedBy,
            s.DeletedDate,
            ISNULL(s.IsDeleted, 0),
            s.FretrackCargoId,
            s.FretrackContainerID
        FROM dbo.Fretrack_ShipmentContainers_Staging s
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.CargomintShipmentId IS NOT NULL
          AND s.CargoMintShipmentServiceId IS NOT NULL
          AND s.CargoMintContainerTypeId IS NOT NULL
          AND ISNULL(s.IsDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentContainers sc
              WHERE sc.FretrackContainerID = s.FretrackContainerID
          );

        UPDATE sc
        SET
            sc.ShipmentServiceId = s.CargoMintShipmentServiceId,
            sc.ContainerTypeId = s.CargoMintContainerTypeId,
            sc.ContainerNo = s.ContainerNo,
            sc.Seal1 = s.Seal1,
            sc.Seal2 = s.Seal2,
            sc.OrgId = s.OrgId,
            sc.VgmWeight = s.VgmWeight,
            sc.CreatedBy = s.CargoMintCreatedBy,
            sc.CreatedDate = s.CreatedDate,
            sc.UpdatedBy = s.CargoMintModifiedBy,
            sc.UpdatedDate = s.DateModified,
            sc.DeletedBy = s.CargoMintDeletedBy,
            sc.DeletedDate = s.DeletedDate,
            sc.IsDeleted = ISNULL(s.IsDeleted, 0),
            sc.FretrackCargoID = s.FretrackCargoId
        FROM dbo.ShipmentContainers sc
        JOIN dbo.Fretrack_ShipmentContainers_Staging s
            ON sc.FretrackContainerID = s.FretrackContainerID
        WHERE s.FretrackCargoId = @FretrackCargoId;

        /* =========================================================
           7. Package staging + insert
           ========================================================= */
        UPDATE sp
        SET sp.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sp.CreatedBy = u.FretrackUserId
        WHERE sp.FretrackCargoID = @FretrackCargoId;

        UPDATE sp
        SET sp.CargoMintModifiedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sp.ModifiedBy = u.FretrackUserId
        WHERE sp.FretrackCargoID = @FretrackCargoId;

        UPDATE sp
        SET sp.CargoMintDeletedBy = u.CargoMintUserID
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON sp.DeletedBy = u.FretrackUserId
        WHERE sp.FretrackCargoID = @FretrackCargoId;

        UPDATE dbo.Fretrack_ShipmentPackages_Staging
        SET
            IsSelectedForMigration = 1,
            MigrationRemarks = 'ShipmentPackages',
            ImportedOn = GETDATE()
        WHERE FretrackCargoID = @FretrackCargoId
          AND ISNULL(isDeleted, 0) = 0;

        UPDATE sp
        SET sp.CargomintShipmentId = sh.ShipmentId
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.Shipments sh
            ON sp.FretrackCargoID = sh.FretrackCargoId
        WHERE sp.FretrackCargoID = @FretrackCargoId;

        UPDATE sp
        SET sp.CargoMintShipmentServiceId = ssd.ShipmentServiceId
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.ShipmentServiceDetails ssd
            ON sp.FretrackCargoID = ssd.FretrackCargoID
        WHERE sp.FretrackCargoID = @FretrackCargoId;

        UPDATE sp
        SET sp.CargoMintContainerId = sc.ContainerID
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.ShipmentContainers sc
            ON sp.ContainerID = sc.FretrackContainerID
        WHERE sp.FretrackCargoID = @FretrackCargoId;

        UPDATE sp
        SET sp.CargoMintPackageTypeId = pt.CargoMintPackageTypeId
        FROM dbo.Fretrack_ShipmentPackages_Staging sp
        JOIN dbo.Fretrack_Container_Package_Types_Master_Staging pt
            ON sp.FretrackPackTypeID = pt.PackageTypeID
        WHERE sp.FretrackCargoID = @FretrackCargoId
          AND pt.CargoMintPackageTypeId IS NOT NULL;

        INSERT INTO dbo.ShipmentPackages
        (
            OrgId,
            ShipmentServiceId,
            PackageTypeId,
            PackageCount,
            PackageLength,
            PackageWidth,
            PackageHeight,
            NetWeight,
            GrossWeight,
            VolumeWeight,
            VolumeCBM,
            IsMetric,
            PackageDescription,
            MarksAndNumbers,
            InvoiceNo,
            InvoiceDate,
            SBNo,
            SBDate,
            IsPerPackage,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate,
            DeletedBy,
            DeletedDate,
            IsDeleted,
            FretrackCargoId,
            FretrackPackageId
        )
        SELECT
            s.OrgId,
            s.CargoMintShipmentServiceId,
            s.CargoMintPackageTypeId,
            s.PackageCount,
            s.Length,
            s.Width,
            s.Height,
            s.NetWeight,
            s.GrossWeight,
            s.VolumeWeight,
            s.Volume,
            1,
            s.PackageDescription,
            s.MarksAndNumbers,
            s.InvoiceNumber,
            s.InvoiceDate,
            s.SBNo,
            s.SBDate,
            s.isPerPackage,
            s.CargoMintCreatedBy,
            s.DateCreated,
            s.CargoMintModifiedBy,
            s.DateModified,
            s.CargoMintDeletedBy,
            s.DateDeleted,
            ISNULL(s.isDeleted, 0),
            s.FretrackCargoId,
            s.FretrackPackId
        FROM dbo.Fretrack_ShipmentPackages_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CargoMintShipmentServiceId IS NOT NULL
          AND s.CargoMintPackageTypeId IS NOT NULL
          AND ISNULL(s.isDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentPackages p
              WHERE p.FretrackPackageId = s.FretrackPackId
          );

        UPDATE p
        SET
            p.OrgId = s.OrgId,
            p.ShipmentServiceId = s.CargoMintShipmentServiceId,
            p.PackageTypeId = s.CargoMintPackageTypeId,
            p.PackageCount = s.PackageCount,
            p.PackageLength = s.Length,
            p.PackageWidth = s.Width,
            p.PackageHeight = s.Height,
            p.NetWeight = s.NetWeight,
            p.GrossWeight = s.GrossWeight,
            p.VolumeWeight = s.VolumeWeight,
            p.VolumeCBM = s.Volume,
            p.IsMetric = 1,
            p.PackageDescription = s.PackageDescription,
            p.MarksAndNumbers = s.MarksAndNumbers,
            p.InvoiceNo = s.InvoiceNumber,
            p.InvoiceDate = s.InvoiceDate,
            p.SBNo = s.SBNo,
            p.SBDate = s.SBDate,
            p.IsPerPackage = s.isPerPackage,
            p.CreatedBy = s.CargoMintCreatedBy,
            p.CreatedDate = s.DateCreated,
            p.UpdatedBy = s.CargoMintModifiedBy,
            p.UpdatedDate = s.DateModified,
            p.DeletedBy = s.CargoMintDeletedBy,
            p.DeletedDate = s.DateDeleted,
            p.IsDeleted = ISNULL(s.isDeleted, 0),
            p.FretrackCargoId = s.FretrackCargoId
        FROM dbo.ShipmentPackages p
        JOIN dbo.Fretrack_ShipmentPackages_Staging s
            ON p.FretrackPackageId = s.FretrackPackId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        /* =========================================================
           8. Routing staging + insert
           ========================================================= */
        UPDATE s
        SET s.CargomintShipmentId = sh.ShipmentId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.Shipments sh
            ON s.FretrackCargoID = sh.FretrackCargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintShipmentServiceId = sd.ShipmentServiceId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.ShipmentServiceDetails sd
            ON s.FretrackCargoID = sd.FretrackCargoID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintPOLID = lm.CargoMintLocationId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.Fretrack_Location_Master lm
            ON s.POLID = lm.LocationID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintPODID = lm.CargoMintLocationId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.Fretrack_Location_Master lm
            ON s.PODID = lm.LocationID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintCarrierId = a.CargoMintCarrierId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.Fretrack_AirlineMaster_Staging a
            ON s.CarrierID = a.AirlineID
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND UPPER(LTRIM(RTRIM(s.RoutingMode))) = 'AIR'
          AND a.CargoMintCarrierId IS NOT NULL;

        UPDATE s
        SET s.CargoMintCarrierId = o.CargoMintCarrierId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.Fretrack_OceanLineMaster_Staging o
            ON s.CarrierID = o.OceanLineID
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND UPPER(LTRIM(RTRIM(s.RoutingMode))) = 'OCEAN'
          AND o.CargoMintCarrierId IS NOT NULL;

        UPDATE s
        SET s.CargoMintTransportModeId = m.CargoMintTransportModeId
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.Fretrack_Service_Mode_Mapping m
            ON UPPER(LTRIM(RTRIM(s.JobTypeName))) = UPPER(LTRIM(RTRIM(m.JobTypeName)))
           AND UPPER(LTRIM(RTRIM(s.ModeOfTransport))) = UPPER(LTRIM(RTRIM(m.ModeOfTransport)))
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintTransportMode = tm.ModeName
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        JOIN dbo.TransportModes tm
            ON s.CargoMintTransportModeId = tm.ModeId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CargoMintTransportModeId IS NOT NULL;

        UPDATE r
        SET
            r.IsSelectedForMigration = 1,
            r.ImportedOn = GETDATE(),
            r.MigrationRemarks = 'ShipmentRouting Inserted'
        FROM dbo.Fretrack_Shipment_Routing_Staging_New r
        WHERE r.FretrackCargoID = @FretrackCargoId
          AND r.CargomintShipmentId IS NOT NULL
          AND r.CargoMintShipmentServiceId IS NOT NULL
          AND ISNULL(r.IsDeleted, 0) = 0;

        INSERT INTO dbo.ShipmentRouting
        (
            OrgId,
            ShipmentServiceId,
            LegNumber,
            LegTypeId,
            TransportMode,
            FromLocationId,
            ToLocationId,
            FromTerminal,
            ToTerminal,
            ETD,
            ETA,
            CarrierId,
            BookingNo,
            BookingDate,
            VesselName,
            VoyageNumber,
            FlightNumber,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsDeleted,
            DeletedDate,
            DeletedBy,
            ShipmentId,
            transportModeId,
            FretrackRoutingID,
            FretrackCargoID
        )
        SELECT
            s.OrgId,
            s.CargoMintShipmentServiceId,
            ISNULL(s.RoutingOrder, 1),
            2,
            s.CargoMintTransportMode,
            s.CargoMintPOLID,
            s.CargoMintPODID,
            NULL,
            NULL,
            s.ETD,
            s.ETA,
            s.CargoMintCarrierId,
            s.BookingNumber,
            CAST(s.BookingDate AS date),
            s.VesselName,
            CASE WHEN UPPER(LTRIM(RTRIM(s.RoutingMode))) = 'OCEAN' THEN s.FlightOrVoyageNo ELSE NULL END,
            CASE WHEN UPPER(LTRIM(RTRIM(s.RoutingMode))) = 'AIR' THEN s.FlightOrVoyageNo ELSE NULL END,
            GETDATE(),
            NULL,
            NULL,
            NULL,
            ISNULL(s.IsDeleted, 0),
            NULL,
            NULL,
            s.CargomintShipmentId,
            s.CargoMintTransportModeId,
            s.FretrackRoutingID,
            s.FretrackCargoID
        FROM dbo.Fretrack_Shipment_Routing_Staging_New s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND ISNULL(s.IsDeleted, 0) = 0
          AND ISNULL(s.IsSelectedForMigration, 1) = 1
          AND s.CargomintShipmentId IS NOT NULL
          AND s.CargoMintShipmentServiceId IS NOT NULL
          AND s.CargoMintPOLID IS NOT NULL
          AND s.CargoMintPODID IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentRouting r
              WHERE r.FretrackRoutingID = s.FretrackRoutingID
          );

        UPDATE r
        SET
            r.OrgId = s.OrgId,
            r.ShipmentServiceId = s.CargoMintShipmentServiceId,
            r.LegNumber = ISNULL(s.RoutingOrder, 1),
            r.LegTypeId = 2,
            r.TransportMode = s.CargoMintTransportMode,
            r.FromLocationId = s.CargoMintPOLID,
            r.ToLocationId = s.CargoMintPODID,
            r.ETD = s.ETD,
            r.ETA = s.ETA,
            r.CarrierId = s.CargoMintCarrierId,
            r.BookingNo = s.BookingNumber,
            r.BookingDate = CAST(s.BookingDate AS date),
            r.VesselName = s.VesselName,
            r.VoyageNumber = CASE WHEN UPPER(LTRIM(RTRIM(s.RoutingMode))) = 'OCEAN' THEN s.FlightOrVoyageNo ELSE NULL END,
            r.FlightNumber = CASE WHEN UPPER(LTRIM(RTRIM(s.RoutingMode))) = 'AIR' THEN s.FlightOrVoyageNo ELSE NULL END,
            r.CreatedDate = GETDATE(),
            r.IsDeleted = ISNULL(s.IsDeleted, 0),
            r.ShipmentId = s.CargomintShipmentId,
            r.transportModeId = s.CargoMintTransportModeId,
            r.FretrackCargoID = s.FretrackCargoID
        FROM dbo.ShipmentRouting r
        JOIN dbo.Fretrack_Shipment_Routing_Staging_New s
            ON r.FretrackRoutingID = s.FretrackRoutingID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        /* =========================================================
           9. Cargo entities staging + insert
           PartyName is refreshed again after insert from CompanyAddress.
           ========================================================= */
        UPDATE s
        SET s.CargoMintCompanyId = c.CompanyId
        FROM dbo.Fretrack_CargoEntities_Staging s
        JOIN dbo.Companies c
            ON ',' + c.FretrackCompanyIDs + ',' LIKE '%,' + CAST(s.FretrackCompanyId AS varchar(20)) + ',%'
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.FretrackCompanyId IS NOT NULL;

        UPDATE s
        SET s.CargoMintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_CargoEntities_Staging s
        JOIN dbo.Shipments sh
            ON s.FretrackCargoID = sh.FretrackCargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.EntityDisplayText =
            LTRIM(RTRIM(
                ISNULL(s.EntityDisplayText1, '') +
                CASE
                    WHEN ISNULL(s.EntityDisplayText1, '') <> '' AND ISNULL(s.EntityDisplayText2, '') <> '' THEN ' '
                    ELSE ''
                END +
                ISNULL(s.EntityDisplayText2, '')
            ))
        FROM dbo.Fretrack_CargoEntities_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_CargoEntities_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.CreatedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE f
        SET f.CargoMintEntityTypeID = r.RoleId
        FROM dbo.Fretrack_EntityType_Master_Staging f
        JOIN dbo.ShipmentPartyRoles r
            ON UPPER(LTRIM(RTRIM(f.EntityTypeName))) = UPPER(LTRIM(RTRIM(r.RoleName)))
        WHERE f.CargoMintEntityTypeID IS NULL;

        UPDATE c
        SET c.CargoMintEntityTypeID = e.CargoMintEntityTypeID
        FROM dbo.Fretrack_CargoEntities_Staging c
        JOIN dbo.Fretrack_EntityType_Master_Staging e
            ON c.EntityTypeID = e.EntityTypeID
        WHERE c.FretrackCargoID = @FretrackCargoId
          AND e.CargoMintEntityTypeID IS NOT NULL;

        UPDATE s
        SET s.CargoMintEntityAddressID = ca.CompanyAddressId
        FROM dbo.Fretrack_CargoEntities_Staging s
        JOIN dbo.CompanyAddress ca
            ON s.CargoMintCompanyId = ca.CompanyId
           AND s.FretrackEntityAddressID = ca.FretrackAddressId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CargoMintCompanyId IS NOT NULL
          AND s.FretrackEntityAddressID IS NOT NULL;
        
        UPDATE s
        SET s.CargoMintEntityAddressID = NULL
        FROM dbo.Fretrack_CargoEntities_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CargoMintEntityAddressID IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.CompanyAddress ca
              WHERE ca.CompanyAddressId = s.CargoMintEntityAddressID
          );

        UPDATE c
        SET c.CargoMintCountryID = f.CargoMintCountryID
        FROM dbo.Fretrack_CargoEntities_Staging c
        JOIN dbo.Fretrack_CountryMaster_Staging f
            ON c.EntityCountryID = f.FretrackCountryId
        WHERE c.FretrackCargoID = @FretrackCargoId
          AND c.EntityCountryID IS NOT NULL;

        UPDATE c
        SET c.CargoMintStateID = s.CargoMintStateID
        FROM dbo.Fretrack_CargoEntities_Staging c
        JOIN dbo.Fretrack_StateMaster_Staging s
            ON c.EntityStateID = s.StateID
        WHERE c.FretrackCargoID = @FretrackCargoId
          AND c.EntityStateID IS NOT NULL;

        UPDATE dbo.Fretrack_CargoEntities_Staging
        SET
            IsSelectedForMigration = 1,
            ImportedOn = GETDATE(),
            MigrationRemarks = 'CargoEntities Inserted'
        WHERE FretrackCargoID = @FretrackCargoId
          AND CargoMintShipmentID IS NOT NULL
          AND ISNULL(isDeleted, 0) = 0;

        INSERT INTO dbo.ShipmentParties
        (
            OrgId,
            ShipmentId,
            RoleId,
            PartyRole,
            CompanyID,
            CompanyAddressId,
            PartyName,
            AddressLine1,
            AddressLine2,
            City,
            StateId,
            CountryId,
            ZipCode,
            Phone,
            Email,
            GSTNumber,
            DisplayAddress,
            CreatedDate,
            CreatedBy,
            IsDeleted,
            FretrackCargoID,
            FretrackCargoEntityID
        )
        SELECT
            s.OrgId,
            s.CargoMintShipmentID,
            s.CargoMintEntityTypeID,
            s.EntityRole,
            s.CargoMintCompanyId,
            s.CargoMintEntityAddressID,
            NULL,
            s.EntityAddressLine1,
            NULL,
            s.EntityCity,
            s.CargoMintStateID,
            s.CargoMintCountryID,
            s.EntityZipCode,
            NULL,
            NULL,
            NULL,
            s.EntityDisplayText,
            s.DateCreated,
            s.CargoMintCreatedBy,
            0,
            s.FretrackCargoID,
            s.FretrackCargoEntityID
        FROM dbo.Fretrack_CargoEntities_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.IsSelectedForMigration = 1
          AND s.CargoMintShipmentID IS NOT NULL
          AND s.CargoMintEntityTypeID IS NOT NULL
          AND ISNULL(s.isDeleted, 0) = 0
          AND EXISTS
          (
              SELECT 1
              FROM dbo.Shipments sh
              WHERE sh.ShipmentId = s.CargoMintShipmentID
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentParties sp
              WHERE sp.FretrackCargoEntityID = s.FretrackCargoEntityID
          );

        UPDATE sp
        SET sp.PartyName = ca.AddressLine2
        FROM dbo.ShipmentParties sp
        JOIN dbo.CompanyAddress ca
            ON sp.CompanyAddressId = ca.CompanyAddressId
        WHERE sp.FretrackCargoID = @FretrackCargoId
          AND ca.AddressLine2 IS NOT NULL;

        UPDATE sp
        SET
            sp.OrgId = s.OrgId,
            sp.ShipmentId = s.CargoMintShipmentID,
            sp.RoleId = s.CargoMintEntityTypeID,
            sp.PartyRole = s.EntityRole,
            sp.CompanyID = s.CargoMintCompanyId,
            sp.CompanyAddressId = s.CargoMintEntityAddressID,
            sp.AddressLine1 = s.EntityAddressLine1,
            sp.City = s.EntityCity,
            sp.StateId = s.CargoMintStateID,
            sp.CountryId = s.CargoMintCountryID,
            sp.ZipCode = s.EntityZipCode,
            sp.DisplayAddress = s.EntityDisplayText,
            sp.CreatedDate = s.DateCreated,
            sp.CreatedBy = s.CargoMintCreatedBy,
            sp.IsDeleted = 0,
            sp.FretrackCargoID = s.FretrackCargoID
        FROM dbo.ShipmentParties sp
        JOIN dbo.Fretrack_CargoEntities_Staging s
            ON sp.FretrackCargoEntityID = s.FretrackCargoEntityID
        WHERE s.FretrackCargoID = @FretrackCargoId;


        /* =========================================================
           10. Shipment charges staging updates
           ========================================================= */
        UPDATE cmm
        SET cmm.CargomintShipmentId = sh.ShipmentId
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Shipments sh
            ON cmm.FretrackCargoId = sh.FretrackCargoId
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintShipmentServiceId = sd.ShipmentServiceId
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.ShipmentServiceDetails sd
            ON cmm.CargomintShipmentId = sd.ShipmentId
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintChargeItemId = cm.CargoMintChargeItemID
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Fretrack_Staging_CC_CM_Mapped cm
            ON UPPER(LTRIM(RTRIM(cmm.FretrackChargeDescription))) = UPPER(LTRIM(RTRIM(cm.FretrackChargeItemName)))
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintChargeDescription = cm.CargoMintChargeItem
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Fretrack_Staging_CC_CM_Mapped cm
            ON cmm.CargomintChargeItemId = cm.CargoMintChargeItemID
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE s
        SET s.CargomintApplyPerId = m.ApplyPerId
        FROM dbo.Fretrack_ShipmentCharges_Staging s
        JOIN
        (
            VALUES
                ('PKG', 29), ('SHEET', 30), ('DAY', 27), ('LABOUR', 31),
                ('CBM', 1), ('AWB', 8), ('CNTR', 10), ('ENTRY', 32),
                ('MONTH', 24), ('PLT', 33), ('KGS', 2), ('HOUR', 34),
                ('VEHICLE', 19), ('MT', 35), ('BOX', 36), ('PIECE', 3),
                ('INVOICE', 38), ('BL', 37), ('SQ FT', 28), ('INV', 38),
                ('DOC', 20), ('TRIP', 22), ('SHPT', 9), ('CRTN', 39)
        ) m(FretrackApplyPer, ApplyPerId)
            ON UPPER(LTRIM(RTRIM(s.FretrackApplyPer))) = UPPER(LTRIM(RTRIM(m.FretrackApplyPer)))
        WHERE s.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintSellCurrencyId = cm.CurrencyId
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Currencies cm
            ON UPPER(LTRIM(RTRIM(cmm.FretrackSellCurrencyCode))) = UPPER(LTRIM(RTRIM(cm.CurrencyCode)))
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintBuyCurrencyId = cm.CurrencyId
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Currencies cm
            ON UPPER(LTRIM(RTRIM(cmm.FretrackBuyCurrencyCode))) = UPPER(LTRIM(RTRIM(cm.CurrencyCode)))
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintBuyTaxId =
            CASE
                WHEN TRY_CAST(cmm.FretrackBuyTaxPercent AS decimal(18,4)) = 18 THEN 8
                ELSE tg.TaxGroupId
            END
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        LEFT JOIN dbo.TaxGroups tg
            ON TRY_CAST(cmm.FretrackBuyTaxPercent AS decimal(18,4)) = TRY_CAST(tg.TotalRate AS decimal(18,4))
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintSellTaxId =
            CASE
                WHEN TRY_CAST(cmm.FretrackSellTaxPercent AS decimal(18,4)) = 18 THEN 8
                ELSE tg.TaxGroupId
            END
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        LEFT JOIN dbo.TaxGroups tg
            ON TRY_CAST(cmm.FretrackSellTaxPercent AS decimal(18,4)) = TRY_CAST(tg.TotalRate AS decimal(18,4))
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE s
        SET s.CargomintInvoiceToId = c.CompanyId
        FROM dbo.Fretrack_ShipmentCharges_Staging s
        JOIN dbo.Companies c
            ON ',' + c.FretrackCompanyIDs + ',' LIKE '%,' + CAST(s.FretrackInvoiceToId AS varchar(20)) + ',%'
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.FretrackInvoiceToId IS NOT NULL;

        UPDATE s
        SET s.CargomintBillToId = c.CompanyId
        FROM dbo.Fretrack_ShipmentCharges_Staging s
        JOIN dbo.Companies c
            ON ',' + c.FretrackCompanyIDs + ',' LIKE '%,' + CAST(s.FretrackBillToId AS varchar(20)) + ',%'
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.FretrackBillToId IS NOT NULL;

        UPDATE dbo.Fretrack_ShipmentCharges_Staging
        SET FretrackSellTaxAmount =
            ROUND(ISNULL(FretrackSellSubtotal, 0) * ISNULL(FretrackSellTaxPercent, 0) / 100.0, 2)
        WHERE FretrackCargoId = @FretrackCargoId;

        UPDATE dbo.Fretrack_ShipmentCharges_Staging
        SET FretrackBuyTaxAmount =
            ROUND(ISNULL(FretrackBuySubtotal, 0) * ISNULL(FretrackBuyTaxPercent, 0) / 100.0, 2)
        WHERE FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintCreatedBy = u.Id
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Users u
            ON cmm.FretrackCreatedBy = u.FretrackUserID
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintUpdatedBy = u.Id
        FROM dbo.Fretrack_ShipmentCharges_Staging cmm
        JOIN dbo.Users u
            ON cmm.FretrackUpdatedBy = u.FretrackUserID
        WHERE cmm.FretrackCargoId = @FretrackCargoId;

        /* =========================================================
           
           11. Shipment charges insert/update
           ========================================================= */
        INSERT INTO dbo.ShipmentCharges
        (
            OrgId,
            ShipmentId,
            ShipmentServiceId,
            ChargeItemId,
            HsnCode,
            ChargeDescription,
            ApplyPerId,
            SellCurrencyId,
            SellExRate,
            SellRate,
            SellQuantity,
            SellSubtotal,
            SellTaxId,
            SellTaxPercent,
            SellTaxAmount,
            SellTotalAmount,
            SellLineItemId,
            BuyCurrencyId,
            BuyExRate,
            BuyRate,
            BuyQuantity,
            BuySubtotal,
            BuyTaxId,
            BuyTaxPercent,
            BuyTaxAmount,
            BuyTotalAmount,
            BuyLineItemId,
            InvoiceToId,
            BillToId,
            IsAllIn,
            Remarks,
            SortOrder,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate,
            IsDeleted,
            DeletedBy,
            DeletedDate
        )
        SELECT
            s.OrgId,
            s.CargomintShipmentId,
            s.CargomintShipmentServiceId,
            s.CargomintChargeItemId,
            s.CargomintHsnCode,
            COALESCE(NULLIF(LTRIM(RTRIM(s.FretrackChargeDescription)), ''), s.FretrackChargeDescription),
            s.CargomintApplyPerId,
            s.CargomintSellCurrencyId,
            ISNULL(s.FretrackSellExRate, 1),
            ISNULL(s.FretrackSellRate, 0),
            ISNULL(s.FretrackSellQuantity, 0),
            ISNULL(s.FretrackSellSubtotal, 0),
            s.CargomintSellTaxId,
            ISNULL(s.FretrackSellTaxPercent, 0),
            ISNULL(s.FretrackSellTaxAmount, 0),
            ISNULL(s.FretrackSellTotalAmount, 0),
            s.CargomintSellLineItemId,
            s.CargomintBuyCurrencyId,
            ISNULL(s.FretrackBuyExRate, 1),
            ISNULL(s.FretrackBuyRate, 0),
            ISNULL(s.FretrackBuyQuantity, 0),
            ISNULL(s.FretrackBuySubtotal, 0),
            s.CargomintBuyTaxId,
            ISNULL(s.FretrackBuyTaxPercent, 0),
            ISNULL(s.FretrackBuyTaxAmount, 0),
            ISNULL(s.FretrackBuyTotalAmount, 0),
            s.CargomintBuyLineItemId,
            s.CargomintInvoiceToId,
            s.CargomintBillToId,
            0,
            CONCAT(
                'Fretrack CargoID: ', CAST(s.FretrackCargoId AS varchar(20)),
                '; Income ChargeID: ', ISNULL(CAST(s.FretrackIncomeChargeId AS varchar(20)), 'NULL'),
                '; Expense ChargeID: ', ISNULL(CAST(s.FretrackExpenseChargeId AS varchar(20)), 'NULL')
            ),
            ISNULL(s.FretrackLineNumber, 0),
            s.CargomintCreatedBy,
            ISNULL(s.FretrackCreatedDate, GETDATE()),
            s.CargomintUpdatedBy,
            s.FretrackUpdatedDate,
            0,
            NULL,
            NULL
        FROM dbo.Fretrack_ShipmentCharges_Staging s
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.CargomintShipmentId IS NOT NULL
          AND s.CargomintShipmentServiceId IS NOT NULL
          AND s.CargomintChargeItemId IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentCharges sc
              WHERE sc.ShipmentId = s.CargomintShipmentId
                AND sc.ShipmentServiceId = s.CargomintShipmentServiceId
                AND sc.ChargeItemId = s.CargomintChargeItemId
                AND ISNULL(sc.SortOrder, 0) = ISNULL(s.FretrackLineNumber, 0)
                AND ISNULL(sc.IsDeleted, 0) = 0
          );

        UPDATE sc
        SET
            sc.OrgId = s.OrgId,
            sc.ShipmentId = s.CargomintShipmentId,
            sc.ShipmentServiceId = s.CargomintShipmentServiceId,
            sc.ChargeItemId = s.CargomintChargeItemId,
            sc.HsnCode = s.CargomintHsnCode,
            sc.ChargeDescription = COALESCE(NULLIF(LTRIM(RTRIM(s.FretrackChargeDescription)), ''), s.FretrackChargeDescription),
            sc.ApplyPerId = s.CargomintApplyPerId,
            sc.SellCurrencyId = s.CargomintSellCurrencyId,
            sc.SellExRate = ISNULL(s.FretrackSellExRate, 1),
            sc.SellRate = ISNULL(s.FretrackSellRate, 0),
            sc.SellQuantity = ISNULL(s.FretrackSellQuantity, 0),
            sc.SellSubtotal = ISNULL(s.FretrackSellSubtotal, 0),
            sc.SellTaxId = s.CargomintSellTaxId,
            sc.SellTaxPercent = ISNULL(s.FretrackSellTaxPercent, 0),
            sc.SellTaxAmount = ISNULL(s.FretrackSellTaxAmount, 0),
            sc.SellTotalAmount = ISNULL(s.FretrackSellTotalAmount, 0),
            sc.SellLineItemId = s.CargomintSellLineItemId,
            sc.BuyCurrencyId = s.CargomintBuyCurrencyId,
            sc.BuyExRate = ISNULL(s.FretrackBuyExRate, 1),
            sc.BuyRate = ISNULL(s.FretrackBuyRate, 0),
            sc.BuyQuantity = ISNULL(s.FretrackBuyQuantity, 0),
            sc.BuySubtotal = ISNULL(s.FretrackBuySubtotal, 0),
            sc.BuyTaxId = s.CargomintBuyTaxId,
            sc.BuyTaxPercent = ISNULL(s.FretrackBuyTaxPercent, 0),
            sc.BuyTaxAmount = ISNULL(s.FretrackBuyTaxAmount, 0),
            sc.BuyTotalAmount = ISNULL(s.FretrackBuyTotalAmount, 0),
            sc.BuyLineItemId = s.CargomintBuyLineItemId,
            sc.InvoiceToId = s.CargomintInvoiceToId,
            sc.BillToId = s.CargomintBillToId,
            sc.IsAllIn = 0,
            sc.Remarks = CONCAT(
                'Fretrack CargoID: ', CAST(s.FretrackCargoId AS varchar(20)),
                '; Income ChargeID: ', ISNULL(CAST(s.FretrackIncomeChargeId AS varchar(20)), 'NULL'),
                '; Expense ChargeID: ', ISNULL(CAST(s.FretrackExpenseChargeId AS varchar(20)), 'NULL')
            ),
            sc.SortOrder = ISNULL(s.FretrackLineNumber, 0),
            sc.CreatedBy = s.CargomintCreatedBy,
            sc.CreatedDate = ISNULL(s.FretrackCreatedDate, GETDATE()),
            sc.UpdatedBy = s.CargomintUpdatedBy,
            sc.UpdatedDate = s.FretrackUpdatedDate,
            sc.IsDeleted = 0,
            sc.DeletedBy = NULL,
            sc.DeletedDate = NULL
        FROM dbo.ShipmentCharges sc
        JOIN dbo.Fretrack_ShipmentCharges_Staging s
            ON sc.ShipmentId = s.CargomintShipmentId
           AND sc.ShipmentServiceId = s.CargomintShipmentServiceId
           AND sc.ChargeItemId = s.CargomintChargeItemId
           AND ISNULL(sc.SortOrder, 0) = ISNULL(s.FretrackLineNumber, 0)
        WHERE s.FretrackCargoId = @FretrackCargoId
          AND s.CargomintShipmentId IS NOT NULL
          AND s.CargomintShipmentServiceId IS NOT NULL
          AND s.CargomintChargeItemId IS NOT NULL
          AND ISNULL(sc.IsDeleted, 0) = 0;


        /* =========================================================
           5. Service vendor update from ShipmentCharges
           ========================================================= */
        ;WITH VendorPick AS
        (
            SELECT
                sc.ShipmentServiceId,
                sc.BillToId,
                sc.ChargeDescription,
                ssd.ServiceTypeId,
                ROW_NUMBER() OVER
                (
                    PARTITION BY sc.ShipmentServiceId
                    ORDER BY
                        CASE
                            WHEN ssd.ServiceTypeId = 7
                                 AND UPPER(LTRIM(RTRIM(sc.ChargeDescription))) LIKE '%CUSTOM%' THEN 1
                            WHEN ssd.ServiceTypeId = 4
                                 AND UPPER(LTRIM(RTRIM(sc.ChargeDescription))) LIKE '%TRANSPORT%' THEN 1
                            WHEN ssd.ServiceTypeId IN (2, 3)
                                 AND UPPER(LTRIM(RTRIM(sc.ChargeDescription))) LIKE '%OCEAN%' THEN 1
                            WHEN ssd.ServiceTypeId = 1
                                 AND UPPER(LTRIM(RTRIM(sc.ChargeDescription))) LIKE '%AIR%' THEN 1
                            ELSE 2
                        END,
                        sc.ChargeId
                ) AS rn
            FROM dbo.ShipmentCharges sc
            JOIN dbo.ShipmentServiceDetails ssd
                ON sc.ShipmentServiceId = ssd.ShipmentServiceId
            JOIN dbo.Shipments sh
                ON ssd.ShipmentId = sh.ShipmentId
            WHERE sh.FretrackCargoId = @FretrackCargoId
              AND sc.BillToId IS NOT NULL
              AND ISNULL(sc.IsDeleted, 0) = 0
        )
        UPDATE ssd
        SET ssd.VendorId = vp.BillToId
        FROM dbo.ShipmentServiceDetails ssd
        JOIN VendorPick vp
            ON ssd.ShipmentServiceId = vp.ShipmentServiceId
           AND vp.rn = 1
        WHERE (ssd.VendorId IS NULL OR ssd.VendorId = 0);
        
        /* =========================================================
           12. Invoice staging + insert
           CompanyGSTIN, GstTreatment, PaymentTermId, and SourceOfSupply
           are refreshed after insert from CompanyAddress / CompanyGstDetails.
           ========================================================= */
        UPDATE s
        SET s.CargoMintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.Shipments sh
            ON s.FretrackCargoID = sh.FretrackCargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintShipmentServiceID = sd.ShipmentServiceId
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.ShipmentServiceDetails sd
            ON s.FretrackCargoID = sd.FretrackCargoID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintPayingPartyID = ca.CompanyId
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.CompanyAddress ca
            ON s.PayingPartyID = ca.FretrackAddressId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.PayingPartyID IS NOT NULL;

        UPDATE s
        SET s.CargoMintPayingPartyAddressID = ca.CompanyAddressId
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.CompanyAddress ca
            ON s.CargoMintPayingPartyID = ca.CompanyId
           AND s.PayingPartyAddressID = ca.FretrackAddressId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CargoMintPayingPartyID IS NOT NULL
          AND s.PayingPartyAddressID IS NOT NULL;

        UPDATE s
        SET s.CargoMintCurrencyID = c.CargoMintCurrencyID
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.Fretrack_CurrencyMaster_Staging c
            ON s.CurrencyID = c.FretrackCurrencyID
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CurrencyID IS NOT NULL;

        UPDATE s
        SET s.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.CreatedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintLockedBy = u.CargoMintUserID
        FROM dbo.Fretrack_Invoices_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.LockedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintInvoiceType =
            CASE
                WHEN UPPER(LTRIM(RTRIM(s.InvoiceType))) = 'INCOME' THEN 'Invoice'
                WHEN UPPER(LTRIM(RTRIM(s.InvoiceType))) = 'EXPENSE' THEN 'Credit Note'
                ELSE s.CargoMintInvoiceType
            END
        FROM dbo.Fretrack_Invoices_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.InvoiceType IS NOT NULL;
        
         

        UPDATE dbo.Fretrack_Invoices_Staging
        SET
            IsSelectedForMigration = 1,
            MigrationRemarks = 'Invoices',
            ImportedOn = GETDATE()
        WHERE FretrackCargoID = @FretrackCargoId
          AND ISNULL(isDeleted, 0) = 0;

        INSERT INTO dbo.Invoices
        (
            OrgId,
            ShipmentId,
            InvoiceNumber,
            InvoiceDate,
            DueDate,
            InvoiceType,
            BranchId,
            SourceOfSupply,
            CompanyId,
            CompanyAddressId,
            CompanyGSTIN,
            GstTreatment,
            PlaceOfSupply,
            JobNo,
            PaymentTermId,
            CurrencyId,
            ExRate,
            TotalAmount,
            TaxAmount,
            LocalCurrencyId,
            TotalAmountLocal,
            TaxAmountLocal,
            InvoiceStatus,
            Notes,
            ServiceType,
            TransportMode,
            TransDirection,
            MasterNo,
            HouseNo,
            VesselFlightDetails,
            Cycle,
            CargoType,
            FreightStatus,
            Pol,
            Pod,
            FinalDestination,
            ShipperInvoiceDetails,
            ContainerWeightDetails,
            IsLocked,
            LockedById,
            DateLocked,
            IsSentToParty,
            SentById,
            SentDate,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsDeleted,
            DeletedDate,
            DeletedBy,
            ShipmentServiceId,
            DocumentPath,
            ExternalCode,
            AmountPaid,
            AmountDue,
            PaymentStatus,
            FretrackCargoID,
            FretrackInvoiceID
        )
        SELECT
            s.OrgId,
            s.CargoMintShipmentID,
            s.InvoiceNumber,
            s.InvoiceDate,
            ISNULL(
                DATEADD(DAY, TRY_CONVERT(int, LEFT(s.CreditDays, PATINDEX('%[^0-9]%', s.CreditDays + 'X') - 1)), s.InvoiceDate),
                s.InvoiceDate
            ),
            ISNULL(s.CargoMintInvoiceType, s.InvoiceType),
            NULL,
            NULL,
            s.CargoMintPayingPartyID,
            s.CargoMintPayingPartyAddressID,
            NULL,
            NULL,
            NULL,
            s.JobNumber,
            NULL,
            s.CargoMintCurrencyID,
            s.ExchangeRate,
            s.InvoiceAmount,
            s.TaxAmount,
            s.CargoMintLocalCurrencyID,
            s.InvoiceAmountLocalCurrency,
            s.TaxAmountLocalCurrency,
            s.InvoiceApprovalStatus,
            s.Notes1,
            NULL,
            c.ModeOfTransport,
            c.TransportDirection,
            c.MasterNo,
            c.HouseNo,
            COALESCE(s.FlightDetails, s.VesselVoyage),
            s.Cycle,
            s.CargoType,
            s.FreightStatus,
            s.POL,
            c.POD,
            s.FinalDestination,
            s.ShipperInvoiceDetails,
            NULL,
            s.isLocked,
            s.CargoMintLockedBy,
            s.DateLocked,
            s.isSentToParty,
            s.CargoMintSentBy,
            s.SentDate,
            s.DateCreated,
            s.CargoMintCreatedBy,
            s.DateModified,
            s.CargoMintModifiedBy,
            ISNULL(s.isDeleted, 0),
            s.DateDeleted,
            s.CargoMintDeletedBy,
            s.CargoMintShipmentServiceID,
            NULL,
            CAST(s.FretrackInvoiceID AS nvarchar(100)),
            NULL,
            s.InvoiceAmount,
            NULL,
            s.FretrackCargoID,
            s.FretrackInvoiceID
        FROM dbo.Fretrack_Invoices_Staging s
        LEFT JOIN dbo.Fretrack_Cargo_Staging c
            ON s.FretrackCargoID = c.CargoId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.IsSelectedForMigration = 1
          AND s.CargoMintShipmentID IS NOT NULL
          AND s.InvoiceNumber IS NOT NULL
          AND s.InvoiceDate IS NOT NULL
          AND ISNULL(s.isDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.Invoices i
              WHERE i.FretrackInvoiceID = s.FretrackInvoiceID
          );

        UPDATE i
        SET
            i.OrgId = s.OrgId,
            i.ShipmentId = s.CargoMintShipmentID,
            i.InvoiceNumber = s.InvoiceNumber,
            i.InvoiceDate = s.InvoiceDate,
            i.DueDate = ISNULL(
                            DATEADD(DAY, TRY_CONVERT(int, LEFT(s.CreditDays, PATINDEX('%[^0-9]%', s.CreditDays + 'X') - 1)), s.InvoiceDate),
                            s.InvoiceDate
                        ),
            i.InvoiceType = ISNULL(s.CargoMintInvoiceType, s.InvoiceType),
            i.CompanyId = s.CargoMintPayingPartyID,
            i.CompanyAddressId = s.CargoMintPayingPartyAddressID,
            i.JobNo = s.JobNumber,
            i.CurrencyId = s.CargoMintCurrencyID,
            i.ExRate = s.ExchangeRate,
            i.TotalAmount = s.InvoiceAmount,
            i.TaxAmount = s.TaxAmount,
            i.LocalCurrencyId = s.CargoMintLocalCurrencyID,
            i.TotalAmountLocal = s.InvoiceAmountLocalCurrency,
            i.TaxAmountLocal = s.TaxAmountLocalCurrency,
            i.InvoiceStatus = s.InvoiceApprovalStatus,
            i.Notes = s.Notes1,
            i.TransportMode = c.ModeOfTransport,
            i.TransDirection = c.TransportDirection,
            i.MasterNo = c.MasterNo,
            i.HouseNo = c.HouseNo,
            i.VesselFlightDetails = COALESCE(s.FlightDetails, s.VesselVoyage),
            i.Cycle = s.Cycle,
            i.CargoType = s.CargoType,
            i.FreightStatus = s.FreightStatus,
            i.Pol = s.POL,
            i.Pod = c.POD,
            i.FinalDestination = s.FinalDestination,
            i.ShipperInvoiceDetails = s.ShipperInvoiceDetails,
            i.IsLocked = s.isLocked,
            i.LockedById = s.CargoMintLockedBy,
            i.DateLocked = s.DateLocked,
            i.IsSentToParty = s.isSentToParty,
            i.SentById = s.CargoMintSentBy,
            i.SentDate = s.SentDate,
            i.CreatedDate = s.DateCreated,
            i.CreatedBy = s.CargoMintCreatedBy,
            i.UpdatedDate = s.DateModified,
            i.UpdatedBy = s.CargoMintModifiedBy,
            i.IsDeleted = ISNULL(s.isDeleted, 0),
            i.DeletedDate = s.DateDeleted,
            i.DeletedBy = s.CargoMintDeletedBy,
            i.ShipmentServiceId = s.CargoMintShipmentServiceID,
            i.ExternalCode = CAST(s.FretrackInvoiceID AS nvarchar(100)),
            i.AmountDue = s.InvoiceAmount
        FROM dbo.Invoices i
        JOIN dbo.Fretrack_Invoices_Staging s
            ON i.FretrackInvoiceID = s.FretrackInvoiceID
        LEFT JOIN dbo.Fretrack_Cargo_Staging c
            ON s.FretrackCargoID = c.CargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;


     UPDATE i
        SET i.BranchId =
            CASE
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'M' THEN 22
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'C' THEN 23
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'P' THEN 24
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'B' THEN 4
                ELSE i.BranchId
            END
        FROM dbo.Invoices i
        JOIN dbo.Fretrack_Invoices_Staging s
            ON i.FretrackCargoID = s.FretrackCargoID
        WHERE s.FretrackCargoID = @FretrackCargoId
        AND s.JobNumber IS NOT NULL
        AND LTRIM(RTRIM(s.JobNumber)) <> '';

    
        UPDATE i
        SET
            i.SourceOfSupply = CASE
                                WHEN NULLIF(LTRIM(RTRIM(z.place_of_supply)), '') IS NOT NULL
                                THEN LTRIM(RTRIM(z.place_of_supply))
                                ELSE i.SourceOfSupply
                            END,
            i.CompanyGSTIN = CASE
                                WHEN NULLIF(LTRIM(RTRIM(z.gst_no)), '') IS NOT NULL
                                THEN LTRIM(RTRIM(z.gst_no))
                                ELSE i.CompanyGSTIN
                            END
        FROM dbo.Invoices i
        JOIN dbo.Fretrack_ZohoInvoicesForGSTMapping z
            ON LTRIM(RTRIM(i.InvoiceNumber)) = LTRIM(RTRIM(z.invoice_number))
        WHERE i.FretrackCargoID = @FretrackCargoId;

         UPDATE i
        SET
            i.GstTreatment = gtm.GstRegType,
            i.PaymentTermId = c.PaymentTermId
        FROM dbo.Invoices i
        JOIN dbo.Companies c
            ON i.CompanyId = c.CompanyId
        JOIN dbo.GstTypeMaster gtm
            ON c.GstTreatmentId = gtm.GstTypeId
        WHERE i.FretrackCargoID = @FretrackCargoId and 
            i.CompanyId IS NOT NULL;


        ---GstTreatment

        UPDATE i
        SET i.GstTreatment = g.GstRegType
        FROM dbo.Invoices i
        JOIN dbo.Fretrack_ZohoInvoicesForGSTMapping z
            ON LTRIM(RTRIM(i.InvoiceNumber)) = LTRIM(RTRIM(z.invoice_number))
        JOIN dbo.GstTypeMaster g
            ON UPPER(LTRIM(RTRIM(
                CASE
                    WHEN z.gst_treatment = 'sez_developer' THEN 'business_sez'
                    ELSE z.gst_treatment
                END
            ))) = UPPER(LTRIM(RTRIM(g.ZohoGstType)))
        WHERE i.FretrackCargoID = @FretrackCargoId;

          ---update paymentTermid

       UPDATE i
        SET i.PaymentTermId = pt.PaymentTermId
        FROM dbo.Invoices i
        JOIN dbo.Fretrack_ZohoInvoicesForGSTMapping z
            ON LTRIM(RTRIM(i.InvoiceNumber)) = LTRIM(RTRIM(z.invoice_number))
        JOIN dbo.PaymentTerms pt
            ON pt.TermDays =
            CASE
                WHEN UPPER(LTRIM(RTRIM(z.payment_terms))) IN ('DUE ON RECEIPT', '0 DAYS') THEN 0
                WHEN UPPER(LTRIM(RTRIM(z.payment_terms))) IN ('15 DAYS', 'NET 15') THEN 15
                WHEN UPPER(LTRIM(RTRIM(z.payment_terms))) IN ('30', '30 DAYS', 'NET 30') THEN 30
                WHEN UPPER(LTRIM(RTRIM(z.payment_terms))) IN ('45 DAYS', 'NET 45') THEN 45
                WHEN UPPER(LTRIM(RTRIM(z.payment_terms))) IN ('60 DAYS', 'NET 60') THEN 60
                WHEN UPPER(LTRIM(RTRIM(z.payment_terms))) IN ('90 DAYS', 'NET 90') THEN 90
                ELSE NULL
            END
        WHERE i.FretrackCargoID = @FretrackCargoId;


         UPDATE i
        SET i.PlaceOfSupply =
            CASE
                WHEN UPPER(LEFT(LTRIM(RTRIM(i.JobNo)), 1)) = 'M' THEN '27'
                WHEN UPPER(LEFT(LTRIM(RTRIM(i.JobNo)), 1)) = 'C' THEN '33'
                WHEN UPPER(LEFT(LTRIM(RTRIM(i.JobNo)), 1)) = 'P' THEN '27'
                WHEN UPPER(LEFT(LTRIM(RTRIM(i.JobNo)), 1)) = 'B' THEN '29'
                ELSE i.PlaceOfSupply
            END
        FROM dbo.Invoices i
        WHERE i.FretrackCargoID = @FretrackCargoId
          AND i.JobNo IS NOT NULL
          AND LTRIM(RTRIM(i.JobNo)) <> '';


      UPDATE i
        SET i.SourceOfSupply =
            CASE
                WHEN NULLIF(LTRIM(RTRIM(i.CompanyGSTIN)), '') IS NOT NULL
                THEN LEFT(LTRIM(RTRIM(i.CompanyGSTIN)), 2)
                ELSE i.SourceOfSupply
            END
        FROM dbo.Invoices i
        WHERE i.FretrackCargoID = @FretrackCargoId;

        /* =========================================================
           13. Invoice line items staging updates
           ========================================================= */
        UPDATE cmm
        SET cmm.CargomintInvoiceId = i.InvoiceId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Invoices i
            ON cmm.FretrackInvoiceID = i.FretrackInvoiceID
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Shipments sh
            ON cmm.FretrackCargoID = sh.FretrackCargoId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.FretrackChargeID = scs.FretrackIncomeChargeId
           AND cmm.CargomintShipmentID = scs.CargomintShipmentId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.FretrackChargeID = scs.FretrackExpenseChargeId
           AND cmm.CargomintShipmentID = scs.CargomintShipmentId
        WHERE cmm.FretrackCargoID = @FretrackCargoId
          AND cmm.CargoMintChargeItemID IS NULL;

        UPDATE cmm
        SET cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.FretrackChargeItemId = scs.FretrackChargeItemID
           AND cmm.CargomintShipmentID = scs.CargomintShipmentId
        WHERE cmm.FretrackCargoID = @FretrackCargoId
          AND cmm.CargoMintChargeItemID IS NULL;

        UPDATE cmm
        SET cmm.CargoMintChargeId = sc.ChargeId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.ShipmentCharges sc
            ON cmm.CargomintShipmentID = sc.ShipmentId
           AND cmm.CargoMintChargeItemID = sc.ChargeItemId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintChargeDescription = scs.CargomintChargeDescription
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.CargomintShipmentID = scs.CargomintShipmentId
           AND cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintCurrencyID = c.CurrencyId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Currencies c
            ON UPPER(LTRIM(RTRIM(cmm.FretrackCurrencyCode))) = UPPER(LTRIM(RTRIM(c.CurrencyCode)))
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintTaxID = tg.TaxGroupId
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.TaxGroups tg
            ON TRY_CONVERT(decimal(18,2), tg.TotalRate) = TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent)
           AND tg.TaxGroupName =
                CASE
                    WHEN LTRIM(RTRIM(cmm.FretrackInvoiceTypeGST)) = 'Inter State Registered'
                        THEN 'IGST ' + CONVERT(varchar(20), CONVERT(int, TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent))) + '%'
                    WHEN LTRIM(RTRIM(cmm.FretrackInvoiceTypeGST)) = 'Intra State Registered'
                        THEN 'GST ' + CONVERT(varchar(20), CONVERT(int, TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent))) + '%'
                    WHEN LTRIM(RTRIM(cmm.FretrackInvoiceTypeGST)) IN ('Intra State UnRegistered', 'Inter State UnRegistered')
                         AND TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent) = 0
                        THEN 'GST 0%'
                END
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintCreatedBy = u.Id
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Users u
            ON cmm.FretrackCreatedBy = u.FretrackUserID
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.InvoiceCurrencyId = i.CurrencyId,
            cmm.InvoiceExRate = i.ExRate
        FROM dbo.Fretrack_InvoiceLineItems_Staging cmm
        JOIN dbo.Invoices i
            ON cmm.CargomintInvoiceId = i.InvoiceId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE dbo.Fretrack_InvoiceLineItems_Staging
        SET LineItemExRateNew = 1
        WHERE FretrackCargoID = @FretrackCargoId;

        UPDATE dbo.Fretrack_InvoiceLineItems_Staging
        SET LineItemExRateNew = FretrackExRate
        WHERE FretrackCargoID = @FretrackCargoId;

        UPDATE f
        SET f.LineItemExRateNew = i.ExRate
        FROM dbo.Fretrack_InvoiceLineItems_Staging f
        JOIN dbo.Invoices i
            ON f.CargomintInvoiceId = i.InvoiceId
        WHERE f.FretrackCargoID = @FretrackCargoId
          AND i.CurrencyId <> 1;

        UPDATE s
        SET s.CargomintApplyPerId = m.ApplyPerId
        FROM dbo.Fretrack_InvoiceLineItems_Staging s
        JOIN
        (
            VALUES
                ('PKG', 29), ('SHEET', 30), ('DAY', 27), ('LABOUR', 31),
                ('CBM', 1), ('AWB', 8), ('CNTR', 10), ('ENTRY', 32),
                ('MONTH', 24), ('PLT', 33), ('KGS', 2), ('HOUR', 34),
                ('VEHICLE', 19), ('MT', 35), ('BOX', 36), ('PIECE', 3),
                ('INVOICE', 38), ('BL', 37), ('SQ FT', 28), ('INV', 38),
                ('DOC', 20), ('TRIP', 22), ('SHPT', 9), ('CRTN', 39)
        ) m(FretrackApplyPer, ApplyPerId)
            ON UPPER(LTRIM(RTRIM(s.FretrackApplyPer))) = UPPER(LTRIM(RTRIM(m.FretrackApplyPer)))
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE f
        SET
            LineSubTotal =
                ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                / NULLIF(ISNULL(f.InvoiceExRate, 1), 0),
            LineTaxAmount =
                (
                    ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                    / NULLIF(ISNULL(f.InvoiceExRate, 1), 0)
                ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0),
            LineTotalAmount =
                (
                    ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                    / NULLIF(ISNULL(f.InvoiceExRate, 1), 0)
                )
                +
                (
                    (
                        ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                        / NULLIF(ISNULL(f.InvoiceExRate, 1), 0)
                    ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0)
                ),
            LineTaxAmountLocal =
                (
                    (
                        ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                        / NULLIF(ISNULL(f.InvoiceExRate, 1), 0)
                    ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0)
                ) * ISNULL(f.InvoiceExRate, 1),
            LineTotalAmountLocal =
                (
                    (
                        ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                        / NULLIF(ISNULL(f.InvoiceExRate, 1), 0)
                    )
                    +
                    (
                        (
                            ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0)) * ISNULL(f.LineItemExRateNew, 1))
                            / NULLIF(ISNULL(f.InvoiceExRate, 1), 0)
                        ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0)
                    )
                ) * ISNULL(f.InvoiceExRate, 1)
        FROM dbo.Fretrack_InvoiceLineItems_Staging f
        WHERE f.FretrackCargoID = @FretrackCargoId;

        /* =========================================================
          
           14. Invoice line items insert/update
           ========================================================= */
        INSERT INTO dbo.InvoiceLineItems
        (
            OrgId,
            InvoiceId,
            SortOrder,
            ShipmentChargeId,
            ChargeItemId,
            HsnCode,
            ChargeDescription,
            ApplyPerId,
            CurrencyId,
            CurrencyCode,
            ExRate,
            Quantity,
            Rate,
            SubTotal,
            TaxId,
            TaxPercent,
            TaxAmount,
            TotalAmount,
            TaxAmountLocal,
            TotalAmountLocal,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsDeleted,
            DeletedDate,
            DeletedBy
        )
        SELECT
            s.OrgId,
            s.CargomintInvoiceId,
            s.SortOrder,
            s.CargoMintChargeId,
            s.CargoMintChargeItemID,
            s.ServiceCode,
            s.FretrackChargeDescription,
            s.CargomintApplyPerID,
            s.CargoMintCurrencyID,
            s.FretrackCurrencyCode,
            s.LineItemExRateNew,
            s.FretrackQuantity,
            s.FretrackRate,
            s.LineSubTotal,
            s.CargomintTaxID,
            s.FretrackTaxPercent,
            s.LineTaxAmount,
            s.LineTotalAmount,
            s.LineTaxAmountLocal,
            s.LineTotalAmountLocal,
            GETDATE(),
            s.CargoMintCreatedBy,
            NULL,
            NULL,
            0,
            NULL,
            NULL
        FROM dbo.Fretrack_InvoiceLineItems_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND ISNULL(s.IsImported, 0) = 0
          AND s.CargomintInvoiceId IS NOT NULL
          AND s.CargoMintChargeId IS NOT NULL
          AND s.CargoMintChargeItemID IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.InvoiceLineItems il
              WHERE il.InvoiceId = s.CargomintInvoiceId
                AND il.ShipmentChargeId = s.CargoMintChargeId
                AND il.ChargeItemId = s.CargoMintChargeItemID
          );

        UPDATE il
        SET
            il.OrgId = s.OrgId,
            il.SortOrder = s.SortOrder,
            il.HsnCode = s.ServiceCode,
            il.ChargeDescription = s.FretrackChargeDescription,
            il.ApplyPerId = s.CargomintApplyPerID,
            il.CurrencyId = s.CargoMintCurrencyID,
            il.CurrencyCode = s.FretrackCurrencyCode,
            il.ExRate = s.LineItemExRateNew,
            il.Quantity = s.FretrackQuantity,
            il.Rate = s.FretrackRate,
            il.SubTotal = s.LineSubTotal,
            il.TaxId = s.CargomintTaxID,
            il.TaxPercent = s.FretrackTaxPercent,
            il.TaxAmount = s.LineTaxAmount,
            il.TotalAmount = s.LineTotalAmount,
            il.TaxAmountLocal = s.LineTaxAmountLocal,
            il.TotalAmountLocal = s.LineTotalAmountLocal,
            il.UpdatedDate = GETDATE(),
            il.UpdatedBy = s.CargoMintCreatedBy,
            il.IsDeleted = 0,
            il.DeletedDate = NULL,
            il.DeletedBy = NULL
        FROM dbo.InvoiceLineItems il
        JOIN dbo.Fretrack_InvoiceLineItems_Staging s
            ON il.InvoiceId = s.CargomintInvoiceId
           AND il.ShipmentChargeId = s.CargoMintChargeId
           AND il.ChargeItemId = s.CargoMintChargeItemID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET IsImported = 1
        FROM dbo.Fretrack_InvoiceLineItems_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND ISNULL(s.IsImported, 0) = 0
          AND s.CargomintInvoiceId IS NOT NULL
          AND s.CargoMintChargeId IS NOT NULL
          AND s.CargoMintChargeItemID IS NOT NULL
          AND EXISTS
          (
              SELECT 1
              FROM dbo.InvoiceLineItems il
              WHERE il.InvoiceId = s.CargomintInvoiceId
                AND il.ShipmentChargeId = s.CargoMintChargeId
                AND il.ChargeItemId = s.CargoMintChargeItemID
          );

        --   =======================================================
        -- Insert tax details agaist invoice line item
        --   =======================================================
        INSERT INTO dbo.InvoiceTaxDetails
        (
            OrgId,
            InvoiceId,
            InvoiceLineItemId,
            TaxableAmount,
            TaxId,
            TaxName,
            TaxRate,
            TaxAmount,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsDeleted,
            DeletedDate,
            DeletedBy
        )
        SELECT
            ilm.OrgId,
            ilm.InvoiceId,
            ilm.InvoiceLineItemId,
            ISNULL(ilm.SubTotal, 0) AS TaxableAmount,
            tm.TaxId,
            tm.TaxName,
            tm.TaxRate,
            CAST((ISNULL(ilm.SubTotal, 0) * ISNULL(tm.TaxRate, 0)) / 100.0 AS decimal(18,2)) AS TaxAmount,
            GETDATE() AS CreatedDate,
            ilm.CreatedBy,
            NULL AS UpdatedDate,
            NULL AS UpdatedBy,
            0 AS IsDeleted,
            NULL AS DeletedDate,
            NULL AS DeletedBy
        FROM dbo.InvoiceLineItems ilm
        JOIN dbo.Invoices i
            ON ilm.InvoiceId = i.InvoiceId
        JOIN dbo.TaxGroupDetails tgd
            ON ilm.TaxId = tgd.TaxGroupId
        JOIN dbo.TaxMaster tm
            ON tgd.TaxId = tm.TaxId
        WHERE i.FretrackCargoId = @FretrackCargoId
        AND ISNULL(ilm.IsDeleted, 0) = 0
        AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.InvoiceTaxDetails itd
            WHERE itd.InvoiceLineItemId = ilm.InvoiceLineItemId
                AND itd.TaxId = tm.TaxId
                AND ISNULL(itd.IsDeleted, 0) = 0
        );

        --- Update if Already Exsist

        UPDATE itd
        SET
            itd.OrgId = ilm.OrgId,
            itd.InvoiceId = ilm.InvoiceId,
            itd.TaxableAmount = ISNULL(ilm.SubTotal, 0),
            itd.TaxName = tm.TaxName,
            itd.TaxRate = tm.TaxRate,
            itd.TaxAmount = CAST((ISNULL(ilm.SubTotal, 0) * ISNULL(tm.TaxRate, 0)) / 100.0 AS decimal(18,2)),
            itd.UpdatedDate = GETDATE(),
            itd.UpdatedBy = ilm.CreatedBy,
            itd.IsDeleted = 0,
            itd.DeletedDate = NULL,
            itd.DeletedBy = NULL
        FROM dbo.InvoiceTaxDetails itd
        JOIN dbo.InvoiceLineItems ilm
            ON itd.InvoiceLineItemId = ilm.InvoiceLineItemId
        JOIN dbo.Invoices i
            ON ilm.InvoiceId = i.InvoiceId
        JOIN dbo.TaxGroupDetails tgd
            ON ilm.TaxId = tgd.TaxGroupId
        AND itd.TaxId = tgd.TaxId
        JOIN dbo.TaxMaster tm
            ON tgd.TaxId = tm.TaxId
        WHERE i.FretrackCargoId = @FretrackCargoId;

        /* =========================================================
           15. Vendor bill staging + insert
           ========================================================= */
        UPDATE s
        SET s.CargoMintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Shipments sh
            ON s.FretrackCargoID = sh.FretrackCargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintShipmentServiceID = sd.ShipmentServiceId
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.ShipmentServiceDetails sd
            ON s.FretrackCargoID = sd.FretrackCargoID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintPayingPartyID = c.CompanyId
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Companies c
            ON ',' + c.FretrackCompanyIDs + ',' LIKE '%,' + CAST(s.FretrackPayingPartyID AS varchar(20)) + ',%'
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.FretrackPayingPartyID IS NOT NULL;

        UPDATE s
        SET s.CargoMintCurrencyID = c.CargoMintCurrencyID
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Fretrack_CurrencyMaster_Staging c
            ON s.CurrencyID = c.FretrackCurrencyID
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CurrencyID IS NOT NULL;

        UPDATE s
        SET s.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.CreatedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintLockedBy = u.CargoMintUserID
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.LockedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

       

        UPDATE s
        SET
            s.SourceOfSupply = CASE
                                   WHEN NULLIF(LTRIM(RTRIM(z.gst_no)), '') IS NOT NULL
                                   THEN LEFT(LTRIM(RTRIM(z.gst_no)), 2)
                                   ELSE s.SourceOfSupply
                               END,
            s.CompanyGSTIN = CASE
                                 WHEN NULLIF(LTRIM(RTRIM(z.gst_no)), '') IS NOT NULL
                                 THEN LTRIM(RTRIM(z.gst_no))
                                 ELSE s.CompanyGSTIN
                             END
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Fretrack_ZohoBillsForGSTMapping z
            ON LTRIM(RTRIM(s.VendorBillNumber)) = LTRIM(RTRIM(z.bill_number))
        WHERE s.FretrackCargoID = @FretrackCargoId;

         UPDATE s
        SET s.PlaceOfSupply =
            CASE
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'M' THEN '27'
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'C' THEN '33'
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'P' THEN '27'
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'B' THEN '29'
                ELSE s.PlaceOfSupply
            END
        FROM dbo.Fretrack_VendorBill_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.JobNumber IS NOT NULL
          AND LTRIM(RTRIM(s.JobNumber)) <> '';


        UPDATE s
        SET s.BranchId =
            CASE
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'M' THEN 22
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'C' THEN 23
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'P' THEN 24
                WHEN UPPER(LEFT(LTRIM(RTRIM(s.JobNumber)), 1)) = 'B' THEN 4
                ELSE s.BranchId
            END
        FROM dbo.Fretrack_VendorBill_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.JobNumber IS NOT NULL
          AND LTRIM(RTRIM(s.JobNumber)) <> '';

       
        UPDATE s
        SET s.VendorBillGSTTreatment =
            CASE UPPER(LTRIM(RTRIM(z.gst_treatment)))
                WHEN 'BUSINESS_GST' THEN 'Regular'
                WHEN 'BUSINESS_NONE' THEN 'Unregistered'
                WHEN 'CONSUMER' THEN 'Consumer'
                WHEN 'OVERSEAS' THEN 'Overseas'
                WHEN 'SEZ_DEVELOPER' THEN 'SEZ Unit'
                WHEN 'DEEMED' THEN 'Deemed'
                ELSE s.VendorBillGSTTreatment
            END
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Fretrack_ZohoBillsForGSTMapping z
            ON LTRIM(RTRIM(s.VendorBillNumber)) = LTRIM(RTRIM(z.bill_number))
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.PaymentTermId = c.PaymentTermId
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN dbo.Companies c
            ON s.CargoMintPayingPartyID = c.CompanyId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.CargoMintPayingPartyID IS NOT NULL
          AND c.PaymentTermId IS NOT NULL;

        ;WITH AddressRank AS
        (
            SELECT
                s.FretrackVendorBillID,
                ca.CompanyAddressId,
                ROW_NUMBER() OVER
                (
                    PARTITION BY s.FretrackVendorBillID
                    ORDER BY
                        ISNULL(ca.IsDeleted, 0) ASC,
                        ISNULL(ca.UpdatedDate, ca.CreatedDate) DESC,
                        ca.CompanyAddressId DESC
                ) AS rn
            FROM dbo.Fretrack_VendorBill_Staging s
            JOIN dbo.CompanyAddress ca
                ON s.CargoMintPayingPartyID = ca.CompanyID
               AND LTRIM(RTRIM(s.CompanyGSTIN)) = LTRIM(RTRIM(ca.GSTNumber))
            WHERE s.FretrackCargoID = @FretrackCargoId
              AND s.CompanyGSTIN IS NOT NULL
        )
        UPDATE s
        SET s.CargoMintPayingPartyAddressID = ar.CompanyAddressId
        FROM dbo.Fretrack_VendorBill_Staging s
        JOIN AddressRank ar
            ON s.FretrackVendorBillID = ar.FretrackVendorBillID
           AND ar.rn = 1
        WHERE s.FretrackCargoID = @FretrackCargoId;

     
        
        ---given by dhiraj
         
        --- update currency in Fretrack_VendorBill_Staging
        
            UPDATE vb
        SET vb.ZohoBillCurrency = zm.currency_code
        FROM Fretrack_VendorBill_Staging vb
        INNER JOIN Fretrack_ZohoBillsForGSTMapping zm
            ON vb.VendorBillNumber = zm.bill_number
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        
        
        --- update currencyID in Fretrack_VendorBill_Staging
                UPDATE vb
        SET vb.ZohoBillCurrencyID = zm.CurrencyId
        FROM Fretrack_VendorBill_Staging vb
        INNER JOIN Currencies zm
            ON vb.ZohoBillCurrency = zm.CurrencyCode
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        
        
        --- update PaymentTermID in Fretrack_VendorBill_Staging
        
        
            UPDATE vb
        SET vb.ZohoPaymentTermID = zm.paymentTermID
        FROM Fretrack_VendorBill_Staging vb
        INNER JOIN Fretrack_ZohoBillsForGSTMapping zm
            ON vb.VendorBillNumber = zm.bill_number
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        
        
        
      
        
        
        ---- update correct Currency/PaymentTerm in Bills table
        
        
            UPDATE vb
        SET vb.CurrencyId = zm.ZohoBillCurrencyID,
        vb.PaymentTermId=zm.ZohoPaymentTermID
        FROM Bills vb
        INNER JOIN Fretrack_VendorBill_Staging zm
            ON vb.BillId = zm.CargoMintBillID
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        
        
            ---- update Bill ExRate from Zoho in Fretrack_VendorBill_Staging table
        
        
            UPDATE vb
        SET vb.ExchangeRate = zm.exchange_rate
        FROM Fretrack_VendorBill_Staging vb
        INNER JOIN Fretrack_ZohoBillsForGSTMapping zm
            ON vb.VendorBillNumber = zm.bill_number
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        

        
                ---- update Bill GstTreatment from Fretrack_ZohoBillsForGSTMapping table
        
        
            UPDATE vb
        SET vb.VendorBillTypeGst = zm.VendorBillGSTTreatment
        FROM Fretrack_VendorBill_Staging vb
        INNER JOIN Fretrack_ZohoBillsForGSTMapping zm
            ON vb.VendorBillNumber = zm.bill_number
        WHERE vb.FretrackCargoID = @FretrackCargoId;
 

           UPDATE dbo.Fretrack_VendorBill_Staging
        SET
            IsSelectedForMigration = 1,
            MigrationRemarks = 'VendorBill',
            ImportedOn = GETDATE()
        WHERE FretrackCargoID = @FretrackCargoId
          AND ISNULL(isDeleted, 0) = 0;

        INSERT INTO dbo.Bills
        (
            OrgId,
            ShipmentId,
            InvoiceNumber,
            InvoiceDate,
            DueDate,
            BillType,
            BranchId,
            SourceOfSupply,
            CompanyId,
            CompanyAddressId,
            CompanyGSTIN,
            GstTreatment,
            PlaceOfSupply,
            JobNo,
            PaymentTermId,
            CurrencyId,
            ExRate,
            TotalAmount,
            TaxAmount,
            LocalCurrencyId,
            TotalAmountLocal,
            TaxAmountLocal,
            BillStatus,
            Notes,
            ServiceType,
            TransportMode,
            TransDirection,
            MasterNo,
            HouseNo,
            VesselFlightDetails,
            ShipmentType,
            FreightStatus,
            Pol,
            Pod,
            FinalDestination,
            ShipperInvoiceDetails,
            ContainerWeightDetails,
            IsLocked,
            LockedById,
            DateLocked,
            IsSentToParty,
            SentById,
            SentDate,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsDeleted,
            DeletedDate,
            DeletedBy,
            ExternalCode,
            ActualAmount,
            AmountPaid,
            AmountDue,
            PaymentStatus,
            FretrackVendorBillID,
            FretrackCargoID
        )
        SELECT
            s.OrgId,
            s.CargoMintShipmentID,
            s.VendorBillNumber,
            s.VendorBillDate,
            ISNULL(
                DATEADD(DAY, TRY_CONVERT(int, LEFT(s.CreditDays, PATINDEX('%[^0-9]%', s.CreditDays + 'X') - 1)), s.VendorBillDate),
                s.VendorBillDate
            ),
            CASE WHEN s.ActualAmount < 0 THEN 'Debit Note' ELSE 'Bill' END,
            s.BranchId,
            s.SourceOfSupply,
            s.CargoMintPayingPartyID,
            s.CargoMintPayingPartyAddressID,
            s.CompanyGSTIN,
            s.VendorBillGSTTreatment,
            s.PlaceOfSupply,
            s.JobNumber,
            s.PaymentTermId,
            s.CargoMintCurrencyID,
            s.ExchangeRate,
            s.VendorBillAmount,
            s.TaxAmount,
            s.CargoMintLocalCurrencyID,
            s.VendorBillAmountLocalCurrency,
            s.TaxAmountLocalCurrency,
            s.BillStatus,
            s.Notes1,
            NULL,
            c.ModeOfTransport,
            c.TransportDirection,
            c.MasterNo,
            c.HouseNo,
            COALESCE(s.FlightDetails, s.VesselVoyage),
            c.ShipmentType,
            s.FreightStatus,
            s.POL,
            c.POD,
            s.FinalDestination,
            s.ShipperVendorBillDetails,
            NULL,
            s.isLocked,
            s.CargoMintLockedBy,
            s.DateLocked,
            s.isSentToParty,
            s.CargoMintSentBy,
            s.SentDate,
            s.DateCreated,
            s.CargoMintCreatedBy,
            s.DateModified,
            s.CargoMintModifiedBy,
            ISNULL(s.isDeleted, 0),
            s.DateDeleted,
            s.CargoMintDeletedBy,
            CAST(s.FretrackVendorBillID AS nvarchar(100)),
            s.ActualAmount,
            NULL,
            s.VendorBillAmount,
            NULL,
            s.FretrackVendorBillID,
            s.FretrackCargoID
        FROM dbo.Fretrack_VendorBill_Staging s
        LEFT JOIN dbo.Fretrack_Cargo_Staging c
            ON s.FretrackCargoID = c.CargoId
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.IsSelectedForMigration = 1
          AND s.CargoMintShipmentID IS NOT NULL
          AND s.VendorBillNumber IS NOT NULL
          AND s.VendorBillDate IS NOT NULL
          AND ISNULL(s.isDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.Bills b
              WHERE b.FretrackVendorBillID = s.FretrackVendorBillID
          );

        UPDATE b
        SET
            b.OrgId = b.OrgId,
            b.ShipmentId = s.CargoMintShipmentID,
            b.InvoiceNumber = s.VendorBillNumber,
            b.InvoiceDate = s.VendorBillDate,
            b.DueDate = ISNULL(
                            DATEADD(DAY, TRY_CONVERT(int, LEFT(s.CreditDays, PATINDEX('%[^0-9]%', s.CreditDays + 'X') - 1)), s.VendorBillDate),
                            s.VendorBillDate
                        ),
            b.BillType = CASE WHEN s.ActualAmount < 0 THEN 'Debit Note' ELSE 'Bill' END,
            b.BranchId = s.BranchId,
            b.SourceOfSupply = s.SourceOfSupply,
            b.CompanyId = s.CargoMintPayingPartyID,
            b.CompanyAddressId = s.CargoMintPayingPartyAddressID,
            b.CompanyGSTIN = s.CompanyGSTIN,
            b.GstTreatment = s.VendorBillGSTTreatment,
            b.PlaceOfSupply = s.PlaceOfSupply,
            b.JobNo = s.JobNumber,
            b.PaymentTermId = s.PaymentTermId,
            b.CurrencyId = s.CargoMintCurrencyID,
            b.ExRate = s.ExchangeRate,
            b.TotalAmount = s.VendorBillAmount,
            b.TaxAmount = s.TaxAmount,
            b.LocalCurrencyId = s.CargoMintLocalCurrencyID,
            b.TotalAmountLocal = s.VendorBillAmountLocalCurrency,
            b.TaxAmountLocal = s.TaxAmountLocalCurrency,
            b.BillStatus = s.BillStatus,
            b.Notes = s.Notes1,
            b.TransportMode = c.ModeOfTransport,
            b.TransDirection = c.TransportDirection,
            b.MasterNo = c.MasterNo,
            b.HouseNo = c.HouseNo,
            b.VesselFlightDetails = COALESCE(s.FlightDetails, s.VesselVoyage),
            b.ShipmentType = c.ShipmentType,
            b.FreightStatus = s.FreightStatus,
            b.Pol = s.POL,
            b.Pod = c.POD,
            b.FinalDestination = s.FinalDestination,
            b.ShipperInvoiceDetails = s.ShipperVendorBillDetails,
            b.IsLocked = s.isLocked,
            b.LockedById = s.CargoMintLockedBy,
            b.DateLocked = s.DateLocked,
            b.IsSentToParty = s.isSentToParty,
            b.SentById = s.CargoMintSentBy,
            b.SentDate = s.SentDate,
            b.CreatedDate = s.DateCreated,
            b.CreatedBy = s.CargoMintCreatedBy,
            b.UpdatedDate = s.DateModified,
            b.UpdatedBy = s.CargoMintModifiedBy,
            b.IsDeleted = ISNULL(s.isDeleted, 0),
            b.DeletedDate = s.DateDeleted,
            b.DeletedBy = s.CargoMintDeletedBy,
            b.ExternalCode = CAST(s.FretrackVendorBillID AS nvarchar(100)),
            b.ActualAmount = s.ActualAmount,
            b.AmountDue = s.VendorBillAmount
        FROM dbo.Bills b
        JOIN dbo.Fretrack_VendorBill_Staging s
            ON b.FretrackVendorBillID = s.FretrackVendorBillID
        LEFT JOIN dbo.Fretrack_Cargo_Staging c
            ON s.FretrackCargoID = c.CargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;


        -- after insert LookupQueryEmbeddings
        
          --- update CargoMintBillID in Fretrack_VendorBill_Staging
        
        
            UPDATE vb
        SET vb.CargoMintBillID = zm.BillId
        FROM Fretrack_VendorBill_Staging vb
        INNER JOIN Bills zm
            ON vb.FretrackVendorBillID = zm.FretrackVendorBillID
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        
            ---- update Bill ExRate from Fretrack_VendorBill_Staging table
        
            UPDATE vb
        SET vb.ExRate = zm.ExchangeRate
        FROM Bills vb
        INNER JOIN Fretrack_VendorBill_Staging zm
            ON vb.BillId = zm.CargoMintBillID
        WHERE vb.FretrackCargoID = @FretrackCargoId;
        
        

        /* =========================================================
           16. Vendor bill line items staging updates
           ========================================================= */
        UPDATE cmm
        SET cmm.CragomintBillId = b.BillId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Bills b
            ON cmm.FretrackVendorBillID = b.FretrackVendorBillID
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.VendorBillTypeGST = vb.VendorBillTypeGst
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Fretrack_VendorBill_Staging vb
            ON cmm.CragomintBillId = vb.CargoMintBillID
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Shipments sh
            ON cmm.FretrackCargoID = sh.FretrackCargoId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.FretrackChargeID = scs.FretrackExpenseChargeId
           AND cmm.CargomintShipmentID = scs.CargomintShipmentId
        WHERE cmm.FretrackCargoID = @FretrackCargoId
          AND cmm.CargoMintChargeItemID IS NULL;

        UPDATE cmm
        SET cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.FretrackChargeItemID = scs.FretrackChargeItemId
           AND cmm.CargomintShipmentId = scs.CargomintShipmentID
        WHERE cmm.FretrackCargoID = @FretrackCargoId
          AND cmm.CargoMintChargeItemID IS NULL;

        UPDATE cmm
        SET cmm.CargoMintChargeId = sc.ChargeId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.ShipmentCharges sc
            ON cmm.CargomintShipmentID = sc.ShipmentId
           AND cmm.CargoMintChargeItemID = sc.ChargeItemId
        WHERE cmm.FretrackCargoID = @FretrackCargoId
          AND cmm.CargoMintChargeId IS NULL;

        UPDATE cmm
        SET cmm.CargoMintChargeDescription = scs.CargomintChargeDescription
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Fretrack_ShipmentCharges_Staging scs
            ON cmm.CargomintShipmentID = scs.CargomintShipmentId
           AND cmm.CargoMintChargeItemID = scs.CargomintChargeItemId
        WHERE cmm.FretrackCargoID = @FretrackCargoId
          AND cmm.CargoMintChargeDescription IS NULL;

        UPDATE cmm
        SET cmm.CargoMintCurrencyID = c.CurrencyId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Currencies c
            ON UPPER(LTRIM(RTRIM(cmm.FretrackCurrencyCode))) = UPPER(LTRIM(RTRIM(c.CurrencyCode)))
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargomintTaxID = tg.TaxGroupId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.TaxGroups tg
            ON TRY_CONVERT(decimal(18,2), tg.TotalRate) = TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent)
           AND tg.TaxGroupName =
                CASE
                    WHEN LTRIM(RTRIM(cmm.VendorBillTypeGST)) = 'Inter State Registered'
                        THEN 'IGST ' + CONVERT(varchar(20), CONVERT(int, TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent))) + '%'
                    WHEN LTRIM(RTRIM(cmm.VendorBillTypeGST)) = 'Intra State Registered'
                        THEN 'GST ' + CONVERT(varchar(20), CONVERT(int, TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent))) + '%'
                    WHEN LTRIM(RTRIM(cmm.VendorBillTypeGST)) IN ('Intra State UnRegistered', 'Inter State UnRegistered')
                         AND TRY_CONVERT(decimal(18,2), cmm.FretrackTaxPercent) = 0
                        THEN 'GST 0%'
                END
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.CargoMintCreatedBy = u.Id
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Users u
            ON cmm.FretarckCreatedBy = u.FretrackUserID
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargomintApplyPerId = m.ApplyPerId
        FROM dbo.Fretrack_VendorBillLineItems_Staging s
        JOIN
        (
            VALUES
                ('PKG', 29), ('SHEET', 30), ('DAY', 27), ('LABOUR', 31),
                ('CBM', 1), ('AWB', 8), ('CNTR', 10), ('ENTRY', 32),
                ('MONTH', 24), ('PLT', 33), ('KGS', 2), ('HOUR', 34),
                ('VEHICLE', 19), ('MT', 35), ('BOX', 36), ('PIECE', 3),
                ('INVOICE', 38), ('BL', 37), ('SQ FT', 28), ('INV', 38),
                ('DOC', 20), ('TRIP', 22), ('SHPT', 9), ('CRTN', 39)
        ) m(FretrackApplyPer, ApplyPerId)
            ON UPPER(LTRIM(RTRIM(s.FretrackApplyPer))) = UPPER(LTRIM(RTRIM(m.FretrackApplyPer)))
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE cmm
        SET cmm.BillCurrencyId = b.CurrencyId
        FROM dbo.Fretrack_VendorBillLineItems_Staging cmm
        JOIN dbo.Bills b
            ON cmm.CragomintBillId = b.BillId
        WHERE cmm.FretrackCargoID = @FretrackCargoId;

         
--- update all amount LineSubTotal LineTaxAmount   LineTotalAmount LineTaxAmountLocal  LineTotalAmountLocal
 
    UPDATE f
    SET
        -- Invoice Currency
        LineSubTotal =
            ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
            * ISNULL(f.LineItemExRateNew, 1))
            / NULLIF(ISNULL(f.BillExRate, 1), 0),
    
        LineTaxAmount =
            (
                ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
                * ISNULL(f.LineItemExRateNew, 1))
                / NULLIF(ISNULL(f.BillExRate, 1), 0)
            ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0),
    
        LineTotalAmount =
            (
                ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
                * ISNULL(f.LineItemExRateNew, 1))
                / NULLIF(ISNULL(f.BillExRate, 1), 0)
            )
            +
            (
                (
                    ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
                    * ISNULL(f.LineItemExRateNew, 1))
                    / NULLIF(ISNULL(f.BillExRate, 1), 0)
                ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0)
            ),
    
        -- Org Currency
        LineTaxAmountLocal =
            (
                (
                    ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
                    * ISNULL(f.LineItemExRateNew, 1))
                    / NULLIF(ISNULL(f.BillExRate, 1), 0)
                ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0)
            ) * ISNULL(f.BillExRate, 1),
    
        LineTotalAmountLocal =
            (
                (
                    ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
                    * ISNULL(f.LineItemExRateNew, 1))
                    / NULLIF(ISNULL(f.BillExRate, 1), 0)
                )
                +
                (
                    (
                        ((ISNULL(f.FretrackQuantity, 0) * ISNULL(f.FretrackRate, 0))
                        * ISNULL(f.LineItemExRateNew, 1))
                        / NULLIF(ISNULL(f.BillExRate, 1), 0)
                    ) * (ISNULL(f.FretrackTaxPercent, 0) / 100.0)
                )
            ) * ISNULL(f.BillExRate, 1)
    FROM dbo.Fretrack_VendorBillLineItems_Staging f
    WHERE f.FretrackCargoID = @FretrackCargoId;

    
 
 

        /* =========================================================
           
           15. Bill line items insert/update
           ========================================================= */
        INSERT INTO dbo.BillLineItems
        (
            OrgId,
            BillId,
            SortOrder,
            ShipmentChargeId,
            ChargeItemId,
            HsnCode,
            ChargeDescription,
            ApplyPerId,
            CurrencyId,
            CurrencyCode,
            ExRate,
            Quantity,
            Rate,
            SubTotal,
            TaxId,
            TaxPercent,
            TaxAmount,
            TotalAmount,
            TaxAmountLocal,
            TotalAmountLocal,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsDeleted,
            DeletedDate,
            DeletedBy
        )
        SELECT
            stg.OrgId,
            stg.CragomintBillId,
            ISNULL(stg.SortOrder, 0),
            stg.CargoMintChargeId,
            stg.CargoMintChargeItemID,
            stg.ServiceCode,
            stg.FretrackChargeDescription,
            stg.CargoMintApplyPerID,
            stg.CargoMintCurrencyID,
            stg.FretrackCurrencyCode,
            ISNULL(stg.LineItemExRateNew, 1),
            ISNULL(stg.FretrackQuantity, 0),
            ISNULL(stg.FretrackRate, 0),
            ISNULL(stg.LineSubTotal, 0),
            stg.CargomintTaxID,
            ISNULL(stg.FretrackTaxPercent, 0),
            ISNULL(stg.LineTaxAmount, 0),
            ISNULL(stg.LineTotalAmount, 0),
            ISNULL(stg.LineTaxAmountLocal, 0),
            ISNULL(stg.LineTotalAmountLocal, 0),
            GETDATE(),
            stg.CargoMintCreatedBy,
            NULL,
            NULL,
            0,
            NULL,
            NULL
        FROM dbo.Fretrack_VendorBillLineItems_Staging stg
        WHERE stg.FretrackCargoID = @FretrackCargoId
          AND ISNULL(stg.IsImported, 0) = 0
          AND stg.CragomintBillId IS NOT NULL
          AND stg.CargoMintChargeItemID IS NOT NULL
          AND stg.CargoMintChargeId IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.BillLineItems bli
              WHERE bli.BillId = stg.CragomintBillId
                AND bli.ShipmentChargeId = stg.CargoMintChargeId
                AND bli.ChargeItemId = stg.CargoMintChargeItemID
                AND ISNULL(bli.IsDeleted, 0) = 0
          );

        UPDATE bli
        SET
            bli.OrgId = stg.OrgId,
            bli.SortOrder = ISNULL(stg.SortOrder, 0),
            bli.HsnCode = stg.ServiceCode,
            bli.ChargeDescription = stg.FretrackChargeDescription,
            bli.ApplyPerId = stg.CargoMintApplyPerID,
            bli.CurrencyId = stg.CargoMintCurrencyID,
            bli.CurrencyCode = stg.FretrackCurrencyCode,
            bli.ExRate = ISNULL(stg.LineItemExRateNew, 1),
            bli.Quantity = ISNULL(stg.FretrackQuantity, 0),
            bli.Rate = ISNULL(stg.FretrackRate, 0),
            bli.SubTotal = ISNULL(stg.LineSubTotal, 0),
            bli.TaxId = stg.CargomintTaxID,
            bli.TaxPercent = ISNULL(stg.FretrackTaxPercent, 0),
            bli.TaxAmount = ISNULL(stg.LineTaxAmount, 0),
            bli.TotalAmount = ISNULL(stg.LineTotalAmount, 0),
            bli.TaxAmountLocal = ISNULL(stg.LineTaxAmountLocal, 0),
            bli.TotalAmountLocal = ISNULL(stg.LineTotalAmountLocal, 0),
            bli.UpdatedDate = GETDATE(),
            bli.UpdatedBy = stg.CargoMintCreatedBy,
            bli.IsDeleted = 0,
            bli.DeletedDate = NULL,
            bli.DeletedBy = NULL
        FROM dbo.BillLineItems bli
        JOIN dbo.Fretrack_VendorBillLineItems_Staging stg
            ON bli.BillId = stg.CragomintBillId
           AND bli.ShipmentChargeId = stg.CargoMintChargeId
           AND bli.ChargeItemId = stg.CargoMintChargeItemID
           AND ISNULL(bli.IsDeleted, 0) = 0
        WHERE stg.FretrackCargoID = @FretrackCargoId;

        UPDATE stg
        SET stg.IsImported = 1
        FROM dbo.Fretrack_VendorBillLineItems_Staging stg
        WHERE stg.FretrackCargoID = @FretrackCargoId
          AND ISNULL(stg.IsImported, 0) = 0
          AND stg.CragomintBillId IS NOT NULL
          AND stg.CargoMintChargeItemID IS NOT NULL
          AND stg.CargoMintChargeId IS NOT NULL
          AND EXISTS
          (
              SELECT 1
              FROM dbo.BillLineItems bli
              WHERE bli.BillId = stg.CragomintBillId
                AND bli.ShipmentChargeId = stg.CargoMintChargeId
                AND bli.ChargeItemId = stg.CargoMintChargeItemID
                AND ISNULL(bli.IsDeleted, 0) = 0
          );

----insert Bill Tax Details

       INSERT INTO dbo.BillTaxDetails
    (
        OrgId,
        BillId,
        BillLineItemId,
        TaxableAmount,
        TaxId,
        TaxName,
        TaxRate,
        TaxAmount,
        CreatedDate,
        CreatedBy,
        UpdatedDate,
        UpdatedBy,
        IsDeleted,
        DeletedDate,
        DeletedBy
    )
    SELECT
        bli.OrgId,
        bli.BillId,
        bli.BillLineItemId,
        ISNULL(bli.SubTotal, 0) AS TaxableAmount,
        tm.TaxId,
        tm.TaxName,
        tm.TaxRate,
        CAST((ISNULL(bli.SubTotal, 0) * ISNULL(tm.TaxRate, 0)) / 100.0 AS decimal(18,2)) AS TaxAmount,
        GETDATE() AS CreatedDate,
        bli.CreatedBy,
        NULL AS UpdatedDate,
        NULL AS UpdatedBy,
        0 AS IsDeleted,
        NULL AS DeletedDate,
        NULL AS DeletedBy
    FROM dbo.BillLineItems bli
    JOIN dbo.Bills b
        ON bli.BillId = b.BillId
    JOIN dbo.TaxGroupDetails tgd
        ON bli.TaxId = tgd.TaxGroupId
    JOIN dbo.TaxMaster tm
        ON tgd.TaxId = tm.TaxId
    WHERE b.FretrackCargoId = @FretrackCargoId
    AND ISNULL(bli.IsDeleted, 0) = 0
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.BillTaxDetails btd
        WHERE btd.BillLineItemId = bli.BillLineItemId
            AND btd.TaxId = tm.TaxId
            AND ISNULL(btd.IsDeleted, 0) = 0
    );

    UPDATE btd
    SET
        btd.OrgId = bli.OrgId,
        btd.BillId = bli.BillId,
        btd.TaxableAmount = ISNULL(bli.SubTotal, 0),
        btd.TaxName = tm.TaxName,
        btd.TaxRate = tm.TaxRate,
        btd.TaxAmount = CAST((ISNULL(bli.SubTotal, 0) * ISNULL(tm.TaxRate, 0)) / 100.0 AS decimal(18,2)),
        btd.UpdatedDate = GETDATE(),
        btd.UpdatedBy = bli.CreatedBy,
        btd.IsDeleted = 0,
        btd.DeletedDate = NULL,
        btd.DeletedBy = NULL
    FROM dbo.BillTaxDetails btd
    JOIN dbo.BillLineItems bli
        ON btd.BillLineItemId = bli.BillLineItemId
    JOIN dbo.Bills b
        ON bli.BillId = b.BillId
    JOIN dbo.TaxGroupDetails tgd
        ON bli.TaxId = tgd.TaxGroupId
    AND btd.TaxId = tgd.TaxId
    JOIN dbo.TaxMaster tm
        ON tgd.TaxId = tm.TaxId
    WHERE b.FretrackCargoId = @FretrackCargoId;


 /* =========================================================
           15. HBL staging + insert
           ========================================================= */
        UPDATE s
        SET s.CargoMintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_HBL_Staging s
        JOIN dbo.Shipments sh
            ON s.FretrackCargoID = sh.FretrackCargoId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintShipmentServiceID = sd.ShipmentServiceId
        FROM dbo.Fretrack_HBL_Staging s
        JOIN dbo.ShipmentServiceDetails sd
            ON s.FretrackCargoID = sd.FretrackCargoID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_HBL_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.CreatedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintModifiedBy = u.CargoMintUserID
        FROM dbo.Fretrack_HBL_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.ModifiedBy = u.FretrackUserId
        WHERE s.FretrackCargoID = @FretrackCargoId;

        UPDATE h
        SET h.CargoMintShipperAddressID = ca.CompanyAddressId
        FROM dbo.Fretrack_HBL_Staging h
        JOIN dbo.CompanyAddress ca
            ON h.ShipperAddressID = ca.FretrackAddressId
        WHERE h.FretrackCargoID = @FretrackCargoId
          AND h.ShipperAddressID IS NOT NULL;

        UPDATE h
        SET h.CargoMintConsigneeAddressID = ca.CompanyAddressId
        FROM dbo.Fretrack_HBL_Staging h
        JOIN dbo.CompanyAddress ca
            ON h.ConsigneeAddressID = ca.FretrackAddressId
        WHERE h.FretrackCargoID = @FretrackCargoId
          AND h.ConsigneeAddressID IS NOT NULL;

        UPDATE h
        SET h.CargoMintNotifyAddressID = ca.CompanyAddressId
        FROM dbo.Fretrack_HBL_Staging h
        JOIN dbo.CompanyAddress ca
            ON h.NotifyAddressID = ca.FretrackAddressId
        WHERE h.FretrackCargoID = @FretrackCargoId
          AND h.NotifyAddressID IS NOT NULL;

        UPDATE h
        SET h.CargoMintForwardingAgentAddressID = ca.CompanyAddressId
        FROM dbo.Fretrack_HBL_Staging h
        JOIN dbo.CompanyAddress ca
            ON h.ForwardingAgentAddressID = ca.FretrackAddressId
        WHERE h.FretrackCargoID = @FretrackCargoId
          AND h.ForwardingAgentAddressID IS NOT NULL;

        UPDATE dbo.Fretrack_HBL_Staging
        SET
            IsSelectedForMigration = 1,
            MigrationRemarks = 'HBL',
            ImportedOn = GETDATE()
        WHERE FretrackCargoID = @FretrackCargoId
          AND ISNULL(isDeleted, 0) = 0;

        INSERT INTO dbo.ShipmentHBL
        (
            ShipmentID,
            ShipmentServiceID,
            DocumentTypeID,
            DocumentType,
            MblNumber,
            HblNumber,
            DocumentNumber,
            ShipperAddressID,
            ShipperName,
            ShipperAddress,
            ConsigneeAddressID,
            ConsigneeName,
            ConsigneeAddress,
            NotifyAddressID,
            NotifyName,
            NotifyAddress,
            ForwardingAgentAddressID,
            ForwardingAgent,
            ForwardingAgentAddress,
            DeliveryInstructions,
            PreCarriageBy,
            PlaceOfReceipt,
            VesselVoyage,
            PortOfLoading,
            PortOfDischarge,
            PlaceOfDelivery,
            LoadingTerminal,
            IsContainerized,
            MarksAndNumbers,
            NoOfPackages,
            DescriptionOfPackagesGoods,
            GrossWeight,
            Measurement,
            DeclaredValue,
            HBLDate,
            HBLPlace,
            ByAgentForCarrier,
            FreightCharges,
            Description1,
            Description2,
            CreatedBy,
            DateCreated,
            DeletedBy,
            DateDeleted,
            IsDeleted,
            OrgId,
            ModifiedBy,
            UpdatedDate,
            FretrackCargoID,
            FretrackHBLID
        )
        SELECT
            s.CargoMintShipmentID,
            s.CargoMintShipmentServiceID,
            CASE
                WHEN UPPER(LTRIM(RTRIM(s.DocumentType))) = 'HBL' THEN 1
                WHEN UPPER(LTRIM(RTRIM(s.DocumentType))) = 'MBL' THEN 2
                ELSE s.DocumentTypeID
            END,
            s.DocumentType,
            s.MBLNumber,
            s.HBLNumber,
            s.DocumentNumber,
            s.CargoMintShipperAddressID,
            LEFT(s.ShipperName, 100),
            LEFT(s.ShipperAddress, 400),
            s.CargoMintConsigneeAddressID,
            LEFT(s.ConsigneeName, 100),
            LEFT(s.ConsigneeAddress, 400),
            s.CargoMintNotifyAddressID,
            LEFT(s.NotifyName, 100),
            LEFT(s.NotifyAddress, 400),
            s.CargoMintForwardingAgentAddressID,
            LEFT(s.ForwardingAgent, 100),
            LEFT(s.ForwardingAgentAddress, 300),
            LEFT(s.DeliveryInstructions, 500),
            LEFT(s.PreCarriageBy, 150),
            LEFT(s.PlaceOfReceipt, 150),
            LEFT(s.VesselVoyage, 150),
            LEFT(s.PortOfLoading, 150),
            LEFT(s.PortOfDischarge, 150),
            LEFT(s.PlaceOfDelivery, 150),
            LEFT(s.LoadingTerminal, 150),
            s.isContainerized,
            s.MarksandNumbers,
            CAST(s.NoOfPackages AS nvarchar(max)),
            s.DescriptionOfPackagesGoods,
            CAST(s.GrossWeight AS nvarchar(300)),
            CAST(s.Measurement AS nvarchar(300)),
            CAST(s.DeclaredValue AS nvarchar(100)),
            s.HBLDate,
            LEFT(s.HBLPlace, 50),
            LEFT(s.ByAgentForCarrier, 100),
            s.FreightCharges,
            LEFT(s.Description1, 200),
            LEFT(s.Description2, 200),
            s.CargoMintCreatedBy,
            s.DateCreated,
            s.CargoMintDeletedBy,
            NULL,
            ISNULL(s.isDeleted, 0),
            s.OrgId,
            s.CargoMintModifiedBy,
            s.DateModified,
            s.FretrackCargoID,
            s.FretrackHBLID
        FROM dbo.Fretrack_HBL_Staging s
        WHERE s.FretrackCargoID = @FretrackCargoId
          AND s.IsSelectedForMigration = 1
          AND s.CargoMintShipmentID IS NOT NULL
          AND s.CargoMintShipmentServiceID IS NOT NULL
          AND ISNULL(s.isDeleted, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ShipmentHBL h
              WHERE h.FretrackHBLID = s.FretrackHBLID
          );

        UPDATE h
        SET
            h.ShipmentID = s.CargoMintShipmentID,
            h.ShipmentServiceID = s.CargoMintShipmentServiceID,
            h.DocumentTypeID = CASE
                                   WHEN UPPER(LTRIM(RTRIM(s.DocumentType))) = 'HBL' THEN 1
                                   WHEN UPPER(LTRIM(RTRIM(s.DocumentType))) = 'MBL' THEN 2
                                   ELSE s.DocumentTypeID
                               END,
            h.DocumentType = s.DocumentType,
            h.MblNumber = s.MBLNumber,
            h.HblNumber = s.HBLNumber,
            h.DocumentNumber = s.DocumentNumber,
            h.ShipperAddressID = s.CargoMintShipperAddressID,
            h.ShipperName = LEFT(s.ShipperName, 100),
            h.ShipperAddress = LEFT(s.ShipperAddress, 400),
            h.ConsigneeAddressID = s.CargoMintConsigneeAddressID,
            h.ConsigneeName = LEFT(s.ConsigneeName, 100),
            h.ConsigneeAddress = LEFT(s.ConsigneeAddress, 400),
            h.NotifyAddressID = s.CargoMintNotifyAddressID,
            h.NotifyName = LEFT(s.NotifyName, 100),
            h.NotifyAddress = LEFT(s.NotifyAddress, 400),
            h.ForwardingAgentAddressID = s.CargoMintForwardingAgentAddressID,
            h.ForwardingAgent = LEFT(s.ForwardingAgent, 100),
            h.ForwardingAgentAddress = LEFT(s.ForwardingAgentAddress, 300),
            h.DeliveryInstructions = LEFT(s.DeliveryInstructions, 500),
            h.PreCarriageBy = LEFT(s.PreCarriageBy, 150),
            h.PlaceOfReceipt = LEFT(s.PlaceOfReceipt, 150),
            h.VesselVoyage = LEFT(s.VesselVoyage, 150),
            h.PortOfLoading = LEFT(s.PortOfLoading, 150),
            h.PortOfDischarge = LEFT(s.PortOfDischarge, 150),
            h.PlaceOfDelivery = LEFT(s.PlaceOfDelivery, 150),
            h.LoadingTerminal = LEFT(s.LoadingTerminal, 150),
            h.IsContainerized = s.isContainerized,
            h.MarksAndNumbers = s.MarksandNumbers,
            h.NoOfPackages = CAST(s.NoOfPackages AS nvarchar(max)),
            h.DescriptionOfPackagesGoods = s.DescriptionOfPackagesGoods,
            h.GrossWeight = CAST(s.GrossWeight AS nvarchar(300)),
            h.Measurement = CAST(s.Measurement AS nvarchar(300)),
            h.DeclaredValue = CAST(s.DeclaredValue AS nvarchar(100)),
            h.HBLDate = s.HBLDate,
            h.HBLPlace = LEFT(s.HBLPlace, 50),
            h.ByAgentForCarrier = LEFT(s.ByAgentForCarrier, 100),
            h.FreightCharges = s.FreightCharges,
            h.Description1 = LEFT(s.Description1, 200),
            h.Description2 = LEFT(s.Description2, 200),
            h.CreatedBy = s.CargoMintCreatedBy,
            h.DateCreated = s.DateCreated,
            h.DeletedBy = s.CargoMintDeletedBy,
            h.IsDeleted = ISNULL(s.isDeleted, 0),
            h.OrgId = s.OrgId,
            h.ModifiedBy = s.CargoMintModifiedBy,
            h.UpdatedDate = s.DateModified,
            h.FretrackCargoID = s.FretrackCargoID
        FROM dbo.ShipmentHBL h
        JOIN dbo.Fretrack_HBL_Staging s
            ON h.FretrackHBLID = s.FretrackHBLID
        WHERE s.FretrackCargoID = @FretrackCargoId;

        /* =========================================================
           17. Cargo document staging update only
           Actual upload/insert should be done by API/service layer
           ========================================================= */
        UPDATE s
        SET s.CargoMintShipmentID = sh.ShipmentId
        FROM dbo.Fretrack_CargoDocuments_Staging s
        JOIN dbo.Shipments sh
            ON s.CargoID = sh.FretrackCargoId
        WHERE s.CargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintShipmentServiceID = sd.ShipmentServiceId
        FROM dbo.Fretrack_CargoDocuments_Staging s
        JOIN dbo.ShipmentServiceDetails sd
            ON s.CargoID = sd.FretrackCargoID
        WHERE s.CargoID = @FretrackCargoId;

        UPDATE s
        SET s.CargoMintCreatedBy = u.CargoMintUserID
        FROM dbo.Fretrack_CargoDocuments_Staging s
        JOIN dbo.Fretrack_UserMaster_Staging u
            ON s.CreatedBy = u.FretrackUserId
        WHERE s.CargoID = @FretrackCargoId;

        COMMIT TRAN;

        SELECT
            @FretrackCargoId AS FretrackCargoId,
            sh.ShipmentId,
            sh.PrimaryServiceId,
            sh.JobNo,
            sh.CustomerId
        FROM dbo.Shipments sh
        WHERE sh.FretrackCargoId = @FretrackCargoId;

        SELECT
            'ShipmentServiceDetails' AS TableName,
            COUNT(*) AS TotalRows
        FROM dbo.ShipmentServiceDetails
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'ShipmentContainers',
            COUNT(*)
        FROM dbo.ShipmentContainers
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'ShipmentPackages',
            COUNT(*)
        FROM dbo.ShipmentPackages
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'ShipmentRouting',
            COUNT(*)
        FROM dbo.ShipmentRouting
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'ShipmentParties',
            COUNT(*)
        FROM dbo.ShipmentParties
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'Invoices',
            COUNT(*)
        FROM dbo.Invoices
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'Bills',
            COUNT(*)
        FROM dbo.Bills
        WHERE FretrackCargoID = @FretrackCargoId

        UNION ALL

        SELECT
            'ShipmentHBL',
            COUNT(*)
        FROM dbo.ShipmentHBL
        WHERE FretrackCargoID = @FretrackCargoId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRAN;

        THROW;
    END CATCH;
END;
GO
