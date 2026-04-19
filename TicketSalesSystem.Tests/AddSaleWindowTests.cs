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
    public class AddSaleWindowTests
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
        public void ExecuteSale_CalculatesTotalAndSaves_Successfully()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<AddSaleWindow>(dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));
                windowMock.Setup(w => w.GetScalarValue(It.IsAny<SqlCommand>())).Returns(150.0m);
                windowMock.Setup(w => w.ExecuteCommand(It.IsAny<SqlCommand>()));

                // Act
                windowMock.Object.ExecuteSale(1, 2, 3, 2, 10, 5);

                // Assert
                windowMock.Verify(w => w.GetScalarValue(It.IsAny<SqlCommand>()), Times.Once);
                windowMock.Verify(w => w.ExecuteCommand(It.IsAny<SqlCommand>()), Times.Once);
            });
        }

        [Fact]
        public void ExecuteSale_ThrowsException_WhenPriceNotFound()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<AddSaleWindow>(dbMock.Object) { CallBase = true };
                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));

                windowMock.Setup(w => w.GetScalarValue(It.IsAny<SqlCommand>()))
                          .Throws(new InvalidOperationException("Price not found"));

                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    windowMock.Object.ExecuteSale(1, 2, 3, 2, 10, 5));
                Assert.Equal("Price not found", ex.Message);
            });
        }
        [Fact]
        public void LoadComboBoxes_CallsGetDataThreeTimes()
        {
            RunInSTA(() =>
            {
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPData(It.IsAny<string>())).Returns(new DataTable());

                var windowMock = new Mock<AddSaleWindow>(dbMock.Object) { CallBase = true };

                windowMock.Object.LoadComboBoxes();

                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("Event")), Times.Once);
                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("Manager")), Times.Once);
                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("Client")), Times.Once);
            });
        }

        [Fact]
        public void QuantityBox_TextChanged_CalculatesTotal_WhenInputIsValid()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                dbMock.Setup(db => db.GetOLTPConnection()).Returns(new SqlConnection());

                var windowMock = new Mock<AddSaleWindow>(dbMock.Object) { CallBase = true };
                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));
                windowMock.Setup(w => w.GetScalarValue(It.IsAny<SqlCommand>())).Returns(200.0m);

                windowMock.Object.EventBox.SelectedValue = 1;
                windowMock.Object.QuantityBox.Text = "3";

                // Act
                windowMock.Object.QuantityBox_TextChanged(null, null);

                // Assert
                string expectedTotal = (600.0m).ToString("0.00");
                Assert.Equal(expectedTotal, windowMock.Object.TotalLabel.Text);
            });
        }

        [Fact]
        public void AddSale_Click_ParsesDataAndExecutesSale()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<AddSaleWindow>(dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.ExecuteSale(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()));
                windowMock.Setup(w => w.ShowSuccessMessage(It.IsAny<string>()));
                windowMock.Setup(w => w.CloseWindow());

                windowMock.Object.EventBox.SelectedValue = 10;
                windowMock.Object.ManagerBox.SelectedValue = 20;
                windowMock.Object.ClientBox.SelectedValue = 30;
                windowMock.Object.QuantityBox.Text = "2";
                windowMock.Object.RowBox.Text = "5";
                windowMock.Object.SeatBox.Text = "12";

                // Act
                windowMock.Object.AddSale_Click(null, null);

                // Assert
                windowMock.Verify(w => w.ExecuteSale(10, 20, 30, 2, 5, 12), Times.Once);
                windowMock.Verify(w => w.ShowSuccessMessage(It.IsAny<string>()), Times.Once);
                windowMock.Verify(w => w.CloseWindow(), Times.Once);
            });
        }
        [Fact]
        public void QuantityBox_TextChanged_DoesNothing_WhenEventIsNotSelected()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<AddSaleWindow>(dbMock.Object) { CallBase = true };

                windowMock.Object.EventBox.SelectedValue = null;
                windowMock.Object.QuantityBox.Text = "3";

                // Act
                windowMock.Object.QuantityBox_TextChanged(null, null);

                // Assert
                Assert.Equal("", windowMock.Object.TotalLabel.Text);

                windowMock.Verify(w => w.OpenConnection(It.IsAny<SqlConnection>()), Times.Never);
            });
        }
    }
}
