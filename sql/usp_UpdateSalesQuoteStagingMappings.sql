-- Deploy in CargoMintDB. Updates staging mappings only; no actual-table inserts.
-- Mapping masters must contain one target match per source value.
CREATE OR ALTER PROCEDURE dbo.usp_UpdateSalesQuoteStagingMappings
    @SalesQuoteID int,
    @OrgId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SalesQuoteID IS NULL OR @OrgId IS NULL
        THROW 51100, 'SalesQuoteID and OrgId are required.', 1;
    DECLARE @OwnTransaction bit=CASE WHEN @@TRANCOUNT=0 THEN 1 ELSE 0 END;
    DECLARE @LockResult int, @HeaderRows int, @DetailRows int,
            @NominationRows int, @ChargeRows int;

    BEGIN TRY
        IF @OwnTransaction=1 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION SalesQuoteStagingMappings;
        EXEC @LockResult = sys.sp_getapplock
            @Resource=N'FretrackSalesQuoteSync:v1', @LockMode='Exclusive',
            @LockOwner='Transaction', @LockTimeout=15000;
        IF @LockResult < 0
            THROW 51102, 'Sales quote sync or migration is busy. Retry.', 1;

        IF (SELECT COUNT(*) FROM dbo.Fretrack_SalesQuote_New
            WHERE SalesQuoteID=@SalesQuoteID) <> 1
            THROW 51103, 'Expected exactly one staged quotation for SalesQuoteID.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Organizations WHERE OrgId=@OrgId)
            THROW 51104, 'Invalid CargoMint OrgId.', 1;

        -- 1. Header mappings
        UPDATE s
        SET CargoMintCompanyID=COALESCE(c.CompanyId,s.CargoMintCompanyID),
            CargoMintContactID=COALESCE(ct.ContactId,s.CargoMintContactID),
            CargoMintIncoTermID=COALESCE(it.IncoTermId,s.CargoMintIncoTermID),
            CargoMintPreparedBy=COALESCE(p.CargoMintUserID,s.CargoMintPreparedBy),
            CargoMintSalesPersonID=COALESCE(sp.CargoMintUserID,s.CargoMintSalesPersonID),
            CargoMintCreatedBy=COALESCE(cr.CargoMintUserID,s.CargoMintCreatedBy),
            CargoMintModifiedBy=COALESCE(mo.CargoMintUserID,s.CargoMintModifiedBy),
            CargoMintDeletedBy=COALESCE(de.CargoMintUserID,s.CargoMintDeletedBy),
            CargoMintSalesQuoteCurrencyID=COALESCE(mappedCur.CurrencyId,cur.CurrencyId,s.CargoMintSalesQuoteCurrencyID),
            CargoMintCommodityValueCurrencyID=COALESCE(mappedValueCur.CurrencyId,valueCur.CurrencyId,s.CargoMintCommodityValueCurrencyID),
            CargoMintCompanyAddressID=COALESCE(ca.CompanyAddressId,s.CargoMintCompanyAddressID),
            CargoMintPickupAddressID=COALESCE(pa.CompanyAddressId,s.CargoMintPickupAddressID),
            CargoMintDeliveryAddressID=COALESCE(da.CompanyAddressId,s.CargoMintDeliveryAddressID)
        FROM dbo.Fretrack_SalesQuote_New s
        LEFT JOIN dbo.Companies c
            ON ','+c.FretrackCompanyIDs+',' LIKE '%,'+CAST(s.CompanyID AS varchar(20))+',%'
           AND c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0
        LEFT JOIN dbo.CompanyContacts ct
            ON ct.FretrackContactID=s.ContactID
           AND ct.CompanyID=COALESCE(c.CompanyId,s.CargoMintCompanyID)
           AND ct.OrgId=@OrgId AND ISNULL(ct.IsDeleted,0)=0
        LEFT JOIN dbo.Fretrack_IncoTerm_Master_Staging sourceInco
            ON sourceInco.IncoTermID=s.IncoTermID
        LEFT JOIN dbo.IncoTermMaster it
            ON it.IncoTermId=sourceInco.CargoMintIncoTermID
           AND (it.OrgId=@OrgId OR it.OrgId IS NULL) AND ISNULL(it.IsDeleted,0)=0
        LEFT JOIN dbo.Fretrack_UserMaster_Staging p ON p.FretrackUserId=s.PreparedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging sp ON sp.FretrackUserId=s.SalesPersonID
        LEFT JOIN dbo.Fretrack_UserMaster_Staging cr ON cr.FretrackUserId=s.CreatedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging mo ON mo.FretrackUserId=s.ModifiedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging de ON de.FretrackUserId=s.DeletedBy
        LEFT JOIN dbo.Fretrack_CurrencyMaster_Staging sourceCur
            ON sourceCur.FretrackCurrencyID=s.SalesQuoteCurrencyID
        LEFT JOIN dbo.Currencies mappedCur
            ON mappedCur.CurrencyId=sourceCur.CargoMintCurrencyID AND ISNULL(mappedCur.IsDeleted,0)=0
        LEFT JOIN dbo.Currencies cur
            ON cur.CurrencyCode=COALESCE(NULLIF(LTRIM(RTRIM(s.SalesQuoteCurrency)),''),sourceCur.CurrencyCode)
           AND ISNULL(cur.IsDeleted,0)=0
        LEFT JOIN dbo.Fretrack_CurrencyMaster_Staging sourceValueCur
            ON sourceValueCur.FretrackCurrencyID=s.CommodityValueCurrencyID
        LEFT JOIN dbo.Currencies mappedValueCur
            ON mappedValueCur.CurrencyId=sourceValueCur.CargoMintCurrencyID AND ISNULL(mappedValueCur.IsDeleted,0)=0
        LEFT JOIN dbo.Currencies valueCur
            ON valueCur.CurrencyCode=COALESCE(NULLIF(LTRIM(RTRIM(s.CommodityValueCurrency)),''),sourceValueCur.CurrencyCode)
           AND ISNULL(valueCur.IsDeleted,0)=0
        LEFT JOIN dbo.CompanyAddress ca
            ON ca.CompanyID=COALESCE(c.CompanyId,s.CargoMintCompanyID)
           AND ca.OrgId=@OrgId AND ISNULL(ca.IsDeleted,0)=0
           AND (ca.FretrackAddressID=s.CompanyAddressID OR EXISTS
                (SELECT 1 FROM STRING_SPLIT(ca.FretrackAddressIDs,',') v
                 WHERE TRY_CONVERT(int,LTRIM(RTRIM(v.value)))=s.CompanyAddressID))
        LEFT JOIN dbo.CompanyAddress pa
            ON s.PickupAddressID IS NOT NULL AND pa.OrgId=@OrgId AND ISNULL(pa.IsDeleted,0)=0
           AND (pa.FretrackAddressID=s.PickupAddressID OR EXISTS
                (SELECT 1 FROM STRING_SPLIT(pa.FretrackAddressIDs,',') v
                 WHERE TRY_CONVERT(int,LTRIM(RTRIM(v.value)))=s.PickupAddressID))
        LEFT JOIN dbo.CompanyAddress da
            ON s.DeliveryAddressID IS NOT NULL AND da.OrgId=@OrgId AND ISNULL(da.IsDeleted,0)=0
           AND (da.FretrackAddressID=s.DeliveryAddressID OR EXISTS
                (SELECT 1 FROM STRING_SPLIT(da.FretrackAddressIDs,',') v
                 WHERE TRY_CONVERT(int,LTRIM(RTRIM(v.value)))=s.DeliveryAddressID))
        WHERE s.SalesQuoteID=@SalesQuoteID;
        SET @HeaderRows=@@ROWCOUNT;

        -- 2. Detail location and carrier mappings
        UPDATE d
        SET CargoMintPlaceOfOriginID=COALESCE(origin.CargoMintLocationId,d.CargoMintPlaceOfOriginID),
            CargoMintPOLID=COALESCE(pol.CargoMintLocationId,d.CargoMintPOLID),
            CargoMintPOT1ID=COALESCE(pot.CargoMintLocationId,d.CargoMintPOT1ID),
            CargoMintPODID=COALESCE(pod.CargoMintLocationId,d.CargoMintPODID),
            CargoMintPlaceOfDeliveryID=COALESCE(delivery.CargoMintLocationId,d.CargoMintPlaceOfDeliveryID),
            CargoMintCarrierID=COALESCE(
                CASE WHEN UPPER(LTRIM(RTRIM(h.Modeoftransport)))='AIR' THEN airline.CargoMintCarrierId
                     WHEN UPPER(LTRIM(RTRIM(h.Modeoftransport))) IN ('OCEAN','SURFACE') THEN ocean.CargoMintCarrierId END,
                d.CargoMintCarrierID)
        FROM dbo.Fretrack_SalesQuoteDetails_New d
        JOIN dbo.Fretrack_SalesQuote_New h ON h.SalesQuoteID=d.SalesQuoteID
        LEFT JOIN dbo.Fretrack_Location_Master origin ON origin.LocationID=d.PlaceOfOriginID
        LEFT JOIN dbo.Fretrack_Location_Master pol ON pol.LocationID=d.POLID
        LEFT JOIN dbo.Fretrack_Location_Master pot ON pot.LocationID=d.POT1ID
        LEFT JOIN dbo.Fretrack_Location_Master pod ON pod.LocationID=d.PODID
        LEFT JOIN dbo.Fretrack_Location_Master delivery ON delivery.LocationID=d.PlaceOfDeliveryID
        LEFT JOIN dbo.Fretrack_AirlineMaster_Staging airline ON airline.AirlineID=d.CarrierID
        LEFT JOIN dbo.Fretrack_OceanLineMaster_Staging ocean ON ocean.OceanLineID=d.CarrierID
        WHERE d.SalesQuoteID=@SalesQuoteID;
        SET @DetailRows=@@ROWCOUNT;

        -- 3. Nomination mappings
        UPDATE n
        SET CargoMintAgentID=COALESCE(c.CompanyId,n.CargoMintAgentID),
            CargoMintAgentAddressID=COALESCE(a.CompanyAddressId,n.CargoMintAgentAddressID),
            CargoMintCreatedBy=COALESCE(cr.CargoMintUserID,n.CargoMintCreatedBy),
            CargoMintModifiedBy=COALESCE(mo.CargoMintUserID,n.CargoMintModifiedBy),
            CargoMintDeletedBy=COALESCE(de.CargoMintUserID,n.CargoMintDeletedBy)
        FROM dbo.Fretrack_SalesQuoteNomDetails n
        LEFT JOIN dbo.Companies c
            ON ','+c.FretrackCompanyIDs+',' LIKE '%,'+CAST(n.AgentID AS varchar(20))+',%'
           AND c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0
        LEFT JOIN dbo.CompanyAddress a
            ON a.CompanyId=COALESCE(c.CompanyId,n.CargoMintAgentID)
           AND
           (
               a.FretrackAddressId=n.AgentAddressID
               OR EXISTS
               (
                   SELECT 1
                   FROM STRING_SPLIT(a.FretrackAddressIDs, ',') addressID
                   WHERE TRY_CONVERT(int,LTRIM(RTRIM(addressID.value)))=n.AgentAddressID
               )
           )
        LEFT JOIN dbo.Fretrack_UserMaster_Staging cr ON cr.FretrackUserId=n.CreatedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging mo ON mo.FretrackUserId=n.ModifiedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging de ON de.FretrackUserId=n.DeletedBy
        WHERE n.SQID=@SalesQuoteID;
        SET @NominationRows=@@ROWCOUNT;

        -- 4. Charge mappings
        UPDATE ch
        SET CargoMintPayingPartyID=COALESCE(c.CompanyId,ch.CargoMintPayingPartyID),
            CargoMintChargeItemID=COALESCE(cm.CargoMintChargeItemID,ch.CargoMintChargeItemID),
            CargoMintCurrencyID=COALESCE(cur.CurrencyId,ch.CargoMintCurrencyID),
            CargoMintCreatedBy=COALESCE(cr.CargoMintUserID,ch.CargoMintCreatedBy),
            CargoMintModifiedBy=COALESCE(mo.CargoMintUserID,ch.CargoMintModifiedBy),
            CargoMintDeletedBy=COALESCE(de.CargoMintUserID,ch.CargoMintDeletedBy)
        FROM dbo.Fretrack_SalesQuoteCharges_New ch
        LEFT JOIN dbo.Companies c
            ON ','+c.FretrackCompanyIDs+',' LIKE '%,'+CAST(ch.PayingPartyID AS varchar(20))+',%'
           AND c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0
        LEFT JOIN dbo.Fretrack_Staging_CC_CM_Mapped cm
            ON LTRIM(RTRIM(cm.FretrackChargeItemName))=LTRIM(RTRIM(ch.ChargeDescription))
        LEFT JOIN dbo.Currencies cur ON cur.CurrencyCode=ch.CurrencyCode AND ISNULL(cur.IsDeleted,0)=0
        LEFT JOIN dbo.Fretrack_UserMaster_Staging cr ON cr.FretrackUserId=ch.CreatedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging mo ON mo.FretrackUserId=ch.ModifiedBy
        LEFT JOIN dbo.Fretrack_UserMaster_Staging de ON de.FretrackUserId=ch.DeletedBy
        WHERE ch.SQID=@SalesQuoteID;
        SET @ChargeRows=@@ROWCOUNT;

        IF @OwnTransaction=1 COMMIT TRANSACTION;

        -- Counts are rows processed, not proof that every mapping was resolved.
        SELECT @SalesQuoteID SalesQuoteID,@OrgId OrgId,
               @HeaderRows HeaderRowsProcessed,@DetailRows DetailRowsProcessed,
               @NominationRows NominationRowsProcessed,@ChargeRows ChargeRowsProcessed;
    END TRY
    BEGIN CATCH
        IF @OwnTransaction=1 AND XACT_STATE()<>0 ROLLBACK TRANSACTION;
        ELSE IF @OwnTransaction=0 AND XACT_STATE()=1
            ROLLBACK TRANSACTION SalesQuoteStagingMappings;
        -- A doomed caller transaction must be rolled back by its owner (the API).
        THROW;
    END CATCH;
END;
GO

-- Execute separately after deploying this procedure:
-- EXEC dbo.usp_UpdateSalesQuoteStagingMappings @SalesQuoteID=227759, @OrgId=18;
