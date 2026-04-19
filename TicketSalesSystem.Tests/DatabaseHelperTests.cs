using Microsoft.Data.SqlClient;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TicketSalesSystem.Database;

namespace TicketSalesSystem.Tests
{
    public class DatabaseHelperTests
    {
        [Fact]
        public void GetOLTPConnection_ReturnsConnection_WithCorrectString()
        {
            // Arrange
            var helper = new DatabaseHelper();

            // Act
            var conn = helper.GetOLTPConnection();

            // Assert
            Assert.NotNull(conn);
            Assert.Contains("TicketSales_OLTP", conn.ConnectionString);
        }

        [Fact]
        public void GetDWHConnection_ReturnsConnection_WithCorrectString()
        {
            // Arrange
            var helper = new DatabaseHelper();

            // Act
            var conn = helper.GetDWHConnection();

            // Assert
            Assert.NotNull(conn);
            Assert.Contains("TicketSales_DWH", conn.ConnectionString);
        }

        [Fact]
        public void GetData_ThrowsSqlException_WhenDatabaseIsOffline()
        {
            // Arrange
            var helperMock = new Mock<DatabaseHelper>() { CallBase = true };

            var badConnection = new SqlConnection("Server=invalid_dummy_server;Database=Fake;Connection Timeout=1;");

            helperMock.Setup(h => h.GetDWHConnection()).Returns(badConnection);

            // Act & Assert
            Assert.ThrowsAny<SqlException>(() => helperMock.Object.GetData("SELECT 1"));
        }
        [Fact]
        public void CreateAdapter_ReturnsSqlDataAdapter_WithCorrectProperties()
        {
            // Arrange
            var helper = new DatabaseHelper();
            var conn = new SqlConnection("Server=dummy;Database=dummy;");
            var query = "SELECT * FROM FactTicketSale";

            // Act
            var adapter = helper.CreateAdapter(query, conn);

            // Assert
            Assert.NotNull(adapter);
            Assert.Equal(query, adapter.SelectCommand.CommandText);
            Assert.Equal(conn, adapter.SelectCommand.Connection);
        }
        [Fact]
        public void CreateConnection_ReturnsValidSqlConnection()
        {
            // Arrange
            var helper = new DatabaseHelper();
            string testString = "Server=dummy_server;Database=dummy_db;";

            // Act
            var conn = helper.CreateConnection(testString);

            // Assert
            Assert.NotNull(conn);
            Assert.Equal(testString, conn.ConnectionString);
        }

        [Fact]
        public void GetOLTPData_ThrowsSqlException_WhenDatabaseIsOffline()
        {
            // Arrange
            var helperMock = new Mock<DatabaseHelper>() { CallBase = true };
            var badConnection = new SqlConnection("Server=invalid_dummy_server;Database=Fake;Connection Timeout=1;");

            helperMock.Setup(h => h.GetOLTPConnection()).Returns(badConnection);

            // Act & Assert
            Assert.ThrowsAny<SqlException>(() => helperMock.Object.GetOLTPData("SELECT 1"));
        }
    }
}
