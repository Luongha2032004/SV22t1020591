using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Common;
using SV22T1020591.Models.Sales;

namespace SV22T1020591.DataLayers.SQLServer
{
    public class OrderRepository : IOrderRepository
    {
        private readonly string _connectionString;

        public OrderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> AddAsync(Order data)
        {
            using var connection = GetConnection();

            string sql = @"
                INSERT INTO Orders
                (CustomerID, OrderTime, DeliveryProvince, DeliveryAddress, Status)
                VALUES
                (@CustomerID, @OrderTime, @DeliveryProvince, @DeliveryAddress, @Status);

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> UpdateAsync(Order data)
        {
            using var connection = GetConnection();

            string sql = @"
                UPDATE Orders
                SET CustomerID = @CustomerID,
                    DeliveryProvince = @DeliveryProvince,
                    DeliveryAddress = @DeliveryAddress,
                    EmployeeID = @EmployeeID,
                    AcceptTime = @AcceptTime,
                    ShipperID = @ShipperID,
                    ShippedTime = @ShippedTime,
                    FinishedTime = @FinishedTime,
                    Status = @Status
                WHERE OrderID = @OrderID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteAsync(int orderID)
        {
            using var connection = GetConnection();

            string sql = @"
                DELETE FROM OrderDetails WHERE OrderID = @orderID;
                DELETE FROM Orders WHERE OrderID = @orderID;";

            return await connection.ExecuteAsync(sql, new { orderID }) > 0;
        }

        public async Task<OrderViewInfo?> GetAsync(int orderID)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT
                    o.OrderID,
                    o.CustomerID,
                    o.OrderTime,
                    o.DeliveryProvince,
                    o.DeliveryAddress,
                    o.EmployeeID,
                    o.AcceptTime,
                    o.ShipperID,
                    o.ShippedTime,
                    o.FinishedTime,
                    o.Status,
                    e.FullName AS EmployeeName,
                    c.CustomerName,
                    c.ContactName AS CustomerContactName,
                    c.Email AS CustomerEmail,
                    c.Phone AS CustomerPhone,
                    c.Address AS CustomerAddress,
                    s.ShipperName,
                    s.Phone AS ShipperPhone
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                LEFT JOIN Shippers s ON o.ShipperID = s.ShipperID
                WHERE o.OrderID = @orderID";

            return await connection.QueryFirstOrDefaultAsync<OrderViewInfo>(sql, new { orderID });
        }

        public async Task<PagedResult<OrderSearchInfo>> ListAsync(OrderSearchInput input)
        {
            using var connection = GetConnection();

            // prepare search value for customer name filtering (empty string means no filter)
            var searchValue = string.IsNullOrWhiteSpace(input.SearchValue) ? "" : $"%{input.SearchValue}%";

            string sql = @"
                SELECT COUNT(*)
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                WHERE (@Status = 0 OR o.Status = @Status)
                  AND (@SearchValue = '' OR c.CustomerName LIKE @SearchValue)
                  AND (@DateFrom IS NULL OR o.OrderTime >= @DateFrom)
                  AND (@DateTo IS NULL OR o.OrderTime < DATEADD(day, 1, @DateTo));

                SELECT
                    o.OrderID,
                    o.OrderTime,
                    o.Status,
                    c.CustomerName,
                    e.FullName AS EmployeeName,
                    s.ShipperName,
                    o.AcceptTime,
                    o.ShippedTime,
                    o.FinishedTime
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                LEFT JOIN Shippers s ON o.ShipperID = s.ShipperID
                WHERE (@Status = 0 OR o.Status = @Status)
                  AND (@SearchValue = '' OR c.CustomerName LIKE @SearchValue)
                  AND (@DateFrom IS NULL OR o.OrderTime >= @DateFrom)
                  AND (@DateTo IS NULL OR o.OrderTime < DATEADD(day, 1, @DateTo))
                ORDER BY o.OrderTime DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var parameters = new
            {
                Status = input.Status,
                SearchValue = searchValue,
                DateFrom = input.DateFrom,
                DateTo = input.DateTo?.Date,
                Offset = (input.Page - 1) * input.PageSize,
                PageSize = input.PageSize
            };

            using var multi = await connection.QueryMultipleAsync(sql, parameters);

            int total = await multi.ReadSingleAsync<int>();
            var data = (await multi.ReadAsync<OrderSearchInfo>()).ToList();

            return new PagedResult<OrderSearchInfo>
            {
                Page = input.Page,
                PageSize = input.PageSize,
                RowCount = total,
                DataItems = data,
            };
        }

        public async Task<PagedResult<OrderSearchInfo>> ListByCustomerAsync(int customerID, OrderSearchInput input)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT COUNT(*)
                FROM Orders o
                WHERE o.CustomerID = @customerID
                  AND (@Status = 0 OR o.Status = @Status)
                  AND (@DateFrom IS NULL OR o.OrderTime >= @DateFrom)
                  AND (@DateTo IS NULL OR o.OrderTime < DATEADD(day, 1, @DateTo));

                SELECT
                    o.OrderID,
                    o.OrderTime,
                    o.Status,
                    e.FullName AS EmployeeName,
                    s.ShipperName,
                    o.AcceptTime,
                    o.ShippedTime,
                    o.FinishedTime
                FROM Orders o
                LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                LEFT JOIN Shippers s ON o.ShipperID = s.ShipperID
                WHERE o.CustomerID = @customerID
                  AND (@Status = 0 OR o.Status = @Status)
                  AND (@DateFrom IS NULL OR o.OrderTime >= @DateFrom)
                  AND (@DateTo IS NULL OR o.OrderTime < DATEADD(day, 1, @DateTo))
                ORDER BY o.OrderTime DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var parameters = new
            {
                customerID,
                Status = input.Status,
                DateFrom = input.DateFrom,
                DateTo = input.DateTo?.Date,
                Offset = (input.Page - 1) * input.PageSize,
                PageSize = input.PageSize
            };

            using var multi = await connection.QueryMultipleAsync(sql, parameters);

            int total = await multi.ReadSingleAsync<int>();
            var data = (await multi.ReadAsync<OrderSearchInfo>()).ToList();

            return new PagedResult<OrderSearchInfo>
            {
                Page = input.Page,
                PageSize = input.PageSize,
                RowCount = total,
                DataItems = data,
            };
        }

