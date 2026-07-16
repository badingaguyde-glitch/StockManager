namespace StockManager.Server.Models;

public enum StockMovementType
{
    StockIn,
    StockOut,
    Adjustment,
    TransferOut,
    TransferIn
}

public enum PaymentType
{
    Cash,
    Card,
    Debt
}

public enum StockTransferStatus
{
    Completed,
    Pending,
    Cancelled
}
