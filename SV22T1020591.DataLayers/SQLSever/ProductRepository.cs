using Dapper;
using Microsoft.Data.SqlClient;
using SV22T1020591.DataLayers.Interfaces;
using SV22T1020591.Models.Catalog;
using SV22T1020591.Models.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SV22T1020591.DataLayers.SQLServer
{
    /// <summary>
    /// Thực hiện các thao tác dữ liệu cho mặt hàng
    /// Single canonical implementation — remove duplicate file under ..\SQLSever\ProductRepository.cs
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));

            _connectionString = connectionString;
        }

        private IDbConnection OpenConnection() => new SqlConnection(_connectionString);

        // ===== PRODUCTS =====

        public async Task<PagedResult<Product>> ListAsync(ProductSearchInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            using var connection = OpenConnection();

            var result = new PagedResult<Product>()
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            var searchValue = string.IsNullOrWhiteSpace(input.SearchValue) ? "" : $"%{input.SearchValue}%";

            const string countSql = @"
SELECT COUNT(*)
FROM Products
WHERE (@SearchValue = '' OR ProductName LIKE @SearchValue)
  AND (@CategoryID = 0 OR CategoryID = @CategoryID)
  AND (@SupplierID = 0 OR SupplierID = @SupplierID)
  AND (@MinPrice = 0 OR Price >= @MinPrice)
  AND (@MaxPrice = 0 OR Price <= @MaxPrice);
";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new
            {
                SearchValue = searchValue,
                input.CategoryID,
                input.SupplierID,
                input.MinPrice,
                input.MaxPrice
            });

            string dataSql = @"
SELECT *
FROM Products
WHERE (@SearchValue = '' OR ProductName LIKE @SearchValue)
  AND (@CategoryID = 0 OR CategoryID = @CategoryID)
  AND (@SupplierID = 0 OR SupplierID = @SupplierID)
  AND (@MinPrice = 0 OR Price >= @MinPrice)
  AND (@MaxPrice = 0 OR Price <= @MaxPrice)