        public async Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT
                    d.OrderID,
                    d.ProductID,
                    p.ProductName,
                    p.Photo,
                    d.Quantity,
                    d.SalePrice
                FROM OrderDetails d
                INNER JOIN Products p ON d.ProductID = p.ProductID
                WHERE d.OrderID = @orderID";

            return (await connection.QueryAsync<OrderDetailViewInfo>(sql, new { orderID })).ToList();
        }

        public async Task<bool> AddDetailAsync(OrderDetail data)
        {
            using var connection = GetConnection();

            string sql = @"
                INSERT INTO OrderDetails(OrderID, ProductID, Quantity, SalePrice)
                VALUES(@OrderID, @ProductID, @Quantity, @SalePrice)";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> UpdateDetailAsync(OrderDetail data)
        {
            using var connection = GetConnection();

            string sql = @"
                UPDATE OrderDetails
                SET Quantity = @Quantity,
                    SalePrice = @SalePrice
                WHERE OrderID = @ORDERID
                  AND ProductID = @ProductID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteDetailAsync(int orderID, int productID)
        {
            using var connection = GetConnection();

            string sql = @"
                DELETE FROM OrderDetails
                WHERE OrderID = @orderID
                  AND ProductID = @productID";

            return await connection.ExecuteAsync(sql, new { orderID, productID }) > 0;
        }

        public Task<OrderViewInfo?> GetByCustomerAsync(int customerID, int orderID)
        {
            throw new NotImplementedException();
        }

        public Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID)
        {
            throw new NotImplementedException();
        }
    }
}