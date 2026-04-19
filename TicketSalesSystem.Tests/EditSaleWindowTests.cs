using Microsoft.Data.SqlClient;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TicketSalesSystem.Database.DatabaseHelper;

namespace TicketSalesSystem.Tests
{
    public class EditSaleWindowTests
    {
        private void RunInSTA(Action action)
        {
            Exception exception = null;
            Thread thread = new Thread(() => { try { action(); } catch (Exception ex) { exception = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }

        [Fact]
        public void LoadSaleData_FillsFieldsCorrectly_WhenDataExists()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<EditSaleWindow>(99, dbMock.Object) { CallBase = true };
                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));

                var readerMock = new Mock<IDataReader>();
                readerMock.SetupSequence(r => r.Read()).Returns(true).Returns(false);
                readerMock.Setup(r => r["event_id"]).Returns(10);
                readerMock.Setup(r => r["manager_id"]).Returns(20);
                readerMock.Setup(r => r["client_id"]).Returns(30);
                readerMock.Setup(r => r["row_number"]).Returns(5);
                readerMock.Setup(r => r["seat_number"]).Returns(15);
                readerMock.Setup(r => r["quantity"]).Returns(2);
                readerMock.Setup(r => r["price"]).Returns(100.0m);

                windowMock.Setup(w => w.GetReader(It.IsAny<SqlCommand>())).Returns(readerMock.Object);

                // Act
                windowMock.Object.LoadSaleData();

                // Assert
                Assert.Equal(10, windowMock.Object.EventBox.SelectedValue);
                Assert.Equal("5", windowMock.Object.RowBox.Text);
                Assert.Equal("2", windowMock.Object.QuantityBox.Text);

                string expectedTotal = (200.0m).ToString("0.00");
                Assert.Equal(expectedTotal, windowMock.Object.TotalLabel.Text);
            });
        }

        [Fact]
        public void EventBox_SelectionChanged_TriggersTotalRecalculation()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<EditSaleWindow>(1, dbMock.Object) { CallBase = true };
                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));

                windowMock.Setup(w => w.GetScalarValue(It.IsAny<SqlCommand>())).Returns(300.0m);

                windowMock.Object.EventBox.SelectedValue = 1;
                windowMock.Object.QuantityBox.Text = "3";

                // Act
                windowMock.Object.EventBox_SelectionChanged(null, null);

                // Assert
                string expectedTotal = (900.0m).ToString("0.00");
                Assert.Equal(expectedTotal, windowMock.Object.TotalLabel.Text);
            });
        }

        [Fact]
        public void UpdateSale_Click_ParsesDataAndExecutesUpdate()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<EditSaleWindow>(1, dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.ExecuteUpdate(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()));
                windowMock.Setup(w => w.ShowSuccessMessage(It.IsAny<string>()));
                windowMock.Setup(w => w.CloseWindow());

                windowMock.Object.EventBox.SelectedValue = 10;
                windowMock.Object.ManagerBox.SelectedValue = 20;
                windowMock.Object.ClientBox.SelectedValue = 30;
                windowMock.Object.QuantityBox.Text = "4";
                windowMock.Object.RowBox.Text = "1";
                windowMock.Object.SeatBox.Text = "1";

                // Act
                windowMock.Object.UpdateSale_Click(null, null);

                // Assert
                windowMock.Verify(w => w.ExecuteUpdate(10, 20, 30, 4, 1, 1), Times.Once);
                windowMock.Verify(w => w.ShowSuccessMessage(It.IsAny<string>()), Times.Once);
            });
        }

        [Fact]
        public void ExecuteUpdate_CalculatesTotalAndRunsCommand()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<EditSaleWindow>(99, dbMock.Object) { CallBase = true };
                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));
                windowMock.Setup(w => w.GetScalarValue(It.IsAny<SqlCommand>())).Returns(150.0m);
                windowMock.Setup(w => w.ExecuteCommand(It.IsAny<SqlCommand>()));

                // Act
                windowMock.Object.ExecuteUpdate(1, 2, 3, 2, 10, 5);

                // Assert
                windowMock.Verify(w => w.GetScalarValue(It.IsAny<SqlCommand>()), Times.Once);
                windowMock.Verify(w => w.ExecuteCommand(It.IsAny<SqlCommand>()), Times.Once);
            });
        }
        [Fact]
        public void LoadComboBoxes_CallsGetOLTPDataThreeTimes()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPData(It.IsAny<string>())).Returns(new DataTable());

                var windowMock = new Mock<EditSaleWindow>(1, dbMock.Object) { CallBase = true };

                // Act
                windowMock.Object.LoadComboBoxes();

                // Assert
                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("Event")), Times.Once);
                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("Manager")), Times.Once);
                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("Client")), Times.Once);
            });
        }

        [Fact]
        public void LoadSaleData_DoesNothing_WhenNoDataReturned()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<EditSaleWindow>(99, dbMock.Object) { CallBase = true };
                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));

                var readerMock = new Mock<IDataReader>();
                readerMock.Setup(r => r.Read()).Returns(false);

                windowMock.Setup(w => w.GetReader(It.IsAny<SqlCommand>())).Returns(readerMock.Object);

                // Act
                windowMock.Object.LoadSaleData();

                // Assert
                Assert.Null(windowMock.Object.EventBox.SelectedValue);
                Assert.Equal("", windowMock.Object.QuantityBox.Text);
            });
        }

        [Fact]
        public void QuantityBox_TextChanged_DoesNothing_WhenInputIsInvalid()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<EditSaleWindow>(1, dbMock.Object) { CallBase = true };

                windowMock.Object.EventBox.SelectedValue = null;
                windowMock.Object.QuantityBox.Text = "5";

                // Act
                windowMock.Object.QuantityBox_TextChanged(null, null);
                
                // Assert
                Assert.Equal("", windowMock.Object.TotalLabel.Text);
                windowMock.Verify(w => w.OpenConnection(It.IsAny<SqlConnection>()), Times.Never);
            });
        }
    }
}