ORDER BY ProductName
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY;
";

            if (input.PageSize == 0)
            {
                dataSql = @"
SELECT *
FROM Products
WHERE (@SearchValue = '' OR ProductName LIKE @SearchValue)
  AND (@CategoryID = 0 OR CategoryID = @CategoryID)
  AND (@SupplierID = 0 OR SupplierID = @SupplierID)
  AND (@MinPrice = 0 OR Price >= @MinPrice)
  AND (@MaxPrice = 0 OR Price <= @MaxPrice)
ORDER BY ProductName;
";
            }

            var data = await connection.QueryAsync<Product>(dataSql, new
            {
                SearchValue = searchValue,
                input.CategoryID,
                input.SupplierID,
                input.MinPrice,
                input.MaxPrice,
                Offset = input.Offset,
                PageSize = input.PageSize
            });

            result.DataItems = data.ToList();
            return result;
        }

        public async Task<Product?> GetAsync(int productID)
        {
            using var connection = OpenConnection();

            const string sql = "SELECT * FROM Products WHERE ProductID = @ProductID";
            return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { ProductID = productID });
        }

        public async Task<int> AddAsync(Product data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var connection = OpenConnection();

            const string sql = @"
INSERT INTO Products
(ProductName, ProductDescription, SupplierID, CategoryID, Unit, Price, Photo, IsSelling)
VALUES
(@ProductName, @ProductDescription, @SupplierID, @CategoryID, @Unit, @Price, @Photo, @IsSelling);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                data.ProductName,
                data.ProductDescription,
                data.SupplierID,
                data.CategoryID,
                data.Unit,
                data.Price,
                data.Photo,
                data.IsSelling
            });
        }

        public async Task<bool> UpdateAsync(Product data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var connection = OpenConnection();

            const string sql = @"
UPDATE Products
SET ProductName = @ProductName,
    ProductDescription = @ProductDescription,
    SupplierID = @SupplierID,
    CategoryID = @CategoryID,
    Unit = @Unit,
    Price = @Price,
    Photo = @Photo,
    IsSelling = @IsSelling
WHERE ProductID = @ProductID;
";
            int rows = await connection.ExecuteAsync(sql, new
            {
                data.ProductName,
                data.ProductDescription,
                data.SupplierID,
                data.CategoryID,
                data.Unit,
                data.Price,
                data.Photo,
                data.IsSelling,
                data.ProductID
            });

            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int productID)
        {
            using var connection = OpenConnection();
            const string sql = "DELETE FROM Products WHERE ProductID = @ProductID";
            int rows = await connection.ExecuteAsync(sql, new { ProductID = productID });
            return rows > 0;
        }

        public async Task<bool> IsUsedAsync(int productID)
        {
            using var connection = OpenConnection();
            const string sql = @"
SELECT CASE
    WHEN EXISTS(SELECT 1 FROM OrderDetails WHERE ProductID = @ProductID) THEN 1
    WHEN EXISTS(SELECT 1 FROM CartItems WHERE ProductID = @ProductID) THEN 1
    ELSE 0
END";
            var used = await connection.ExecuteScalarAsync<int>(sql, new { ProductID = productID });
            return used == 1;
        }

        // ===== ATTRIBUTES =====

        public async Task<List<ProductAttribute>> ListAttributesAsync(int productID)
        {
            using var connection = OpenConnection();

            const string sql = @"
SELECT AttributeID, ProductID, AttributeName, AttributeValue, DisplayOrder
FROM ProductAttributes
WHERE ProductID = @ProductID
ORDER BY DisplayOrder, AttributeID;
";
            var list = await connection.QueryAsync<ProductAttribute>(sql, new { ProductID = productID });
            return list.ToList();
        }

        public async Task<ProductAttribute?> GetAttributeAsync(long attributeID)
        {
            using var connection = OpenConnection();

            const string sql = @"
SELECT AttributeID, ProductID, AttributeName, AttributeValue, DisplayOrder
FROM ProductAttributes
WHERE AttributeID = @AttributeID;
";
            return await connection.QueryFirstOrDefaultAsync<ProductAttribute>(sql, new { AttributeID = attributeID });
        }

        public async Task<long> AddAttributeAsync(ProductAttribute data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var connection = OpenConnection();

            const string sql = @"
INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue, DisplayOrder)
VALUES (@ProductID, @AttributeName, @AttributeValue, @DisplayOrder);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
            return await connection.ExecuteScalarAsync<long>(sql, new
            {
                data.ProductID,
                data.AttributeName,
                data.AttributeValue,
                data.DisplayOrder
            });
        }

        public async Task<bool> UpdateAttributeAsync(ProductAttribute data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var connection = OpenConnection();

            const string sql = @"
UPDATE ProductAttributes
SET AttributeName = @AttributeName,
    AttributeValue = @AttributeValue,
    DisplayOrder = @DisplayOrder
WHERE AttributeID = @AttributeID;
";
            int rows = await connection.ExecuteAsync(sql, new
            {
                data.AttributeName,
                data.AttributeValue,
                data.DisplayOrder,
                data.AttributeID
            });
            return rows > 0;
        }

        public async Task<bool> DeleteAttributeAsync(long attributeID)
        {
            using var connection = OpenConnection();
            const string sql = "DELETE FROM ProductAttributes WHERE AttributeID = @AttributeID";
            int rows = await connection.ExecuteAsync(sql, new { AttributeID = attributeID });
            return rows > 0;
        }

        // ===== PHOTOS =====

        public async Task<List<ProductPhoto>> ListPhotosAsync(int productID)
        {
            using var connection = OpenConnection();

            const string sql = @"
SELECT PhotoID, ProductID, Photo, Description, DisplayOrder, IsHidden
FROM ProductPhotos
WHERE ProductID = @ProductID
ORDER BY DisplayOrder, PhotoID;
";
            var list = await connection.QueryAsync<ProductPhoto>(sql, new { ProductID = productID });
            return list.ToList();
        }

        public async Task<ProductPhoto?> GetPhotoAsync(long photoID)
        {
            using var connection = OpenConnection();

            const string sql = @"
SELECT PhotoID, ProductID, Photo, Description, DisplayOrder, IsHidden
FROM ProductPhotos
WHERE PhotoID = @PhotoID;
";
            return await connection.QueryFirstOrDefaultAsync<ProductPhoto>(sql, new { PhotoID = photoID });
        }

        public async Task<long> AddPhotoAsync(ProductPhoto data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var connection = OpenConnection();

            const string sql = @"
INSERT INTO ProductPhotos (ProductID, Photo, Description, DisplayOrder, IsHidden)
VALUES (@ProductID, @Photo, @Description, @DisplayOrder, @IsHidden);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            try
            {
                System.Diagnostics.Debug.WriteLine($"AddPhotoAsync called. ProductID={data.ProductID}, Photo='{data.Photo}', DisplayOrder={data.DisplayOrder}, IsHidden={data.IsHidden}");

                var id = await connection.ExecuteScalarAsync<long>(sql, new
                {
                    data.ProductID,
                    data.Photo,
                    data.Description,
                    data.DisplayOrder,
                    IsHidden = data.IsHidden ? 1 : 0
                });

                System.Diagnostics.Debug.WriteLine($"AddPhotoAsync succeeded. Inserted PhotoID={id}");
                return id;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddPhotoAsync failed: " + ex.ToString());
                throw;
            }
        }

        public async Task<bool> UpdatePhotoAsync(ProductPhoto data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var connection = OpenConnection();

            const string sql = @"
UPDATE ProductPhotos
SET Photo = @Photo,
    Description = @Description,
    DisplayOrder = @DisplayOrder,
    IsHidden = @IsHidden
WHERE PhotoID = @PhotoID;
";
            int rows = await connection.ExecuteAsync(sql, new
            {
                data.Photo,
                data.Description,
                data.DisplayOrder,
                IsHidden = data.IsHidden ? 1 : 0,
                data.PhotoID
            });

            return rows > 0;
        }

        public async Task<bool> DeletePhotoAsync(long photoID)
        {
            using var connection = OpenConnection();

            const string sql = "DELETE FROM ProductPhotos WHERE PhotoID = @PhotoID";
            int rows = await connection.ExecuteAsync(sql, new { PhotoID = photoID });
            return rows > 0;
        }

        // ===== SPECIAL LISTS =====

        public async Task<List<Product>> GetTopSellingProductsAsync(int topN)
        {
            using var connection = OpenConnection();

            const string sql = @"
SELECT TOP(@TopN) p.*
FROM Products p
INNER JOIN OrderDetails od ON p.ProductID = od.ProductID
GROUP BY p.ProductID, p.ProductName, p.CategoryID, p.SupplierID, p.Unit, p.Price, p.ProductDescription, p.Photo
ORDER BY SUM(od.Quantity) DESC;
";
            var data = await connection.QueryAsync<Product>(sql, new { TopN = topN });
            return data.ToList();
        }

        public async Task<List<Product>> GetMostFavoritedProductsAsync(int topN)
        {
            using var connection = OpenConnection();

            const string sql = @"
SELECT TOP(@TopN) p.*
FROM Products p
ORDER BY p.ProductID DESC;
";
            var data = await connection.QueryAsync<Product>(sql, new { TopN = topN });
            return data.ToList();
        }
    }
}