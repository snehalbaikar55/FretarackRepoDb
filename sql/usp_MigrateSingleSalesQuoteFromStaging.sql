-- Deploy in CargoMintDB. SQL Server 2016 SP1+; database compatibility level >= 130.
-- Staging identifiers remain plain int. This script creates no permanent tables or IDENTITY columns.
-- Target IDs must be database-generated (IDENTITY or a configured DEFAULT/sequence).
CREATE OR ALTER PROCEDURE dbo.usp_MigrateSingleSalesQuoteFromStaging
    @SalesQuoteNumber varchar(20),
    @OrgId int,
    @QuotationCurrencyId int,
    @LocalCurrencyId int,
    @HeaderExRate decimal(22,8),
    @ServiceTypeId int,
    @TransportModeId int,
    @TransportDirectionId int,
    @QuotationStatus nvarchar(50),
    @ServiceStatus nvarchar(50),
    @OptionStatus nvarchar(50),
    @ContactId int = NULL,
    @CommodityId int = NULL,
    @IncoTermId int = NULL,
    @DefaultApplyPerId int = NULL,
    @DryRun bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Own the transaction: never roll back an unrelated caller transaction.
    IF @@TRANCOUNT <> 0
        THROW 51000, 'Call this procedure outside an existing transaction.', 1;
    IF @DryRun IS NULL THROW 51000, 'DryRun must be 0 or 1.', 1;
    IF NULLIF(LTRIM(RTRIM(@SalesQuoteNumber)), '') IS NULL
        THROW 51001, 'SalesQuoteNumber is required.', 1;
    IF @HeaderExRate IS NULL OR @HeaderExRate <= 0
        THROW 51002, 'HeaderExRate must be positive and expressed as local currency per quotation currency.', 1;
    IF @QuotationCurrencyId = @LocalCurrencyId AND @HeaderExRate <> 1
        THROW 51003, 'HeaderExRate must be 1 when quotation and local currency are the same.', 1;
    IF NULLIF(LTRIM(RTRIM(@QuotationStatus)), '') IS NULL
       OR NULLIF(LTRIM(RTRIM(@ServiceStatus)), '') IS NULL
       OR NULLIF(LTRIM(RTRIM(@OptionStatus)), '') IS NULL
        THROW 51004, 'Pass explicit valid quotation, service and option statuses.', 1;

    DECLARE @SQID int, @QuotationId int, @LockResult int;
    DECLARE @HeaderToLocal decimal(22,8) = @HeaderExRate;
    BEGIN TRY
        BEGIN TRANSACTION;
        -- Same resource as staging sync: do not read a half-staged quote.
        EXEC @LockResult = sys.sp_getapplock
            @Resource = N'FretrackSalesQuoteSync:v1', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 15000;
        IF @LockResult < 0 THROW 51005, 'Sales quote sync/migration is busy. Retry.', 1;
        -- Prevent this procedure from racing on quotation-number creation across source IDs.
        EXEC @LockResult = sys.sp_getapplock
            @Resource = N'FretrackSalesQuoteActualMigration:v1', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 15000;
        IF @LockResult < 0 THROW 51005, 'Another quotation migration is running. Retry.', 1;

        IF (SELECT COUNT(*) FROM dbo.Fretrack_SalesQuote_New WITH (UPDLOCK, HOLDLOCK)
            WHERE SalesQuoteNumber = @SalesQuoteNumber) <> 1
            THROW 51006, 'Expected exactly one staged quotation with this number.', 1;
        SELECT @SQID = SalesQuoteID, @QuotationId = CargoMintSalesQuoteID
        FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteNumber = @SalesQuoteNumber;
        IF @QuotationId IS NOT NULL
            THROW 51007, 'This staged quote already has a CargoMint quotation ID; automatic re-migration is disabled.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID = @SQID AND isDeleted = 1)
            THROW 51008, 'Deleted source quotations cannot be migrated.', 1;
        IF EXISTS (SELECT 1 FROM dbo.SalesQuotations WITH (UPDLOCK, HOLDLOCK)
                   WHERE orgId = @OrgId AND QuotationNo = @SalesQuoteNumber)
            THROW 51009, 'A quotation with this number already exists in the target organization.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Organizations WHERE OrgId = @OrgId)
            THROW 51010, 'Invalid OrgId.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE CurrencyId = @QuotationCurrencyId)
           OR NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE CurrencyId = @LocalCurrencyId)
            THROW 51011, 'Quotation/local currency does not exist.', 1;
        IF @ServiceTypeId IS NULL OR @TransportModeId IS NULL OR @TransportDirectionId IS NULL
            THROW 51012, 'Pass explicit CargoMint service type, mode and direction IDs.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@SQID AND SalesQuoteDate IS NULL)
            THROW 51013, 'Source SalesQuoteDate is missing.', 1;

        -- Reject ambiguous staged identities rather than letting joins multiply rows.
        IF EXISTS (SELECT SalesQuoteID FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@SQID GROUP BY SalesQuoteID HAVING COUNT(*)>1)
           OR EXISTS (SELECT SalesQuoteDetailID FROM dbo.Fretrack_SalesQuoteDetails_New WHERE SalesQuoteID=@SQID GROUP BY SalesQuoteDetailID HAVING COUNT(*)>1)
           OR EXISTS (SELECT SQChargeID FROM dbo.Fretrack_SalesQuoteCharges_New WHERE SQID=@SQID GROUP BY SQChargeID HAVING COUNT(*)>1)
           OR EXISTS (SELECT SqNomDetailID FROM dbo.Fretrack_SalesQuoteNomDetails WHERE SQID=@SQID GROUP BY SqNomDetailID HAVING COUNT(*)>1)
            THROW 51014, 'Duplicate source IDs exist in staging.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteDetails_New WHERE SalesQuoteID=@SQID)
            THROW 51015, 'No staged detail rows exist; a service cannot be inferred.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteCharges_New c
                   WHERE c.SQID=@SQID AND ISNULL(c.isDeleted,0)=0
                     AND NOT EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteDetails_New d
                                     WHERE d.SalesQuoteID=@SQID AND d.SalesQuoteDetailID=c.SQDetailRowID))
            THROW 51016, 'An active charge has a missing or invalid source detail reference.', 1;

        /* Company mappings: existing job-migration convention, scoped to @OrgId.
           Preserve explicit staged mappings, but require they belong to the target org. */
        SELECT DISTINCT SourceID INTO #CompanySource FROM
        (
            SELECT CompanyID SourceID FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@SQID
            UNION SELECT PayingPartyID FROM dbo.Fretrack_SalesQuoteCharges_New WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0
            UNION SELECT AgentID FROM dbo.Fretrack_SalesQuoteNomDetails WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0
        ) s WHERE SourceID IS NOT NULL AND SourceID <> 0;
        SELECT s.SourceID, c.CompanyId INTO #CompanyCandidates
        FROM #CompanySource s JOIN dbo.Companies c ON c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0
        CROSS APPLY STRING_SPLIT(c.FretrackCompanyIDs, ',') token
        WHERE TRY_CONVERT(int,LTRIM(RTRIM(token.value)))=s.SourceID;
        IF EXISTS (SELECT SourceID FROM #CompanyCandidates GROUP BY SourceID HAVING COUNT(DISTINCT CompanyId)>1)
            THROW 51017, 'A Fretrack company maps to multiple CargoMint companies in this organization.', 1;
        UPDATE h SET CargoMintCompanyID=COALESCE(h.CargoMintCompanyID,m.CompanyId),
                     CargoMintOfficeID=@OrgId,
                     CargoMintContactID=COALESCE(@ContactId,h.CargoMintContactID),
                     CargoMintIncoTermID=COALESCE(@IncoTermId,h.CargoMintIncoTermID),
                     CargoMintSalesQuoteCurrencyID=@QuotationCurrencyId
        FROM dbo.Fretrack_SalesQuote_New h
        LEFT JOIN (SELECT SourceID,MIN(CompanyId) CompanyId FROM #CompanyCandidates GROUP BY SourceID) m ON m.SourceID=h.CompanyID
        WHERE h.SalesQuoteID=@SQID;
        UPDATE c SET CargoMintPayingPartyID=COALESCE(c.CargoMintPayingPartyID,m.CompanyId)
        FROM dbo.Fretrack_SalesQuoteCharges_New c
        JOIN (SELECT SourceID,MIN(CompanyId) CompanyId FROM #CompanyCandidates GROUP BY SourceID) m ON m.SourceID=c.PayingPartyID
        WHERE c.SQID=@SQID;
        UPDATE n SET CargoMintAgentID=COALESCE(n.CargoMintAgentID,m.CompanyId)
        FROM dbo.Fretrack_SalesQuoteNomDetails n
        JOIN (SELECT SourceID,MIN(CompanyId) CompanyId FROM #CompanyCandidates GROUP BY SourceID) m ON m.SourceID=n.AgentID
        WHERE n.SQID=@SQID;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuote_New h WHERE h.SalesQuoteID=@SQID
                   AND NOT EXISTS (SELECT 1 FROM dbo.Companies c WHERE c.CompanyId=h.CargoMintCompanyID AND c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0))
            THROW 51018, 'Customer mapping is missing or belongs to another organization.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteCharges_New s WHERE s.SQID=@SQID AND ISNULL(s.isDeleted,0)=0
                   AND NOT EXISTS (SELECT 1 FROM dbo.Companies c WHERE c.CompanyId=s.CargoMintPayingPartyID AND c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0))
            THROW 51019, 'A charge paying-party mapping is missing or belongs to another organization.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuote_New h WHERE h.SalesQuoteID=@SQID AND
                   (h.CargoMintContactID IS NULL OR NOT EXISTS
                    (SELECT 1 FROM dbo.CompanyContacts c WHERE c.ContactId=h.CargoMintContactID AND c.CompanyId=h.CargoMintCompanyID
                     AND c.OrgId=@OrgId AND ISNULL(c.IsDeleted,0)=0)))
            THROW 51020, 'Supply ContactId or a staged CargoMintContactID belonging to the mapped customer and org.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@SQID AND NULLIF(IncoTermID,0) IS NOT NULL AND CargoMintIncoTermID IS NULL)
            THROW 51021, 'Source Incoterm requires an explicit target mapping.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@SQID AND NULLIF(LTRIM(RTRIM(Commodity)),N'') IS NOT NULL) AND @CommodityId IS NULL
            THROW 51022, 'Pass CommodityId for the source commodity text.', 1;

        /* User mappings, same master used by the existing job procedure. */
        SELECT FretrackUserId, MIN(CargoMintUserID) TargetID INTO #UserMap
        FROM dbo.Fretrack_UserMaster_Staging
        GROUP BY FretrackUserId HAVING COUNT(DISTINCT CargoMintUserID)=1;
        -- Only touched source users need validation; unresolved nonzero IDs fail below.
        UPDATE s SET CargoMintPreparedBy=COALESCE(s.CargoMintPreparedBy,p.TargetID),
                     CargoMintSalesPersonID=COALESCE(s.CargoMintSalesPersonID,sp.TargetID),
                     CargoMintCreatedBy=COALESCE(s.CargoMintCreatedBy,cr.TargetID),
                     CargoMintModifiedBy=COALESCE(s.CargoMintModifiedBy,mo.TargetID),
                     CargoMintDeletedBy=COALESCE(s.CargoMintDeletedBy,de.TargetID)
        FROM dbo.Fretrack_SalesQuote_New s
        LEFT JOIN #UserMap p ON p.FretrackUserId=s.PreparedBy
        LEFT JOIN #UserMap sp ON sp.FretrackUserId=s.SalesPersonID
        LEFT JOIN #UserMap cr ON cr.FretrackUserId=s.CreatedBy
        LEFT JOIN #UserMap mo ON mo.FretrackUserId=s.ModifiedBy
        LEFT JOIN #UserMap de ON de.FretrackUserId=s.DeletedBy
        WHERE s.SalesQuoteID=@SQID;
        UPDATE s SET CargoMintCreatedBy=COALESCE(s.CargoMintCreatedBy,cr.TargetID),
                     CargoMintModifiedBy=COALESCE(s.CargoMintModifiedBy,mo.TargetID),
                     CargoMintDeletedBy=COALESCE(s.CargoMintDeletedBy,de.TargetID)
        FROM dbo.Fretrack_SalesQuoteCharges_New s
        LEFT JOIN #UserMap cr ON cr.FretrackUserId=s.CreatedBy
        LEFT JOIN #UserMap mo ON mo.FretrackUserId=s.ModifiedBy
        LEFT JOIN #UserMap de ON de.FretrackUserId=s.DeletedBy WHERE s.SQID=@SQID;
        UPDATE s SET CargoMintCreatedBy=COALESCE(s.CargoMintCreatedBy,cr.TargetID),
                     CargoMintModifiedBy=COALESCE(s.CargoMintModifiedBy,mo.TargetID),
                     CargoMintDeletedBy=COALESCE(s.CargoMintDeletedBy,de.TargetID)
        FROM dbo.Fretrack_SalesQuoteNomDetails s
        LEFT JOIN #UserMap cr ON cr.FretrackUserId=s.CreatedBy
        LEFT JOIN #UserMap mo ON mo.FretrackUserId=s.ModifiedBy
        LEFT JOIN #UserMap de ON de.FretrackUserId=s.DeletedBy WHERE s.SQID=@SQID;
        SELECT v.SourceID,v.TargetID INTO #TouchedUsers
        FROM dbo.Fretrack_SalesQuote_New h
        CROSS APPLY (VALUES (h.PreparedBy,h.CargoMintPreparedBy),(h.SalesPersonID,h.CargoMintSalesPersonID),
                    (h.CreatedBy,h.CargoMintCreatedBy),(h.ModifiedBy,h.CargoMintModifiedBy),(h.DeletedBy,h.CargoMintDeletedBy)) v(SourceID,TargetID)
        WHERE h.SalesQuoteID=@SQID
        UNION ALL
        SELECT v.SourceID,v.TargetID FROM dbo.Fretrack_SalesQuoteCharges_New c
        CROSS APPLY (VALUES (c.CreatedBy,c.CargoMintCreatedBy),(c.ModifiedBy,c.CargoMintModifiedBy),(c.DeletedBy,c.CargoMintDeletedBy)) v(SourceID,TargetID)
        WHERE c.SQID=@SQID AND ISNULL(c.isDeleted,0)=0;
        IF EXISTS (SELECT 1 FROM #TouchedUsers t WHERE NULLIF(t.SourceID,0) IS NOT NULL
                   AND (t.TargetID IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Id=t.TargetID)))
            THROW 51023, 'A required user mapping is missing, ambiguous, or invalid.', 1;

        /* Location mappings retain route fields even where the production schema has no column. */
        SELECT LocationID,MIN(CargoMintLocationId) TargetID INTO #LocationMap
        FROM dbo.Fretrack_Location_Master GROUP BY LocationID HAVING COUNT(DISTINCT CargoMintLocationId)=1;
        UPDATE d SET CargoMintPOLID=COALESCE(d.CargoMintPOLID,pol.TargetID),
                     CargoMintPODID=COALESCE(d.CargoMintPODID,pod.TargetID),
                     CargoMintPOT1ID=COALESCE(d.CargoMintPOT1ID,pot.TargetID),
                     CargoMintPlaceOfOriginID=COALESCE(d.CargoMintPlaceOfOriginID,po.TargetID),
                     CargoMintPlaceOfDeliveryID=COALESCE(d.CargoMintPlaceOfDeliveryID,pd.TargetID)
        FROM dbo.Fretrack_SalesQuoteDetails_New d
        LEFT JOIN #LocationMap pol ON pol.LocationID=d.POLID
        LEFT JOIN #LocationMap pod ON pod.LocationID=d.PODID
        LEFT JOIN #LocationMap pot ON pot.LocationID=d.POT1ID
        LEFT JOIN #LocationMap po ON po.LocationID=d.PlaceOfOriginID
        LEFT JOIN #LocationMap pd ON pd.LocationID=d.PlaceOfDeliveryID WHERE d.SalesQuoteID=@SQID;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteDetails_New WHERE SalesQuoteID=@SQID AND
                   ((NULLIF(POLID,0) IS NOT NULL AND CargoMintPOLID IS NULL) OR (NULLIF(PODID,0) IS NOT NULL AND CargoMintPODID IS NULL)))
            THROW 51024, 'POL/POD mappings are missing or ambiguous.', 1;

        SELECT AirlineID SourceID,MIN(CargoMintCarrierId) TargetID INTO #AirMap
        FROM dbo.Fretrack_AirlineMaster_Staging GROUP BY AirlineID HAVING COUNT(DISTINCT CargoMintCarrierId)=1;
        SELECT OceanLineID SourceID,MIN(CargoMintCarrierId) TargetID INTO #OceanMap
        FROM dbo.Fretrack_OceanLineMaster_Staging GROUP BY OceanLineID HAVING COUNT(DISTINCT CargoMintCarrierId)=1;
        UPDATE d SET CargoMintCarrierID=COALESCE(d.CargoMintCarrierID,
                  CASE WHEN UPPER(LTRIM(RTRIM(h.Modeoftransport)))='AIR' THEN a.TargetID
                       WHEN UPPER(LTRIM(RTRIM(h.Modeoftransport))) IN ('OCEAN','SURFACE') THEN o.TargetID END)
        FROM dbo.Fretrack_SalesQuoteDetails_New d
        JOIN dbo.Fretrack_SalesQuote_New h ON h.SalesQuoteID=d.SalesQuoteID
        LEFT JOIN #AirMap a ON a.SourceID=d.CarrierID
        LEFT JOIN #OceanMap o ON o.SourceID=d.CarrierID WHERE d.SalesQuoteID=@SQID;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteDetails_New WHERE SalesQuoteID=@SQID AND NULLIF(CarrierID,0) IS NOT NULL AND CargoMintCarrierID IS NULL)
            THROW 51025, 'Carrier mapping is missing, ambiguous, or source transport mode is unsupported.', 1;

        /* Currency, charge and tax mappings. Never choose an arbitrary duplicate match. */
        SELECT UPPER(LTRIM(RTRIM(CurrencyCode))) Code,MIN(CurrencyId) TargetID INTO #CurrencyMap
        FROM dbo.Currencies WHERE ISNULL(IsDeleted,0)=0
        GROUP BY UPPER(LTRIM(RTRIM(CurrencyCode))) HAVING COUNT(DISTINCT CurrencyId)=1;
        SELECT UPPER(LTRIM(RTRIM(FretrackChargeItemName))) Name,MIN(CargoMintChargeItemID) TargetID INTO #ChargeMap
        FROM dbo.Fretrack_Staging_CC_CM_Mapped
        GROUP BY UPPER(LTRIM(RTRIM(FretrackChargeItemName))) HAVING COUNT(DISTINCT CargoMintChargeItemID)=1;
        SELECT CONVERT(decimal(18,2),TotalRate) Rate,MIN(TaxGroupId) TargetID INTO #TaxMap
        FROM dbo.TaxGroups GROUP BY CONVERT(decimal(18,2),TotalRate) HAVING COUNT(DISTINCT TaxGroupId)=1;
        UPDATE c SET CargoMintCurrencyID=COALESCE(c.CargoMintCurrencyID,cu.TargetID),
                     CargoMintChargeItemID=COALESCE(c.CargoMintChargeItemID,ch.TargetID),
                     CargoMintTaxRateID=COALESCE(c.CargoMintTaxRateID,tx.TargetID)
        FROM dbo.Fretrack_SalesQuoteCharges_New c
        LEFT JOIN #CurrencyMap cu ON cu.Code=UPPER(LTRIM(RTRIM(c.CurrencyCode)))
        LEFT JOIN #ChargeMap ch ON ch.Name=UPPER(LTRIM(RTRIM(c.ChargeDescription)))
        LEFT JOIN #TaxMap tx ON tx.Rate=CONVERT(decimal(18,2),c.TaxPercent)
        WHERE c.SQID=@SQID AND ISNULL(c.isDeleted,0)=0;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteCharges_New WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0 AND
                   (CargoMintCurrencyID IS NULL OR CargoMintChargeItemID IS NULL OR
                    (ISNULL(TaxPercent,0)<>0 AND CargoMintTaxRateID IS NULL)))
            THROW 51026, 'A charge currency/item/tax mapping is missing or ambiguous. Populate the staged mapping explicitly.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteCharges_New WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0 AND
                   (UPPER(LTRIM(RTRIM(ISNULL(IncomeExpense,'')))) NOT IN ('INCOME','EXPENSE') OR
                    Quantity IS NULL OR Rate IS NULL OR ExRate IS NULL OR ExRate<=0 OR TaxPercent IS NULL))
            THROW 51027, 'An active charge has invalid direction or missing quantity/rate/exchange-rate/tax-percent.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteCharges_New c WHERE c.SQID=@SQID AND ISNULL(c.isDeleted,0)=0 AND
                   (NOT EXISTS (SELECT 1 FROM dbo.Currencies m WHERE m.CurrencyId=c.CargoMintCurrencyID AND ISNULL(m.IsDeleted,0)=0
                                AND UPPER(LTRIM(RTRIM(m.CurrencyCode)))=UPPER(LTRIM(RTRIM(c.CurrencyCode))))
                    OR (c.CargoMintCurrencyID=@LocalCurrencyId AND c.ExRate<>1)))
            THROW 51031, 'A charge currency mapping conflicts with its code or local currency has an exchange rate other than 1.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Fretrack_SalesQuoteCharges_New c WHERE c.SQID=@SQID AND ISNULL(c.isDeleted,0)=0
                   AND c.CargoMintTaxRateID IS NOT NULL AND NOT EXISTS
                   (SELECT 1 FROM dbo.TaxGroups t WHERE t.TaxGroupId=c.CargoMintTaxRateID
                    AND CONVERT(decimal(18,2),t.TotalRate)=CONVERT(decimal(18,2),c.TaxPercent)))
            THROW 51032, 'A staged target tax mapping does not match the charge tax percentage.', 1;

        /* Pairing key is source detail + charge item + line number + normalized ApplyPer.
           Multiple same-side rows are ambiguous, even if their amounts happen to match. */
        SELECT c.*, UPPER(LTRIM(RTRIM(c.IncomeExpense))) Side,
               UPPER(LTRIM(RTRIM(ISNULL(c.ApplyPer,'')))) UnitKey INTO #Charges
        FROM dbo.Fretrack_SalesQuoteCharges_New c WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0;
        IF EXISTS (SELECT 1 FROM #Charges GROUP BY SQDetailRowID,ChargeItemID,LineNumber,UnitKey,Side HAVING COUNT(*)>1)
            THROW 51028, 'Ambiguous buy/sell pairing: multiple charges have the same detail/item/line/unit/side.', 1;
        SELECT SQDetailRowID,ChargeItemID,LineNumber,UnitKey,
               MAX(CASE WHEN Side='INCOME' THEN SQChargeID END) SellSourceID,
               MAX(CASE WHEN Side='EXPENSE' THEN SQChargeID END) BuySourceID
        INTO #Pairs FROM #Charges GROUP BY SQDetailRowID,ChargeItemID,LineNumber,UnitKey;
        IF EXISTS (SELECT 1 FROM #Pairs p JOIN #Charges s ON s.SQChargeID=p.SellSourceID
                   JOIN #Charges b ON b.SQChargeID=p.BuySourceID WHERE s.CargoMintChargeItemID<>b.CargoMintChargeItemID)
            THROW 51033, 'The two sides of a paired charge map to different CargoMint charge items.', 1;

        CREATE TABLE #ServiceMap (SourceDetailID int PRIMARY KEY, ServiceID int NOT NULL, OptionID int NOT NULL);
        CREATE TABLE #LineMap (SellSourceID int NULL,BuySourceID int NULL,LineID int NOT NULL);
        DECLARE @NewID TABLE (ID int);
        DECLARE @Nomination varchar(50);
        IF (SELECT COUNT(DISTINCT NominationType) FROM dbo.Fretrack_SalesQuoteNomDetails WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0)>1
            THROW 51029, 'Multiple nomination types cannot be represented by a single quotation header.', 1;
        SELECT @Nomination=MAX(NominationType) FROM dbo.Fretrack_SalesQuoteNomDetails WHERE SQID=@SQID AND ISNULL(isDeleted,0)=0;

        INSERT dbo.SalesQuotations
        (QuotationNo,QuotationDate,ClosedDate,ValidityDate,QuotationStatus,CustomerId,ContactId,SalesPersonId,
         IncoTermId,CommodityId,orgId,ReceivedDate,CurrencyId,LocalCurrencyId,ExRate,TermsConditions,NominationType,
         ShowTax,ShowCarrier,ShowTerms,CreatedBy,CreatedDate,UpdatedBy,UpdatedDate,IsDeleted)
        OUTPUT inserted.QuotationId INTO @NewID
        SELECT SalesQuoteNumber,SalesQuoteDate,NULL,ExpiryDate,@QuotationStatus,CargoMintCompanyID,CargoMintContactID,CargoMintSalesPersonID,
               CargoMintIncoTermID,@CommodityId,@OrgId,EnqReceivedDate,@QuotationCurrencyId,@LocalCurrencyId,@HeaderExRate,TermsConditions,@Nomination,
               ISNULL(showTax,1),ISNULL(showCarrier,0),1,CargoMintCreatedBy,DateCreated,CargoMintModifiedBy,DateModified,0
        FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@SQID;
        SELECT @QuotationId=ID FROM @NewID; DELETE @NewID;

        DECLARE @DetailID int,@ServiceID int,@OptionID int;
        DECLARE detail_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT SalesQuoteDetailID FROM dbo.Fretrack_SalesQuoteDetails_New WHERE SalesQuoteID=@SQID ORDER BY SortOrder,SalesQuoteDetailID;
        OPEN detail_cursor;
        FETCH NEXT FROM detail_cursor INTO @DetailID;
        WHILE @@FETCH_STATUS=0
        BEGIN
            INSERT dbo.SalesQuotationServiceDetails
            (QuotationId,ServiceTypeId,ServiceLabel,ServiceStatus,ServiceNotes,TransportMode,TransportDirection,
             PolId,PodId,orgId,TransportModeId,TransportDirectionId,CurrencyId,LocalCurrencyId,ExRate,IsPrimary,CreatedBy,CreatedDate,IsDeleted)
            OUTPUT inserted.QuotationServiceId INTO @NewID
            SELECT @QuotationId,@ServiceTypeId,CONCAT(h.SalesQuoteType,N' ',h.Modeoftransport),@ServiceStatus,
                   CASE WHEN LEN(d.Notes)<=255 THEN d.Notes ELSE NULL END,h.Modeoftransport,h.Direction,
                   d.CargoMintPOLID,d.CargoMintPODID,@OrgId,@TransportModeId,@TransportDirectionId,@QuotationCurrencyId,@LocalCurrencyId,@HeaderExRate,
                   CASE WHEN d.SalesQuoteDetailID=(SELECT TOP(1) SalesQuoteDetailID FROM dbo.Fretrack_SalesQuoteDetails_New WHERE SalesQuoteID=@SQID ORDER BY SortOrder,SalesQuoteDetailID) THEN 1 ELSE 0 END,
                   h.CargoMintCreatedBy,h.DateCreated,0
            FROM dbo.Fretrack_SalesQuoteDetails_New d JOIN dbo.Fretrack_SalesQuote_New h ON h.SalesQuoteID=d.SalesQuoteID
            WHERE d.SalesQuoteID=@SQID AND d.SalesQuoteDetailID=@DetailID;
            SELECT @ServiceID=ID FROM @NewID; DELETE @NewID;
            -- One service and one rate option per source detail, as confirmed.
            INSERT dbo.SalesQuotationRateOptions
            (QuotationServiceId,OptionLabel,OptionStatus,CarrierId,OptionNotes,orgId,CurrencyId,LocalCurrencyId,ExRate,CreatedBy,CreatedDate,IsDeleted)
            OUTPUT inserted.QuotationOptionId INTO @NewID
            SELECT @ServiceID,N'Migrated',@OptionStatus,d.CargoMintCarrierID,d.Notes,@OrgId,@QuotationCurrencyId,@LocalCurrencyId,@HeaderExRate,
                   h.CargoMintCreatedBy,h.DateCreated,0
            FROM dbo.Fretrack_SalesQuoteDetails_New d JOIN dbo.Fretrack_SalesQuote_New h ON h.SalesQuoteID=d.SalesQuoteID
            WHERE d.SalesQuoteID=@SQID AND d.SalesQuoteDetailID=@DetailID;
            SELECT @OptionID=ID FROM @NewID; DELETE @NewID;
            INSERT #ServiceMap VALUES (@DetailID,@ServiceID,@OptionID);
            FETCH NEXT FROM detail_cursor INTO @DetailID;
        END;
        CLOSE detail_cursor; DEALLOCATE detail_cursor;

        /* ApplyPer IDs follow the existing job-migration mapping. NULL/unknown units
           require an explicit @DefaultApplyPerId; no arbitrary unit is invented. */
        SELECT p.*,sm.ServiceID,sm.OptionID,
               COALESCE(u.TargetID,@DefaultApplyPerId) ApplyPerID INTO #ResolvedPairs
        FROM #Pairs p JOIN #ServiceMap sm ON sm.SourceDetailID=p.SQDetailRowID
        LEFT JOIN (VALUES ('PKG',29),('SHEET',30),('DAY',27),('LABOUR',31),('CBM',1),('AWB',8),('CNTR',10),('ENTRY',32),
                  ('MONTH',24),('PLT',33),('KGS',2),('HOUR',34),('VEHICLE',19),('MT',35),('BOX',36),('PIECE',3),
                  ('INVOICE',38),('BL',37),('SQ FT',28),('INV',38),('DOC',20),('TRIP',22),('SHPT',9),('CRTN',39)) u(Code,TargetID)
          ON p.UnitKey=u.Code;
        IF EXISTS (SELECT 1 FROM #ResolvedPairs WHERE ApplyPerID IS NULL)
            THROW 51030, 'An ApplyPer mapping is missing; pass an explicit DefaultApplyPerId if appropriate.', 1;

        DECLARE @SellSourceID int,@BuySourceID int,@ApplyPerID int,@LineID int;
        DECLARE pair_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT SellSourceID,BuySourceID,OptionID,ApplyPerID FROM #ResolvedPairs ORDER BY SQDetailRowID,LineNumber,ChargeItemID;
        OPEN pair_cursor;
        FETCH NEXT FROM pair_cursor INTO @SellSourceID,@BuySourceID,@OptionID,@ApplyPerID;
        WHILE @@FETCH_STATUS=0
        BEGIN
            INSERT dbo.SalesQuotationLineItems
            (QuotationId,QuotationOptionId,ChargeItemId,ChargeDescription,ApplyPerId,
             SellCurrencyId,SellExRate,SellRate,SellQuantity,SellTaxId,SellTaxPercent,
             BuyCurrencyId,BuyExRate,BuyRate,BuyQuantity,BuyTaxId,BuyTaxPercent,
             InvoiceToId,BillToId,SortOrder,CreatedBy,CreatedDate,UpdatedBy,UpdatedDate,orgId,LocalCurrencyId,IsDeleted)
            OUTPUT inserted.SellLineItemId INTO @NewID
            SELECT @QuotationId,@OptionID,COALESCE(s.CargoMintChargeItemID,b.CargoMintChargeItemID),
                   COALESCE(s.ChargeDescription,b.ChargeDescription),@ApplyPerID,
                   s.CargoMintCurrencyID,s.ExRate,ROUND(s.Rate,2),s.Quantity,s.CargoMintTaxRateID,ROUND(s.TaxPercent,2),
                   b.CargoMintCurrencyID,b.ExRate,ROUND(b.Rate,2),b.Quantity,b.CargoMintTaxRateID,ROUND(b.TaxPercent,2),
                   s.CargoMintPayingPartyID,b.CargoMintPayingPartyID,COALESCE(s.LineNumber,b.LineNumber),
                   COALESCE(s.CargoMintCreatedBy,b.CargoMintCreatedBy),COALESCE(s.DateCreated,b.DateCreated),
                   COALESCE(s.CargoMintModifiedBy,b.CargoMintModifiedBy),COALESCE(s.DateModified,b.DateModified),@OrgId,@LocalCurrencyId,0
            FROM (SELECT 1 Dummy) seed
            LEFT JOIN #Charges s ON s.SQChargeID=@SellSourceID
            LEFT JOIN #Charges b ON b.SQChargeID=@BuySourceID;
            SELECT @LineID=ID FROM @NewID; DELETE @NewID;
            INSERT #LineMap VALUES (@SellSourceID,@BuySourceID,@LineID);
            FETCH NEXT FROM pair_cursor INTO @SellSourceID,@BuySourceID,@OptionID,@ApplyPerID;
        END;
        CLOSE pair_cursor; DEALLOCATE pair_cursor;

        /* Match CargoMint QuotationFinancials: line amounts are in HEADER currency.
           Calculate from persisted scale-2 rate/tax and scale-4 quantity.
           Tax is recalculated; source monetary totals remain unchanged in staging. */
        UPDATE l SET SellSubtotal=ROUND(a.SellRaw,2),BuySubtotal=ROUND(a.BuyRaw,2),
                     SellTaxAmount=ROUND(a.SellRaw*ISNULL(SellTaxPercent,0)/100,2),
                     BuyTaxAmount=ROUND(a.BuyRaw*ISNULL(BuyTaxPercent,0)/100,2)
        FROM dbo.SalesQuotationLineItems l
        CROSS APPLY (SELECT ISNULL(SellRate,0)*ISNULL(SellQuantity,0)*ISNULL(SellExRate,1)/@HeaderExRate SellRaw,
                            ISNULL(BuyRate,0)*ISNULL(BuyQuantity,0)*ISNULL(BuyExRate,1)/@HeaderExRate BuyRaw) a
        WHERE QuotationId=@QuotationId;
        UPDATE l SET SellTotalAmount=SellSubtotal+SellTaxAmount,BuyTotalAmount=BuySubtotal+BuyTaxAmount
        FROM dbo.SalesQuotationLineItems l WHERE QuotationId=@QuotationId;
        UPDATE l SET SellSubtotalLocal=ROUND(SellSubtotal*@HeaderToLocal,4),SellTaxAmountLocal=ROUND(SellTaxAmount*@HeaderToLocal,4),
                     SellTotalAmountLocal=ROUND(SellTotalAmount*@HeaderToLocal,4),BuySubtotalLocal=ROUND(BuySubtotal*@HeaderToLocal,4),
                     BuyTaxAmountLocal=ROUND(BuyTaxAmount*@HeaderToLocal,4),BuyTotalAmountLocal=ROUND(BuyTotalAmount*@HeaderToLocal,4)
        FROM dbo.SalesQuotationLineItems l WHERE QuotationId=@QuotationId;
        UPDATE o SET ApprovedCostRate=t.BuyAmount,FinalSalesRate=t.SellAmount
        FROM dbo.SalesQuotationRateOptions o
        CROSS APPLY (SELECT ISNULL(SUM(BuyTotalAmount),0) BuyAmount,ISNULL(SUM(SellTotalAmount),0) SellAmount
                     FROM dbo.SalesQuotationLineItems WHERE QuotationOptionId=o.QuotationOptionId AND IsDeleted=0) t
        WHERE o.QuotationServiceId IN (SELECT ServiceID FROM #ServiceMap);
        UPDATE s SET ApprovedCostRate=o.ApprovedCostRate,FinalSalesRate=o.FinalSalesRate,
                     ApprovedCostRateLocal=ROUND(o.ApprovedCostRate*@HeaderToLocal,4),FinalSalesRateLocal=ROUND(o.FinalSalesRate*@HeaderToLocal,4)
        FROM dbo.SalesQuotationServiceDetails s JOIN #ServiceMap m ON m.ServiceID=s.QuotationServiceId
        JOIN dbo.SalesQuotationRateOptions o ON o.QuotationOptionId=m.OptionID;
        UPDATE h SET TotalApprovedCostRate=t.BuyAmount,TotalFinalSalesRate=t.SellAmount,
                     TotalProfit=t.SellAmount-t.BuyAmount,
                     TotalProfitPercentage=CASE WHEN t.SellAmount>0 THEN ROUND((t.SellAmount-t.BuyAmount)/t.SellAmount*100,2) END,
                     TotalApprovedCostRateLocal=ROUND(t.BuyAmount*@HeaderToLocal,4),TotalFinalSalesRateLocal=ROUND(t.SellAmount*@HeaderToLocal,4),
                     TotalProfitLocal=ROUND((t.SellAmount-t.BuyAmount)*@HeaderToLocal,4)
        FROM dbo.SalesQuotations h
        CROSS APPLY (SELECT ISNULL(SUM(BuyTotalAmount),0) BuyAmount,ISNULL(SUM(SellTotalAmount),0) SellAmount
                     FROM dbo.SalesQuotationLineItems WHERE QuotationId=@QuotationId AND IsDeleted=0) t WHERE h.QuotationId=@QuotationId;

        /* Write generated target IDs back to staging, in the SAME transaction. */
        UPDATE dbo.Fretrack_SalesQuote_New SET CargoMintSalesQuoteID=@QuotationId WHERE SalesQuoteID=@SQID;
        UPDATE d SET CargoMintSalesQuoteID=@QuotationId,CargoMintSalesQuoteDetailID=m.ServiceID
        FROM dbo.Fretrack_SalesQuoteDetails_New d JOIN #ServiceMap m ON m.SourceDetailID=d.SalesQuoteDetailID WHERE d.SalesQuoteID=@SQID;
        UPDATE c SET CargoMintSQID=@QuotationId,CargoMintSQDetailRowID=sm.ServiceID,CargoMintSQChargeID=lm.LineID
        FROM dbo.Fretrack_SalesQuoteCharges_New c JOIN #ServiceMap sm ON sm.SourceDetailID=c.SQDetailRowID
        JOIN #LineMap lm ON c.SQChargeID=lm.SellSourceID OR c.SQChargeID=lm.BuySourceID WHERE c.SQID=@SQID;
        UPDATE dbo.Fretrack_SalesQuoteNomDetails SET CargoMintSQID=@QuotationId WHERE SQID=@SQID;

        -- There are no package/container counts or dimensions in the four supplied staging tables.
        -- Do not invent rows in SalesQuotationContainers or SalesQuotationPackages.
        DECLARE @ServiceCount int=(SELECT COUNT(*) FROM #ServiceMap),@LineCount int=(SELECT COUNT(*) FROM #LineMap),
                @Cost decimal(18,2),@Sales decimal(18,2),@Profit decimal(18,2),@Margin decimal(5,2),
                @CostLocal decimal(18,4),@SalesLocal decimal(18,4),@ProfitLocal decimal(18,4);
        SELECT @Cost=TotalApprovedCostRate,@Sales=TotalFinalSalesRate,@Profit=TotalProfit,@Margin=TotalProfitPercentage,
               @CostLocal=TotalApprovedCostRateLocal,@SalesLocal=TotalFinalSalesRateLocal,@ProfitLocal=TotalProfitLocal
        FROM dbo.SalesQuotations WHERE QuotationId=@QuotationId;
        IF @DryRun=1 ROLLBACK TRANSACTION;
        ELSE COMMIT TRANSACTION;
        -- Emit success only after COMMIT/ROLLBACK completes. Dry-run IDs are previews only.
        SELECT @SalesQuoteNumber SalesQuoteNumber,@SQID FretrackSalesQuoteID,@QuotationId CargoMintSalesQuoteID,
               @DryRun DryRun,CASE WHEN @DryRun=1 THEN 'validated_rollback' ELSE 'migrated' END Status,
               @ServiceCount ServicesCreated,@ServiceCount OptionsCreated,@LineCount CombinedLinesCreated,
               @Cost TotalApprovedCostRate,@Sales TotalFinalSalesRate,@Profit TotalProfit,@Margin TotalProfitPercentage,
               @CostLocal TotalApprovedCostRateLocal,@SalesLocal TotalFinalSalesRateLocal,@ProfitLocal TotalProfitLocal;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
