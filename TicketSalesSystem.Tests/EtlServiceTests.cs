using Microsoft.Data.SqlClient;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TicketSalesSystem.Services;
using static TicketSalesSystem.Database.DatabaseHelper;

namespace TicketSalesSystem.Tests
{
    public class EtlServiceTests
    {
        [Fact]
        public void GetLastDate_ReturnsCorrectId_WhenQueryIsSuccessful()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };

            serviceMock.Setup(s => s.ExecuteScalar(It.IsAny<SqlCommand>())).Returns(20231015);

            // Act
            var result = serviceMock.Object.GetLastDate(null);

            // Assert
            Assert.Equal(20231015, result);
        }

        [Fact]
        public void GetLastDate_ThrowsException_WhenDatabaseFails()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };

            serviceMock.Setup(s => s.ExecuteScalar(It.IsAny<SqlCommand>()))
                       .Throws(new InvalidOperationException("Database connection lost"));

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => serviceMock.Object.GetLastDate(null));
            Assert.Equal("Database connection lost", exception.Message);
        }

        [Fact]
        public void GetNewData_ReturnsMappedData_WhenDataExists()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };

            var readerMock = new Mock<IDataReader>();

            readerMock.SetupSequence(r => r.Read())
                      .Returns(true)
                      .Returns(false);

            readerMock.Setup(r => r["event_id"]).Returns(101);
            readerMock.Setup(r => r["event_type_id"]).Returns(1);
            readerMock.Setup(r => r["venue_id"]).Returns(5);
            readerMock.Setup(r => r["manager_id"]).Returns(12);
            readerMock.Setup(r => r["date_id"]).Returns(20231016);
            readerMock.Setup(r => r["quantity"]).Returns(2);
            readerMock.Setup(r => r["price"]).Returns(150.00m);
            readerMock.Setup(r => r["total_amount"]).Returns(300.00m);
            readerMock.Setup(r => r["is_returned"]).Returns(false);

            serviceMock.Setup(s => s.ExecuteReader(It.IsAny<SqlCommand>())).Returns(readerMock.Object);

            // Act
            var result = serviceMock.Object.GetNewData(null, 20231015);

            // Assert
            Assert.Single(result);
            Assert.Equal(101, result[0]["event"]);
            Assert.Equal(300.00m, result[0]["sum"]);
        }
        
        [Fact]
        public void InsertData_CallsExecuteNonQuery_CorrectNumberOfTimes()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };

            serviceMock.Setup(s => s.ExecuteNonQuery(It.IsAny<SqlCommand>()));

            var dataToInsert = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "event", 1 }, { "type", 1 }, { "venue", 1 }, { "manager", 1 }, { "date", 20231016 }, { "q", 1 }, { "price", 100 }, { "sum", 100 }, { "ret", false } },
                new Dictionary<string, object> { { "event", 2 }, { "type", 2 }, { "venue", 2 }, { "manager", 2 }, { "date", 20231017 }, { "q", 2 }, { "price", 200 }, { "sum", 400 }, { "ret", true } }
            };

            // Act
            serviceMock.Object.InsertData(null, dataToInsert);

            // Assert
            serviceMock.Verify(s => s.ExecuteNonQuery(It.IsAny<SqlCommand>()), Times.Exactly(2));
        }
        [Fact]
        public void TransferData_ExecutesCompleteFlow_Successfully()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();

            dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());
            dbMock.Setup(db => db.GetDWHConnection()).Returns(new SqlConnection());

            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };

            serviceMock.Setup(s => s.OpenConnection(It.IsAny<SqlConnection>()));
            serviceMock.Setup(s => s.GetLastDate(It.IsAny<SqlConnection>())).Returns(20231015);

            var dummyData = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "test", 1 } } };
            serviceMock.Setup(s => s.GetNewData(It.IsAny<SqlConnection>(), 20231015)).Returns(dummyData);

            serviceMock.Setup(s => s.InsertData(It.IsAny<SqlConnection>(), dummyData));

            // Act
            serviceMock.Object.TransferData();

            // Assert
            serviceMock.Verify(s => s.OpenConnection(It.IsAny<SqlConnection>()), Times.Exactly(2));
            serviceMock.Verify(s => s.GetLastDate(It.IsAny<SqlConnection>()), Times.Once);
            serviceMock.Verify(s => s.GetNewData(It.IsAny<SqlConnection>(), 20231015), Times.Once);
            serviceMock.Verify(s => s.InsertData(It.IsAny<SqlConnection>(), dummyData), Times.Once);
        }
        [Fact]
        public void GetNewData_ReturnsEmptyList_WhenReaderHasNoRows()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };
            var readerMock = new Mock<IDataReader>();

            readerMock.Setup(r => r.Read()).Returns(false);
            serviceMock.Setup(s => s.ExecuteReader(It.IsAny<SqlCommand>())).Returns(readerMock.Object);

            // Act
            var result = serviceMock.Object.GetNewData(null, 20231015);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void InsertData_DoesNotCallExecute_WhenDataIsEmpty()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var serviceMock = new Mock<EtlService>(dbMock.Object) { CallBase = true };

            var emptyData = new List<Dictionary<string, object>>();

            // Act
            serviceMock.Object.InsertData(null, emptyData);

            // Assert
            serviceMock.Verify(s => s.ExecuteNonQuery(It.IsAny<SqlCommand>()), Times.Never);
        }

        [Fact]
        public void OpenConnection_ThrowsInvalidOperationException_WhenConnectionIsEmpty()
        {
            // Arrange
            var dbMock = new Mock<IDatabaseHelper>();
            var service = new EtlService(dbMock.Object);
            var emptyConn = new SqlConnection();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => service.OpenConnection(emptyConn));
        }
    }
}
