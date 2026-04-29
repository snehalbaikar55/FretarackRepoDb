SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER VIEW [dbo].[vw_CargoMint_CostSheetCharges]
AS
SELECT 
    cc.CargoID,
    cc.CostSheetID,

    -- Safe Charge Name
    ISNULL(cim.OperationsGroup, 'UNKNOWN') AS OperationsGroup,
    ISNULL(cim.ChargeItemName, cc.ChargeDescription) AS ChargeName,

    cc.ChargeDescription,
    cc.ApplyPer,
    cc.LineNumber,

    -- BUY (Expense)
    SUM(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.Quantity ELSE 0 END) AS BuyQty,
    SUM(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TotalAmount ELSE 0 END) AS BuyAmount,

    -- SELL (Income)
    SUM(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.Quantity ELSE 0 END) AS SellQty,
    SUM(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TotalAmount ELSE 0 END) AS SellAmount,

    -- PARTY IDs
    MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.PayingPartyID END) AS PayToID,
    MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.PayingPartyID END) AS InvoiceToID,

    -- PARTY Names
    MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN compPay.CompanyName END) AS PayToName,
    MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN compInv.CompanyName END) AS InvoiceToName,

    -- Created / Modified by names
    MAX(COALESCE(umC.UserDisplayName, umC.UserName)) AS CreatedByName,
    MAX(COALESCE(umM.UserDisplayName, umM.UserName)) AS ModifiedByName,

    -- PROFIT
    SUM(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TotalAmount ELSE 0 END)
    -
    SUM(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TotalAmount ELSE 0 END) AS Profit

FROM CargoCharges cc

-- LEFT JOIN instead of INNER JOIN to preserve rows
LEFT JOIN ChargeItemMaster cim 
    ON cc.ChargeItemID = cim.ChargeItemID 
    AND cim.isDeleted = 0

-- Companies joins (kept as LEFT JOIN)
LEFT JOIN Companies compPay 
    ON cc.PayingPartyID = compPay.CompanyID 
    AND cc.IncomeExpense = 'EXPENSE'

LEFT JOIN Companies compInv 
    ON cc.PayingPartyID = compInv.CompanyID 
    AND cc.IncomeExpense = 'INCOME'

-- User master joins for created/modified
LEFT JOIN [UserMaster] umC ON umC.[UserID] = cc.[CreatedBy]
LEFT JOIN [UserMaster] umM ON umM.[UserID] = cc.[ModifiedBy]

WHERE cc.isDeleted = 0

GROUP BY 
    cc.CargoID,
    cc.CostSheetID,
    ISNULL(cim.OperationsGroup, 'UNKNOWN'),
    ISNULL(cim.ChargeItemName, cc.ChargeDescription),
    cc.ChargeDescription,
    cc.ApplyPer,
    cc.LineNumber
    
GO
